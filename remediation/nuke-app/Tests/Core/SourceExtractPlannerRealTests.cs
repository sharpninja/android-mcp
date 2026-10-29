using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourceExtractPlannerRealTests
{
    [Theory,MemberData(nameof(SourceExtractPlannerCases.Cases),MemberType=typeof(SourceExtractPlannerCases))]
    public void SamePinnedWorkflowCasesUseNativePlanner(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
