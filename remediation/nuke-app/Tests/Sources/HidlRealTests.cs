using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class HidlRealTests
{
    [Theory, MemberData(nameof(HidlContractCases.Cases), MemberType = typeof(HidlContractCases))]
    public void RealHidlUsesUnchangedMockProvenAssertions(string id) => HidlContractCases.AssertContract(HidlContractCases.Get(id), new HidlSourceExtractor());
}
