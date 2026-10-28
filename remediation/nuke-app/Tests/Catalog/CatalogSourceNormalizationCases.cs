using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogSourceNormalizationCall(string Variant);
public sealed record CatalogSourceNormalizationReply(string Outcome,long? ApiLevel=null,string? Channel=null,
    bool OriginalRowUnchanged=true,bool ProjectionUnchanged=true,bool SourceContextUnchanged=true,
    bool ProvidedNormalizationAudit=true,bool NoApplicabilityInference=true,bool NoEffects=true,bool InputsRetained=true);
public sealed record CatalogSourceNormalizationCase(string Id,CatalogSourceNormalizationCall Call,CatalogSourceNormalizationReply Expected);
public interface ICatalogSourceNormalizationCaseProvider { CatalogSourceNormalizationReply Normalize(CatalogSourceNormalizationCall call); }
public static class CatalogSourceNormalizationCases
{
    // Recipe SDK row has platform context37.2 but no api_level/channel. Provided
    // metadata is a distinct Catalog assertion. Global API999 must not fill NULL.
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogSourceNormalizationCase("NORMALIZE-01-explicit-fixture-catalog-context",new("explicit"),new("ok",37,"fixture")),
        new CatalogSourceNormalizationCase("NORMALIZE-02-null-API-remains-unknown",new("null-api"),new("ok",null,"fixture")),
        new CatalogSourceNormalizationCase("NORMALIZE-03-selected-row-requires-sidecar",new("missing"),new("catalog-source-normalization-missing")),
        new CatalogSourceNormalizationCase("NORMALIZE-04-foreign-row-rejected",new("foreign-row"),new("catalog-source-normalization-row")),
        new CatalogSourceNormalizationCase("NORMALIZE-05-original-row-hash-cannot-substitute",new("foreign-hash"),new("catalog-source-normalization-identity")),
        new CatalogSourceNormalizationCase("NORMALIZE-06-kind-context-identity-not-editable",new("identity-field"),new("catalog-source-normalization-schema")),
        new CatalogSourceNormalizationCase("NORMALIZE-07-invalid-positive-API-rejected",new("invalid-api"),new("catalog-source-normalization-metadata")),
        new CatalogSourceNormalizationCase("NORMALIZE-08-explicit-nonempty-channel-required",new("empty-channel"),new("catalog-source-normalization-metadata"))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogSourceNormalizationCase test,ICatalogSourceNormalizationCaseProvider provider)=>Assert.Equal(test.Expected,provider.Normalize(test.Call));
}
public sealed class ScriptedCatalogSourceNormalization(CatalogSourceNormalizationCase test,int wrong=-1):ICatalogSourceNormalizationCaseProvider
{
    public CatalogSourceNormalizationReply Normalize(CatalogSourceNormalizationCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{ApiLevel=(value.ApiLevel??0)+1},2=>value with{Channel="wrong"},
            3=>value with{OriginalRowUnchanged=!value.OriginalRowUnchanged},4=>value with{ProjectionUnchanged=!value.ProjectionUnchanged},
            5=>value with{SourceContextUnchanged=!value.SourceContextUnchanged},6=>value with{ProvidedNormalizationAudit=!value.ProvidedNormalizationAudit},
            7=>value with{NoApplicabilityInference=!value.NoApplicabilityInference},8=>value with{NoEffects=!value.NoEffects},
            9=>value with{InputsRetained=!value.InputsRetained},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourceNormalizationMockTests
{
    [Theory,MemberData(nameof(CatalogSourceNormalizationCases.All),MemberType=typeof(CatalogSourceNormalizationCases))]
    public void CorrectProvidedNormalizationAndFailClosedRepliesPass(CatalogSourceNormalizationCase test)=>CatalogSourceNormalizationCases.AssertCase(test,new ScriptedCatalogSourceNormalization(test));
    [Theory,MemberData(nameof(CatalogSourceNormalizationCases.All),MemberType=typeof(CatalogSourceNormalizationCases))]
    public void EveryMetadataIdentityAuditAndEffectMutationRejected(CatalogSourceNormalizationCase test)
    {for(var wrong=0;wrong<10;wrong++)Assert.NotNull(Record.Exception(()=>CatalogSourceNormalizationCases.AssertCase(test,new ScriptedCatalogSourceNormalization(test,wrong))));}
}
