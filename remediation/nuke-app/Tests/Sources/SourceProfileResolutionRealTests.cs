using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
public sealed class SourceProfileResolutionRealTests
{
    [Theory, MemberData(nameof(SourceProfileResolutionContractCases.Cases), MemberType = typeof(SourceProfileResolutionContractCases))]
    public void NativeSharedResolverPreservesSameEightCatalogBehaviorCases(string id) =>
        SourceProfileResolutionContractCases.AssertContract(SourceProfileResolutionContractCases.Get(id), SourceExtractionProfile.Resolve);
}
