using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateSdkChildNativeTests
{
    private sealed class FixedNonce:ITestGateNonceSource
    {public string CreateNonce()=>SdkChildFixture.Nonce;}

    [Theory,MemberData(nameof(SdkChildCases.Data),MemberType=typeof(SdkChildCases))]
    public async Task Same_fifteen_cases_bind_to_actual_parent_evaluator_without_child_execution(string name)
    {
        var fixture=new SdkChildFixture(name);var io=new SdkChildDependencies(fixture);
        var configuration=new TestGateNativeFactsConfiguration(fixture.Native.Evaluation.Sdk,
            fixture.Native.ApprovedReadRoots,fixture.Native.Evaluation.Configuration,fixture.Native.Evaluation.TargetFramework);
        var evaluator=new NativeTestGateSdkChildEvaluator(configuration,fixture.Request.Monolith,
            fixture.Files["bin/App.dll"].LongLength,fixture.Request.RuntimeFiles,io,io,io,io,new FixedNonce());
        TestGateSdkEvaluation? result=null;var code="OK";
        try{result=await evaluator.EvaluateAsync(fixture.Request);}
        catch(TestGateFactsException error){code=error.Code;}
        SdkChildCases.AssertContract(fixture,JsonSerializer.SerializeToNode(io.Observe(code,result))!);
    }
}
