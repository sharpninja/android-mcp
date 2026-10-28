using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateAdmissionNativeTests
{
    [Theory,MemberData(nameof(TestGateAdmissionCases.Data),MemberType=typeof(TestGateAdmissionCases))]
    public void SameInputsAndStructuredAssertionsBindToNativePureEvaluators(string kind,string name)
    {
        JsonNode actual;
        if(kind=="trx")
        {
            var fixture=new TrxAdmissionFixture(name);var calls=new List<TrxAdmissionRequest>();
            ITrxResultAdmission service=new RecordingNativeTrxAdmission(new TrxResultAdmission(),calls);
            var result=service.Evaluate(fixture.Request);
            actual=JsonSerializer.SerializeToNode(new TrxAdmissionObservation(fixture.Request,calls,result,[]))!;
        }
        else
        {
            var fixture=new TestBuildFixture(name);var planCalls=new List<TestBuildPlanRequest>();var evidenceCalls=new List<TestBuildEvidence>();
            ITestBuildPlanAdmission service=new RecordingNativeBuildAdmission(new TestBuildPlanAdmission(),planCalls,evidenceCalls);
            var plan=service.Plan(fixture.Request);var result=service.Evaluate(plan,fixture.Evidence);
            actual=JsonSerializer.SerializeToNode(new TestBuildObservation(fixture.Request,plan,fixture.Evidence,planCalls,evidenceCalls,result,[]))!;
        }
        TestGateAdmissionCases.AssertContract(kind,name,actual);
    }
}
public sealed class RecordingNativeTrxAdmission(ITrxResultAdmission inner,List<TrxAdmissionRequest> calls):ITrxResultAdmission
{
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request){calls.Add(request);return inner.Evaluate(request);}
}
public sealed class RecordingNativeBuildAdmission(ITestBuildPlanAdmission inner,List<TestBuildPlanRequest> plans,List<TestBuildEvidence> evidence):ITestBuildPlanAdmission
{
    public TestBuildPlan Plan(TestBuildPlanRequest request){plans.Add(request);return inner.Plan(request);}
    public TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence receipt){evidence.Add(receipt);return inner.Evaluate(plan,receipt);}
}
