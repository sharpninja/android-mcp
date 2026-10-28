using AndroidMcp.BuildTool.Execution;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchOperationRealTests
{
    [Theory]
    [InlineData("normal")][InlineData("explicit-options")][InlineData("invalid-plan")][InlineData("missing-projection")][InlineData("cancelled")]
    public Task SameDependencyContractAgainstNativeFacade(string mode)
    {
        var test=new SourcesFetchHarness(mode);
        return test.AssertContract(new SourcesFetchOperation(test.Planner,test.Batch));
    }
}
