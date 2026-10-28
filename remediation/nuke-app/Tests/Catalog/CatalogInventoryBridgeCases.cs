using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogInventoryBridgeCall(string Variant);
public sealed record CatalogInventoryBridgeReply(string Outcome,int? LogicalRows=null,string? VerifiedLogicalRows=null,string? NativeStatuses=null,
    bool? ExactOriginalRows=null,bool? ProjectionPreserved=null,bool? ExplicitCatalogMetadataPreserved=null,bool? ExplicitRawRootPreserved=null,
    bool? ExtractionBindingPreserved=null,bool? UnselectedHasNoCurrentProof=null,bool? LogicalAliasesRetained=null,bool? TypedContextRequired=null,
    bool NoEffects=true,bool InputsRetained=true);
public sealed record CatalogInventoryBridgeCase(string Id,CatalogInventoryBridgeCall Call,CatalogInventoryBridgeReply Expected);
public interface ICatalogInventoryBridgeCaseProvider { CatalogInventoryBridgeReply Adapt(CatalogInventoryBridgeCall call); }
public static class CatalogInventoryBridgeCases
{
    // Three ORIGINAL logical rows: rows0/1 alias physical0, row2 physical1.
    // Historical status is deliberately not current acquisition proof. Explicit
    // metadata is registry7/android'fixture Android'/API37/selected coverage.
    private static CatalogInventoryBridgeReply Success(string verified,string statuses)=>new("ok",3,verified,statuses,true,true,true,true,true,true,true,true);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogInventoryBridgeCase("BRIDGE-01-reused-physical-proves-two-distinct-logical-aliases",new("reused-alias"),Success("0,1","0:reused;1:reused;2:not-selected")),
        new CatalogInventoryBridgeCase("BRIDGE-02-current-proof-overrides-only-eligibility-not-original-body",new("all-selected"),Success("0,1,2","0:downloaded;1:downloaded;2:reused")),
        new CatalogInventoryBridgeCase("BRIDGE-03-historical-downloaded-is-not-current-proof",new("none-selected"),Success("","0:not-selected;1:not-selected;2:not-selected")),
        new CatalogInventoryBridgeCase("BRIDGE-04-selected-physical-needs-verified-acquisition",new("missing-verified-source"),new("catalog-inventory-proof")),
        new CatalogInventoryBridgeCase("BRIDGE-05-extraction-projection-cannot-be-swapped",new("foreign-extraction"),new("catalog-extraction-projection")),
        new CatalogInventoryBridgeCase("BRIDGE-06-catalog-metadata-is-explicit-and-valid",new("invalid-metadata"),new("catalog-metadata")),
        new CatalogInventoryBridgeCase("BRIDGE-07-rawroot-is-explicit-not-inventory-artifact-parent",new("relative-raw-root"),new("catalog-raw-root"))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogInventoryBridgeCase test,ICatalogInventoryBridgeCaseProvider provider)=>Assert.Equal(test.Expected,provider.Adapt(test.Call));
}
public sealed class ScriptedCatalogInventoryBridge(CatalogInventoryBridgeCase test,int wrong=-1):ICatalogInventoryBridgeCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public CatalogInventoryBridgeReply Adapt(CatalogInventoryBridgeCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{LogicalRows=(value.LogicalRows ?? 0)+1},2=>value with{VerifiedLogicalRows="wrong"},3=>value with{NativeStatuses="wrong"},
            4=>value with{ExactOriginalRows=Flip(value.ExactOriginalRows)},5=>value with{ProjectionPreserved=Flip(value.ProjectionPreserved)},6=>value with{ExplicitCatalogMetadataPreserved=Flip(value.ExplicitCatalogMetadataPreserved)},
            7=>value with{ExplicitRawRootPreserved=Flip(value.ExplicitRawRootPreserved)},8=>value with{ExtractionBindingPreserved=Flip(value.ExtractionBindingPreserved)},9=>value with{UnselectedHasNoCurrentProof=Flip(value.UnselectedHasNoCurrentProof)},
            10=>value with{LogicalAliasesRetained=Flip(value.LogicalAliasesRetained)},11=>value with{TypedContextRequired=Flip(value.TypedContextRequired)},12=>value with{NoEffects=!value.NoEffects},13=>value with{InputsRetained=!value.InputsRetained},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogInventoryBridgeMockTests
{
    [Theory,MemberData(nameof(CatalogInventoryBridgeCases.All),MemberType=typeof(CatalogInventoryBridgeCases))]
    public void CorrectTypedInventoryBridgeResponsesPass(CatalogInventoryBridgeCase test)=>CatalogInventoryBridgeCases.AssertCase(test,new ScriptedCatalogInventoryBridge(test));
    [Theory,MemberData(nameof(CatalogInventoryBridgeCases.All),MemberType=typeof(CatalogInventoryBridgeCases))]
    public void EveryIdentitySelectionMetadataRootAndSideEffectMutationRejected(CatalogInventoryBridgeCase test)
    {for(var wrong=0;wrong<14;wrong++) Assert.NotNull(Record.Exception(()=>CatalogInventoryBridgeCases.AssertCase(test,new ScriptedCatalogInventoryBridge(test,wrong))));}
}
