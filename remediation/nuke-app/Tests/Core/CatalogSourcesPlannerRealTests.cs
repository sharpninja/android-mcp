using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogSourcesPlannerRealTests
{
    [Theory,MemberData(nameof(CatalogSourcesPlannerCases.Cases),MemberType=typeof(CatalogSourcesPlannerCases))]
    public void SameTypedCatalogCasesUseNativeRegistry(PlannerCase test)=>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
