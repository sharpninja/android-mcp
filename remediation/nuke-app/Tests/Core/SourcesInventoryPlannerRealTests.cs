using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesInventoryPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesInventoryPlannerCases.Cases),MemberType=typeof(SourcesInventoryPlannerCases))]
    public void SameInventoryPlanningCasesBindNativeRegistry(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
