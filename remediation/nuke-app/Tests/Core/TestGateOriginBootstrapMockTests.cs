using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record OriginBootstrapObservation(string Code,TestGateOriginRuntimeResult? Result,
    IReadOnlyList<string> ContextReads,IReadOnlyList<string> WriterRoots,
    IReadOnlyList<TestGateOriginRuntimeRequest> WriterRequests,IReadOnlyList<string> Order);
public sealed class OriginBootstrapFixture
{
    public static readonly string[] Names=["no-origin-environment","scoped-runtime-proof","missing-compiled-scope",
        "duplicate-compiled-scope","relative-compiled-scope","runtime-scope-rejected","retained-reply-collision"];
    public string Name { get; }
    public string? ChallengePath=>Name=="no-origin-environment" ? null : "/fixture/app/artifacts/tests/current-run/origin-request.json";
    public string CompiledRoot=>Name=="missing-compiled-scope" ? "" : Name=="relative-compiled-scope" ? "../untrusted" : "/fixture/app";
    public bool ReadsScope=>Name!="no-origin-environment";
    public bool InvokesWriter=>Name is "scoped-runtime-proof" or "runtime-scope-rejected" or "retained-reply-collision";
    public string Code=>Name switch
    {
        "no-origin-environment"=>"NO_ORIGIN_REQUEST","scoped-runtime-proof"=>"RECORDED",
        "runtime-scope-rejected"=>"ORIGIN_PATH_SCOPE","retained-reply-collision"=>"ORIGIN_ALREADY_EXISTS",
        _=>"ORIGIN_COMPILED_SCOPE_INVALID"
    };
    public TestGateOriginRuntimeResult WriterResult=>new(Code,Name=="scoped-runtime-proof"
        ? new("artifacts/tests/current-run/origin-response.json",new string('a',64),731,"application/json") : null);
    public OriginBootstrapFixture(string name){Name=name;}
    public OriginBootstrapObservation Expected
    {
        get
        {
            var order=new List<string>{"read-fixed-origin-key"};if(ReadsScope)order.Add("read-compiled-root");
            if(InvokesWriter)order.AddRange(["create-scoped-writer","write-runtime-proof"]);
            return new(Code,Name is "no-origin-environment" or "scoped-runtime-proof" ? WriterResult : null,
                ReadsScope ? ["ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST","AndroidMcp.TestGate.WorkspaceRoot"] : ["ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST"],
                InvokesWriter ? [CompiledRoot] : [],InvokesWriter ? [new(CompiledRoot,ChallengePath)] : [],order);
        }
    }
}
public sealed class OriginBootstrapDependencies(OriginBootstrapFixture fixture)
    : ITestGateOriginBootstrapSource,ITestGateOriginRuntimeWriterFactory,ITestGateOriginRuntimeWriter
{
    public List<string> Reads { get; }=[];public List<string> Roots { get; }=[];
    public List<TestGateOriginRuntimeRequest> Requests { get; }=[];public List<string> Order { get; }=[];
    public string? ReadChallengePath()
    {Reads.Add("ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST");Order.Add("read-fixed-origin-key");return fixture.ChallengePath;}
    public string ReadCompiledWorkspaceRoot()
    {
        Reads.Add("AndroidMcp.TestGate.WorkspaceRoot");Order.Add("read-compiled-root");
        if(fixture.Name=="duplicate-compiled-scope")throw new TestGateFactsException("ORIGIN_COMPILED_SCOPE_INVALID","Duplicate compiled scope.");
        return fixture.CompiledRoot;
    }
    public ITestGateOriginRuntimeWriter CreateForCompiledRoot(string compiledWorkspaceRoot)
    {Roots.Add(compiledWorkspaceRoot);Order.Add("create-scoped-writer");return this;}
    public Task<TestGateOriginRuntimeResult> WriteIfRequestedAsync(TestGateOriginRuntimeRequest request,CancellationToken cancellationToken=default)
    {Requests.Add(request);Order.Add("write-runtime-proof");return Task.FromResult(fixture.WriterResult);}
    public OriginBootstrapObservation Observe(string code,TestGateOriginRuntimeResult? result)=>new(code,result,Reads,Roots,Requests,Order);
}
public sealed class ScriptedOriginBootstrapCases
{
    public async Task<OriginBootstrapObservation> ExecuteAsync(OriginBootstrapFixture fixture)
    {
        var io=new OriginBootstrapDependencies(fixture);var challenge=io.ReadChallengePath();
        if(fixture.ReadsScope)
            try{io.ReadCompiledWorkspaceRoot();}catch(TestGateFactsException){}
        if(fixture.InvokesWriter)await io.CreateForCompiledRoot(fixture.CompiledRoot).WriteIfRequestedAsync(new(fixture.CompiledRoot,challenge));
        return io.Observe(fixture.Code,fixture.Expected.Result);
    }
}
public static class OriginBootstrapCases
{
    public static IEnumerable<object[]> Data=>OriginBootstrapFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(OriginBootstrapFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in OriginBootstrapFixture.Names)
            {
                var members=ReviewOfficialTransportCases.Leaves(JsonSerializer.SerializeToNode(new OriginBootstrapFixture(name).Expected)!).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
}
public sealed class TestGateOriginBootstrapMockTests
{
    [Theory,MemberData(nameof(OriginBootstrapCases.Data),MemberType=typeof(OriginBootstrapCases))]
    public async Task Scoped_bootstrap_uses_compiled_metadata_and_existing_runtime_writer(string name)
    {
        var fixture=new OriginBootstrapFixture(name);var actual=await new ScriptedOriginBootstrapCases().ExecuteAsync(fixture);
        OriginBootstrapCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(OriginBootstrapCases.Controls),MemberType=typeof(OriginBootstrapCases))]
    public void Each_scope_read_or_runtime_proof_field_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new OriginBootstrapFixture(name);var actual=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var member=ReviewOfficialTransportCases.Leaves(actual).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(actual,member.Path);Assert.ThrowsAny<Exception>(()=>OriginBootstrapCases.AssertContract(fixture,actual));
    }
}
