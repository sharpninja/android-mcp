using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogTombstoneObservation(string EntryId,string EntityKind,int VariantCount,long Revision,
    string ReasonCodes,bool SchemaAbsent,bool ContractAbsent,bool GraphAbsent,int DependencyCount,bool DocumentHashMatches,
    bool SnapshotPinsMatch,bool InventoryDigestMatches,bool Ordered,bool BaseEntitiesUnchanged,bool OverlayUnchanged,
    bool HistoryUnchanged,long CatalogGenerationDelta,long CaptureOverlayGenerationDelta);

public static class CatalogTombstoneCases
{
    public sealed record DocumentFixture(string Name,string? Raw,string? CanonicalHash);
    public const string Signature="aidl:a.I#added(int)";
    // SHA256 of the signature above, independently computed before the mock gate.
    public const string EntryId="6580d74e4f77c4959a4dec983a787e029d6c5614ec5e2852b230668b08f27efd";
    public const string OpaqueDocument="{ \"retained\" : \"opaque tombstone\" }";
    // SHA256 of the independently specified canonical {"retained":"opaque tombstone"}.
    public const string OpaqueHash="22b398c70d9e02c88f20299f53d41d6bad9d36c71e7528f42f13197e0ee71990";
    public const int NegativeControls=35;
    // json_valid(document_json) accepts every JSON value kind. The original
    // inactive branch hashes the value without applying the Entry schema.
    public static IReadOnlyList<DocumentFixture> Documents {get;}=new DocumentFixture[]
    {
        new("entry",null,null),new("opaque",OpaqueDocument,OpaqueHash),
        new("array"," [ 1 , { \"retained\" : \"opaque tombstone\" } ] ","3907ea12094a74abaa5a6041040069b2324f8d2de339cbade46a0cbf9004e37f"),
        new("text"," \"retained tombstone\" ","0d6b591f41ab9aa53d9b9908399a6852932ecfe2a3e25c8666116dd43165f815"),
        new("number"," 7 ","7902699be42c8a8e46fbbb4501726517e86b22c56a189f7625a6da49081b2451"),
        new("boolean"," false ","fcbcf165908dd18a9e49f7ff27810176db8e9f63b4352213741664245224f8aa"),
        new("null"," null ","74234e98afe7498fb5daf1f36ac2d78acc339464f950703b8c019892f982b90b")
    };
    public static DocumentFixture Document(CatalogEffectiveInventoryCall call)=>
        Documents.Single(document=>document.Name==(call.Variant.Split('-').Length==2 ? "entry" : call.Variant.Split('-')[2]));
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var state in new[]{"deleted","conflict"})
            foreach(var document in Documents)
            {
                var variant="tombstone-"+state+(document.Name=="entry" ? "" : "-"+document.Name);
                yield return new object[]{new CatalogEffectiveInventoryCase(variant,new(variant),
                    new("ok",InventoryDelta:1,BaseMembershipRetained:true,BindingState:"unknown",CatalogState:state,
                        InventoryHashChanged:true,OverlayGenerationDelta:1,
                        Tombstone:new(EntryId,"other",0,1,"NON_EXECUTABLE_INVENTORY_NODE",true,true,true,0,true,true,true,true,true,true,true,0,0)))};
            }
        }
    }
}

public sealed class ScriptedCatalogTombstone(CatalogEffectiveInventoryCase test,int wrong=-1):ICatalogEffectiveInventoryCaseProvider
{
    public async Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call)
    {
        if(wrong<17)return await new ScriptedCatalogEffectiveInventory(test,wrong).CaptureAsync(call);
        Assert.Same(test.Call,call);var value=test.Expected;var row=value.Tombstone!;
        return value with{Tombstone=wrong switch
        {
            17=>row with{EntryId=new string('0',64)},18=>row with{EntityKind="binding"},19=>row with{VariantCount=1},20=>row with{Revision=2},
            21=>row with{ReasonCodes="APPLICABLE_EVIDENCE"},22=>row with{SchemaAbsent=false},23=>row with{ContractAbsent=false},24=>row with{GraphAbsent=false},
            25=>row with{DependencyCount=1},26=>row with{DocumentHashMatches=false},27=>row with{SnapshotPinsMatch=false},28=>row with{InventoryDigestMatches=false},
            29=>row with{Ordered=false},30=>row with{BaseEntitiesUnchanged=false},31=>row with{OverlayUnchanged=false},32=>row with{HistoryUnchanged=false},
            33=>row with{CatalogGenerationDelta=1},34=>row with{CaptureOverlayGenerationDelta=1},_=>row
        }};
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogTombstoneMockTests
{
    [Theory,MemberData(nameof(CatalogTombstoneCases.All),MemberType=typeof(CatalogTombstoneCases))]
    public Task CorrectOriginalInactiveLocalBranchResponsesPass(CatalogEffectiveInventoryCase test)=>
        CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogTombstone(test));

    [Theory,MemberData(nameof(CatalogTombstoneCases.All),MemberType=typeof(CatalogTombstoneCases))]
    public async Task EveryMembershipShapePinTransactionAndSideEffectMutationRejected(CatalogEffectiveInventoryCase test)
    {
        for(var wrong=0;wrong<CatalogTombstoneCases.NegativeControls;wrong++)
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogTombstone(test,wrong))));
    }
}
