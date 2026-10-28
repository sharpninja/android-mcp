using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkSourceEvaluationObservation(string Code,TestGateSdkEvaluation? Evaluation,
    IReadOnlyList<TestGateSdkNativeOpenRequest> Opens,IReadOnlyList<string> ProjectLoads,
    IReadOnlyList<TestGateResolvedReferenceQueryRequest> Queries,int Disposals);

public sealed class SdkSourceEvaluationFixture
{
    public static readonly string[] Names=["source-graph","project-cycle","resolved-copy",
        "missing-fullpath","outside-scope","wrong-sdk","wrong-configuration",
        "unresolved-import","load-pin-failure","query-failure"];
    public string Name { get; }
    public TestGateSdkEvaluationRequest Request { get; }
    public TestGateNativeFactsConfiguration Configuration { get; }
    public Dictionary<string,TestGateSdkNativeProject> Projects { get; }=new(StringComparer.Ordinal);
    public string[] ProjectOrder { get; }
    public TestGateSdkEvaluation Expected { get; }
    public TestGateResolvedReferenceQueryRequest CopyRequest { get; }
    public TestGateResolvedReferenceQueryResult CopyResult { get; }
    public string Code=>Name switch
    {
        "missing-fullpath" or "unresolved-import"=>"SOURCE_INPUTS_UNRESOLVED",
        "outside-scope"=>"INPUT_SCOPE_VIOLATION","wrong-sdk" or "load-pin-failure"=>"SDK_PIN_MISMATCH",
        "wrong-configuration"=>"SDK_CONFIGURATION_MISMATCH","query-failure"=>"RESOLVED_REFERENCE_MISSING",_=>"OK"
    };
    public bool IsResolved=>Name is "resolved-copy" or "query-failure";
    public SdkSourceEvaluationFixture(string name)
    {
        Name=name;
        var copy=new ReferenceQueryFixture("resolved-copy");
        CopyRequest=copy.Request;CopyResult=name=="query-failure"
            ? new("RESOLVED_REFERENCE_MISSING",[],[]) : copy.Result;
        Configuration=new(copy.Request.Sdk,["/fixture/app","/fixture/sdk"],"Debug","net10.0");
        Request=new(copy.Request.Run,copy.Request.Sdk,"Debug","net10.0",
            IsResolved ? TestGateSdkEvaluationPhase.ResolvedBuildOutputs : TestGateSdkEvaluationPhase.SourceInputs);
        var host=Request.Run.HostProject;var tests=Request.Run.TestsProject;var library="/fixture/app/Library/Library.csproj";
        ProjectOrder=[host,tests,library];
        var nativeImport=new TestGateSdkNativeImport("/fixture/sdk/Microsoft.Common.targets",host);
        Projects[host]=new(host,"/fixture/sdk","Debug","net10.0",copy.Request.HostOutputPath,
            [new("Compile","Core/Host.cs","/fixture/app/Core/Host.cs",host),
             new("EmbeddedResource","Assets/data.bin","/fixture/app/Assets/data.bin",host),
             new("AdditionalFiles","config/input.txt","/fixture/app/config/input.txt",host),
             new("Analyzer","analyzers/Tool.dll","/fixture/app/analyzers/Tool.dll",host),
             new("EditorConfigFiles",".editorconfig","/fixture/app/.editorconfig",host),
             new("ProjectReference","Library/Library.csproj",library,host)],
            [nativeImport],new Dictionary<string,string>{{"ProjectAssetsFile","/fixture/app/obj/project.assets.json"}},[]);
        Projects[tests]=new(tests,"/fixture/sdk","Debug","net10.0","/fixture/app/Tests/bin/Debug/net10.0/Tests.dll",
            [new("Compile","Tests.cs","/fixture/app/Tests/Tests.cs",tests),new("ProjectReference","../App.csproj",host,tests)],
            [nativeImport with {ImportingProjectFullPath=tests}],new Dictionary<string,string>(),[]);
        Projects[library]=new(library,"/fixture/sdk","Debug","net10.0","/fixture/app/Library/bin/Debug/net10.0/Library.dll",
            name=="project-cycle" ? [new("Compile","Library.cs","/fixture/app/Library/Library.cs",library),
                new("ProjectReference","../App.csproj",host,library)] : [new("Compile","Library.cs","/fixture/app/Library/Library.cs",library)],
            [nativeImport with {ImportingProjectFullPath=library}],new Dictionary<string,string>(),[]);
        // Expected collector DTO comes directly from this pinned contract fixture,
        // with explicit independent mapping for every source role and project edge.
        Expected=new([
            new(host,copy.Request.HostOutputPath,
                [new("Project",host,host),new("Compile","/fixture/app/Core/Host.cs",host),
                 new("EmbeddedResource","/fixture/app/Assets/data.bin",host),
                 new("AdditionalFiles","/fixture/app/config/input.txt",host),new("Analyzer","/fixture/app/analyzers/Tool.dll",host),
                 new("EditorConfigFiles","/fixture/app/.editorconfig",host),new("ProjectAssetsFile","/fixture/app/obj/project.assets.json",host)],
                ["/fixture/sdk/Microsoft.Common.targets"],[library],[],[]),
            new(tests,"/fixture/app/Tests/bin/Debug/net10.0/Tests.dll",
                [new("Project",tests,tests),new("Compile","/fixture/app/Tests/Tests.cs",tests)],
                ["/fixture/sdk/Microsoft.Common.targets"],[host],[],IsResolved ? CopyResult.Copies : []),
            new(library,"/fixture/app/Library/bin/Debug/net10.0/Library.dll",
                [new("Project",library,library),new("Compile","/fixture/app/Library/Library.cs",library)],
                ["/fixture/sdk/Microsoft.Common.targets"],name=="project-cycle" ? [host] : [],[],[])
        ],Request.Sdk.AssemblyFiles);
        if(name=="missing-fullpath")Projects[host]=Projects[host] with {Items=Projects[host].Items.Select((item,index)=>index==0 ? item with {FullPath=""} : item).ToArray()};
        if(name=="outside-scope")Projects[host]=Projects[host] with {Items=Projects[host].Items.Select((item,index)=>index==0 ? item with {FullPath="/private/unrelated.cs"} : item).ToArray()};
        if(name=="wrong-sdk")Projects[host]=Projects[host] with {ToolsPath="/other/sdk"};
        if(name=="wrong-configuration")Projects[host]=Projects[host] with {Configuration="Release"};
        if(name=="unresolved-import")Projects[host]=Projects[host] with {EvaluationIssues=["native import evaluation failed"]};
    }
    public TestGateSdkNativeOpenRequest OpenRequest=>new(Request,Configuration.ApprovedReadRoots);
    public SdkSourceEvaluationObservation ExpectedObservation=>new(Code,Code=="OK" ? Expected : null,
        [OpenRequest],Name=="load-pin-failure" ? [] : Code!="OK" && Name!="query-failure" ? [ProjectOrder[0]] : ProjectOrder,
        IsResolved ? [CopyRequest] : [],Name=="load-pin-failure" ? 0 : 1);
}

