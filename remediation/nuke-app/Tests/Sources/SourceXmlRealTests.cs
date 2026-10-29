using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceXmlRealTests
{
    [Theory, MemberData(nameof(SourceXmlContractCases.Cases), MemberType = typeof(SourceXmlContractCases))]
    public void RealXmlRenderingUsesUnchangedMockProvenAssertions(string id) => SourceXmlContractCases.AssertContract(SourceXmlContractCases.Get(id), new SourceXmlFormatter());
}
