using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class SourcesSeedPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesSeedPlannerCases.Cases),MemberType=typeof(SourcesSeedPlannerCases))]
    public void SameSeedCasesBindAuthoritativeRegistry(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
