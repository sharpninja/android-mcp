using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class SourcesSnapshotPlannerRealTests
{
    [Theory,MemberData(nameof(SourcesSnapshotPlannerCases.Cases),MemberType=typeof(SourcesSnapshotPlannerCases))]
    public void SameRouteAndDeclaredGroupCases(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
