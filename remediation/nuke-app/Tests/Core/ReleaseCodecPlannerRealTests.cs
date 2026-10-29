using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseCodecPlannerRealTests
{
    [Theory,MemberData(nameof(ReleaseCodecPlannerCases.Cases),MemberType=typeof(ReleaseCodecPlannerCases))]
    public void SamePlansBindAuthoritativeRegistry(PlannerCase test)=>PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
    [Theory]
    [InlineData(Operation.Split,OperationExecutor.ReleaseSplit)]
    [InlineData(Operation.Reassemble,OperationExecutor.ReleaseReassemble)]
    public void OneDefinitionSelectsEachNativeCaller(Operation operation,OperationExecutor executor)=>
        Assert.Equal(executor,new OperationRegistry().Find(new(Category.Publish,operation))!.Executor);
}
