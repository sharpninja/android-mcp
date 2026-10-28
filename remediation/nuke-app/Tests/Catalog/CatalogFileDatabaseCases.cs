using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogFileDatabaseCall(string Variant,SchemaFixtureProfile Profile=SchemaFixtureProfile.Entry);
public sealed record CatalogFileDatabaseReply(string Outcome,bool StageExists,bool? SourceOriginalsRetained,bool PriorStageBytesRetained,bool Reopened,
    int? SourceCount,bool? Integrity,bool? ForeignKeys,bool? ImmutableSources,bool? BridgeTables,bool? MetadataTables,bool? FileBackedConnection);
public sealed record CatalogFileDatabaseCase(string Id,CatalogFileDatabaseCall Call,CatalogFileDatabaseReply Expected);
public interface ICatalogFileDatabaseCaseProvider { CatalogFileDatabaseReply Execute(CatalogFileDatabaseCall call); }
public static class CatalogFileDatabaseCases
{
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogFileDatabaseCase("FILEDB-01-entry-commit-close-reopen",new("new"),new("ok",true,true,false,true,1,true,true,true,false,false,true)),
        new CatalogFileDatabaseCase("FILEDB-02-combined-same-schema-engine",new("new",SchemaFixtureProfile.Combined),new("ok",true,true,false,true,1,true,true,true,true,true,true)),
        new CatalogFileDatabaseCase("FILEDB-03-prior-file-refused-intact",new("existing"),new("exists",true,true,true,false,null,null,null,null,null,null,null)),
        new CatalogFileDatabaseCase("FILEDB-04-prerequisites-read-before-file-create",new("missing-schema"),new("prerequisite",false,true,false,false,null,null,null,null,null,null,null)),
        new CatalogFileDatabaseCase("FILEDB-05-schema-error-retains-private-stage",new("invalid-schema"),new("schema-error",true,true,false,false,null,null,null,null,null,null,null))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogFileDatabaseCase test,ICatalogFileDatabaseCaseProvider provider)=>Assert.Equal(test.Expected,provider.Execute(test.Call));
}
public sealed class ScriptedCatalogFileDatabase(CatalogFileDatabaseCase test,int wrong=-1):ICatalogFileDatabaseCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public CatalogFileDatabaseReply Execute(CatalogFileDatabaseCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{StageExists=!value.StageExists},2=>value with{SourceOriginalsRetained=Flip(value.SourceOriginalsRetained)},
            3=>value with{PriorStageBytesRetained=!value.PriorStageBytesRetained},4=>value with{Reopened=!value.Reopened},5=>value with{SourceCount=(value.SourceCount ?? 0)+1},
            6=>value with{Integrity=Flip(value.Integrity)},7=>value with{ForeignKeys=Flip(value.ForeignKeys)},8=>value with{ImmutableSources=Flip(value.ImmutableSources)},
            9=>value with{BridgeTables=Flip(value.BridgeTables)},10=>value with{MetadataTables=Flip(value.MetadataTables)},11=>value with{FileBackedConnection=Flip(value.FileBackedConnection)},_=>value
        };
    }
}
[Trait("Gate","Mock")]
public sealed class CatalogFileDatabaseMockTests
{
    [Theory,MemberData(nameof(CatalogFileDatabaseCases.All),MemberType=typeof(CatalogFileDatabaseCases))]
    public void CorrectFileStageOutcomesPass(CatalogFileDatabaseCase test)=>CatalogFileDatabaseCases.AssertCase(test,new ScriptedCatalogFileDatabase(test));
    [Theory,MemberData(nameof(CatalogFileDatabaseCases.All),MemberType=typeof(CatalogFileDatabaseCases))]
    public void EveryPersistencePrerequisiteAndRetainedOriginalMutationRejected(CatalogFileDatabaseCase test)
    {for(var wrong=0;wrong<12;wrong++) Assert.NotNull(Record.Exception(()=>CatalogFileDatabaseCases.AssertCase(test,new ScriptedCatalogFileDatabase(test,wrong))));}
}
