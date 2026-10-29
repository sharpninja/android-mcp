using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogAidlProfileCall(string Profile,string Shape,string Raw);
public sealed record CatalogAidlProfileReply(string Outcome,bool InheritedPresent,bool? InheritedOneWay,bool OwnOneWay,bool ContractOneWay,bool RootOneWay,
    bool DeclarationsRetained,bool ProfileSelected,bool SourceDispatchedOnce,bool InputsRetained);
public sealed record CatalogAidlProfileCase(string Id,CatalogAidlProfileCall Call,CatalogAidlProfileReply Expected);
public interface ICatalogAidlProfileProvider{Task<CatalogAidlProfileReply> BuildAsync(CatalogAidlProfileCall call);}
public static class CatalogAidlProfileCases
{
    public const int NegativeControls=10;
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var profile in new[]{"current-candidate","release-011"})
            foreach(var shape in new[]{"interface-oneway","method-oneway","ordinary"})
            {
                var current=profile=="current-candidate";var root=shape=="interface-oneway";var own=shape=="method-oneway";
                var raw=shape switch{"interface-oneway"=>"package a; oneway interface I { void ping(); }","method-oneway"=>"package a; interface I { oneway void ping(); }",_=>"package a; interface I { void ping(); }"};
                yield return new object[]{new CatalogAidlProfileCase(profile+"/"+shape,new(profile,shape,raw),new("ok",current,current ? root : null,own,own || current && root,root,true,true,true,true))};
            }
        }
    }
    public static async Task AssertCase(CatalogAidlProfileCase test,ICatalogAidlProfileProvider provider)=>Assert.Equal(test.Expected,await provider.BuildAsync(test.Call));
}
public sealed class ScriptedCatalogAidlProfile(CatalogAidlProfileCase test,int wrong=-1):ICatalogAidlProfileProvider
{
    public Task<CatalogAidlProfileReply> BuildAsync(CatalogAidlProfileCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{InheritedPresent=!value.InheritedPresent},2=>value with{InheritedOneWay=value.InheritedOneWay is null ? false : !value.InheritedOneWay.Value},
            3=>value with{OwnOneWay=!value.OwnOneWay},4=>value with{ContractOneWay=!value.ContractOneWay},5=>value with{RootOneWay=!value.RootOneWay},
            6=>value with{DeclarationsRetained=false},7=>value with{ProfileSelected=false},8=>value with{SourceDispatchedOnce=false},9=>value with{InputsRetained=false},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAidlProfileMockTests
{
    [Theory,MemberData(nameof(CatalogAidlProfileCases.All),MemberType=typeof(CatalogAidlProfileCases))]
    public Task CorrectOriginalProfileCallerResponsesPass(CatalogAidlProfileCase test)=>CatalogAidlProfileCases.AssertCase(test,new ScriptedCatalogAidlProfile(test));
    [Theory,MemberData(nameof(CatalogAidlProfileCases.All),MemberType=typeof(CatalogAidlProfileCases))]
    public async Task EveryProfileFieldPresenceOwnRootContractAndEffectMutationRejected(CatalogAidlProfileCase test)
    {for(var wrong=0;wrong<CatalogAidlProfileCases.NegativeControls;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogAidlProfileCases.AssertCase(test,new ScriptedCatalogAidlProfile(test,wrong))));}
}
