using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

// Original validate_group uses numeric truthiness after the existing schema
// has admitted an integer. It does not narrow that admitted value to Int32.
public static class CatalogMetadataNumericDomainCases
{
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var (id,token) in new[]
            {
                ("ZERO","0"),("ZERO-FRACTION","0.0"),("ZERO-EXPONENT","0e8"),
                ("INT32-MAX","2147483647"),("INT32-PLUS-ONE","2147483648"),
                ("INT64-MAX","9223372036854775807"),("BEYOND-INT64","9223372036854775808"),
                ("INTEGER-FRACTION","1.0"),("INTEGER-EXPONENT","1e0"),("LARGE-FINITE-EXPONENT","1e20")
            })
            {
                var document=MetadataCrudCases.Group();document["estimated_tokens"]=JsonNode.Parse(token);
                document["estimator_version"]="fixture-estimator-v1";
                yield return new object[]{new CatalogMetadataBodyCase("META-NUMERIC-"+id,"validate_group",
                    new("numeric-token-domain",document),new(true))};
            }
            foreach(var token in new[]{"2147483648","1e20"})
            {
                var document=MetadataCrudCases.Group();document["estimated_tokens"]=JsonNode.Parse(token);
                yield return new object[]{new CatalogMetadataBodyCase("META-NUMERIC-MISSING-ESTIMATOR-"+token,"validate_group",
                    new("numeric-token-domain",document),new(false,ErrorContains:"Nonzero token estimate needs estimator version"))};
            }
            foreach(var token in new[]{"-1","0.5"})
            {
                var document=MetadataCrudCases.Group();document["estimated_tokens"]=JsonNode.Parse(token);
                document["estimator_version"]="fixture-estimator-v1";
                yield return new object[]{new CatalogMetadataBodyCase("META-NUMERIC-SCHEMA-REJECT-"+token,"validate_group",
                    new("numeric-token-domain",document),new(false,ErrorContains:"Invalid FunctionalGroup"))};
            }
        }
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataNumericDomainMockTests
{
    [Theory,MemberData(nameof(CatalogMetadataNumericDomainCases.All),MemberType=typeof(CatalogMetadataNumericDomainCases))]
    public Task CorrectOriginalNumericDomainResponsesPass(CatalogMetadataBodyCase test)
        =>CatalogMetadataBodyCases.AssertCase(test,new ScriptedCatalogMetadataBody(test));

    [Theory,MemberData(nameof(CatalogMetadataNumericDomainCases.All),MemberType=typeof(CatalogMetadataNumericDomainCases))]
    public async Task EachOutcomeErrorAndInputAssertionRejectsWrongResponses(CatalogMetadataBodyCase test)
    {
        for(var wrong=0;wrong<7;wrong++)
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMetadataBodyCases.AssertCase(test,new ScriptedCatalogMetadataBody(test,wrong))));
    }
}
