using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class MetadataReasonWhitespaceRealTests
{
    [Theory,MemberData(nameof(MetadataReasonWhitespaceCases.All),MemberType=typeof(MetadataReasonWhitespaceCases))]
    public void SameOriginalReasonCasesBindExistingNativeSqliteAppend(MetadataCrudCase test)
    {
        using var provider=new NativeMetadataProvider();MetadataCrudCases.AssertCase(test,provider);
    }
}
