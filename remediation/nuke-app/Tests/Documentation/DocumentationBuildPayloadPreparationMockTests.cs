using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class BuildPayloadPreparationEnvironment
{
    public string Scenario {get;}
    public string Generation=>"fixture-build-payload";
    public DocumentationBuildPayloadInput Input {get;}
    public DocumentationBuildPayloadPlan Plan {get;}
    public TestGateProducerRequest Run {get;}
    public IReadOnlyList<Artifact> Sources {get;}
    public Artifact Metadata {get;}
    public Artifact ChildManifest {get;}=new("generation-manifest.json",Encoding.UTF8.GetBytes("{\"fixture\":\"committed child generation\"}\n"),"application/json");
    public IReadOnlyList<Artifact> NestedOutputs {get;}
    public IReadOnlyList<Artifact> Published {get;}
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public IArtifactReceiptVerifier Receipts {get;}=new ArtifactReceiptVerifier(new NativeContentHash());
    public BuildPayloadPreparationEnvironment(string scenario)
    {
        Scenario=scenario;var original=DocumentationBuildPayloadCases.Create()[0];
        Sources=original.Expected.Sources.Select(route=>new Artifact(route.Source.RelativePath,Encoding.UTF8.GetBytes("retained source:"+route.Source.RelativePath),route.Source.MediaType)).ToArray();
        var sourceByAbsolute=Sources.ToDictionary(source=>Path.Combine(original.Input.SourceRoot,source.RelativePath.Replace('/',Path.DirectorySeparatorChar)),StringComparer.Ordinal);
        var manifest=JsonSerializer.Deserialize<TestGateSourceManifest>(original.Input.SourceManifest.Bytes.Span)!;
        manifest=manifest with{Inputs=manifest.Inputs.Select(row=>sourceByAbsolute.TryGetValue(row.AbsolutePath,out var source)
            ?row with{Sha256=Receipts.Describe(source).Sha256,Length=source.Bytes.Length}:row).ToArray()};
        var sourceReceipt=original.Input.SourceManifest with{Bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(manifest))};
        var sourceIdentity=Receipts.Describe(sourceReceipt);
        Input=original.Input with{SourceManifest=sourceReceipt,Snapshot=new(sourceIdentity,sourceIdentity.Sha256,Receipts.Describe(original.Input.NativeEvaluation))};
        Plan=original.Expected with{Sources=Array.AsReadOnly(original.Expected.Sources.Select(route=>route with
            {Source=route.Source with{ExpectedIdentity=Receipts.Describe(Sources.Single(source=>source.RelativePath==route.Source.RelativePath))}}).ToArray())};
        Run=new(Input.SourceRoot,Input.HostProject,manifest.TestsProject,new("/admitted/dotnet",new string('a',64),"fixture"),Path.Combine(Input.SourceRoot,"artifacts/fixture-evidence"),null,[]);
        Metadata=new("build-tool/prerequisites.json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(new
        {
            schema_version="android-mcp-native-build-prerequisites-1",configuration=Input.NativeConfiguration.Configuration,
            target_framework=Input.NativeConfiguration.TargetFramework,sdk_files=Input.NativeConfiguration.Sdk.AssemblyFiles,
            participating_prerequisites=Plan.Prerequisites,restore_generated_hashes_are_observations=true
        })),"application/json");
        NestedOutputs=Plan.Sources.Select(route=>Sources.Single(source=>source.RelativePath==route.Source.RelativePath) with{RelativePath=route.PublicationPath})
            .Append(Metadata).OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
        Published=NestedOutputs.Append(ChildManifest with{RelativePath="build-tool/source-generation.json"}).OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
        if(scenario=="CANCEL-BEFORE")Cancellation.Cancel();
    }
    public string[] BeforeTrace=>["capture:BeforeBuild","evidence:source","evidence:native","plan"];
    public string[] InnerTrace=>["runtime","recipe.prepare","recipe.materialize","capture:AfterExecution","runtime.commit"];
    public string[] OutputTrace=>NestedOutputs.Select(artifact=>"output:"+artifact.RelativePath).Append("output:generation-manifest.json").ToArray();
}

