using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class PermissionExportPlannerRealTests
{
    [Theory,MemberData(nameof(PermissionExportPlannerCases.Cases),MemberType=typeof(PermissionExportPlannerCases))]
    public void SamePlansBindAuthoritativeRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
    [Fact]
    public void TheSingleCatalogExportDefinitionSelectsTheImplementedExecutor()=>
        Assert.Equal(OperationExecutor.CatalogPermissionExport,new OperationRegistry().Find(new(Category.Catalog,Operation.Export))!.Executor);
}
