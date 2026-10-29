using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogTombstoneRealTests
{
    [Theory,MemberData(nameof(CatalogTombstoneCases.All),MemberType=typeof(CatalogTombstoneCases))]
    public Task ExistingNativeCaptureUsesUnchangedMockProvenInactiveLocalCases(CatalogEffectiveInventoryCase test)=>
        CatalogEffectiveInventoryCases.AssertCase(test,new NativeCatalogEffectiveInventoryProvider());
}
