using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogProducerIdentityCall(string Variant);
public sealed record CatalogProducerIdentityReply(string Outcome,bool ProcessingChanged,bool SourceChanged,bool RegistryChanged,int ContractCount,
    bool ProfileCorrect,bool NativeIdentityCorrect,bool InputsRetained,bool PublicationComplete,bool NoPartialEffects,bool RootIndependent);
public sealed record CatalogProducerIdentityCase(string Id,CatalogProducerIdentityCall Call,CatalogProducerIdentityReply Expected);
public interface ICatalogProducerIdentityProvider{Task<CatalogProducerIdentityReply> BuildAsync(CatalogProducerIdentityCall call);}
public static class CatalogProducerIdentityCases
{
    public const int NegativeControls=11;
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var variant in new[]{"replay","profile","entry","permission","resolution","ddl","metadata-used","metadata-unused","metadata-unused-missing","root-order","native-code","intake-profile-wins","profile-mismatch","contract-snapshot"})
            {
                var error=variant=="profile-mismatch";
                var changes=variant is "profile" or "entry" or "permission" or "resolution" or "ddl" or "metadata-used" or "native-code" or "intake-profile-wins";
                var count=error ? 0 : variant is "metadata-unused" or "metadata-unused-missing" ? 4 : 5;
                yield return new object[]{new CatalogProducerIdentityCase("PRODUCER-V2-"+variant,new(variant),new(error ? "catalog-profile-mismatch" : "ok",changes,changes,changes,count,true,true,true,!error,true,true))};
            }
        }
    }
    public static async Task AssertCase(CatalogProducerIdentityCase test,ICatalogProducerIdentityProvider provider)=>Assert.Equal(test.Expected,await provider.BuildAsync(test.Call));
}
public sealed class ScriptedCatalogProducerIdentity(CatalogProducerIdentityCase test,int wrong=-1):ICatalogProducerIdentityProvider
{
    public Task<CatalogProducerIdentityReply> BuildAsync(CatalogProducerIdentityCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{ProcessingChanged=!value.ProcessingChanged},2=>value with{SourceChanged=!value.SourceChanged},3=>value with{RegistryChanged=!value.RegistryChanged},4=>value with{ContractCount=value.ContractCount+1},
            5=>value with{ProfileCorrect=false},6=>value with{NativeIdentityCorrect=false},7=>value with{InputsRetained=false},8=>value with{PublicationComplete=!value.PublicationComplete},9=>value with{NoPartialEffects=false},10=>value with{RootIndependent=false},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogProducerIdentityMockTests
{
    [Theory,MemberData(nameof(CatalogProducerIdentityCases.All),MemberType=typeof(CatalogProducerIdentityCases))]
    public Task CorrectAdmittedIdentityAndCoupledCallerResponsesPass(CatalogProducerIdentityCase test)=>CatalogProducerIdentityCases.AssertCase(test,new ScriptedCatalogProducerIdentity(test));
    [Theory,MemberData(nameof(CatalogProducerIdentityCases.All),MemberType=typeof(CatalogProducerIdentityCases))]
    public async Task EveryIdentitySourceRegistryModeAndEffectMutationRejected(CatalogProducerIdentityCase test)
    {for(var wrong=0;wrong<CatalogProducerIdentityCases.NegativeControls;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogProducerIdentityCases.AssertCase(test,new ScriptedCatalogProducerIdentity(test,wrong))));}
}
