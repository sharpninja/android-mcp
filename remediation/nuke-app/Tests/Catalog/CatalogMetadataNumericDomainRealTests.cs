using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataNumericDomainRealTests
{
    [Theory,MemberData(nameof(CatalogMetadataNumericDomainCases.All),MemberType=typeof(CatalogMetadataNumericDomainCases))]
    public Task SameOriginalNumericDomainAssertionsBindNativeValidation(CatalogMetadataBodyCase test)
        =>CatalogMetadataBodyCases.AssertCase(test,new NativeCatalogMetadataBodyProvider());
}
