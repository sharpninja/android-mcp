using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogEffectiveInventoryCall(string Variant);
public sealed record CatalogEffectiveInventoryReply(string Outcome,int? InventoryDelta=null,bool? BaseMembershipRetained=null,string? BindingState=null,string? CatalogState=null,
    string? Admission=null,string? NoAuthorityAdmission=null,string? ChangedDocumentAdmission=null,bool? SchemaChanged=null,bool? DocumentPinChanged=null,
    bool? InventoryHashChanged=null,long? OverlayGenerationDelta=null,string? DependencyCatalogState=null,bool ImportedInventoryUnchanged=true,
    bool TransactionClosed=true,bool InputsRetained=true,bool NoPartialCapture=true,CatalogTombstoneObservation? Tombstone=null,CatalogLocalOwnerObservation? LocalOwner=null,CatalogStructuredOverlayObservation? Structured=null);
public sealed record CatalogEffectiveInventoryCase(string Id,CatalogEffectiveInventoryCall Call,CatalogEffectiveInventoryReply Expected);
public interface ICatalogEffectiveInventoryCaseProvider { Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call); }
public static class CatalogEffectiveInventoryCases
{
    // Exact raw AIDL from the five preserved EffectiveInventory methods. The
    // shared original three-source Build fixture supplies the remaining inputs.
    public const string Aidl="package a; interface I { parcelable P { int x; } void take(in P p); }";
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogEffectiveInventoryCase("EFFECTIVE-01-suppression-is-not-version-absence",new("suppressed-binding"),new("ok",0,true,"available","suppressed",Admission:"deny")),
        new CatalogEffectiveInventoryCase("EFFECTIVE-02-local-addition-visible-version-unknown",new("local-addition"),new("ok",1,true,"unknown","active")),
        new CatalogEffectiveInventoryCase("EFFECTIVE-03-field-override-repins-transitive-schema",new("field-override"),new("ok",0,true,"available","active",SchemaChanged:true,InventoryHashChanged:true,OverlayGenerationDelta:1)),
        new CatalogEffectiveInventoryCase("EFFECTIVE-04-current-document-pin-is-separate-from-schema",new("document-pin"),new("ok",0,true,"available","active",Admission:"admit-candidate",NoAuthorityAdmission:"os-unavailable",ChangedDocumentAdmission:"deny",SchemaChanged:false,DocumentPinChanged:true)),
        new CatalogEffectiveInventoryCase("EFFECTIVE-05-suppressed-type-denies-known-binding",new("suppressed-type"),new("ok",0,true,"available","active",Admission:"deny",DependencyCatalogState:"suppressed")),
        new CatalogEffectiveInventoryCase("LOAD-01-missing-contract-version-never-synthesizes",new("missing-version"),new("No supported complete device inventory contract")),
        new CatalogEffectiveInventoryCase("LOAD-02-missing-projection-row-never-synthesizes",new("missing-projection"),new("Incomplete registry inventory")),
        new CatalogEffectiveInventoryCase("LOAD-03-record-digest-mismatch-rejected",new("wrong-digest"),new("Inventory identity or digest mismatch")),
        new CatalogEffectiveInventoryCase("LOAD-04-record-identity-mismatch-rejected",new("wrong-identity"),new("Inventory identity or digest mismatch")),
        new CatalogEffectiveInventoryCase("CAPTURE-01-outer-transaction-preserved-on-refusal",new("caller-transaction"),new("Reference inventory capture owns its read transaction",TransactionClosed:false)),
        new CatalogEffectiveInventoryCase("CAPTURE-02-overlay-canonical-identity-rejected",new("invalid-overlay-identity"),new("Local overlay canonical identity mismatch")),
        new CatalogEffectiveInventoryCase("CAPTURE-03-missing-structured-shape-not-invented",new("missing-structured-shape"),new("Structured type override needs a producer shape"))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogEffectiveInventoryCase test,ICatalogEffectiveInventoryCaseProvider provider)=>Assert.Equal(test.Expected,await provider.CaptureAsync(test.Call));
}
public sealed class ScriptedCatalogEffectiveInventory(CatalogEffectiveInventoryCase test,int wrong=-1):ICatalogEffectiveInventoryCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{InventoryDelta=(value.InventoryDelta ?? 0)+1},2=>value with{BaseMembershipRetained=Flip(value.BaseMembershipRetained)},
            3=>value with{BindingState="wrong"},4=>value with{CatalogState="wrong"},5=>value with{Admission="wrong"},6=>value with{NoAuthorityAdmission="wrong"},7=>value with{ChangedDocumentAdmission="wrong"},
            8=>value with{SchemaChanged=Flip(value.SchemaChanged)},9=>value with{DocumentPinChanged=Flip(value.DocumentPinChanged)},10=>value with{InventoryHashChanged=Flip(value.InventoryHashChanged)},
            11=>value with{OverlayGenerationDelta=(value.OverlayGenerationDelta ?? 0)+1},12=>value with{DependencyCatalogState="wrong"},13=>value with{ImportedInventoryUnchanged=!value.ImportedInventoryUnchanged},
            14=>value with{TransactionClosed=!value.TransactionClosed},15=>value with{InputsRetained=!value.InputsRetained},16=>value with{NoPartialCapture=!value.NoPartialCapture},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogEffectiveInventoryMockTests
{
    [Theory,MemberData(nameof(CatalogEffectiveInventoryCases.All),MemberType=typeof(CatalogEffectiveInventoryCases))]
    public Task CorrectOriginalCaptureAndCheckedLoaderResponsesPass(CatalogEffectiveInventoryCase test)=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogEffectiveInventory(test));
    [Theory,MemberData(nameof(CatalogEffectiveInventoryCases.All),MemberType=typeof(CatalogEffectiveInventoryCases))]
    public async Task EveryExistenceExposurePinTransactionAndInventoryMutationRejected(CatalogEffectiveInventoryCase test)
    {for(var wrong=0;wrong<17;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogEffectiveInventory(test,wrong))));}
}
