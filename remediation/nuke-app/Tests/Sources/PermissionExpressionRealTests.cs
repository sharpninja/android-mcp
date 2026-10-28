using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PermissionExpressionRealTests
{
    [Theory, MemberData(nameof(PermissionExpressionContractCases.Cases), MemberType = typeof(PermissionExpressionContractCases))]
    public void RealPermissionExpressionsUseUnchangedMockProvenAssertions(string id) =>
        PermissionExpressionContractCases.AssertContract(PermissionExpressionContractCases.Get(id), new PermissionExpressionSourceFacts());
}