public sealed class BuildPayloadPreparationServices(BuildPayloadPreparationEnvironment e):ITestGateNativeFacts,
    IDocumentationBuildPayloadPlanner,IArtifactGenerationRuntime
{
    private int captures;
    public Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,TestGateSnapshotPoint point,CancellationToken token=default)
    {
        e.Trace.Add("capture:"+point);Assert.Same(e.Run,request);Assert.Equal(e.Cancellation.Token,token);token.ThrowIfCancellationRequested();
        Assert.Equal(captures++==0?TestGateSnapshotPoint.BeforeBuild:TestGateSnapshotPoint.AfterExecution,point);
        if(e.Scenario=="CAPTURE-FAIL")throw new TestGateFactsException("CAPTURE","scripted source authority failure");
        var changed=e.Scenario=="CHANGED-INNER"&&captures==2||e.Scenario=="CHANGED-OUTER"&&captures==3;
        var hash=changed?new string('f',64):e.Input.Snapshot.ContentFingerprint;
        return Task.FromResult(e.Input.Snapshot with{ContentFingerprint=hash,Manifest=e.Input.Snapshot.Manifest with{Sha256=hash}});
    }
    public DocumentationBuildPayloadPlan Plan(DocumentationBuildPayloadInput input)
    {
        e.Trace.Add("plan");Assert.Equal(JsonSerializer.Serialize(e.Input),JsonSerializer.Serialize(input));
        if(e.Scenario=="PLAN-FAIL")throw new DocumentationBuildPayloadException("PLAN");return e.Plan;
    }
    public async Task<ArtifactGenerationResult> ExecuteAsync(string generationId,ArtifactGenerationRecipe recipe,CancellationToken token=default)
    {
        e.Trace.Add("runtime");Assert.Equal(e.Generation,generationId);Assert.Equal(e.Cancellation.Token,token);
        if(e.Scenario=="CHILD-FAIL")throw new IOException("scripted child failure before commit");
        Assert.Equal(e.Plan.Sources.Select(route=>route.Source),recipe.Sources);
        e.Trace.Add("recipe.prepare");var prepared=recipe.Prepare(e.Sources);
        Assert.Equal(e.NestedOutputs.Select(artifact=>artifact.RelativePath),prepared.OutputPaths.Order(StringComparer.Ordinal));
        e.Trace.Add("recipe.materialize");var artifacts=await prepared.MaterializeAsync(token);
        DesignDocumentationStageCases.AssertArtifacts(e.NestedOutputs,artifacts.OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray());
        Assert.NotNull(prepared.VerifyBeforeCommitAsync);await prepared.VerifyBeforeCommitAsync(token);e.Trace.Add("runtime.commit");
        return new(e.Generation,e.Plan.Sources.Select(route=>route.Source.ExpectedIdentity!).ToArray(),e.NestedOutputs.Select(e.Receipts.Describe).ToArray(),e.Receipts.Describe(e.ChildManifest));
    }
    public Guid ReadRunningHostModuleId()=>throw new InvalidOperationException("Payload capture does not claim execution/build facts");
    public Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken token=default)=>throw new InvalidOperationException("No build in source collection");
    public Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken token=default)=>throw new InvalidOperationException("No tests build in source collection");
    public Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,CancellationToken token=default)=>throw new InvalidOperationException("No test execution in source collection");
}
public sealed class BuildPayloadPreparationReader(BuildPayloadPreparationEnvironment e,bool output):IPinnedArtifactReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken token=default)
    {
        Assert.Equal(e.Cancellation.Token,token);token.ThrowIfCancellationRequested();
        Artifact artifact;
        if(output)
        {
            e.Trace.Add("output:"+pin.RelativePath);
            if(e.Scenario=="OUTPUT-READ-FAIL"||e.Scenario=="MANIFEST-READ-FAIL"&&pin.RelativePath==e.ChildManifest.RelativePath)throw new IOException("scripted retained output read failure");
            artifact=e.NestedOutputs.Append(e.ChildManifest).Single(artifact=>artifact.RelativePath==pin.RelativePath);
        }
        else
        {
            var source=pin.RelativePath==e.Input.SourceManifest.RelativePath;e.Trace.Add(source?"evidence:source":"evidence:native");
            if(e.Scenario=="SOURCE-READ-FAIL"&&source||e.Scenario=="NATIVE-READ-FAIL"&&!source)throw new IOException("scripted correlated evidence read failure");
            artifact=source?e.Input.SourceManifest:e.Input.NativeEvaluation;
        }
        Assert.Equal(e.Receipts.Describe(artifact),pin);return Task.FromResult<ReadOnlyMemory<byte>>(artifact.Bytes.ToArray());
    }
}

