using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogTypedContextCall(bool DegradedBridge);
public sealed record CatalogTypedContextReply(string Outcome,bool Published,bool StageEffects,bool LegacyInputUnchanged,
    int ImportedSourceRows,bool RequiresTypedContextRejected,bool InputsRetained);
public sealed record CatalogTypedContextCase(string Id,CatalogTypedContextCall Call,CatalogTypedContextReply Expected);
public interface ICatalogTypedContextCaseProvider { Task<CatalogTypedContextReply> BuildAsync(CatalogTypedContextCall call); }
public static class CatalogTypedContextCases
{
    // Ordinary original three-source manifest remains compatible. The second
    // manifest adds only the exact bridge contract marker but supplies no typed
    // bridge/result. It must not quietly import historical downloaded rows.
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogTypedContextCase("TYPED-CONTEXT-01-original-legacy-input-still-builds",new(false),new("ok",true,true,true,3,false,true)),
        new CatalogTypedContextCase("TYPED-CONTEXT-02-no-bridge-marker-degradation",new(true),new("catalog-typed-context-required",false,false,true,0,true,true))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogTypedContextCase test,ICatalogTypedContextCaseProvider provider)=>Assert.Equal(test.Expected,await provider.BuildAsync(test.Call));
}
public sealed class ScriptedCatalogTypedContext(CatalogTypedContextCase test,int wrong=-1):ICatalogTypedContextCaseProvider
{
    public Task<CatalogTypedContextReply> BuildAsync(CatalogTypedContextCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{Published=!value.Published},2=>value with{StageEffects=!value.StageEffects},
            3=>value with{LegacyInputUnchanged=!value.LegacyInputUnchanged},4=>value with{ImportedSourceRows=value.ImportedSourceRows+1},
            5=>value with{RequiresTypedContextRejected=!value.RequiresTypedContextRejected},6=>value with{InputsRetained=!value.InputsRetained},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogTypedContextMockTests
{
    [Theory,MemberData(nameof(CatalogTypedContextCases.All),MemberType=typeof(CatalogTypedContextCases))]
    public Task CorrectLegacyAndDegradedBridgeRepliesPass(CatalogTypedContextCase test)=>CatalogTypedContextCases.AssertCase(test,new ScriptedCatalogTypedContext(test));
    [Theory,MemberData(nameof(CatalogTypedContextCases.All),MemberType=typeof(CatalogTypedContextCases))]
    public async Task EveryDowngradeEffectsAndRetentionMutationRejected(CatalogTypedContextCase test)
    {for(var wrong=0;wrong<7;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogTypedContextCases.AssertCase(test,new ScriptedCatalogTypedContext(test,wrong))));}
}
