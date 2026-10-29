using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class ManifestRealTests
{
    [Theory, MemberData(nameof(ManifestContractCases.Cases), MemberType = typeof(ManifestContractCases))]
    public void RealManifestUsesUnchangedMockProvenAssertions(string id) => ManifestContractCases.AssertContract(ManifestContractCases.Get(id), new ManifestSourceExtractor());
    [Theory, MemberData(nameof(ManifestFailureCases.Cases), MemberType = typeof(ManifestFailureCases))]
    public void RealManifestFailuresUseUnchangedMockProvenAssertions(string id) => ManifestFailureCases.AssertContract(ManifestFailureCases.Get(id), new ManifestSourceExtractor());
}
