using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogIntakeHistoricalAuditReply(CatalogSourceIntakeReply Import,string Disposition,bool DeclarationRetained,
    bool HistoricalBodyAbsent,bool NativeReceiptSeparate,bool HistoricalIdentityNotClaimed);
public interface ICatalogIntakeHistoricalAuditProvider { Task<CatalogIntakeHistoricalAuditReply> ImportAsync(CatalogSourceIntakeCall call); }
public static class CatalogIntakeHistoricalAuditCases
{
    public static readonly CatalogSourceIntakeCall Call=new("declared-historical");
    public static CatalogIntakeHistoricalAuditReply Expected=>new(
        CatalogSourceIntakeCases.All.Select(data=>(CatalogSourceIntakeCase)data[0]).Single(test=>test.Call.Variant=="two-hits").Expected,
        "declared-unconsumed-unverified",true,true,true,true);
    public static async Task AssertCase(ICatalogIntakeHistoricalAuditProvider provider)=>Assert.Equal(Expected,await provider.ImportAsync(Call));
}
public sealed class ScriptedCatalogIntakeHistoricalAudit(int wrong=-1,int wrongIntake=-1,int wrongBuild=-1):ICatalogIntakeHistoricalAuditProvider
{
    public async Task<CatalogIntakeHistoricalAuditReply> ImportAsync(CatalogSourceIntakeCall call)
    {
        Assert.Same(CatalogIntakeHistoricalAuditCases.Call,call);var value=CatalogIntakeHistoricalAuditCases.Expected;
        if(wrongIntake>=0 || wrongBuild>=0)
        {
            var test=new CatalogSourceIntakeCase("historical-native-intake",call,value.Import);
            value=value with{Import=await new ScriptedCatalogSourceIntake(test,wrongIntake,wrongBuild).ImportAsync(call)};
        }
        return wrong switch
        {
            0=>value with{Disposition="verified-historical-import"},1=>value with{DeclarationRetained=false},2=>value with{HistoricalBodyAbsent=false},
            3=>value with{NativeReceiptSeparate=false},4=>value with{HistoricalIdentityNotClaimed=false},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogIntakeHistoricalAuditMockTests
{
    [Fact] public Task DeclaredMissingHistoricalBodyIsExplicitlyUnverified()=>CatalogIntakeHistoricalAuditCases.AssertCase(new ScriptedCatalogIntakeHistoricalAudit());
    [Fact] public async Task EveryAuditAndExistingIntakeBuildMutationRejected()
    {
        for(var wrong=0;wrong<5;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogIntakeHistoricalAuditCases.AssertCase(new ScriptedCatalogIntakeHistoricalAudit(wrong))));
        for(var wrong=0;wrong<18;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogIntakeHistoricalAuditCases.AssertCase(new ScriptedCatalogIntakeHistoricalAudit(wrongIntake:wrong))));
        for(var wrong=0;wrong<24;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogIntakeHistoricalAuditCases.AssertCase(new ScriptedCatalogIntakeHistoricalAudit(wrongBuild:wrong))));
    }
}