public static class BuildPayloadPreparationCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","CANCEL-BEFORE","CAPTURE-FAIL","SOURCE-READ-FAIL","NATIVE-READ-FAIL","PLAN-FAIL",
        "CANCEL-MATERIALIZE","CHILD-FAIL","OUTPUT-READ-FAIL","MANIFEST-READ-FAIL","CHANGED-INNER","CHANGED-OUTER","CANCEL-VERIFY"}.Select(scenario=>new object[]{scenario});
    public static async Task AssertContract(BuildPayloadPreparationEnvironment e,IDocumentationBuildPayloadPreparationFactory provider)
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(e.Input);ArtifactGenerationPreparation? prepared=null;
        var early=await Record.ExceptionAsync(async()=>prepared=await provider.PrepareAsync(e.Cancellation.Token));
        if(e.Scenario is "CANCEL-BEFORE" or "CAPTURE-FAIL" or "SOURCE-READ-FAIL" or "NATIVE-READ-FAIL" or "PLAN-FAIL")
        {
            AssertError(e.Scenario,early);Assert.Null(prepared);
            var count=e.Scenario switch{"CANCEL-BEFORE"=>0,"CAPTURE-FAIL"=>1,"SOURCE-READ-FAIL"=>2,"NATIVE-READ-FAIL"=>3,_=>4};
            Assert.Equal(e.BeforeTrace.Take(count),e.Trace);Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(e.Input));return;
        }
        Assert.Null(early);Assert.NotNull(prepared);Assert.NotNull(prepared.VerifyBeforeCommitAsync);
        Assert.Equal(e.Published.Select(artifact=>artifact.RelativePath),prepared.OutputPaths.Order(StringComparer.Ordinal));Assert.Equal(e.BeforeTrace,e.Trace);
        if(e.Scenario=="CANCEL-MATERIALIZE")e.Cancellation.Cancel();
        IReadOnlyList<Artifact>? actual=null;var materialize=await Record.ExceptionAsync(async()=>actual=await prepared.MaterializeAsync(e.Cancellation.Token));
        var materializeFailure=e.Scenario is "CANCEL-MATERIALIZE" or "CHILD-FAIL" or "OUTPUT-READ-FAIL" or "MANIFEST-READ-FAIL" or "CHANGED-INNER";
        if(materializeFailure)
        {
            AssertError(e.Scenario,materialize);Assert.Null(actual);
            var tail=e.Scenario switch
            {
                "CANCEL-MATERIALIZE"=>Array.Empty<string>(),"CHILD-FAIL"=>e.InnerTrace.Take(1).ToArray(),
                "CHANGED-INNER"=>e.InnerTrace.Take(4).ToArray(),"OUTPUT-READ-FAIL"=>e.InnerTrace.Concat(e.OutputTrace.Take(1)).ToArray(),
                _=>e.InnerTrace.Concat(e.OutputTrace).ToArray()
            };
            Assert.Equal(e.BeforeTrace.Concat(tail),e.Trace);
        }
        else
        {
            Assert.Null(materialize);Assert.NotNull(actual);DesignDocumentationStageCases.AssertArtifacts(e.Published,actual.OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray());
            Assert.Equal(e.BeforeTrace.Concat(e.InnerTrace).Concat(e.OutputTrace),e.Trace);
            if(e.Scenario=="CANCEL-VERIFY")e.Cancellation.Cancel();
            var verify=await Record.ExceptionAsync(()=>prepared.VerifyBeforeCommitAsync(e.Cancellation.Token));
            if(e.Scenario is "CHANGED-OUTER" or "CANCEL-VERIFY")AssertError(e.Scenario,verify);else Assert.Null(verify);
            Assert.Equal(e.BeforeTrace.Concat(e.InnerTrace).Concat(e.OutputTrace).Concat(e.Scenario=="CANCEL-VERIFY"?[]:new[]{"capture:AfterExecution"}),e.Trace);
        }
        Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(e.Input));
    }
    private static void AssertError(string scenario,Exception? error)
    {
        if(scenario.StartsWith("CANCEL",StringComparison.Ordinal)){Assert.IsAssignableFrom<OperationCanceledException>(error);return;}
        if(scenario=="CAPTURE-FAIL"){Assert.Equal("CAPTURE",Assert.IsType<TestGateFactsException>(error).Code);return;}
        if(scenario=="PLAN-FAIL"||scenario.StartsWith("CHANGED",StringComparison.Ordinal)){Assert.Equal(scenario=="PLAN-FAIL"?"PLAN":"BUILD_SOURCE_CHANGED",Assert.IsType<DocumentationBuildPayloadException>(error).Code);return;}
        Assert.IsAssignableFrom<IOException>(error);
    }
}

