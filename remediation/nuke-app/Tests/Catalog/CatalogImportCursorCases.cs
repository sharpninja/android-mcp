using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogImportCursorCall(string Variant);
public sealed record CatalogImportCursorReply(string Outcome,int SourceRecordWindows,int ReadsAfterNextSource,int RetainedRawRecords,int ImportedDeclarations,
    int EntryBatches,int LargestOccurrenceBatch,bool RawStageRetained,bool? RawTablesExcludedFromFinal,bool FinalMarker,bool OriginalsRetained);
public sealed record CatalogImportCursorCase(string Id,CatalogImportCursorCall Call,CatalogImportCursorReply Expected);
public interface ICatalogImportCursorCaseProvider { CatalogImportCursorReply Import(CatalogImportCursorCall call); }
public static class CatalogImportCursorCases
{
    // Same raw BuildTests.fixture: signature8 + manifest1 + AIDL2 raw records.
    // Only the ten declaration records become immutable binding occurrences.
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogImportCursorCase("CURSOR-01-one-source-window-and-one-identity-batch",new("base"),new("ok",3,0,11,10,10,1,true,true,true,true)),
        new CatalogImportCursorCase("CURSOR-02-duplicate-source-occurrences-not-global-array",new("duplicate-source"),new("ok",4,0,19,18,10,2,true,true,true,true)),
        new CatalogImportCursorCase("CURSOR-03-import-rejection-retains-captured-source-records",new("invalid-permission-ast"),new("invalid-permission-ast",3,0,11,0,0,0,true,null,false,true)),
        new CatalogImportCursorCase("CURSOR-04-populated-output-does-not-capture-sources",new("nonempty-output"),new("nonempty-output",0,0,0,0,0,0,false,null,false,true))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogImportCursorCase test,ICatalogImportCursorCaseProvider provider)=>Assert.Equal(test.Expected,provider.Import(test.Call));
}
public sealed class ScriptedCatalogImportCursor(CatalogImportCursorCase test,int wrong=-1):ICatalogImportCursorCaseProvider
{
    public CatalogImportCursorReply Import(CatalogImportCursorCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{SourceRecordWindows=value.SourceRecordWindows+1},2=>value with{ReadsAfterNextSource=value.ReadsAfterNextSource+1},
            3=>value with{RetainedRawRecords=value.RetainedRawRecords+1},4=>value with{ImportedDeclarations=value.ImportedDeclarations+1},5=>value with{EntryBatches=value.EntryBatches+1},
            6=>value with{LargestOccurrenceBatch=value.LargestOccurrenceBatch+1},7=>value with{RawStageRetained=!value.RawStageRetained},8=>value with{RawTablesExcludedFromFinal=value.RawTablesExcludedFromFinal is null ? true : !value.RawTablesExcludedFromFinal},
            9=>value with{FinalMarker=!value.FinalMarker},10=>value with{OriginalsRetained=!value.OriginalsRetained},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogImportCursorMockTests
{
    [Theory,MemberData(nameof(CatalogImportCursorCases.All),MemberType=typeof(CatalogImportCursorCases))]
    public void CorrectSourceWindowAndRetainedCursorObservationsPass(CatalogImportCursorCase test)=>CatalogImportCursorCases.AssertCase(test,new ScriptedCatalogImportCursor(test));
    [Theory,MemberData(nameof(CatalogImportCursorCases.All),MemberType=typeof(CatalogImportCursorCases))]
    public void EverySourceWindowBatchAndRetainedEvidenceMutationRejected(CatalogImportCursorCase test)
    {for(var wrong=0;wrong<11;wrong++) Assert.NotNull(Record.Exception(()=>CatalogImportCursorCases.AssertCase(test,new ScriptedCatalogImportCursor(test,wrong))));}
}
