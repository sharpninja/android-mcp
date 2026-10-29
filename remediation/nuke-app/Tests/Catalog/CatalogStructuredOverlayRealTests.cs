using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogStructuredOverlayRealTests
{
    [Theory,MemberData(nameof(CatalogStructuredOverlayCases.All),MemberType=typeof(CatalogStructuredOverlayCases))]
    public Task SameParentFieldAndImportedCasesUseExistingNativeCaptureAndCallers(CatalogEffectiveInventoryCase test)=>
        CatalogEffectiveInventoryCases.AssertCase(test,new NativeCatalogEffectiveInventoryProvider());
}
