using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogPublishedRetentionReply(string Outcome,bool FinalDatabase,string? SqlRawText,string? DecodedRawHex,
    string FlatRawEncoding,int? ExactDeclarations,bool? CompilerValidation,string FixtureUrl,string FixtureChannel,
    int FixtureSourceCount,bool ReleaseContextAbsent,bool InputsRetained,int QueriedIssueRows=1);
public sealed record CatalogPublishedRetentionCase(string Id,string LegacyMethod,CatalogHidlImportCall Call,CatalogPublishedRetentionReply Expected);
public interface ICatalogPublishedRetentionProvider { Task<CatalogPublishedRetentionReply> ImportAsync(CatalogHidlImportCall call); }
public static class CatalogPublishedRetentionCases
{
    // Frozen release011 differs from the retained local test file. These are
    // its exact raw inputs, SQL boundary, helper context and assertions.
    public const string SourceSha256="08e88e08a55ddb44a7a61f9992ad078a189ce1d00763af4f4ff8ad652f9fbd83";
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogPublishedRetentionCase("PUBLISHED-RAW-01-exact-three-declarations","test_hidl_raw_schema_and_declared_structure_are_retained",
            new("published-retained-hidl",Encoding.UTF8.GetBytes(CatalogHidlImportCases.Retained),CatalogHidlImportCases.Scope,"raw-schema",null,CatalogHidlImportCases.RetentionSql),
            new("ok",true,CatalogHidlImportCases.Retained,null,"utf-8",3,false,"https://dl.google.com/fixture/raw-schema","unknown",4,true,true)),
        new CatalogPublishedRetentionCase("PUBLISHED-RAW-02-native-bytes-lossless","test_non_utf8_native_evidence_is_lossless",
            new("published-retained-native",[114,97,119,255,98,105,110,100,101,114],"aosp-native-binder-search-candidate-evidence","raw-schema",null,CatalogHidlImportCases.RetentionSql),
            new("ok",true,null,"726177ff62696e646572","base64",null,null,"https://dl.google.com/fixture/raw-schema","unknown",4,true,true))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogPublishedRetentionCase test,ICatalogPublishedRetentionProvider provider)
    {var original=test.Call.Raw.ToArray();Assert.Equal(test.Expected,await provider.ImportAsync(test.Call));Assert.Equal(original,test.Call.Raw);}
}
public sealed class ScriptedCatalogPublishedRetention(CatalogPublishedRetentionCase test,int wrong=-1):ICatalogPublishedRetentionProvider
{
    public Task<CatalogPublishedRetentionReply> ImportAsync(CatalogHidlImportCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{FinalDatabase=!value.FinalDatabase},2=>value with{SqlRawText="wrong"},
            3=>value with{DecodedRawHex="wrong"},4=>value with{FlatRawEncoding="wrong"},5=>value with{ExactDeclarations=(value.ExactDeclarations??0)+1},
            6=>value with{CompilerValidation=value.CompilerValidation is null?true:!value.CompilerValidation},7=>value with{FixtureUrl="https://android.googlesource.com/mock/api.txt"},
            8=>value with{FixtureChannel="stable"},9=>value with{FixtureSourceCount=3},10=>value with{ReleaseContextAbsent=false},11=>value with{InputsRetained=false},
            12=>value with{QueriedIssueRows=2},_=>value
        });
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogPublishedRetentionMockTests
{
    [Theory,MemberData(nameof(CatalogPublishedRetentionCases.All),MemberType=typeof(CatalogPublishedRetentionCases))]
    public Task ExactFrozenFixtureAndReportResponsesPass(CatalogPublishedRetentionCase test)=>CatalogPublishedRetentionCases.AssertCase(test,new ScriptedCatalogPublishedRetention(test));
    [Theory,MemberData(nameof(CatalogPublishedRetentionCases.All),MemberType=typeof(CatalogPublishedRetentionCases))]
    public async Task EveryOriginalAssertionAndFixtureContextMutationRejected(CatalogPublishedRetentionCase test)
    {for(var wrong=0;wrong<13;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogPublishedRetentionCases.AssertCase(test,new ScriptedCatalogPublishedRetention(test,wrong))));}
}
