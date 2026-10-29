using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogLocalOwnerRealTests
{
    [Theory,MemberData(nameof(CatalogLocalOwnerCases.All),MemberType=typeof(CatalogLocalOwnerCases))]
    public Task ExistingNativeCaptureResolveAndPolicyUseSameOwnerCases(CatalogEffectiveInventoryCase test)=>
        CatalogEffectiveInventoryCases.AssertCase(test,new NativeCatalogEffectiveInventoryProvider());
}
