using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SourceTypeFactsRealTests
{
    private static SourceTypeFacts Provider() => new(new JvmSourceLinker(), new CanonicalJson());
    [Theory, MemberData(nameof(SourceTypeFactsContractCases.XmlCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void NativeXmlKeysUseTheSameOriginalSpellingAssertions(string id) => SourceTypeFactsContractCases.AssertXml(SourceTypeFactsContractCases.Xml.Single(x => x.Id == id), Provider());
    [Theory, MemberData(nameof(SourceTypeFactsContractCases.FormalCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void NativeFormalsUseTheSameExactLegacyFailureAndSetAssertions(string id) => SourceTypeFactsContractCases.AssertFormals(SourceTypeFactsContractCases.Formals.Single(x => x.Id == id), Provider());
    [Theory, MemberData(nameof(SourceTypeFactsContractCases.HeaderCases), MemberType = typeof(SourceTypeFactsContractCases))]
    public void NativeStructuralHeadersUseTheSameRawAndCanonicalTextAssertions(string id) => SourceTypeFactsContractCases.AssertHeader(SourceTypeFactsContractCases.Headers.Single(x => x.Id == id), Provider());
}
