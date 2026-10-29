using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationFixtureCarrierRealTests
{
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.Cases),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void SameSevenPinnedDataCasesBindNativeMap(DocumentationFixtureCarrierCase test)
        =>DocumentationFixtureCarrierCases.AssertCarrier(test,new DocumentationFixtureCarrierMap());
    [Theory,MemberData(nameof(DocumentationFixtureCarrierCases.UnknownPaths),MemberType=typeof(DocumentationFixtureCarrierCases))]
    public void SameUnknownAndCurrentCanonicalPathsCannotBeRouted(string path)
        =>DocumentationFixtureCarrierCases.AssertUnknown(path,new DocumentationFixtureCarrierMap());
}
