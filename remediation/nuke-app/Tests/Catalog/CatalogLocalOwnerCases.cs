using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogLocalOwnerObservation(string EntityKind,int VariantCount,string UnknownReason,int OwnerCandidates,int DependencyCount,
    string DependencyIds,bool ContractMatches,bool DependencyHashMatches,bool PinsMatch,bool SchemaAbsent,bool ContractAbsent,bool GraphAbsent,
    string ReasonCodes,bool Ordered,bool BaseEntitiesUnchanged,bool CaptureWritesNone);
public static class CatalogLocalOwnerCases
{
    public const int NegativeControls=33;
    public const string OwnerId="ccb3fe9fb454c63daf45ce6282838a11209916ea73e8fecd01b7e2ecb92173ff";
    public const string MissingOwner="Containing local type is absent or ambiguous";
    public const string LocalAvailability="Local addition has no imported version applicability evidence";
    public const string SecondOwnerSource="\npackage a {\n public class I {\n }\n}\n";
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var variant in new[]{"owner-absent","owner-ambiguous","owner-unique"})
            {
                var unique=variant=="owner-unique";var candidates=unique ? 1 : variant=="owner-absent" ? 0 : 2;
                yield return new object[]{new CatalogEffectiveInventoryCase(variant,new(variant),new("ok",InventoryDelta:1,BaseMembershipRetained:true,
                    BindingState:"unknown",CatalogState:"active",Admission:"deny",InventoryHashChanged:true,OverlayGenerationDelta:1,
                    LocalOwner:new("binding",1,LocalAvailability+(unique ? "" : "; "+MissingOwner),candidates,unique ? 1 : 0,unique ? OwnerId : "",
                        true,true,true,true,true,true,"APPLICABILITY_UNKNOWN",true,true,true)))};
            }
        }
    }
}
public sealed class ScriptedCatalogLocalOwner(CatalogEffectiveInventoryCase test,int wrong=-1):ICatalogEffectiveInventoryCaseProvider
{
    public async Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call)
    {
        if(wrong<17)return await new ScriptedCatalogEffectiveInventory(test,wrong).CaptureAsync(call);
        Assert.Same(test.Call,call);var value=test.Expected;var row=value.LocalOwner!;
        return value with{LocalOwner=wrong switch
        {
            17=>row with{EntityKind="other"},18=>row with{VariantCount=2},19=>row with{UnknownReason=""},20=>row with{OwnerCandidates=row.OwnerCandidates+1},
            21=>row with{DependencyCount=row.DependencyCount+1},22=>row with{DependencyIds="wrong"},23=>row with{ContractMatches=false},24=>row with{DependencyHashMatches=false},
            25=>row with{PinsMatch=false},26=>row with{SchemaAbsent=false},27=>row with{ContractAbsent=false},28=>row with{GraphAbsent=false},
            29=>row with{ReasonCodes="APPLICABLE_EVIDENCE"},30=>row with{Ordered=false},31=>row with{BaseEntitiesUnchanged=false},32=>row with{CaptureWritesNone=false},_=>row
        }};
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogLocalOwnerMockTests
{
    [Theory,MemberData(nameof(CatalogLocalOwnerCases.All),MemberType=typeof(CatalogLocalOwnerCases))]
    public Task CorrectOriginalContainingOwnerBranchResponsesPass(CatalogEffectiveInventoryCase test)=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogLocalOwner(test));
    [Theory,MemberData(nameof(CatalogLocalOwnerCases.All),MemberType=typeof(CatalogLocalOwnerCases))]
    public async Task EveryContainingTypeDependencyPinApplicabilityAndEffectMutationRejected(CatalogEffectiveInventoryCase test)
    {for(var wrong=0;wrong<CatalogLocalOwnerCases.NegativeControls;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogEffectiveInventoryCases.AssertCase(test,new ScriptedCatalogLocalOwner(test,wrong))));}
}
