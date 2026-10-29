using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceHtmlRealTests
{
    [Theory, MemberData(nameof(SourceHtmlContractCases.DecoderCases), MemberType = typeof(SourceHtmlContractCases))]
    public void NativeDecoderUsesTheSameLegacyHtmlEntityVectors(string id) =>
        SourceHtmlContractCases.AssertDecode(SourceHtmlContractCases.Decoder(id), new SourceHtmlDecoder());

    [Theory, MemberData(nameof(SourceHtmlContractCases.AuditCases), MemberType = typeof(SourceHtmlContractCases))]
    public void NativeRestrictedDataAuditUsesTheSameDataAssertions(string id) =>
        SourceHtmlContractCases.AssertAudit(SourceHtmlContractCases.Audit(id), new HtmlEntityDataAudit());

    [Theory, MemberData(nameof(SourceHtmlFailureCases.Cases), MemberType = typeof(SourceHtmlFailureCases))]
    public void NativeAuditRejectsTheSameMalformedDataAtItsBoundary(string id) =>
        SourceHtmlFailureCases.AssertContract(SourceHtmlFailureCases.Get(id), new HtmlEntityDataAudit());
}
