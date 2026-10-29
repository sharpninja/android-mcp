using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real"), Trait("Requirement", "TEST-BUILD-004"), Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class PermissionReferenceAttributeRealTests
{
    [Theory, MemberData(nameof(PermissionReferenceAttributeContractCases.Cases), MemberType = typeof(PermissionReferenceAttributeContractCases))]
    public void SameTwoSectionsUseNativeParserAndProfiledDecoder(string id) =>
        PermissionReferenceAttributeContractCases.AssertSection(PermissionReferenceAttributeContractCases.Get(id),
            decoder => new PermissionReferenceSourceFacts(decoder), new SourceHtmlDecoder());

    [Theory, MemberData(nameof(PermissionReferenceAttributeContractCases.Cases), MemberType = typeof(PermissionReferenceAttributeContractCases))]
    public void SameAttributeDecoderFailurePropagatesFromNativeParser(string id) =>
        PermissionReferenceAttributeContractCases.AssertDecoderFailure(PermissionReferenceAttributeContractCases.Get(id),
            decoder => new PermissionReferenceSourceFacts(decoder), new SourceHtmlDecoder());
}
