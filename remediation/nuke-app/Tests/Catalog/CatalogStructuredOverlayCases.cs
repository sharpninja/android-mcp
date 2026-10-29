using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogStructuredOverlayObservation(bool SelectedParentContract,bool CounterfactualParentRejected,bool FieldContract,
    bool ParentDocumentPin,bool FieldDocumentPin,bool ParentStateAndRevision,bool FieldStateAndRevision,bool MethodDependencyRepinned,
    bool TypeHasNoImplicitSelfDependency,bool HistoricalCandidateWithoutEntry,bool LocalRowsUnchanged,bool ImportedEntriesUnchanged,
    bool ImportedInventoryUnchanged,bool MembershipAndCountUnchanged,bool RegistryPinsUnchanged,long OverlayGenerationDelta,
    bool InventoryDigest,bool InventorySorted,string MethodState,string MethodAdmission,bool NoAuthorityDenied);
public static class CatalogStructuredOverlayCases
{
    public const int ObservationControls=21;
    public const string ParentAnnotation="android.annotation.Nullable";
    public const string Constant="17";
    public const string HistoricalSignature="aidl:a.I.P.Historical";
    public static IEnumerable<object[]> All=>new[]{"parent-field","constant-only","parent-only","historical-parent"}.Select(variant=>new object[]{
        new CatalogEffectiveInventoryCase("CAPTURE-STRUCTURED-"+variant,new("structured-"+variant),new("ok",Structured:new(true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,
            variant=="parent-field" ? 2 : 1,true,true,"available","admit-candidate",true)))});
    public static CatalogStructuredOverlayObservation Mutate(CatalogStructuredOverlayObservation value,int wrong)=>wrong switch
    {
        0=>value with{SelectedParentContract=false},1=>value with{CounterfactualParentRejected=false},2=>value with{FieldContract=false},
        3=>value with{ParentDocumentPin=false},4=>value with{FieldDocumentPin=false},5=>value with{ParentStateAndRevision=false},6=>value with{FieldStateAndRevision=false},
        7=>value with{MethodDependencyRepinned=false},8=>value with{TypeHasNoImplicitSelfDependency=false},9=>value with{HistoricalCandidateWithoutEntry=false},
        10=>value with{LocalRowsUnchanged=false},11=>value with{ImportedEntriesUnchanged=false},12=>value with{ImportedInventoryUnchanged=false},
        13=>value with{MembershipAndCountUnchanged=false},14=>value with{RegistryPinsUnchanged=false},15=>value with{OverlayGenerationDelta=value.OverlayGenerationDelta+1},
        16=>value with{InventoryDigest=false},17=>value with{InventorySorted=false},18=>value with{MethodState="unknown"},19=>value with{MethodAdmission="deny"},20=>value with{NoAuthorityDenied=false},_=>value
    };
}
public sealed class ScriptedCatalogStructuredOverlay(CatalogEffectiveInventoryCase test,int wrong=-1):ICatalogEffectiveInventoryCaseProvider
{
    public Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call)
    {Assert.Same(test.Call,call);return Task.FromResult(test.Expected with{Structured=CatalogStructuredOverlayCases.Mutate(test.Expected.Structured!,wrong)});}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogStructuredOverlayMockTests
{
    [Theory,MemberData(nameof(CatalogStructuredOverlayCases.All),MemberType=typeof(CatalogStructuredOverlayCases))]
    public Task CorrectOriginalParentFieldAndImportedBranchResponsesPass(CatalogEffectiveInventoryCase test)=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogStructuredOverlay(test));
    [Theory,MemberData(nameof(CatalogStructuredOverlayCases.All),MemberType=typeof(CatalogStructuredOverlayCases))]
    public async Task EveryShapePinDependencyCallerAndEffectMutationRejected(CatalogEffectiveInventoryCase test)
    {
        for(var wrong=0;wrong<CatalogStructuredOverlayCases.ObservationControls;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogStructuredOverlay(test,wrong))));
        for(var wrong=0;wrong<17;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogEffectiveInventory(test,wrong))));
    }
}