public sealed class ScriptedBuildPayloadPreparationFactory(BuildPayloadPreparationEnvironment e,string mutation=""):IDocumentationBuildPayloadPreparationFactory
{
    public async Task<ArtifactGenerationPreparation> PrepareAsync(CancellationToken token=default)
    {
        if(mutation!="cancel")token.ThrowIfCancellationRequested();var services=new BuildPayloadPreparationServices(e);
        var evidence=new BuildPayloadPreparationReader(e,false);var output=new BuildPayloadPreparationReader(e,true);
        var snapshot=mutation=="capture"?e.Input.Snapshot:await services.CaptureSourceAsync(e.Run,TestGateSnapshotPoint.BeforeBuild,token);
        var source=await evidence.ReadAsync(snapshot.Manifest,token);
        var native=mutation=="evidence"?e.Input.NativeEvaluation.Bytes:await evidence.ReadAsync(snapshot.NativeEvaluationReceipt,token);
        var plan=services.Plan(new(e.Input.SourceRoot,e.Run.HostProject,snapshot,new(snapshot.Manifest.RelativePath,source,snapshot.Manifest.MediaType),
            new(snapshot.NativeEvaluationReceipt.RelativePath,native,snapshot.NativeEvaluationReceipt.MediaType),e.Input.NativeConfiguration,e.Input.PrerequisiteScopes));
        async Task Verify(CancellationToken current)
        {current.ThrowIfCancellationRequested();var observed=await services.CaptureSourceAsync(e.Run,TestGateSnapshotPoint.AfterExecution,current);if(observed.ContentFingerprint!=snapshot.ContentFingerprint&&mutation!="stale")throw new DocumentationBuildPayloadException("BUILD_SOURCE_CHANGED");}
        var recipe=new ArtifactGenerationRecipe((mutation=="sources"?plan.Sources.Skip(1):plan.Sources).Select(route=>route.Source).ToArray(),inputs=>
            new(e.NestedOutputs.Select(artifact=>artifact.RelativePath).ToArray(),current=>
            {
                current.ThrowIfCancellationRequested();var artifacts=plan.Sources.Select(route=>inputs.Single(artifact=>artifact.RelativePath==route.Source.RelativePath) with{RelativePath=route.PublicationPath}).Append(e.Metadata).ToArray();
                if(mutation=="metadata")artifacts[^1]=e.Metadata with{Bytes=Encoding.UTF8.GetBytes("{}")};return Task.FromResult<IReadOnlyList<Artifact>>(artifacts);
            },mutation=="inner-hook"?null:Verify));
        return new(e.Published.Select(artifact=>artifact.RelativePath).ToArray(),async current=>
        {
            current.ThrowIfCancellationRequested();var result=await services.ExecuteAsync(e.Generation,recipe,mutation=="token"?CancellationToken.None:current);
            var artifacts=new List<Artifact>();
            foreach(var pin in result.Outputs)artifacts.Add(new(pin.RelativePath,await output.ReadAsync(pin,current),pin.MediaType));
            artifacts.Add(new(mutation=="manifest"?"build-tool/wrong.json":"build-tool/source-generation.json",await output.ReadAsync(result.Manifest,current),result.Manifest.MediaType));
            return mutation=="outputs"?artifacts.Skip(1).ToArray():artifacts;
        },mutation=="outer-hook"?null:Verify);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadPreparationMockTests
{
    [Theory,MemberData(nameof(BuildPayloadPreparationCases.Cases),MemberType=typeof(BuildPayloadPreparationCases))]
    public Task CorrectCaptureGenerationAndRecheckContractsPass(string scenario)
    {var e=new BuildPayloadPreparationEnvironment(scenario);return BuildPayloadPreparationCases.AssertContract(e,new ScriptedBuildPayloadPreparationFactory(e));}
    [Theory]
    [InlineData("FULL","capture")][InlineData("FULL","evidence")][InlineData("FULL","sources")][InlineData("FULL","metadata")]
    [InlineData("FULL","inner-hook")][InlineData("FULL","outer-hook")][InlineData("FULL","token")][InlineData("FULL","manifest")][InlineData("FULL","outputs")]
    [InlineData("CHANGED-INNER","stale")][InlineData("CHANGED-OUTER","stale")][InlineData("CANCEL-BEFORE","cancel")]
    public async Task EachOmittedBoundarySourceReceiptAndCancellationControlIsRejected(string scenario,string mutation)
    {var e=new BuildPayloadPreparationEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>BuildPayloadPreparationCases.AssertContract(e,new ScriptedBuildPayloadPreparationFactory(e,mutation))));}
}
