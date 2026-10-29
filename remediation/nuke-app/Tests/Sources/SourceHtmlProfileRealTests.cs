using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceHtmlProfileRealTests
{
    [Theory, MemberData(nameof(SourceHtmlProfileContractCases.Cases), MemberType = typeof(SourceHtmlProfileContractCases))]
    public void SameDiscriminatorsUseTheSingleNativeDecoder(string id, SourceHtmlDecodingProfile profile) =>
        SourceHtmlProfileContractCases.AssertDecode(SourceHtmlProfileContractCases.Get(id), profile, new SourceHtmlDecoder());

    [Fact]
    public void SameNullPreconditionUsesBothNativeProfilesAndExistingOverload() =>
        SourceHtmlProfileContractCases.AssertNullPrecondition(new SourceHtmlDecoder());

    [Fact]
    public void SameUnknownProfileRefusalUsesTheNativeDecoder() =>
        SourceHtmlProfileContractCases.AssertUnknownProfile(new SourceHtmlDecoder());
}
