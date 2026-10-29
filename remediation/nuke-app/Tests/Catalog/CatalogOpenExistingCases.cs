using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogOpenExistingCall(string Variant,bool? ReadOnly=null);
public sealed record CatalogOpenExistingReply(string Outcome,bool FileExists,bool ExistingBytesRetained,bool ParentStateRetained,
    string? StoredValue,bool? WriteAllowed,bool? ForeignKeysEnabled,bool? SameFile,bool NoSchemaCreated);
public sealed record CatalogOpenExistingCase(string Id,CatalogOpenExistingCall Call,CatalogOpenExistingReply Expected);
public interface ICatalogOpenExistingCaseProvider { CatalogOpenExistingReply Open(CatalogOpenExistingCall call); }
public static class CatalogOpenExistingCases
{
    // A minimal existing database contains only fixture_probe and one literal
    // row. Admission must not apply any canonical DDL or create missing files.
    public const string Value="retained sealed fixture";
    private static CatalogOpenExistingReply Existing(bool writable)=>new("ok",true,true,true,Value,writable,true,true,true);
    private static CatalogOpenExistingReply Missing=>new("missing",false,true,true,null,null,null,null,true);
    private static CatalogOpenExistingReply Invalid=>new("sqlite-error",true,true,true,null,null,null,null,true);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogOpenExistingCase("OPEN-01-default-is-read-only",new("existing"),Existing(false)),
        new CatalogOpenExistingCase("OPEN-02-explicit-read-only",new("existing",true),Existing(false)),
        new CatalogOpenExistingCase("OPEN-03-explicit-read-write-with-rollback",new("existing",false),Existing(true)),
        new CatalogOpenExistingCase("OPEN-04-default-missing-file-never-created",new("missing"),Missing),
        new CatalogOpenExistingCase("OPEN-05-read-write-missing-file-never-created",new("missing",false),Missing),
        new CatalogOpenExistingCase("OPEN-06-invalid-existing-default-bytes-retained",new("invalid"),Invalid),
        new CatalogOpenExistingCase("OPEN-07-invalid-existing-read-write-bytes-retained",new("invalid",false),Invalid)
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogOpenExistingCase test,ICatalogOpenExistingCaseProvider provider)=>Assert.Equal(test.Expected,provider.Open(test.Call));
}
public sealed class ScriptedCatalogOpenExisting(CatalogOpenExistingCase test,int wrong=-1):ICatalogOpenExistingCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public CatalogOpenExistingReply Open(CatalogOpenExistingCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{FileExists=!value.FileExists},2=>value with{ExistingBytesRetained=!value.ExistingBytesRetained},
            3=>value with{ParentStateRetained=!value.ParentStateRetained},4=>value with{StoredValue="wrong"},5=>value with{WriteAllowed=Flip(value.WriteAllowed)},
            6=>value with{ForeignKeysEnabled=Flip(value.ForeignKeysEnabled)},7=>value with{SameFile=Flip(value.SameFile)},8=>value with{NoSchemaCreated=!value.NoSchemaCreated},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogOpenExistingMockTests
{
    [Theory,MemberData(nameof(CatalogOpenExistingCases.All),MemberType=typeof(CatalogOpenExistingCases))]
    public void CorrectExistingFileAdmissionResponsesPass(CatalogOpenExistingCase test)=>CatalogOpenExistingCases.AssertCase(test,new ScriptedCatalogOpenExisting(test));
    [Theory,MemberData(nameof(CatalogOpenExistingCases.All),MemberType=typeof(CatalogOpenExistingCases))]
    public void EveryReadOnlyPersistenceAndNoCreateMutationRejected(CatalogOpenExistingCase test)
    {for(var wrong=0;wrong<9;wrong++) Assert.NotNull(Record.Exception(()=>CatalogOpenExistingCases.AssertCase(test,new ScriptedCatalogOpenExisting(test,wrong))));}
}
