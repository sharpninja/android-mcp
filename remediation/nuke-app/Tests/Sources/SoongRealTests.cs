using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class SoongRealTests
{
    private static ISoongSourceFacts Provider() => new SoongSourceFacts(new GlobMatcher());
    [Theory, MemberData(nameof(SoongContractCases.Cases), MemberType = typeof(SoongContractCases))]
    public void NativeBuildModulesUseTheSameStaticFacts(string id) => SoongContractCases.AssertContract(SoongContractCases.Get(id), Provider());
    [Theory, MemberData(nameof(SoongAuxiliaryCases.LiteralCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void NativeLiteralProjectionUsesTheSameAmbiguityAssertions(string id) => SoongAuxiliaryCases.AssertLiteral(SoongAuxiliaryCases.Literals.Single(x => x.Id == id), Provider());
    [Theory, MemberData(nameof(SoongAuxiliaryCases.MatchCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void NativeSelectorsUseTheSameSegmentAndRefusalAssertions(string id) => SoongAuxiliaryCases.AssertMatch(SoongAuxiliaryCases.Matches.Single(x => x.Id == id), Provider());
    [Theory, MemberData(nameof(SoongAuxiliaryCases.ManifestCases), MemberType = typeof(SoongAuxiliaryCases))]
    public void NativeApexPathsUseTheSameExplicitOnlyAssertions(string id) => SoongAuxiliaryCases.AssertManifest(SoongAuxiliaryCases.Manifests.Single(x => x.Id == id), Provider());
}
