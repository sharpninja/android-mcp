using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogValidationPlannerRealTests
{
    [Theory,MemberData(nameof(CatalogValidationPlannerCases.Cases),MemberType=typeof(CatalogValidationPlannerCases))]
    public void SamePlansBindTheAuthoritativeRegistry(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
