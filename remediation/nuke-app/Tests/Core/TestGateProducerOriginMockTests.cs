using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record ProducerOriginObservation(string Code,ProducerObservation? Completed,
    IReadOnlyList<ProcessRequest> Processes,IReadOnlyList<TestGateProducerRequest> OriginCalls,IReadOnlyList<string> Order);
public sealed class ProducerOriginFixture
{
    public const string Key="ANDROIDMCP_TEST_GATE_ORIGIN_REQUEST";
    public static readonly string[] Names=["test-phase-only","host-fails-before-origin","stale-reference-before-origin",
        "wrong-environment-key","escaped-challenge-path","extra-environment-key","origin-provider-fails"];
    public string Name { get; }
    public TestGateProducerFixture Original { get; }
    public IReadOnlyDictionary<string,string> Environment { get; }
    public bool ReachesOrigin=>Name is not ("host-fails-before-origin" or "stale-reference-before-origin");
    public bool Completes=>Name is "test-phase-only" or "host-fails-before-origin" or "stale-reference-before-origin";
    public string Code=>Name switch
    {
        "wrong-environment-key" or "escaped-challenge-path" or "extra-environment-key"=>"ORIGIN_ENVIRONMENT_INVALID",
        "origin-provider-fails"=>"ORIGIN_ALREADY_EXISTS",_=>Original.ExpectedResult.Code
    };
    public ProducerOriginFixture(string name)
    {
        Name=name;Original=new(name=="host-fails-before-origin" ? "host-build-failure" :
            name=="stale-reference-before-origin" ? "old-host-test-reference" : "fresh-native-sequence");
        var environment=new Dictionary<string,string>{{Key,Original.Request.ReceiptDirectory+"/origin-request.json"}};
        if(name=="wrong-environment-key"){environment.Clear();environment["UNREQUESTED_ORIGIN"]="/fixture/app/other.json";}
        if(name=="escaped-challenge-path")environment[Key]="/private/origin-request.json";
        if(name=="extra-environment-key")environment["UNREQUESTED_SETTING"]="unexpected";
        Environment=environment;
    }
    public ProducerOriginObservation Expected
    {
        get
        {
            var original=TestGateProducerCases.Expected(Original);var order=original.Order.ToList();
            var processes=original.ProcessCalls.ToList();
            if(ReachesOrigin)
            {
                var index=order.IndexOf("process:2");order.Insert(index,"origin:prepare");
                if(Completes)processes[2]=processes[2] with {PublicEnvironment=Environment};
                else{order=order.Take(index+1).ToList();processes=processes.Take(2).ToList();}
            }
            return new(Code,Completes ? original with {ProcessCalls=processes,Order=order} : null,
                processes,ReachesOrigin ? [Original.Request] : [],order);
        }
    }
}
public sealed class OriginAwareProducerFacts(ProducerOriginFixture fixture,ProducerMockDependencies original)
    : ITestGateNativeFacts,ITestGateTestHostOriginProvider
{
    public List<TestGateProducerRequest> OriginCalls { get; }=[];
    public Task<TestGateSourceSnapshot> CaptureSourceAsync(TestGateProducerRequest request,TestGateSnapshotPoint point,CancellationToken cancellationToken=default)
        =>original.CaptureSourceAsync(request,point,cancellationToken);
    public Guid ReadRunningHostModuleId()=>original.ReadRunningHostModuleId();
    public Task<TestGateBinaryIdentity> ReadBuiltHostAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
        =>original.ReadBuiltHostAsync(request,cancellationToken);
    public Task<TestGateTestsOutput> ReadBuiltTestsAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
        =>original.ReadBuiltTestsAsync(request,cancellationToken);
    public Task<TestGateBinaryIdentity> ReadExecutedTestsAsync(TestGateProducerRequest request,ProcessResult result,CancellationToken cancellationToken=default)
        =>original.ReadExecutedTestsAsync(request,result,cancellationToken);
    public Task<IReadOnlyDictionary<string,string>> PrepareTestHostEnvironmentAsync(TestGateProducerRequest request,CancellationToken cancellationToken=default)
    {
        OriginCalls.Add(request);original.Order.Add("origin:prepare");
        if(fixture.Name=="origin-provider-fails")throw new TestGateFactsException("ORIGIN_ALREADY_EXISTS","Retained challenge cannot be reused.");
        return Task.FromResult(fixture.Environment);
    }
}
public sealed class ScriptedProducerOriginCases
{
    public async Task<ProducerOriginObservation> ExecuteAsync(ProducerOriginFixture fixture)
    {
        var original=new ProducerMockDependencies(fixture.Original);var facts=new OriginAwareProducerFacts(fixture,original);
        var plan=original.Plan(new(fixture.Original.Request.HostProject,fixture.Original.Request.TestsProject,fixture.Original.Request.DotNet));
        await facts.CaptureSourceAsync(fixture.Original.Request,TestGateSnapshotPoint.BeforeBuild);facts.ReadRunningHostModuleId();
        for(var index=0;index<fixture.Expected.Processes.Count;index++)
        {
            if(index==2)await facts.PrepareTestHostEnvironmentAsync(fixture.Original.Request);
            var result=await original.RunAsync(fixture.Expected.Processes[index]);
            await original.ReadAsync(result,false);await original.ReadAsync(result,true);
            if(index==0&&fixture.Name!="host-fails-before-origin")
            {await facts.ReadBuiltHostAsync(fixture.Original.Request);await facts.CaptureSourceAsync(fixture.Original.Request,TestGateSnapshotPoint.AfterHostBuild);}
            if(index==1)
            {await facts.ReadBuiltTestsAsync(fixture.Original.Request);await facts.CaptureSourceAsync(fixture.Original.Request,TestGateSnapshotPoint.AfterTestsBuild);}
            if(index==2)
            {await facts.ReadExecutedTestsAsync(fixture.Original.Request,result);await facts.CaptureSourceAsync(fixture.Original.Request,TestGateSnapshotPoint.AfterExecution);}
        }
        if(!fixture.Completes)
            try{await facts.PrepareTestHostEnvironmentAsync(fixture.Original.Request);}catch(TestGateFactsException){}
        if(fixture.Completes&&fixture.Original.ExpectedBuild is {} evidence)
        {
            original.Evaluate(plan,evidence);
            if(fixture.Original.ExpectedResult.TrxAdmission is not null)
            {var bytes=await original.ReadAsync(fixture.Original.TrxPath);original.Evaluate(new(bytes,fixture.Original.Request.ExpectedClasses));}
        }
        return new(fixture.Code,fixture.Completes ? original.Observe(fixture.Original.ExpectedResult) : null,
            original.Processes,facts.OriginCalls,original.Order);
    }
}
public static class ProducerOriginCases
{
    public static IEnumerable<object[]> Data=>ProducerOriginFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(ProducerOriginFixture fixture,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(fixture.Expected)),
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static IEnumerable<object[]> Controls
    {
        get
        {
            foreach(var name in ProducerOriginFixture.Names)
            {
                var members=ReviewOfficialTransportCases.Leaves(JsonSerializer.SerializeToNode(new ProducerOriginFixture(name).Expected)!).ToArray();
                for(var memberIndex=0;memberIndex<members.Length;memberIndex++)yield return new object[]{name,memberIndex,members[memberIndex].Label};
            }
        }
    }
}
public sealed class TestGateProducerOriginMockTests
{
    [Theory,MemberData(nameof(ProducerOriginCases.Data),MemberType=typeof(ProducerOriginCases))]
    public async Task Exact_origin_environment_and_stop_order(string name)
    {
        var fixture=new ProducerOriginFixture(name);var actual=await new ScriptedProducerOriginCases().ExecuteAsync(fixture);
        ProducerOriginCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(ProducerOriginCases.Controls),MemberType=typeof(ProducerOriginCases))]
    public void Each_phase_environment_or_proof_mutation_is_rejected(string name,int memberIndex,string label)
    {
        var fixture=new ProducerOriginFixture(name);var actual=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var member=ReviewOfficialTransportCases.Leaves(actual).ElementAt(memberIndex);Assert.Equal(label,member.Label);
        ReviewOfficialTransportCases.Mutate(actual,member.Path);Assert.ThrowsAny<Exception>(()=>ProducerOriginCases.AssertContract(fixture,actual));
    }
}
