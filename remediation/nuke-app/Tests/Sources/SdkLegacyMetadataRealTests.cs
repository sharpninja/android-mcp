using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SdkLegacyMetadataRealTests
{
    [Theory, MemberData(nameof(SdkLegacyMetadataContractCases.Cases), MemberType = typeof(SdkLegacyMetadataContractCases))]
    public void RealSdkMetadataPassesUnchangedMockAssertions(string id) =>
        SdkLegacyMetadataContractCases.AssertContract(SdkLegacyMetadataContractCases.Get(id), new SdkApiIndexExtractor());
}
