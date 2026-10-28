using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SdkXmlRealTests
{
    [Theory, MemberData(nameof(SdkXmlContractCases.Cases), MemberType = typeof(SdkXmlContractCases))]
    public void RealSdkRowsPassUnchangedMockAssertions(string id) =>
        SdkXmlContractCases.AssertContract(SdkXmlContractCases.Get(id), new SdkApiIndexExtractor());

    [Theory, MemberData(nameof(SdkXmlFailureCases.Cases), MemberType = typeof(SdkXmlFailureCases))]
    public void RealSdkFailuresPassUnchangedMockAssertions(string id) =>
        SdkXmlFailureCases.AssertContract(SdkXmlFailureCases.Get(id), new SdkApiIndexExtractor());
}
