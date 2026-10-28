using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record ToolchainHostObservation(ToolchainSdkQueryCall Call,ToolchainSdkQueryPublicResult Result,
    IReadOnlyList<StoredArtifact> PinReads,IReadOnlyList<TestGateResolvedReferenceQueryRequest> Queries,IReadOnlyList<string> Order);
public sealed class ToolchainHostFixture
{
    public static readonly string[] Names=["sdk-query","pin-length","pin-hash","wrong-workspace","wrong-host",
        "bad-receipt-root","malformed-request","query-failure","unsupported-purpose"];
    public string Name { get; }
    public ToolchainSdkQueryCall Call { get; }
    public TestGateResolvedReferenceQueryRequest Query { get; }
    public byte[] Bytes { get; }
    public TestGateResolvedReferenceQueryResult QueryResult { get; }
    public ToolchainSdkQueryPublicResult Result { get; }
    public bool Reads=>Name!="unsupported-purpose";
    public bool Invokes=>Name is "sdk-query" or "query-failure";
    public ToolchainHostFixture(string name)
    {
        Name=name;var fixture=new ReferenceQueryFixture(name=="query-failure" ? "malformed-json" : "resolved-copy");Query=fixture.Request;QueryResult=fixture.Result;
        if(name=="wrong-workspace")Query=Query with {Run=Query.Run with {WorkspaceRoot="/fixture/other"}};
        if(name=="wrong-host")Query=Query with {Run=Query.Run with {DotNet=Query.Run.DotNet with {ExecutablePath="/fixture/other-dotnet"}}};
        if(name=="bad-receipt-root")Query=Query with {Run=Query.Run with {ReceiptDirectory="/private/receipt"}};
        var bytes=NativeFactsFixture.Canonical(Query);Bytes=name=="malformed-request" ? bytes[..^1] : bytes;
        var pin=NativeFactsFixture.Stored("artifacts/tests/current-run/pipeline-input.json",Bytes);
        if(name=="pin-length")pin=pin with {Length=pin.Length+1};if(name=="pin-hash")pin=pin with {Sha256=new string('e',64)};
        Call=new(name=="unsupported-purpose" ? ToolchainCheck.None : ToolchainCheck.SdkReferenceCopy,"/fixture/app",pin,"/fixture/dotnet");
        var code=name switch{"sdk-query"=>"OK","pin-length"=>"ARTIFACT_PIN_LENGTH","pin-hash"=>"ARTIFACT_PIN_HASH",
            "wrong-workspace"=>"PIPELINE_WORKSPACE_MISMATCH","wrong-host"=>"PIPELINE_DOTNET_MISMATCH","bad-receipt-root"=>"PIPELINE_SCOPE_INVALID",
            "malformed-request"=>"PIPELINE_REQUEST_INVALID","query-failure"=>QueryResult.Code,_=>"UNSUPPORTED_TOOLCHAIN_CHECK"};
        var receipts=new List<StoredArtifact>();if(Reads&&name is not ("pin-length" or "pin-hash"))receipts.Add(pin);
        if(Invokes)receipts.AddRange(QueryResult.PrivateReceipts);
        Result=new(Call.Check.ToString(),code,Invokes ? QueryResult.Copies.Count : 0,receipts);
    }
    public ProcessRequest CliRequest=>new(new("/fixture/dotnet",new string('d',64),"10.0"),
        ["run","--project","/fixture/app/App.csproj","--no-build","--no-restore","--","Validate","--operation","Toolchain",
        "--toolchain-check","SdkReferenceCopy","--project-root",Call.ProjectRoot,"--pipeline-request",Call.PipelineRequest.RelativePath,
        "--pipeline-request-sha256",Call.PipelineRequest.Sha256,"--pipeline-request-length",Call.PipelineRequest.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--dotnet-host",Call.DotNetHost],Call.ProjectRoot,new Dictionary<string,string>(),"/fixture/app/artifacts/tests/cli-proof");
}
public sealed class ToolchainHostDependencies(ToolchainHostFixture fixture):IPinnedArtifactReader,ITestGateResolvedReferenceQuery
{
    public List<StoredArtifact> Reads { get; }=[];public List<TestGateResolvedReferenceQueryRequest> Queries { get; }=[];public List<string> Order { get; }=[];
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        Reads.Add(callerPin);Order.Add("pinned-read");
        if(fixture.Name=="pin-length")throw new PinnedArtifactException("ARTIFACT_PIN_LENGTH");
        if(fixture.Name=="pin-hash")throw new PinnedArtifactException("ARTIFACT_PIN_HASH");
        return Task.FromResult<ReadOnlyMemory<byte>>(fixture.Bytes);
    }
    public Task<TestGateResolvedReferenceQueryResult> QueryAsync(TestGateResolvedReferenceQueryRequest request,CancellationToken cancellationToken=default)
    {Queries.Add(request);Order.Add("native-query");return Task.FromResult(fixture.QueryResult);}
    public ToolchainHostObservation Observe(ToolchainSdkQueryPublicResult result)=>new(fixture.Call,result,Reads,Queries,Order);
}
public sealed class ScriptedToolchainHostCases
{
    public async Task<ToolchainHostObservation> ExecuteAsync(ToolchainHostFixture fixture)
    {
        var io=new ToolchainHostDependencies(fixture);
        if(fixture.Reads)
            try{await io.ReadAsync(fixture.Call.PipelineRequest);}catch(PinnedArtifactException){return io.Observe(fixture.Result);}
        if(fixture.Invokes)await io.QueryAsync(fixture.Query);
        return io.Observe(fixture.Result);
    }
}
public static class ToolchainHostCases
{
    public static IEnumerable<object[]> Data=>ToolchainHostFixture.Names.Select(name=>new object[]{name});
    public static ToolchainHostObservation Expected(ToolchainHostFixture fixture)=>new(fixture.Call,fixture.Result,
        fixture.Reads ? [fixture.Call.PipelineRequest] : [],fixture.Invokes ? [fixture.Query] : [],
        fixture.Invokes ? ["pinned-read","native-query"] : fixture.Reads ? ["pinned-read"] : []);
    public static JsonNode Json(ToolchainHostObservation value)=>JsonSerializer.SerializeToNode(value)!;
    public static void AssertContract(ToolchainHostFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(Expected(fixture))),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static void AssertCliContract(ToolchainHostFixture fixture,JsonNode actual)
    {
        var expected=new ProcessRequest(new("/fixture/dotnet",new string('d',64),"10.0"),
            ["run","--project","/fixture/app/App.csproj","--no-build","--no-restore","--","Validate","--operation","Toolchain",
            "--toolchain-check","SdkReferenceCopy","--project-root","/fixture/app","--pipeline-request",fixture.Call.PipelineRequest.RelativePath,
            "--pipeline-request-sha256",fixture.Call.PipelineRequest.Sha256,"--pipeline-request-length",fixture.Call.PipelineRequest.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dotnet-host","/fixture/dotnet"],"/fixture/app",new Dictionary<string,string>(),"/fixture/app/artifacts/tests/cli-proof");
        Assert.Equal(new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(expected)),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    }
    public static IEnumerable<object[]> CliControls
    {
        get
        {
            var node=JsonSerializer.SerializeToNode(new ToolchainHostFixture("sdk-query").CliRequest)!;var index=0;
            foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{index++,member.Label};
        }
    }
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in ToolchainHostFixture.Names)
            {
                var node=Json(Expected(new(name)));var index=0;
                foreach(var member in ReviewOfficialTransportCases.Leaves(node))yield return new object[]{name,index++,member.Label};
            }
        }
    }
}
[Trait("Gate","Mock")]
public sealed class ToolchainSdkQueryHostMockTests
{
    [Theory,MemberData(nameof(ToolchainHostCases.Data),MemberType=typeof(ToolchainHostCases))]
    public async Task CorrectScopedHostMock(string name)
    {var fixture=new ToolchainHostFixture(name);ToolchainHostCases.AssertContract(fixture,ToolchainHostCases.Json(await new ScriptedToolchainHostCases().ExecuteAsync(fixture)));}
    [Theory,MemberData(nameof(ToolchainHostCases.Controls),MemberType=typeof(ToolchainHostCases))]
    public async Task EachPinQueryScopeAndPublicSummaryFieldRejects(string name,int memberIndex,string label)
    {
        var fixture=new ToolchainHostFixture(name);var actual=ToolchainHostCases.Json(await new ScriptedToolchainHostCases().ExecuteAsync(fixture));
        ToolchainHostCases.AssertContract(fixture,actual);var members=ReviewOfficialTransportCases.Leaves(actual).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(actual,members[memberIndex].Path);Assert.NotNull(Record.Exception(()=>ToolchainHostCases.AssertContract(fixture,actual)));
    }
    [Fact]
    public void ExactNormalNukeArgvUsesSharedPipelineFlags()
    {
        var fixture=new ToolchainHostFixture("sdk-query");var command=fixture.CliRequest;
        ToolchainHostCases.AssertCliContract(fixture,JsonSerializer.SerializeToNode(command)!);
    }
    [Theory,MemberData(nameof(ToolchainHostCases.CliControls),MemberType=typeof(ToolchainHostCases))]
    public void EachNativeNukeArgvIdentityAndEffectFieldRejects(int memberIndex,string label)
    {
        var fixture=new ToolchainHostFixture("sdk-query");var actual=JsonSerializer.SerializeToNode(fixture.CliRequest)!;
        ToolchainHostCases.AssertCliContract(fixture,actual);var members=ReviewOfficialTransportCases.Leaves(actual).ToArray();Assert.Equal(label,members[memberIndex].Label);
        ReviewOfficialTransportCases.Mutate(actual,members[memberIndex].Path);Assert.NotNull(Record.Exception(()=>ToolchainHostCases.AssertCliContract(fixture,actual)));
    }
}