public sealed class SdkSourceEvaluationDependencies(SdkSourceEvaluationFixture fixture)
    : ITestGateSdkNativeSessionFactory,ITestGateSdkNativeSession,ITestGateResolvedReferenceQuery
{
    public List<TestGateSdkNativeOpenRequest> Opens { get; }=[];
    public List<string> Loads { get; }=[];
    public List<TestGateResolvedReferenceQueryRequest> Queries { get; }=[];
    public int Disposals { get; private set; }
    public Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken=default)
    {
        Opens.Add(request);
        if(fixture.Name=="load-pin-failure")throw new TestGateFactsException("SDK_PIN_MISMATCH","Pinned native load rejected.");
        return Task.FromResult<ITestGateSdkNativeSession>(this);
    }
    public Task<TestGateSdkNativeProject> LoadProjectAsync(string absoluteProjectPath,CancellationToken cancellationToken=default)
    {Loads.Add(absoluteProjectPath);return Task.FromResult(fixture.Projects[absoluteProjectPath]);}
    public Task<TestGateResolvedReferenceQueryResult> QueryAsync(TestGateResolvedReferenceQueryRequest request,CancellationToken cancellationToken=default)
    {Queries.Add(request);return Task.FromResult(fixture.CopyResult);}
    public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    public SdkSourceEvaluationObservation Observe(string code,TestGateSdkEvaluation? evaluation)=>new(code,evaluation,Opens,Loads,Queries,Disposals);
}

// Scripted correct boundary behavior proves the exact assertions before production.
// No real evaluator, native SDK load, process or provider is invoked by this mock.
public sealed class ScriptedSdkSourceEvaluationCases
{
    public async Task<SdkSourceEvaluationObservation> ExecuteAsync(SdkSourceEvaluationFixture fixture)
    {
        var io=new SdkSourceEvaluationDependencies(fixture);
        try
        {
            await using(var session=await io.OpenAsync(fixture.OpenRequest))
            {
                foreach(var path in fixture.ExpectedObservation.ProjectLoads)await session.LoadProjectAsync(path);
                if(fixture.IsResolved)await io.QueryAsync(fixture.CopyRequest);
            }
        }
        catch(TestGateFactsException error)when(error.Code=="SDK_PIN_MISMATCH"){}
        return io.Observe(fixture.Code,fixture.Code=="OK" ? fixture.Expected : null);
    }
}
public static class SdkSourceEvaluationCases
{
    public static IEnumerable<object[]> Data=>SdkSourceEvaluationFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(SdkSourceEvaluationFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.ExpectedObservation)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in SdkSourceEvaluationFixture.Names)
            {
                var expected=JsonSerializer.SerializeToNode(new SdkSourceEvaluationFixture(name).ExpectedObservation)!;
                // Finite required field discriminators: result code, native source
                // mapping, every typed open/query input, graph closure and disposal.
                var members=ReviewOfficialTransportCases.Leaves(expected).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)
                    yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
}
public sealed class TestGateSdkSourceEvaluationMockTests
{
    [Theory,MemberData(nameof(SdkSourceEvaluationCases.Data),MemberType=typeof(SdkSourceEvaluationCases))]
    public async Task Correct_native_API_fixture_and_exact_interactions(string name)
    {
        var fixture=new SdkSourceEvaluationFixture(name);
        var actual=await new ScriptedSdkSourceEvaluationCases().ExecuteAsync(fixture);
        SdkSourceEvaluationCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(SdkSourceEvaluationCases.Controls),MemberType=typeof(SdkSourceEvaluationCases))]
    public void Each_required_native_proof_or_interaction_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new SdkSourceEvaluationFixture(name);var actual=JsonSerializer.SerializeToNode(fixture.ExpectedObservation)!;
        var member=ReviewOfficialTransportCases.Leaves(actual).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(actual,member.Path);
        Assert.ThrowsAny<Exception>(()=>SdkSourceEvaluationCases.AssertContract(fixture,actual));
    }
}
