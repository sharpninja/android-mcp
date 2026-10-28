using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogSourceIntegrationCall(string Variant);
public sealed record CatalogSourceIntegrationReply(string Outcome,bool FinalDatabase,int? Sources=null,int? StructuredFields=null,bool? FieldWireTagsUnknown=null,
    string? RootKind=null,string? RootSchemaVersion=null,bool? NoStructuredUnresolved=null,int? AidlSemanticGaps=null,string? JavaPermission=null,string? JvmPermission=null,
    string? ProvenJvmIdentity=null,bool? DistinctSourceJvm=null,string? RawSchema=null,string? ParseStatus=null,bool? AnyIssues=null,string? FailureReason=null,bool OriginalsRetained=true);
public sealed record CatalogSourceIntegrationCase(string Id,CatalogSourceIntegrationCall Call,CatalogSourceIntegrationReply Expected);
public interface ICatalogSourceIntegrationCaseProvider { CatalogSourceIntegrationReply Import(CatalogSourceIntegrationCall call); }
public static class CatalogSourceIntegrationCases
{
    // Exact raw source inputs from the four original SourceIntegrationTests
    // methods. The original three-source BuildTests.fixture supplies the base.
    public const string Structured="package a; parcelable P { @nullable String name; int[] values={1,2}; }";
    public const string Java="package a; public class C { @android.annotation.RequiresPermission(\"android.permission.CAMERA\") public void f(int x){} }";
    public const string Index="<api version=\"4\"><class name=\"a/C\"><method name=\"f(I)V\"/><field name=\"VALUE\"/></class></api>";
    public const string Malformed="package a; @Broken( interface IFoo {";
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogSourceIntegrationCase("SOURCE-IMPORT-01-structured-aidl-publication",new("structured-aidl"),new("ok",true,Sources:3,StructuredFields:2,FieldWireTagsUnknown:true,RootKind:"aidl-parcelable",RootSchemaVersion:"1.1.0",NoStructuredUnresolved:true,AidlSemanticGaps:0)),
        new CatalogSourceIntegrationCase("SOURCE-IMPORT-02-distinct-java-and-current-jvm-evidence",new("java-jvm-link"),new("ok",true,Sources:5,JavaPermission:"android.permission.CAMERA",JvmPermission:"android.permission.CAMERA",ProvenJvmIdentity:"a/C#f(I)V",DistinctSourceJvm:true)),
        new CatalogSourceIntegrationCase("SOURCE-IMPORT-03-cache-sha-failure-before-extraction",new("java-cache-sha-mismatch"),new("cache-sha-mismatch",false,FailureReason:"Metadata cache SHA256 mismatch api.txt")),
        new CatalogSourceIntegrationCase("SOURCE-IMPORT-04-malformed-aidl-raw-unknown-retained",new("malformed-aidl"),new("ok",true,Sources:3,RawSchema:Malformed,ParseStatus:"unsupported",AnyIssues:true))
    }.Select(test=>new object[]{test});
    public static void AssertCase(CatalogSourceIntegrationCase test,ICatalogSourceIntegrationCaseProvider provider)=>Assert.Equal(test.Expected,provider.Import(test.Call));
}
public sealed class ScriptedCatalogSourceIntegration(CatalogSourceIntegrationCase test,int wrong=-1):ICatalogSourceIntegrationCaseProvider
{
    private static bool? Flip(bool? value)=>value is null ? true : !value;
    public CatalogSourceIntegrationReply Import(CatalogSourceIntegrationCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{FinalDatabase=!value.FinalDatabase},2=>value with{Sources=(value.Sources ?? 0)+1},3=>value with{StructuredFields=(value.StructuredFields ?? 0)+1},
            4=>value with{FieldWireTagsUnknown=Flip(value.FieldWireTagsUnknown)},5=>value with{RootKind="wrong"},6=>value with{RootSchemaVersion="wrong"},7=>value with{NoStructuredUnresolved=Flip(value.NoStructuredUnresolved)},
            8=>value with{AidlSemanticGaps=(value.AidlSemanticGaps ?? 0)+1},9=>value with{JavaPermission="wrong"},10=>value with{JvmPermission="wrong"},11=>value with{ProvenJvmIdentity="wrong"},
            12=>value with{DistinctSourceJvm=Flip(value.DistinctSourceJvm)},13=>value with{RawSchema="wrong"},14=>value with{ParseStatus="wrong"},15=>value with{AnyIssues=Flip(value.AnyIssues)},16=>value with{FailureReason="wrong"},17=>value with{OriginalsRetained=!value.OriginalsRetained},_=>value
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogSourceIntegrationMockTests
{
    [Theory,MemberData(nameof(CatalogSourceIntegrationCases.All),MemberType=typeof(CatalogSourceIntegrationCases))]
    public void CorrectOriginalSourceIntegrationResponsesPass(CatalogSourceIntegrationCase test)=>CatalogSourceIntegrationCases.AssertCase(test,new ScriptedCatalogSourceIntegration(test));
    [Theory,MemberData(nameof(CatalogSourceIntegrationCases.All),MemberType=typeof(CatalogSourceIntegrationCases))]
    public void EveryOriginalSchemaJvmLinkUnknownAndSideEffectMutationRejected(CatalogSourceIntegrationCase test)
    {for(var wrong=0;wrong<18;wrong++) Assert.NotNull(Record.Exception(()=>CatalogSourceIntegrationCases.AssertCase(test,new ScriptedCatalogSourceIntegration(test,wrong))));}
}
