using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationCheckPlannerRealTests
{
    [Theory,MemberData(nameof(PublicationCheckPlannerCases.Cases),MemberType=typeof(PublicationCheckPlannerCases))]
    public void SameCasesBindNativeRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
