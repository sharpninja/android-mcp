using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogFullValidationInvocation(CatalogValidator Service,string OutputDirectory,string InventoryPath,string DatabasePath,string ReportPath,bool DatabaseOnly);
public sealed class NativeCatalogFullValidationProvider:ICatalogFullValidationCaseProvider
{
    internal Func<CatalogFullValidationInvocation,Task<CatalogValidationResult>>? Execution {get;init;}
    public async Task<CatalogFullValidationReply> ExecuteAsync(CatalogFullValidationCall call)
    {
        using var producer=new NativeCatalogBuildProvider();var build=producer.Build(new());Assert.Equal("ok",build.Outcome);
        var originalOutput=Path.Combine(producer.EvidenceRoot,"out");var output=Path.Combine(producer.EvidenceRoot,"owned-validation-copy");Directory.CreateDirectory(output);
        static string Hash(string path){using var file=File.OpenRead(path);return ContentHash.Sha256(file);}
        void Copy(string source,string target){Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target,false);Assert.Equal(Hash(source),Hash(target));}
        Dictionary<string,string> Fingerprints(string directory)=>Directory.EnumerateFiles(directory).ToDictionary(path=>new FileInfo(path).Name,path=>Hash(path),StringComparer.Ordinal);
        var originalOutputPins=Fingerprints(originalOutput);foreach(var path in Directory.EnumerateFiles(originalOutput))Copy(path,Path.Combine(output,Path.GetFileName(path)));
        var originalInventory=Path.Combine(producer.EvidenceRoot,"inventory.json");var inventoryPath=originalInventory;
        var originalSources=JsonNode.Parse(File.ReadAllText(originalInventory))!["sources"]!.AsArray().OfType<JsonObject>().ToArray();
        var originalInputPins=originalSources.ToDictionary(source=>source["local_path"]!.GetValue<string>(),source=>Hash(ArtifactPaths.Resolve(producer.EvidenceRoot,source["local_path"]!.GetValue<string>())),StringComparer.Ordinal);
        bool? suppressed=null,restored=null,missingRejected=null;int? history=null;
        if(call.ApplyOriginalOverlay)
        {
            using var database=CatalogDatabase.OpenExisting(Path.Combine(output,"catalog.sqlite"),false);
            object? Scalar(string sql,params (string Name,object? Value)[] args){using var command=database.Command(sql,null,args);return command.ExecuteScalar();}
            var id=(string)Scalar(CatalogFullValidationCases.SelectedEntrySql)!;
            var immutable=Record.Exception(()=>{using var command=database.Command("UPDATE bindings SET name='changed' WHERE binding_id=$id",null,("$id",id));command.ExecuteNonQuery();});Assert.IsType<SqliteException>(immutable);
            var count=(long)Scalar(CatalogFullValidationCases.RuntimeCountSql)!;var hash=(string)Scalar(CatalogFullValidationCases.BaseHashSql,("$id",id))!;
            var registry=(string)Scalar(CatalogFullValidationCases.ActiveRegistrySql)!;var signature=(string)Scalar(CatalogFullValidationCases.SignatureSql,("$id",id))!;
            using(var transaction=database.Connection.BeginTransaction())
            {
                using(var command=database.Command(CatalogFullValidationCases.SuppressSql,transaction,("$id",id),("$signature",signature),("$registry",registry),("$hash",hash),("$now",CatalogFullValidationCases.Timestamp)))command.ExecuteNonQuery();
                using(var command=database.Command(CatalogFullValidationCases.SuppressHistorySql,transaction,("$id",id),("$now",CatalogFullValidationCases.Timestamp)))command.ExecuteNonQuery();
                using(var command=database.Command(CatalogFullValidationCases.RuntimeCountSql,transaction))suppressed=(long)command.ExecuteScalar()! ==count-1;
                using(var command=database.Command(CatalogFullValidationCases.RestoreSql,transaction,("$id",id),("$document",CatalogFullValidationCases.OverrideDocument)))command.ExecuteNonQuery();
                using(var command=database.Command("SELECT document_json FROM runtime_catalog WHERE entry_id=$id",transaction,("$id",id)))restored=JsonNode.DeepEquals(JsonNode.Parse((string)command.ExecuteScalar()!),JsonNode.Parse(CatalogFullValidationCases.OverrideDocument));
                using(var command=database.Command(CatalogFullValidationCases.RestoreHistorySql,transaction,("$id",id),("$document",CatalogFullValidationCases.OverrideDocument),("$now",CatalogFullValidationCases.Timestamp)))command.ExecuteNonQuery();
                transaction.Commit();
            }
            history=checked((int)(long)Scalar("SELECT count(*) FROM entry_history WHERE entry_id=$id",("$id",id))!);
            var missing=Record.Exception(()=>{using var command=database.Command(CatalogFullValidationCases.MissingOccurrenceSql);command.ExecuteNonQuery();});missingRejected=missing is SqliteException error && error.SqliteErrorCode==19;
        }
        if(call.Variant=="source-tamper")
        {
            var copies=Path.Combine(producer.EvidenceRoot,"owned-validator-source-copy");Directory.CreateDirectory(copies);
            foreach(var source in originalSources){var relative=source["local_path"]!.GetValue<string>();Copy(ArtifactPaths.Resolve(producer.EvidenceRoot,relative),ArtifactPaths.Resolve(copies,relative));}
            inventoryPath=Path.Combine(copies,"inventory.json");Copy(originalInventory,inventoryPath);File.WriteAllText(Path.Combine(copies,"api.txt"),"tampered",new UTF8Encoding(false));
        }
        if(call.Variant=="export-tamper")
        {
            var path=Path.Combine(output,"entries.jsonl");var lines=File.ReadAllLines(path);var first=JsonNode.Parse(lines[0])!;
            lines[0]=new JsonObject{["entry_id"]=first["entry_id"]!.DeepClone(),["tampered"]=true}.ToJsonString();File.WriteAllLines(path,lines,new UTF8Encoding(false));
        }
        var databasePath=Path.Combine(output,"catalog.sqlite");if(call.Variant=="database-only"){databasePath=Path.Combine(producer.EvidenceRoot,"owned-db-only.sqlite");Copy(Path.Combine(output,"catalog.sqlite"),databasePath);}
        var reportPath=call.Variant=="report-inside"?Path.Combine(output,"validation-report.json"):Path.Combine(producer.EvidenceRoot,"validation-report.json");
        if(call.Variant=="existing-report")File.WriteAllText(reportPath,"retained preexisting report",new UTF8Encoding(false));
        var reportWasPresent=File.Exists(reportPath);var priorReport=reportWasPresent?Hash(reportPath):null;
        var before=Fingerprints(output);var databaseBefore=Hash(databasePath);var service=new CatalogValidator(NativeSchemaProvider.Root(),new CanonicalJson());CatalogValidationResult? result=null;Exception? failure=null;
        try{result=Execution is not null?await Execution(new(service,output,inventoryPath,databasePath,reportPath,call.Variant=="database-only")):call.Variant=="database-only"?await service.ValidateDatabaseAsync(databasePath,reportPath):await service.ValidateAsync(output,inventoryPath,reportPath);}
        catch(Exception error){failure=error;}
        var after=Fingerprints(output);var unchanged=before.Count==after.Count && before.All(pair=>after.TryGetValue(pair.Key,out var hash) && hash==pair.Value) && Hash(databasePath)==databaseBefore;
        var originals=build.OriginalInputsRetained && originalOutputPins.All(pair=>Hash(Path.Combine(originalOutput,pair.Key))==pair.Value) && originalInputPins.All(pair=>Hash(ArtifactPaths.Resolve(producer.EvidenceRoot,pair.Key))==pair.Value);
        var created=!reportWasPresent && File.Exists(reportPath);bool? retained=call.Variant=="existing-report"?Hash(reportPath)==priorReport:null;
        if(failure is not null)
        {
            var expected=call.Variant switch{"source-tamper"=>"SHA256","export-tamper"=>"Entry export","report-inside"=>"outside immutable","existing-report"=>"already exists",_=>"unexpected"};
            Assert.Contains(expected,failure.Message,StringComparison.OrdinalIgnoreCase);
            return new(build,"rejected",unchanged,originals,ReportCreated:created,ExistingReportRetained:retained,ErrorContains:expected);
        }
        Assert.NotNull(result);var report=result!.Report;Assert.True(File.Exists(reportPath));Assert.Equal(result.Artifact.Sha256,Hash(reportPath));
        var producerInfo=report["native_producer"]!;var native=producerInfo["id"]!.GetValue<string>()==CatalogValidator.ProducerId && producerInfo["sha256"]!.GetValue<string>()==ContentHash.Sha256(File.ReadAllBytes(typeof(CatalogValidator).Assembly.Location)) && producerInfo["historical_python_identity"]!.GetValue<bool>()==false;
        return new(build,"ok",unchanged,originals,SuppressionHidesOne:suppressed,RestoreExactDocument:restored,HistoryRows:history,MissingOccurrenceRejected:missingRejected,
            EntriesValidated:checked((int)report["entries_validated"]!.GetValue<long>()),Integrity:report["integrity_check"]!.GetValue<string>(),ForeignKeyFailures:report["foreign_key_check"]!.AsArray().Count,SchemaFailures:report["json_schema_failures"]!.AsArray().Count,
            InventoryValidated:call.Variant=="database-only"?null:report["device_inventory"]!["status"]!.GetValue<string>()=="validated",ReadOnlyDatabase:result.DatabaseReadOnly,ReportCreated:created,
            Complete:report["complete"]!.GetValue<bool>(),ApplicabilityRevalidated:call.Variant=="database-only"?report["source_applicability_facts_revalidated"]!.GetValue<bool>():null,
            DeclarationVariantOmissions:call.Variant=="database-only"?checked((int)report["declaration_variant_omissions"]!.GetValue<long>()):null,NativeProducerIdentity:native);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogFullValidationRealTests
{
    [Theory,MemberData(nameof(CatalogFullValidationCases.All),MemberType=typeof(CatalogFullValidationCases))]
    public Task SameSixOriginalFullValidatorAndDatabaseCallers(CatalogFullValidationCase test)=>CatalogFullValidationCases.AssertCase(test,new NativeCatalogFullValidationProvider());
}
