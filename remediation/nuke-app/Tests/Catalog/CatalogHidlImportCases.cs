using System.Text;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogHidlImportCall(string Variant,byte[] Raw,string Scope,string Path,string? PlatformVersion,string Sql);
public sealed record CatalogHidlImportReply(string Outcome,bool FinalDatabase=true,int? HidlEntries=null,int? MethodResults=null,
    string? PermissionOp=null,string? CodecStatus=null,bool? ContainsPoint=null,bool? EveryAvailabilityUnknown=null,
    int? QueriedIssueRows=null,string? SqlRawText=null,string? DecodedRawHex=null,string? FlatRawEncoding=null,
    bool? AtLeastTwoDeclarations=null,bool? FlatNormalizerComplete=null,bool? FlatCompilerValidation=null,
    string? FlatWireResolution=null,string? FlatAndroidAvailability=null,string? ErrorContains=null,bool OriginalInputsRetained=true);
public sealed record CatalogHidlImportCase(string Id,string LegacyMethod,CatalogHidlImportCall Call,CatalogHidlImportReply Expected);
public interface ICatalogHidlImportCaseProvider { Task<CatalogHidlImportReply> ImportAsync(CatalogHidlImportCall call); }
public static class CatalogHidlImportCases
{
    public const string Scope="aosp-pinned-hidl-source-evidence";
    public const string Structural="package hardware.test@2.7; interface IFoo { struct Point { int32_t x; }; get(Point[3]points) generates (bool ok, Point result); };";
    public const string Retained="package a@1.0; interface IFoo { f(); };";
    // Exact original SQL. The retention join and category constraints must not
    // be replaced by reading nested source metadata or a different issue code.
    public const string HidlDocumentsSql="SELECT document_json FROM registry_entries WHERE json_extract(document_json,'$.extensions.language')='hidl'";
    public const string InvalidUtf8Sql="SELECT raw FROM issues WHERE category='hidl-source'";
    public const string RetentionSql="SELECT i.raw,p.document_json FROM issues i JOIN parse_reports p USING(source_id) WHERE i.category IN ('source-format-unknown','hidl-source')";
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogHidlImportCase("HIDL-IMPORT-01-structure-results-through-schema", "test_structural_records_and_multi_results_survive_schema_gate",
            new("structural",Encoding.UTF8.GetBytes(Structural),Scope,"IFoo.hal",null,HidlDocumentsSql),
            new("ok",HidlEntries:5,MethodResults:2,PermissionOp:"unknown",CodecStatus:"unknown",ContainsPoint:true,EveryAvailabilityUnknown:true)),
        new CatalogHidlImportCase("HIDL-IMPORT-02-package-is-not-Android-version", "test_package_version_cannot_become_android_api_applicability",
            new("platform-inference",Encoding.UTF8.GetBytes("package hardware.test@2.7; interface IFoo {};"),Scope,"IFoo.hal","37.0",""),
            new("source-metadata",FinalDatabase:false,ErrorContains:"does not establish Android applicability")),
        new CatalogHidlImportCase("HIDL-IMPORT-03-invalid-UTF8-no-bindings", "test_invalid_utf8_retains_source_without_binding_inference",
            new("invalid-hidl",[105,110,118,97,108,105,100,255,72,73,68,76],Scope,"IFoo.hal",null,InvalidUtf8Sql),
            new("ok",HidlEntries:0,QueriedIssueRows:1,DecodedRawHex:"696e76616c6964ff4849444c")),
        new CatalogHidlImportCase("RAW-RETAIN-01-public-flat-HIDL-report", "test_hidl_source_retains_raw_bytes_and_declared_only_structure_without_wire_claims",
            new("retained-hidl",Encoding.UTF8.GetBytes(Retained),Scope,"raw-schema",null,RetentionSql),
            new("ok",QueriedIssueRows:1,SqlRawText:Retained,FlatRawEncoding:"utf-8",AtLeastTwoDeclarations:true,FlatNormalizerComplete:false,
                FlatCompilerValidation:false,FlatWireResolution:"unknown",FlatAndroidAvailability:"unknown")),
        new CatalogHidlImportCase("RAW-RETAIN-02-native-bytes-lossless", "test_non_utf8_native_evidence_is_lossless",
            new("retained-native",[114,97,119,255,98,105,110,100,101,114],"aosp-native-binder-search-candidate-evidence","raw-schema",null,RetentionSql),
            new("ok",QueriedIssueRows:1,DecodedRawHex:"726177ff62696e646572",FlatRawEncoding:"base64"))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogHidlImportCase test,ICatalogHidlImportCaseProvider provider)
    {
        var original=test.Call.Raw.ToArray();Assert.Equal(test.Expected,await provider.ImportAsync(test.Call));Assert.Equal(original,test.Call.Raw);
    }
}
public sealed class ScriptedCatalogHidlImport(CatalogHidlImportCase test,int wrong=-1):ICatalogHidlImportCaseProvider
{
    private static bool? Flip(bool? value)=>value is null?true:!value;
    public Task<CatalogHidlImportReply> ImportAsync(CatalogHidlImportCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{FinalDatabase=!value.FinalDatabase},2=>value with{HidlEntries=(value.HidlEntries??0)+1},
            3=>value with{MethodResults=(value.MethodResults??0)+1},4=>value with{PermissionOp="allow"},5=>value with{CodecStatus="proven"},
            6=>value with{ContainsPoint=Flip(value.ContainsPoint)},7=>value with{EveryAvailabilityUnknown=Flip(value.EveryAvailabilityUnknown)},
            8=>value with{QueriedIssueRows=(value.QueriedIssueRows??0)+1},9=>value with{SqlRawText="wrong"},10=>value with{DecodedRawHex="00"},
            11=>value with{FlatRawEncoding="wrong"},12=>value with{AtLeastTwoDeclarations=Flip(value.AtLeastTwoDeclarations)},
            13=>value with{FlatNormalizerComplete=Flip(value.FlatNormalizerComplete)},14=>value with{FlatCompilerValidation=Flip(value.FlatCompilerValidation)},
            15=>value with{FlatWireResolution="proven"},16=>value with{FlatAndroidAvailability="available"},17=>value with{ErrorContains="wrong"},
            18=>value with{OriginalInputsRetained=!value.OriginalInputsRetained},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogHidlImportMockTests
{
    [Theory,MemberData(nameof(CatalogHidlImportCases.All),MemberType=typeof(CatalogHidlImportCases))]
    public Task ExactOriginalSqlAndPublicReportAssertionsPass(CatalogHidlImportCase test)=>CatalogHidlImportCases.AssertCase(test,new ScriptedCatalogHidlImport(test));
    [Theory,MemberData(nameof(CatalogHidlImportCases.All),MemberType=typeof(CatalogHidlImportCases))]
    public async Task EverySqlReportRawEncodingIdentityAndInferenceMutationRejected(CatalogHidlImportCase test)
    {for(var wrong=0;wrong<19;wrong++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogHidlImportCases.AssertCase(test,new ScriptedCatalogHidlImport(test,wrong))));}
}
