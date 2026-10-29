using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class ToolchainSdkQueryHostNativeTests
{
    [Theory,MemberData(nameof(ToolchainHostCases.Data),MemberType=typeof(ToolchainHostCases))]
    public async Task SameNineCasesBindToThinNativeHostAndStrictSharedJson(string name)
    {
        var fixture=new ToolchainHostFixture(name);var io=new ToolchainHostDependencies(fixture);
        var result=await new ToolchainSdkQueryHost(io,io).ExecuteAsync(fixture.Call);
        ToolchainHostCases.AssertContract(fixture,ToolchainHostCases.Json(io.Observe(result)));
    }
}
