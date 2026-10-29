using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationPlannerRealTests
{
    [Theory,MemberData(nameof(RepositoryDocumentationPlannerCases.Cases),MemberType=typeof(RepositoryDocumentationPlannerCases))]
    public void SameCasesBindTheNativeRegistry(PlannerCase test) =>
        PlannerContractCases.AssertContract(test,new OperationPlanner(new OperationRegistry()));
}
