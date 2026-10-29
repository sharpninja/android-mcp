using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Json.Schema;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogValidationResult(JsonObject Report,StoredArtifact Artifact,bool DatabaseReadOnly);

/// <summary>Read-only legacy release/database validation over the existing checked inventory and schema engines.</summary>
public sealed class CatalogValidator(string catalogRoot,ICanonicalJson canonical,IAidlLexicalInventorySourceFacts? aidlInventory=null)
{
    public const string ProducerId="csharp-nuke-catalog-validator-v1";
    public string ProducerFingerprint {get;}=ContentHash.Sha256(File.ReadAllBytes(typeof(CatalogValidator).Assembly.Location));
    private static string S(JsonNode? value)=>value?.GetValue<string>() ?? "";
    private static JsonElement Element(JsonNode value)=>JsonSerializer.SerializeToElement(value);
    private static string Sha(string text)=>ContentHash.Sha256(Encoding.UTF8.GetBytes(text));
    private static void Check(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
    private static void Increment(JsonObject counts,string key,long amount=1)=>counts[key]=(counts[key]?.GetValue<long>() ?? 0)+amount;
    private static long Count(CatalogDatabase database,string table)
    {using var command=database.Command("SELECT count(*) FROM "+table);return (long)command.ExecuteScalar()!;}
    private static object? Scalar(CatalogDatabase database,string sql,params (string Name,object? Value)[] args)
    {using var command=database.Command(sql,null,args);return command.ExecuteScalar();}
    private static JsonArray ForeignKeys(CatalogDatabase database)
    {
        var result=new JsonArray();using var command=database.Command("PRAGMA foreign_key_check");using var rows=command.ExecuteReader();
        while(rows.Read()){var row=new JsonArray();for(var index=0;index<rows.FieldCount;index++)row.Add(rows.IsDBNull(index)?null:JsonSerializer.SerializeToNode(rows.GetValue(index)));result.Add(row);}return result;
    }
    private static void Walk(JsonObject value,Action<JsonObject> visit)
    {
        visit(value);foreach(var item in value)
        {
            if(item.Value is JsonObject child && child.ContainsKey("op"))Walk(child,visit);
            else if(item.Value is JsonArray children)foreach(var member in children.OfType<JsonObject>())if(member.ContainsKey("op"))Walk(member,visit);
        }
    }
    private sealed record Inputs(JsonSchema Entries,JsonSchema Permissions,byte[] EntryBytes,byte[] PermissionBytes);
    private Inputs Schemas()
    {
        var contracts=Path.Combine(catalogRoot,"contracts");
        foreach(var path in Directory.EnumerateFiles(contracts,"*.schema.json"))
        {
            var bytes=File.ReadAllBytes(path);_=canonical.Serialize(bytes);using var document=JsonDocument.Parse(bytes);
            Check(CatalogSourceMetadata.IsValidSchema(document.RootElement),"Invalid JSON schema "+Path.GetFileName(path));
            _=JsonSchema.FromText(Encoding.UTF8.GetString(bytes));
        }
        var entry=File.ReadAllBytes(Path.Combine(contracts,"Entry.schema.json"));var permission=File.ReadAllBytes(Path.Combine(contracts,"PermissionExpression.schema.json"));
        return new(JsonSchema.FromText(Encoding.UTF8.GetString(entry)),JsonSchema.FromText(Encoding.UTF8.GetString(permission)),entry,permission);
    }
    private static bool Valid(JsonSchema schema,JsonObject value)=>schema.Evaluate(Element(value),new(){OutputFormat=OutputFormat.Flag,RequireFormatValidation=true}).IsValid;
    private sealed record EntryScan(long Entries,long PermissionVariants,JsonArray Failures,JsonObject Kinds,JsonObject PermissionNodes);
    private EntryScan ValidateEntries(CatalogDatabase database,Inputs inputs)
    {
        var generation=(long)Scalar(database,"SELECT registry_generation FROM catalog_view WHERE singleton=1")!;
        var failures=new JsonArray();var kinds=new JsonObject();var nodes=new JsonObject();long total=0,variants=0;
        using var command=database.Command("SELECT entry_id,document_json,record_sha256,etag FROM effective_entries");using var rows=command.ExecuteReader();
        while(rows.Read())
        {
            var id=rows.GetString(0);var raw=rows.GetString(1);_=canonical.Serialize(Encoding.UTF8.GetBytes(raw));var doc=JsonNode.Parse(raw)!.AsObject();
            var evaluation=inputs.Entries.Evaluate(Element(doc),new(){OutputFormat=OutputFormat.List,RequireFormatValidation=true});
            if(!evaluation.IsValid)Collect(evaluation);
            void Collect(EvaluationResults result)
            {
                if(result.Errors is not null)foreach(var error in result.Errors)
                    failures.Add(new JsonObject{["entry_id"]=id,["path"]=PathSegments(doc,result.InstanceLocation.ToString()),["message"]=error.Value});
                foreach(var detail in result.Details)Collect(detail);
            }
            Check(Sha(S(doc["canonical_signature"]))==id,"Canonical Entry identity mismatch "+id);
            Check(Sha(raw)==rows.GetString(2),"Entry document hash mismatch "+id);
            Check(rows.GetString(3)==$"\"cat1:{generation}:{rows.GetString(2)}:{id}:0:active\"","Current Entry ETag mismatch "+id);
            Increment(kinds,S(doc["kind"]));Walk(doc["permission"]!.AsObject(),ast=>Increment(nodes,S(ast["op"])));
            if(doc["extensions"]!.AsObject().ContainsKey("permission_variants"))variants++;
            foreach(var parameter in (doc["extensions"]!["parameters"] as JsonArray ?? new()).OfType<JsonObject>())
                if(parameter["permission"] is JsonObject ast)Check(Valid(inputs.Permissions,ast),"Invalid parameter permission AST");
            total++;
        }
        return new(total,variants,failures,kinds,nodes);
    }
    private static JsonArray PathSegments(JsonObject document,string pointer)
    {
        var path=new JsonArray();JsonNode? current=document;
        foreach(var encoded in pointer.Split('/').Skip(1))
        {
            var part=encoded.Replace("~1","/",StringComparison.Ordinal).Replace("~0","~",StringComparison.Ordinal);
            if(current is JsonArray array && int.TryParse(part,out var index)){path.Add(index);current=index>=0 && index<array.Count?array[index]:null;}
            else{path.Add(part);current=current is JsonObject map && map.TryGetPropertyValue(part,out var value)?value:null;}
        }
        return path;
    }
    private JsonObject Producer()=>new(){["id"]=ProducerId,["sha256"]=ProducerFingerprint,["identity_kind"]="loaded-monolithic-assembly",["historical_python_identity"]=false};
    private static string ReportPath(string reportPath)
    {var full=Path.GetFullPath(reportPath);return ArtifactPaths.Resolve(Path.GetDirectoryName(full)!,Path.GetFileName(full));}
    private static async Task<StoredArtifact> WriteReport(string full,JsonObject report,ICanonicalJson canonical,CancellationToken token)
    {
        ICreateNewArtifactStore store=new FileArtifactStore(Path.GetDirectoryName(full)!,bytes=>ContentHash.Sha256(bytes.Span));
        return await store.WriteNewAsync(new(Path.GetFileName(full),canonical.Serialize(Element(report)),"application/json"),token);
    }
    public async Task<CatalogValidationResult> ValidateAsync(string outputDirectory,string inventoryPath,string reportPath,CancellationToken cancellationToken=default)
    {
        var output=Path.GetFullPath(outputDirectory);var reportFull=ReportPath(reportPath);var relative=Path.GetRelativePath(output,reportFull);
        Check(relative==".." || relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal) || Path.IsPathRooted(relative),"Validation report must be outside immutable release directory");
        var inventoryFull=Path.GetFullPath(inventoryPath);var inventoryBytes=await File.ReadAllBytesAsync(inventoryFull,cancellationToken);_=canonical.Serialize(inventoryBytes);
        var sourceRows=JsonNode.Parse(inventoryBytes)!["sources"]!.AsArray().OfType<JsonObject>().ToArray();var inputs=Schemas();
        JsonObject report;bool readOnly;
        using(var database=CatalogDatabase.OpenExisting(ArtifactPaths.Resolve(output,"catalog.sqlite")))
        {
            readOnly=new SqliteConnectionStringBuilder(database.Connection.ConnectionString).Mode==SqliteOpenMode.ReadOnly;
            var inventory=new CatalogInventory(database,new CatalogContracts(canonical),new DeviceResolver(canonical,Path.Combine(catalogRoot,"contracts/DeviceResolution.schema.json")),canonical,Path.Combine(catalogRoot,"contracts/Entry.schema.json"));
            var device=new CatalogInventoryValidator(database,inventory,canonical).Validate(output);var entries=ValidateEntries(database,inputs);
            Check(entries.Entries==Count(database,"bindings"),"Entry/binding count reconciliation failed");long exported=0;
            using(var stream=new StreamReader(ArtifactPaths.Resolve(output,"entries.jsonl"),new UTF8Encoding(false,true),false))
            {
                while(await stream.ReadLineAsync(cancellationToken) is { } line)
                {
                    _=canonical.Serialize(Encoding.UTF8.GetBytes(line));var doc=JsonNode.Parse(line)!.AsObject();
                    var id=S(doc["entry_id"]);Check(id.Length!=0,"Entry export identity missing");
                    var hash=Scalar(database,"SELECT record_sha256 FROM effective_entries WHERE entry_id=$id",("$id",id)) as string;
                    Check(hash is not null && Sha(line)==hash,"Entry export hash mismatch "+id);exported++;
                }
            }
            Check(exported==entries.Entries,"Entry export count mismatch");var downloaded=sourceRows.Where(source=>S(source["status"])=="downloaded").ToArray();
            Check(downloaded.LongLength==Count(database,"sources"),"Downloaded source count reconciliation failed");
            foreach(var source in downloaded)
            {
                cancellationToken.ThrowIfCancellationRequested();using var raw=File.OpenRead(ArtifactPaths.Resolve(Path.GetDirectoryName(inventoryFull)!,S(source["local_path"])));
                Check(ContentHash.Sha256(raw)==S(source["sha256"]),"Source SHA256 mismatch "+S(source["path"]));
            }
            var reports=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
            using(var command=database.Command("SELECT document_json FROM parse_reports"))using(var rows=command.ExecuteReader())while(rows.Read())
            {
                var value=JsonNode.Parse(rows.GetString(0))!.AsObject();reports.Add(S(value["source_id"]),value);
                if(value.ContainsKey("expected_declarations"))Check(value["expected_declarations"]!.GetValue<long>()==value["declarations"]!.GetValue<long>(),"Expected declaration count mismatch");
            }
            var reconciliation=new JsonObject();using(var command=database.Command("SELECT source_id,raw_schema FROM aidl_schemas"))using(var rows=command.ExecuteReader())while(rows.Read())
            {
                var id=rows.GetString(0);var matched=S(reports[id]["aidl_reconciliation"])=="matched";
                if(matched)
                {
                    if(aidlInventory is null)throw new InvalidDataException("AIDL matched reconciliation requires the shared Source lexical inventory verifier");
                    var lexical=aidlInventory.Inventory(rows.GetString(1));var actual=(long)Scalar(database,"SELECT count(*) FROM binding_declarations WHERE source_id=$source",("$source",id))!;
                    Check(lexical.Reconciled && lexical.ExpectedRecords==actual,"AIDL reconciliation failed in validator "+id);
                }
                Increment(reconciliation,matched?"matched":"unreconciled");
            }
            var signatureStats=new JsonObject();foreach(var kind in new[]{"sdk-signature","source-signature"})
            {var selected=reports.Values.Where(value=>S(value["kind"])==kind).ToArray();signatureStats[kind]=new JsonObject{["files"]=selected.Length,["declarations"]=selected.Sum(value=>value["declarations"]!.GetValue<long>()),["unsupported"]=selected.Sum(value=>value["unsupported"]!.GetValue<long>())};}
            var integrity=(string)Scalar(database,"PRAGMA integrity_check")!;var fk=ForeignKeys(database);Check(integrity=="ok" && fk.Count==0,"SQLite integrity or foreign-key check failed");
            var duplicates=(long)Scalar(database,"SELECT count(*) FROM (SELECT binding_id,source_id,line,count(*) n FROM occurrences GROUP BY binding_id,source_id,line HAVING n>1)")!;Check(duplicates==0,"Duplicate occurrences");
            var scoped=new JsonObject();using(var command=database.Command("SELECT document_json,scope FROM annotation_evidence"))using(var rows=command.ExecuteReader())while(rows.Read())
            {var annotation=JsonNode.Parse(rows.GetString(0))!["annotation"]!;if(annotation["permission"] is JsonObject ast){Check(Valid(inputs.Permissions,ast),"Invalid annotation permission AST");Increment(scoped,rows.GetString(1)+":"+S(ast["op"]));}}
            HashSet<string> Names(string sql){using var command=database.Command(sql);using var rows=command.ExecuteReader();var result=new HashSet<string>(StringComparer.Ordinal);while(rows.Read())result.Add(rows.GetString(0));return result;}
            var definitions=Names("SELECT DISTINCT name FROM permission_definitions");var symbols=Names("SELECT DISTINCT value FROM permission_symbols");var leaves=new HashSet<string>(StringComparer.Ordinal);
            using(var command=database.Command("SELECT expression_json FROM permission_relations"))using(var rows=command.ExecuteReader())while(rows.Read())
            {var ast=JsonNode.Parse(rows.GetString(0))!.AsObject();Check(Valid(inputs.Permissions,ast),"Invalid permission relation AST");Walk(ast,value=>{if(S(value["op"])=="permission")leaves.Add(S(value["name"]));});}
            var counts=new JsonObject();foreach(var table in new[]{"sources","bindings","occurrences","binding_declarations","permission_definitions","permission_symbols","permission_relations","aidl_schemas","annotation_evidence","issues"})counts[table]=Count(database,table);
            report=new(){["validated_at"]=DateTimeOffset.UtcNow.ToString("O"),["schema_version"]="1.2.0",["entries_validated"]=entries.Entries,["json_schema_failures"]=entries.Failures,["integrity_check"]=integrity,["foreign_key_check"]=fk,["duplicate_occurrences"]=duplicates,["counts"]=counts,
                ["entry_kinds"]=entries.Kinds,["effective_permission_nodes"]=entries.PermissionNodes,["scoped_annotation_permission_nodes"]=scoped,["source_dependent_permission_entries"]=entries.PermissionVariants,["signature_reconciliation"]=signatureStats,
                ["external_annotations_unmatched"]=(long)Scalar(database,"SELECT count(*) FROM annotation_evidence WHERE binding_id IS NULL")!,["permission_reference_names"]=new JsonObject{["total"]=leaves.Count,["with_definition"]=leaves.Intersect(definitions).Count(),["constant_only"]=leaves.Intersect(symbols).Except(definitions).Count(),["without_definition_or_constant"]=leaves.Except(definitions).Except(symbols).Count()},
                ["undefined_permission_names"]=new JsonArray(leaves.Except(definitions).Except(symbols).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars)).Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray()),["complete"]=false,["aidl_reconciliation"]=reconciliation,
                ["device_inventory"]=new JsonObject{["status"]=device.Status,["entries"]=device.Entries,["contract"]=device.Contract,["inventory_sha256"]=device.InventorySha256,["complete"]=device.Complete,["scope"]=device.Scope},["native_producer"]=Producer()};
        }
        var artifact=await WriteReport(reportFull,report,canonical,cancellationToken);Check(report["json_schema_failures"]!.AsArray().Count==0,"JSON Schema validation failed; see report");return new(report,artifact,readOnly);
    }
    public async Task<CatalogValidationResult> ValidateDatabaseAsync(string databasePath,string reportPath,CancellationToken cancellationToken=default)
    {
        var full=Path.GetFullPath(databasePath);var reportFull=ReportPath(reportPath);Check(full!=reportFull,"Report cannot replace database");var inputs=Schemas();JsonObject report;bool readOnly;
        using(var database=CatalogDatabase.OpenExisting(full))
        {
            readOnly=new SqliteConnectionStringBuilder(database.Connection.ConnectionString).Mode==SqliteOpenMode.ReadOnly;
            Check((long)Scalar(database,"PRAGMA user_version")! ==3,"Expected storage v3; migrate legacy storage to a new copy first");var entries=ValidateEntries(database,inputs);
            using(var command=database.Command("SELECT expression_json FROM permission_relations"))using(var rows=command.ExecuteReader())while(rows.Read())Check(Valid(inputs.Permissions,JsonNode.Parse(rows.GetString(0))!.AsObject()),"Invalid permission relation AST");
            long omissions=0;var samples=new JsonArray();string? previous=null;var shapes=new HashSet<string>(StringComparer.Ordinal);
            void RecordOmission()
            {
                if(previous is null || shapes.Count<2)return;var raw=Scalar(database,"SELECT document_json FROM effective_entries WHERE entry_id=$id",("$id",previous)) as string;
                if(raw is null || JsonNode.Parse(raw)!["extensions"]?["declaration_variants"] is JsonArray {Count:>0})return;
                omissions++;if(samples.Count<20)samples.Add(previous);
            }
            using(var command=database.Command("SELECT binding_id,document_json FROM binding_declarations ORDER BY binding_id"))using(var rows=command.ExecuteReader())while(rows.Read())
            {
                var id=rows.GetString(0);if(previous!=id){RecordOmission();shapes.Clear();previous=id;}var declaration=JsonNode.Parse(rows.GetString(1))!;
                var shape=new JsonObject();foreach(var key in new[]{"kind","arguments","return_type"})shape[key]=declaration[key]?.DeepClone();shapes.Add(canonical.Hash(Element(shape)));
            }
            RecordOmission();var integrity=(string)Scalar(database,"PRAGMA integrity_check")!;var fk=ForeignKeys(database);var rebuilt=Scalar(database,"SELECT value FROM catalog_meta WHERE key='rebuilt_with_corrected_extractor'") as string ?? "unknown";
            var counts=new JsonObject();foreach(var table in new[]{"sources","bindings","occurrences","binding_declarations","permission_definitions","registry_entries"})counts[table]=Count(database,table);
            report=new(){["database"]=full,["database_open_mode"]="read-only",["validated_at"]=DateTimeOffset.UtcNow.ToString("O"),["entries_validated"]=entries.Entries,["json_schema_failures"]=entries.Failures,["integrity_check"]=integrity,["foreign_key_check"]=fk,
                ["registry_generation"]=(long)Scalar(database,"SELECT registry_generation FROM catalog_view")!,["rebuilt_with_corrected_extractor"]=rebuilt,["source_applicability_facts_revalidated"]=false,["declaration_variant_omissions"]=omissions,["declaration_variant_omission_sample_ids"]=samples,["complete"]=false,
                ["limitations"]=new JsonArray("No source acquisition or semantic rebuild performed","Legacy API/channel claims are preserved, not independently re-established"),["counts"]=counts,
                ["validation_inputs"]=new JsonObject{["loaded_validator_sha256"]=ProducerFingerprint,["loaded_validator_identity"]="csharp-monolithic-assembly",["entry_schema_sha256"]=ContentHash.Sha256(inputs.EntryBytes),["permission_schema_sha256"]=ContentHash.Sha256(inputs.PermissionBytes)},["native_producer"]=Producer()};
        }
        var artifact=await WriteReport(reportFull,report,canonical,cancellationToken);
        Check(report["json_schema_failures"]!.AsArray().Count==0 && S(report["integrity_check"])=="ok" && report["foreign_key_check"]!.AsArray().Count==0,"Database validation failed; see report");
        Check(S(report["rebuilt_with_corrected_extractor"])!="true" || report["declaration_variant_omissions"]!.GetValue<long>()==0,"Corrected rebuild omits declaration variants");return new(report,artifact,readOnly);
    }
}
