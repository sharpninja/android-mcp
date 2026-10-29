using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Documentation;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-002")]
public sealed class MigrationPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesPlanPlannerCases.Cases),MemberType=typeof(SourcesPlanPlannerCases))]
    public void SameSourcePlanCasesBindRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
    [Theory,MemberData(nameof(CatalogBuildPlannerCases.Cases),MemberType=typeof(CatalogBuildPlannerCases))]
    public void SameCatalogBuildCasesBindRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
    [Theory,MemberData(nameof(ArchitectureCliPlanningCases.Data),MemberType=typeof(ArchitectureCliPlanningCases))]
    public void SameArchitectureCasesBindRegistry(ArchitectureCliPlanningCase test)=>ArchitectureCliPlanningCases.AssertPlan(test,new OperationPlanner(new OperationRegistry()));
}
