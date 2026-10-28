using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateOriginBootstrapNativeTests
{
    [Theory,MemberData(nameof(OriginBootstrapCases.Data),MemberType=typeof(OriginBootstrapCases))]
    public async Task Same_seven_compiled_scope_bootstrap_cases_bind_native_service(string name)
    {
        var fixture=new OriginBootstrapFixture(name);var io=new OriginBootstrapDependencies(fixture);
        var native=new NativeTestGateOriginBootstrap(io,io);
        TestGateOriginRuntimeResult? result=null;var code="OK";
        try{result=await native.InitializeAsync();code=result.Code;}
        catch(TestGateFactsException error){code=error.Code;}
        OriginBootstrapCases.AssertContract(fixture,JsonSerializer.SerializeToNode(io.Observe(code,result))!);
    }
}
