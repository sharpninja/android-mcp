using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateProducerNativeTests
{
    [Theory,MemberData(nameof(TestGateProducerCases.Data),MemberType=typeof(TestGateProducerCases))]
    public async Task SameEightTypedProducerCasesBindToNativeOrchestrationAndSharedAdmissions(string name)
    {
        var fixture=new TestGateProducerFixture(name);var dependencies=new ProducerMockDependencies(fixture);
        var producer=new NativeTestGateEvidenceProducer(dependencies,dependencies,dependencies,
            new RecordingNativeTestBuildAdmission(dependencies),new RecordingNativeProducerTrxAdmission(dependencies),dependencies);
        var actual=await producer.ProduceAsync(fixture.Request);
        TestGateProducerCases.AssertContract(fixture,TestGateProducerCases.Json(dependencies.Observe(actual)));
    }
}
public sealed class RecordingNativeTestBuildAdmission(ProducerMockDependencies observations):ITestBuildPlanAdmission
{
    private readonly TestBuildPlanAdmission native=new();
    public TestBuildPlan Plan(TestBuildPlanRequest request)
    {observations.Order.Add("plan");observations.Plans.Add(request);return native.Plan(request);}
    public TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence evidence)
    {observations.Order.Add("build-admission");observations.Builds.Add(new(plan,evidence));return native.Evaluate(plan,evidence);}
}
public sealed class RecordingNativeProducerTrxAdmission(ProducerMockDependencies observations):ITrxResultAdmission
{
    private readonly TrxResultAdmission native=new();
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request)
    {
        observations.Order.Add("trx-admission");observations.Trxs.Add(new(request.TrxBytes is {} bytes ? Convert.ToBase64String(bytes.Span) : null,request.ExpectedClasses));
        return native.Evaluate(request);
    }
}
