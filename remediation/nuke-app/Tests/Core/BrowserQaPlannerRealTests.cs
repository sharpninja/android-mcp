using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"), Trait("Requirement","TEST-BUILD-005")]
public sealed class BrowserQaPlannerRealTests
{
    [Theory, MemberData(nameof(BrowserQaPlannerCases.Cases), MemberType=typeof(BrowserQaPlannerCases))]
    public void SamePlanningCasesBindNativeRegistry(PlannerCase test) =>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
