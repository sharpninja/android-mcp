using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesFetchPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesFetchPlannerCases.Cases),MemberType=typeof(SourcesFetchPlannerCases))]
    public void SameRouteCasesBindAuthoritativeRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
