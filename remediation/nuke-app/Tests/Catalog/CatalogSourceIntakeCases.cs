using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogSourceIntakeCall(string Variant);
public sealed record CatalogSourceIntakeReply(CatalogBuildReply? Build,string? ErrorCode,int ReaderOpens,int RowResolutions,int CacheHits,int RawResolutions,
    int CapturedRecords,bool AdmissionBeforeEffects,bool HitRawInputsAbsent,bool NoCatalogFallback,bool? ContextPreserved,bool? SqlIdentitySeparate,
    bool? SealedAuditPreserved,bool? LosslessRawIssuesRetained,bool GenerationDisposed,bool OriginalsRetained,bool NoCatalogEffects,bool FinalPublished);
public sealed record CatalogSourceIntakeCase(string Id,CatalogSourceIntakeCall Call,CatalogSourceIntakeReply Expected);
public interface ICatalogSourceIntakeCaseProvider { Task<CatalogSourceIntakeReply> ImportAsync(CatalogSourceIntakeCall call); }
public static class CatalogSourceIntakeCases
{
    // Original SourceIntegration raw C.java + API.xml add three Java declarations
    // and three JVM index declarations to the ten-binding base fixture. Only
    // actual admitted Java/index providers are cache-capable. Other three rows
    // resolve raw through the SAME facade, not Catalog's dispatcher.
    public static CatalogBuildReply FiveSourceBuild=>CatalogBuildCases.Base with{Sources=5,Bindings=16,Occurrences=16,Declarations=16,Entries=16,Inventory=16};
    private static CatalogSourceIntakeReply Success(int hits=2,int raw=3,bool alias=false)=>new(
        alias ? FiveSourceBuild with{Sources=6,Occurrences=19,Declarations=19} : FiveSourceBuild,null,1,alias ? 6 : 5,hits,raw,alias ? 20 : 17,
        true,true,true,true,true,true,true,true,true,false,true);
    private static CatalogSourceIntakeReply Reject(string code,int rows=0,bool beforeEffects=true,bool disposed=false,int hits=0)=>new(
        null,code,1,rows,hits,0,0,true,true,true,null,null,null,null,disposed,true,beforeEffects,false);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogSourceIntakeCase("INTAKE-01-two-sealed-hits-absent-raw-three-shared-facade-misses",new("two-hits"),Success()),
        new CatalogSourceIntakeCase("INTAKE-02-logical-cache-limit-keeps-unindexed-row-raw-in-same-facade",new("one-hit"),Success(1,4)),
        new CatalogSourceIntakeCase("INTAKE-03-explicit-no-index-is-same-facade-raw-intake",new("no-index"),Success(0,5)),
        new CatalogSourceIntakeCase("INTAKE-04-diagnostic-hit-retains-exact-sealed-whole-source-evidence",new("diagnostic-hit"),Success()),
        new CatalogSourceIntakeCase("INTAKE-05-logical-alias-does-not-collapse-distinct-source-occurrence",new("logical-alias"),Success(3,3,true)),
        new CatalogSourceIntakeCase("INTAKE-06-caller-index-pin-rejected-before-catalog-effects",new("index-pin"),Reject("SOURCE_EXTRACT_INDEX_PIN")),
        new CatalogSourceIntakeCase("INTAKE-07-edited-inventory-row-cannot-borrow-admitted-projection",new("edited-row"),Reject("catalog-source-row-mismatch",disposed:true)),
        new CatalogSourceIntakeCase("INTAKE-08-malformed-record-shape-fails-without-catalog-fallback",new("invalid-record-shape"),Reject("catalog-source-records",1,false,true,1)),
        new CatalogSourceIntakeCase("INTAKE-09-missing-sealed-raw-issue-evidence-fails-without-reread",new("missing-raw-evidence"),Reject("SOURCE_EXTRACT_RAW_EVIDENCE"))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogSourceIntakeCase test,ICatalogSourceIntakeCaseProvider provider)=>Assert.Equal(test.Expected,await provider.ImportAsync(test.Call));
}
public sealed class ScriptedCatalogSourceIntake(CatalogSourceIntakeCase test,int wrong=-1,int wrongBuild=-1):ICatalogSourceIntakeCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public Task<CatalogSourceIntakeReply> ImportAsync(CatalogSourceIntakeCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        if(wrongBuild>=0 && value.Build is not null)
        {var nestedCall=new CatalogBuildCall();using var nested=new ScriptedCatalogBuild(new("intake-build",nestedCall,value.Build),wrongBuild);value=value with{Build=nested.Build(nestedCall)};}
        return Task.FromResult(wrong switch
        {
            0=>value with{Build=value.Build is null ? CatalogBuildCases.Base : null},1=>value with{ErrorCode="wrong"},2=>value with{ReaderOpens=value.ReaderOpens+1},
            3=>value with{RowResolutions=value.RowResolutions+1},4=>value with{CacheHits=value.CacheHits+1},5=>value with{RawResolutions=value.RawResolutions+1},
            6=>value with{CapturedRecords=value.CapturedRecords+1},7=>value with{AdmissionBeforeEffects=!value.AdmissionBeforeEffects},8=>value with{HitRawInputsAbsent=!value.HitRawInputsAbsent},
            9=>value with{NoCatalogFallback=!value.NoCatalogFallback},10=>value with{ContextPreserved=Flip(value.ContextPreserved)},11=>value with{SqlIdentitySeparate=Flip(value.SqlIdentitySeparate)},
            12=>value with{SealedAuditPreserved=Flip(value.SealedAuditPreserved)},13=>value with{LosslessRawIssuesRetained=Flip(value.LosslessRawIssuesRetained)},14=>value with{GenerationDisposed=!value.GenerationDisposed},
            15=>value with{OriginalsRetained=!value.OriginalsRetained},16=>value with{NoCatalogEffects=!value.NoCatalogEffects},17=>value with{FinalPublished=!value.FinalPublished},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourceIntakeMockTests
{
    [Theory,MemberData(nameof(CatalogSourceIntakeCases.All),MemberType=typeof(CatalogSourceIntakeCases))]
    public Task CorrectSharedFacadeAndCatalogIntakeResponsesPass(CatalogSourceIntakeCase test)=>CatalogSourceIntakeCases.AssertCase(test,new ScriptedCatalogSourceIntake(test));
    [Theory,MemberData(nameof(CatalogSourceIntakeCases.All),MemberType=typeof(CatalogSourceIntakeCases))]
    public async Task EveryContextSealNoRereadIdentityAndPublicationMutationRejected(CatalogSourceIntakeCase test)
    {
        for(var wrong=0;wrong<18;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogSourceIntakeCases.AssertCase(test,new ScriptedCatalogSourceIntake(test,wrong))));
        if(test.Expected.Build is not null) for(var wrong=0;wrong<24;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogSourceIntakeCases.AssertCase(test,new ScriptedCatalogSourceIntake(test,wrongBuild:wrong))));
    }
}
