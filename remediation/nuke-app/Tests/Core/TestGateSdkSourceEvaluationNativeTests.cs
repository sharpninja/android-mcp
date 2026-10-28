using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateSdkSourceEvaluationNativeTests
{
    [Theory,MemberData(nameof(SdkSourceEvaluationCases.Data),MemberType=typeof(SdkSourceEvaluationCases))]
    public async Task Same_native_API_fixtures_and_full_assertions_bind_to_actual_evaluator(string name)
    {
        var fixture=new SdkSourceEvaluationFixture(name);var io=new SdkSourceEvaluationDependencies(fixture);
        var evaluator=new NativeTestGateSdkEvaluator(fixture.Configuration,io,io);
        TestGateSdkEvaluation? result=null;var code="OK";
        try{result=await evaluator.EvaluateAsync(fixture.Request);}
        catch(TestGateFactsException error){code=error.Code;}
        SdkSourceEvaluationCases.AssertContract(fixture,JsonSerializer.SerializeToNode(io.Observe(code,result))!);
    }
}
