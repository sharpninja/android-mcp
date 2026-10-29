using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogCaptureAdmissionRealTests
{
    [Theory,MemberData(nameof(CatalogCaptureAdmissionCases.All),MemberType=typeof(CatalogCaptureAdmissionCases))]
    public Task ExistingNativeCaptureUsesSameIndependentAdmissionGuardCases(CatalogEffectiveInventoryCase test)=>
        CatalogEffectiveInventoryCases.AssertCase(test,new NativeCatalogEffectiveInventoryProvider());
}
