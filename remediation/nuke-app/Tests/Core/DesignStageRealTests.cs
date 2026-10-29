using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-005")]
public sealed class DesignStageRealTests
{
    [Theory, MemberData(nameof(DesignStageCases.Cases), MemberType = typeof(DesignStageCases))]
    public void SameSelectionCasesBindAuthoritativeNativeRegistry(DesignStageCase test) =>
        DesignStageCases.AssertCase(test, new DesignStagePlanner());
}
