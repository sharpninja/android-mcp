using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
[Trait("Gate","Real")]
public sealed class NativeParserPlannerRealTests
{
    [Theory,MemberData(nameof(NativeParserPlannerCases.Cases),MemberType=typeof(NativeParserPlannerCases))]
    public void SameContractAgainstPlanner(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
