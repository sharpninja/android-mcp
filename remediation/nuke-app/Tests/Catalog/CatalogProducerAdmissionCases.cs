using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogProducerAdmissionCall(string Variant);
public sealed record CatalogProducerAdmissionReply(string Outcome,bool AdmissionObserved,bool ProfileCorrect,bool CurrentAssemblyVerified,bool NoUnexpectedEffects,bool InputsRetained);
public sealed record CatalogProducerAdmissionCase(string Id,CatalogProducerAdmissionCall Call,CatalogProducerAdmissionReply Expected);
public interface ICatalogProducerAdmissionProvider{Task<CatalogProducerAdmissionReply> BuildAsync(CatalogProducerAdmissionCall call);}
public static class CatalogProducerAdmissionCases
{
    public const int NegativeControls=6;
    public static IEnumerable<object[]> All=>new[]{"current","release","wrong-assembly","wrong-context"}.Select(variant=>new object[]{new CatalogProducerAdmissionCase(variant,new(variant),
        new(variant.StartsWith("wrong-",StringComparison.Ordinal) ? "catalog-producer-admission-mismatch" : "ok",!variant.StartsWith("wrong-",StringComparison.Ordinal),true,true,true,true))});
    public static async Task AssertCase(CatalogProducerAdmissionCase test,ICatalogProducerAdmissionProvider provider)=>Assert.Equal(test.Expected,await provider.BuildAsync(test.Call));
}
public sealed class ScriptedCatalogProducerAdmission(CatalogProducerAdmissionCase test,int wrong=-1):ICatalogProducerAdmissionProvider
{
    public Task<CatalogProducerAdmissionReply> BuildAsync(CatalogProducerAdmissionCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch{0=>value with{Outcome="wrong"},1=>value with{AdmissionObserved=!value.AdmissionObserved},2=>value with{ProfileCorrect=false},3=>value with{CurrentAssemblyVerified=false},4=>value with{NoUnexpectedEffects=false},5=>value with{InputsRetained=false},_=>value});
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogProducerAdmissionMockTests
{
    [Theory,MemberData(nameof(CatalogProducerAdmissionCases.All),MemberType=typeof(CatalogProducerAdmissionCases))]
    public Task CorrectExistingTypedAdmissionAndProfileResponsesPass(CatalogProducerAdmissionCase test)=>CatalogProducerAdmissionCases.AssertCase(test,new ScriptedCatalogProducerAdmission(test));
    [Theory,MemberData(nameof(CatalogProducerAdmissionCases.All),MemberType=typeof(CatalogProducerAdmissionCases))]
    public async Task EveryAdmissionProfileCurrentModuleAndEffectMutationRejected(CatalogProducerAdmissionCase test)
    {for(var wrong=0;wrong<CatalogProducerAdmissionCases.NegativeControls;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogProducerAdmissionCases.AssertCase(test,new ScriptedCatalogProducerAdmission(test,wrong))));}
}
