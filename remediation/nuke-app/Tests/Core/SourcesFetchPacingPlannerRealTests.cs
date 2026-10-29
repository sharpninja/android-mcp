using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesFetchPacingPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesFetchPacingPlannerCases.All),MemberType=typeof(SourcesFetchPacingPlannerCases))]
    public void SamePacingParameterCasesBindTheAuthoritativeRegistry(PlannerCase test)
        =>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
