using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class ExtractWorkflowFixture : IDisposable
{
    public string Mode { get; }
    public SourcesInventoryCase InventoryCase { get; }=SourcesInventoryCase.Create("partial");
    public CancellationTokenSource Cancellation { get; }=new();
    public SourcesExtractWorkflowRequest Request { get; }
    public RehydrationPlanResult Planned { get; }
    public SourcesInventoryResult Inventory { get; }
    public SourceProducerIdentityAdmission Identity { get; }
    public SourceExtractResult Expected { get; }=new(new("generation/index.json",new('a',64),37,"application/json"),[],2,1,1,0);
    public Exception Sentinel { get; }=new IOException("Literal upstream workflow failure");
    public List<string> Calls { get; }=[];
    public List<RehydrationPlanRequest> Plans { get; }=[];
    public List<(SourcesInventoryReadRequest Request,CancellationToken Token)> Inventories { get; }=[];
    public List<SourceJavaAdmission?> Identities { get; }=[];
    public List<(SourceExtractRequest Request,CancellationToken Token)> Extracts { get; }=[];
    public List<string?> Profiles { get; }=[];
    public bool Success=>Mode is "normal" or "options" or "java";
    public string? ErrorCode=>Mode switch{
        "invalid-plan"=>"EXTRACT_WORKFLOW_PLAN_INVALID","projection"=>"EXTRACT_WORKFLOW_PROJECTION_REQUIRED",
        "network" or "complete" or "historical"=>"EXTRACT_WORKFLOW_PLAN_NOT_PURE",
        "java-no-hash" or "java-no-length" or "java-negative-length" or "java-bad-hash"=>"EXTRACT_WORKFLOW_JAVA_PIN",_=>null};
    public ExtractWorkflowFixture(string mode)
    {
        Mode=mode;Planned=InventoryCase.Planned;
        Request=new(InventoryCase.Request.Recipe,new("receipts/inventory.json",new('b',64),71,"application/json"),"generation");
        if(mode=="options")Request=Request with{ProfileId="release-011",TrustedPriorIndex=new("prior/index.json",new('c',64),13,"application/json"),CacheSourceLimit=1,
            Recipe=Request.Recipe with{Limit=1,ExpectedRelease="fixture-release",Profile=AcquisitionCompatibilityProfile.GenericRehydrate}};
        if(mode.StartsWith("java",StringComparison.Ordinal))Request=Request with{Java=new("java/manifest.json",new('d',64),17)};
        if(mode=="java-no-hash")Request=Request with{Java=Request.Java! with{ExpectedManifestSha256=null}};
        if(mode=="java-bad-hash")Request=Request with{Java=Request.Java! with{ExpectedManifestSha256="not-a-pin"}};
        if(mode=="java-no-length")Request=Request with{Java=Request.Java! with{ExpectedManifestLength=null}};
        if(mode=="java-negative-length")Request=Request with{Java=Request.Java! with{ExpectedManifestLength=-1}};
        if(mode=="invalid-plan")Planned=new(null,[new("LOCK_MISMATCH","recipes/lock.json")]);
        if(mode=="projection")Planned=Planned with{Plan=Planned.Plan! with{SourceProjection=null}};
        if(mode=="network")Planned=Planned with{Plan=Planned.Plan! with{Network=true}};
        if(mode=="complete")Planned=Planned with{Plan=Planned.Plan! with{Complete=true}};
        if(mode=="historical")Planned=Planned with{Plan=Planned.Plan! with{HistoricalByteIdentityClaimed=true}};
        Inventory=new(InventoryCase.ExpectedReceipt,Request.InventoryManifest);
        // A distinct returned admission proves the composer forwards the builder's admitted value.
        Identity=new(new("observed-native",new Dictionary<string,string>{{"assembly",new('e',64)}},"observed-runtime","observed-parser","observed-context"),
            Request.Java is null?null:Request.Java with{ManifestPath="observed/java/manifest.json"});
        if(mode=="cancelled")Cancellation.Cancel();
    }
    public SourceExtractionProfile Resolve(string? id)
    {Calls.Add("profile");Profiles.Add(id);if(Mode=="profile-failure")throw Sentinel;return id=="release-011"?SourceExtractionProfile.RELEASE011:SourceExtractionProfile.CurrentCandidate;}
    public async Task AssertContract(ISourcesExtractWorkflowOperation provider)
    {
        Exception? error=null;SourceExtractResult? result=null;
        try{result=await provider.RunAsync(Request,Cancellation.Token);}catch(Exception e){error=e;}
        if(Success){Assert.Null(error);Assert.Same(Expected,result);}
        else if(Mode is "cancelled" or "cancel-after-plan")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(ErrorCode is not null){var typed=Assert.IsType<SourcesExtractWorkflowException>(error);Assert.Equal(ErrorCode,typed.Code);Assert.Equal(Mode=="invalid-plan"?Planned.Issues:Array.Empty<PublicationIssue>(),typed.Issues);}
        else Assert.Same(Sentinel,error);
        var expectedCalls=Mode switch{
            "cancelled"=>Array.Empty<string>(),"cancel-after-plan" or "invalid-plan" or "projection" or "network" or "complete" or "historical"=>new[]{"plan"},
            "profile-failure" or "java-no-hash" or "java-no-length" or "java-negative-length" or "java-bad-hash"=>new[]{"plan","profile"},
            "inventory-failure"=>new[]{"plan","profile","inventory"},"identity-failure"=>new[]{"plan","profile","inventory","identity"},
            _=>new[]{"plan","profile","inventory","identity","extract"}};
        Assert.Equal(expectedCalls,Calls);
        if(Plans.Count>0){Assert.Single(Plans);Assert.Same(Request.Recipe,Plans[0]);}
        Assert.Equal(expectedCalls.Count(x=>x=="profile"),Profiles.Count);if(Profiles.Count>0)Assert.Equal(Request.ProfileId,Profiles[0]);
        Assert.Equal(expectedCalls.Count(x=>x=="inventory"),Inventories.Count);
        if(Inventories.Count>0){Assert.Same(Planned.Plan!.SourceProjection,Inventories[0].Request.Projection);Assert.Same(Request.InventoryManifest,Inventories[0].Request.InventoryManifest);Assert.Equal(Cancellation.Token,Inventories[0].Token);}
        Assert.Equal(expectedCalls.Count(x=>x=="identity"),Identities.Count);if(Identities.Count>0)Assert.Same(Request.Java,Identities[0]);
        Assert.Equal(expectedCalls.Count(x=>x=="extract"),Extracts.Count);
        if(Extracts.Count>0){var actual=Extracts[0];Assert.Same(Inventory.Receipt.Acquisition.Receipt.Projection,actual.Request.Projection);
            Assert.Equal(Request.GenerationRoot,actual.Request.GenerationRoot);Assert.Same(Request.ProfileId=="release-011"?SourceExtractionProfile.RELEASE011:SourceExtractionProfile.CurrentCandidate,actual.Request.Profile);
            Assert.Same(Identity.Identity,actual.Request.Producer);Assert.Same(Identity.JavaAdmission,actual.Request.Java);Assert.Same(Request.TrustedPriorIndex,actual.Request.TrustedPriorIndex);
            Assert.Equal(Request.CacheSourceLimit,actual.Request.CacheSourceLimit);Assert.Equal(Cancellation.Token,actual.Token);}
    }
    public void Dispose(){InventoryCase.Dispose();Cancellation.Dispose();}
}
public sealed class WorkflowPlannerProbe(ExtractWorkflowFixture test):IRehydrationPlanner
{
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){test.Calls.Add("plan");test.Plans.Add(request);if(test.Mode=="cancel-after-plan")test.Cancellation.Cancel();return test.Planned;}
}
public sealed class WorkflowInventoryProbe(ExtractWorkflowFixture test):ISourcesInventoryReader
{
    public Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken cancellationToken=default)
    {test.Calls.Add("inventory");test.Inventories.Add((request,cancellationToken));if(test.Mode=="inventory-failure")throw test.Sentinel;return Task.FromResult(test.Inventory);}
}
public sealed class WorkflowIdentityProbe(ExtractWorkflowFixture test):ISourceProducerIdentityBuilder
{
    public SourceProducerIdentityAdmission Create(SourceJavaAdmission? java=null){test.Calls.Add("identity");test.Identities.Add(java);if(test.Mode=="identity-failure")throw test.Sentinel;return test.Identity;}
}
public sealed class WorkflowExtractProbe(ExtractWorkflowFixture test):ISourcesExtractOperation
{
    public Task<SourceExtractResult> ExtractAsync(SourceExtractRequest request,CancellationToken cancellationToken=default)
    {test.Calls.Add("extract");test.Extracts.Add((request,cancellationToken));if(test.Mode=="extract-failure")throw test.Sentinel;return Task.FromResult(test.Expected);}
}
// Independent scripted test provider. Production will bind the same fixture/assertions to the real composer.
public sealed class ScriptedExtractWorkflow(ExtractWorkflowFixture test,string mutation=""):ISourcesExtractWorkflowOperation
{
    public async Task<SourceExtractResult> RunAsync(SourcesExtractWorkflowRequest request,CancellationToken cancellationToken=default)
    {
        if(mutation!="effects-before-cancel")cancellationToken.ThrowIfCancellationRequested();
        var planner=new WorkflowPlannerProbe(test);var plan=planner.Plan(mutation=="recipe"?request.Recipe with{Limit=99}:request.Recipe);
        if(mutation=="double-plan")planner.Plan(request.Recipe);
        if(mutation!="effects-before-cancel")cancellationToken.ThrowIfCancellationRequested();
        if(!plan.IsValid&&mutation!="ignore-failure")throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PLAN_INVALID",plan.Issues);
        if(plan.Plan?.SourceProjection is null&&mutation!="ignore-failure")throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PROJECTION_REQUIRED",[]);
        if(plan.Plan is{} value&&(value.Network||value.Complete||value.HistoricalByteIdentityClaimed)&&mutation!="ignore-failure")throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_PLAN_NOT_PURE",[]);
        var profile=test.Resolve(mutation=="profile-id"?"wrong":request.ProfileId);
        if(request.Java is{} java&&(java.ExpectedManifestLength is null or <0||java.ExpectedManifestSha256 is null||java.ExpectedManifestSha256.Length!=64||!java.ExpectedManifestSha256.All(Uri.IsHexDigit))&&mutation!="ignore-failure")throw new SourcesExtractWorkflowException("EXTRACT_WORKFLOW_JAVA_PIN",[]);
        var inventory=await new WorkflowInventoryProbe(test).ReadAsync(new(mutation=="inventory-projection"?plan.Plan!.SourceProjection! with{Rows=[]}:plan.Plan!.SourceProjection!,
            mutation=="inventory-pin"?request.InventoryManifest with{Length=99}:request.InventoryManifest),mutation=="inventory-token"?CancellationToken.None:cancellationToken);
        var identity=new WorkflowIdentityProbe(test).Create(mutation=="identity-java"?null:request.Java);
        var result=await new WorkflowExtractProbe(test).ExtractAsync(new(mutation=="extract-projection"?inventory.Receipt.Acquisition.Receipt.Projection with{Rows=[]}:inventory.Receipt.Acquisition.Receipt.Projection,
            mutation=="generation"?"wrong":request.GenerationRoot,mutation=="profile"?SourceExtractionProfile.RELEASE011:profile,
            mutation=="producer"?identity.Identity with{ProducerId="editable"}:identity.Identity,
            mutation=="prior"?null:request.TrustedPriorIndex,mutation=="java"?request.Java:identity.JavaAdmission,
            mutation=="limit"?99:request.CacheSourceLimit),mutation=="extract-token"?CancellationToken.None:cancellationToken);
        return mutation=="result"?result with{RawExtractions=99}:result;
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesExtractWorkflowMockTests
{
    public static IEnumerable<object[]> Modes()=>new[]{"normal","options","java","invalid-plan","projection","network","complete","historical","java-no-hash","java-no-length","java-negative-length","java-bad-hash","profile-failure","inventory-failure","identity-failure","extract-failure","cancelled","cancel-after-plan"}.Select(x=>new object[]{x});
    [Theory][MemberData(nameof(Modes))]public Task CorrectContractPasses(string mode){var fixture=new ExtractWorkflowFixture(mode);return Run(fixture,new ScriptedExtractWorkflow(fixture));}
    private static async Task Run(ExtractWorkflowFixture fixture,ISourcesExtractWorkflowOperation provider){using(fixture)await fixture.AssertContract(provider);}
    [Theory][InlineData("recipe")][InlineData("double-plan")][InlineData("profile-id")][InlineData("inventory-projection")][InlineData("inventory-pin")][InlineData("inventory-token")][InlineData("extract-projection")][InlineData("generation")][InlineData("producer")][InlineData("extract-token")][InlineData("result")]
    public async Task EveryOrdinaryBoundaryRejectsMutation(string mutation){using var f=new ExtractWorkflowFixture("normal");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedExtractWorkflow(f,mutation))));}
    [Theory][InlineData("options","prior")][InlineData("options","limit")][InlineData("normal","profile")][InlineData("java","java")][InlineData("java","identity-java")]
    public async Task OptionsAndObservedIdentityRejectMutation(string mode,string mutation){using var f=new ExtractWorkflowFixture(mode);Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedExtractWorkflow(f,mutation))));}
    [Theory][InlineData("invalid-plan")][InlineData("projection")][InlineData("network")][InlineData("complete")][InlineData("historical")][InlineData("java-no-hash")][InlineData("java-no-length")][InlineData("java-negative-length")][InlineData("java-bad-hash")]
    public async Task AdmissionCannotReachLaterEffects(string mode){using var f=new ExtractWorkflowFixture(mode);Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedExtractWorkflow(f,"ignore-failure"))));}
    [Theory][InlineData("cancelled")][InlineData("cancel-after-plan")]
    public async Task CancellationRejectsLaterEffects(string mode){using var f=new ExtractWorkflowFixture(mode);Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedExtractWorkflow(f,"effects-before-cancel"))));}
}
