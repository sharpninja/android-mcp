using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class PlannerRealTests
{
    [Theory,MemberData(nameof(PlannerContractCases.Cases),MemberType=typeof(PlannerContractCases))]
    public void SameContractAgainstProductionPlanner(PlannerCase test) =>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
