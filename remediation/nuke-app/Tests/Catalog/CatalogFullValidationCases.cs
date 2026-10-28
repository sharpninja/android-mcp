using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogFullValidationCall(string Variant,bool ApplyOriginalOverlay=false);
public sealed record CatalogFullValidationReply(CatalogBuildReply Build,string Outcome,bool ExistingOutputsUnchanged,bool OriginalInputsRetained,
    bool? SuppressionHidesOne=null,bool? RestoreExactDocument=null,int? HistoryRows=null,bool? MissingOccurrenceRejected=null,
    int? EntriesValidated=null,string? Integrity=null,int? ForeignKeyFailures=null,int? SchemaFailures=null,bool? InventoryValidated=null,
    bool? ReadOnlyDatabase=null,bool? ReportCreated=null,bool? ExistingReportRetained=null,string? ErrorContains=null,
    bool? Complete=null,bool? ApplicabilityRevalidated=null,int? DeclarationVariantOmissions=null,bool? NativeProducerIdentity=null);
public sealed record CatalogFullValidationCase(string Id,CatalogFullValidationCall Call,CatalogFullValidationReply Expected);
public interface ICatalogFullValidationCaseProvider { Task<CatalogFullValidationReply> ExecuteAsync(CatalogFullValidationCall call); }
public static class CatalogFullValidationCases
{
    // Exact test_build.py fixture stays in CatalogBuildCases. The composite
    // retains original SQL and runtime_catalog overlay semantics. Validation
    // reads immutable effective_entries, not the restored runtime override.
    public const string SelectedEntrySql="SELECT entry_id FROM effective_entries LIMIT 1";
    public const string RuntimeCountSql="SELECT count(*) FROM runtime_catalog";
    public const string BaseHashSql="SELECT record_sha256 FROM effective_entries WHERE entry_id=$id";
    public const string ActiveRegistrySql="SELECT registry_id FROM active_registry";
    public const string SignatureSql="SELECT signature FROM bindings WHERE binding_id=$id";
    public const string SuppressSql="INSERT INTO overlays VALUES ($id,$id,$signature,1,$registry,$hash,'suppressed','{}',$now,$now)";
    public const string SuppressHistorySql="INSERT INTO entry_history VALUES ($id,1,'suppress','{}','fixture',$now)";
    public const string RestoreSql="UPDATE overlays SET state='active',revision=2,document_json=$document WHERE entry_id=$id";
    public const string RestoreHistorySql="INSERT INTO entry_history VALUES ($id,2,'restore',$document,'fixture',$now)";
    public const string MissingOccurrenceSql="INSERT INTO occurrences VALUES ('missing','missing',1)";
    public const string Timestamp="2026-10-26T00:00:00Z";
    public const string OverrideDocument="{\"fixture\":\"override\"}";
    private static CatalogFullValidationReply Reject(string error,bool? retained=null)=>new(CatalogBuildCases.Base,"rejected",true,true,ReportCreated:false,ExistingReportRetained:retained,ErrorContains:error);
    public static IEnumerable<object[]> All=>new[]
    {
        new CatalogFullValidationCase("FULL-VAL-01-original-overlay-full-readonly-validation",new("original",true),
            new(CatalogBuildCases.Base,"ok",true,true,SuppressionHidesOne:true,RestoreExactDocument:true,HistoryRows:2,MissingOccurrenceRejected:true,
                EntriesValidated:10,Integrity:"ok",ForeignKeyFailures:0,SchemaFailures:0,InventoryValidated:true,ReadOnlyDatabase:true,ReportCreated:true,Complete:false,NativeProducerIdentity:true)),
        new CatalogFullValidationCase("FULL-VAL-02-source-reconciliation-sha",new("source-tamper"),Reject("SHA256")),
        new CatalogFullValidationCase("FULL-VAL-03-entry-export-hash",new("export-tamper"),Reject("Entry export")),
        new CatalogFullValidationCase("FULL-VAL-04-report-outside-immutable",new("report-inside"),Reject("outside immutable")),
        new CatalogFullValidationCase("FULL-VAL-05-report-create-new",new("existing-report"),Reject("already exists",true)),
        // Round3.test_database_validation_without_exports
        // calls run_database on a copied generated DB, not the exported release.
        new CatalogFullValidationCase("FULL-VAL-06-original-v3-database-readonly",new("database-only"),
            new(CatalogBuildCases.Base,"ok",true,true,EntriesValidated:10,Integrity:"ok",ForeignKeyFailures:0,SchemaFailures:0,
                ReadOnlyDatabase:true,ReportCreated:true,Complete:false,ApplicabilityRevalidated:false,DeclarationVariantOmissions:0,NativeProducerIdentity:true))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogFullValidationCase test,ICatalogFullValidationCaseProvider provider)=>Assert.Equal(test.Expected,await provider.ExecuteAsync(test.Call));
}
public sealed class ScriptedCatalogFullValidation(CatalogFullValidationCase test,int wrong=-1,int wrongBuild=-1):ICatalogFullValidationCaseProvider
{
    private static bool? Flip(bool? value)=>value is null?true:!value;
    public Task<CatalogFullValidationReply> ExecuteAsync(CatalogFullValidationCall call)
    {
        Assert.Same(test.Call,call);var value=test.Expected;
        if(wrongBuild>=0){var nestedCall=new CatalogBuildCall();value=value with{Build=new ScriptedCatalogBuild(new("full-validation-base",nestedCall,CatalogBuildCases.Base),wrongBuild).Build(nestedCall)};}
        return Task.FromResult(wrong switch
        {
            0=>value with{Outcome="wrong"},1=>value with{ExistingOutputsUnchanged=!value.ExistingOutputsUnchanged},2=>value with{OriginalInputsRetained=!value.OriginalInputsRetained},
            3=>value with{SuppressionHidesOne=Flip(value.SuppressionHidesOne)},4=>value with{RestoreExactDocument=Flip(value.RestoreExactDocument)},5=>value with{HistoryRows=(value.HistoryRows??0)+1},
            6=>value with{MissingOccurrenceRejected=Flip(value.MissingOccurrenceRejected)},7=>value with{EntriesValidated=(value.EntriesValidated??0)+1},8=>value with{Integrity="wrong"},
            9=>value with{ForeignKeyFailures=(value.ForeignKeyFailures??0)+1},10=>value with{SchemaFailures=(value.SchemaFailures??0)+1},11=>value with{InventoryValidated=Flip(value.InventoryValidated)},
            12=>value with{ReadOnlyDatabase=Flip(value.ReadOnlyDatabase)},13=>value with{ReportCreated=Flip(value.ReportCreated)},14=>value with{ExistingReportRetained=Flip(value.ExistingReportRetained)},
            15=>value with{ErrorContains="wrong"},16=>value with{Complete=Flip(value.Complete)},17=>value with{ApplicabilityRevalidated=Flip(value.ApplicabilityRevalidated)},
            18=>value with{DeclarationVariantOmissions=(value.DeclarationVariantOmissions??0)+1},19=>value with{NativeProducerIdentity=Flip(value.NativeProducerIdentity)},_=>value
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogFullValidationMockTests
{
    [Theory,MemberData(nameof(CatalogFullValidationCases.All),MemberType=typeof(CatalogFullValidationCases))]
    public Task ExactOriginalCompositeAndValidatorResponsesPass(CatalogFullValidationCase test)=>CatalogFullValidationCases.AssertCase(test,new ScriptedCatalogFullValidation(test));
    [Theory,MemberData(nameof(CatalogFullValidationCases.All),MemberType=typeof(CatalogFullValidationCases))]
    public async Task EveryOriginalBuildOverlayReportAndNonmutationAssertionRejectsMutation(CatalogFullValidationCase test)
    {
        for(var field=0;field<20;field++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogFullValidationCases.AssertCase(test,new ScriptedCatalogFullValidation(test,field))));
        for(var field=0;field<24;field++)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogFullValidationCases.AssertCase(test,new ScriptedCatalogFullValidation(test,wrongBuild:field))));
    }
}
