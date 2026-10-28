using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Json.Schema;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed class CatalogBuildException(string code,string message,Exception? inner=null):IOException(message,inner)
{ public string Code {get;}=code; }
public sealed record CatalogBuildResult(string RegistryId,string ProducerFingerprint,IReadOnlyList<StoredArtifact> Artifacts,JsonObject Validation);

/// <summary>Offline native source-to-sealed-catalog generation. No runtime authority or device observations.</summary>
public sealed class CatalogBuild(string catalogRoot,ISourcesExtractionDispatcher dispatcher,IPermissionExpressionSourceFacts permissions,
    IAnnotationTargetSourceFacts formals,IStructuralTypeHeaderSourceFacts headers,ICanonicalJson canonical,ISourceXmlFormatter? xmlFormatter=null)
{
    public const string ProducerId="csharp-nuke-catalog-v1";
    public string ProducerFingerprint {get;}=ContentHash.Sha256(File.ReadAllBytes(typeof(CatalogBuild).Assembly.Location));
    private readonly ISourceXmlFormatter xml=xmlFormatter ?? new SourceXmlFormatter();
    private static string S(JsonNode? value)=>value?.GetValue<string>() ?? "";
    private static JsonElement Element(JsonNode? value)=>JsonSerializer.SerializeToElement(value);
    private string Text(JsonNode? value)=>Encoding.UTF8.GetString(canonical.Serialize(Element(value)));
    private string Hash(JsonNode? value)=>canonical.Hash(Element(value));
    private static string Sha(ReadOnlyMemory<byte> value)=>ContentHash.Sha256(value.Span);
    private static CatalogBuildException Failure(string code,string message)=>new(code,message);
    private static long Positive(JsonNode? value,string label)
    {
        var element=Element(value);
        if(element.ValueKind!=JsonValueKind.Number || !StrictJsonInspector.IsIntegerToken(element.GetRawText()) || !element.TryGetInt64(out var result) || result<1)
            throw Failure("catalog-metadata",label+" must be a positive integer");
        return result;
    }
    private sealed record Input(JsonObject Source,byte[] Bytes,ExtractionResult Extraction);
    public async Task<CatalogBuildResult> BuildAsync(string inventoryPath,string outputDirectory,string privateStageDirectory,CancellationToken cancellationToken=default)
    {
        var output=Path.GetFullPath(outputDirectory);var inventoryFull=Path.GetFullPath(inventoryPath);
        _=ArtifactPaths.Resolve(output,"catalog.sqlite");_=ArtifactPaths.Resolve(privateStageDirectory,"generation.lock");
        await using var lease=await new FileArtifactLeaseProvider(privateStageDirectory).AcquireAsync(output,cancellationToken);
        if(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw Failure("nonempty-output","Output must be empty; preserve immutable earlier publication");
        var store=new FileArtifactStore(output,Sha);var artifacts=new List<StoredArtifact>();
        var producerFingerprint=ProducerFingerprint;
        var producer=new JsonObject{["id"]=ProducerId,["assembly_sha256"]=producerFingerprint,["historical_python_identity"]=false};
        try
        {
            var inventoryBytes=await File.ReadAllBytesAsync(inventoryFull,cancellationToken);
            // The canonical authority performs strict UTF8, duplicate-name and finite-number inspection.
            _=canonical.Serialize(inventoryBytes);var spec=JsonNode.Parse(inventoryBytes)!.AsObject();
            var metadata=spec["catalog"]?.AsObject() ?? throw Failure("catalog-metadata","Required catalog metadata is absent");
            var version=Positive(metadata["registry_version"],"registry_version");_=Positive(metadata["api_level"],"api_level");
            foreach(var name in new[]{"android_version","coverage"}) if(string.IsNullOrEmpty(S(metadata[name]))) throw Failure("catalog-metadata",name+" must be nonempty");
            var sourceRoot=Path.GetDirectoryName(inventoryFull)!;var inputs=new List<Input>();
            foreach(var original in spec["sources"]!.AsArray().OfType<JsonObject>().Where(item=>S(item["status"])=="downloaded").OrderBy(item=>S(item["kind"])=="sdk-api-index" ? 0 : 1))
            {
                cancellationToken.ThrowIfCancellationRequested();var source=(JsonObject)original.DeepClone();
                if(!source.ContainsKey("api_level") || string.IsNullOrEmpty(S(source["channel"]))) throw Failure("source-metadata","Explicit source api_level/channel metadata required");
                if(source["api_level"] is not null) _=Positive(source["api_level"],"source api_level");
                if(S(source["kind"])=="sdk-annotations" && string.IsNullOrEmpty(S(source["platform_api_version"]))) throw Failure("source-metadata","Explicit platform_api_version required for sdk-annotations");
                if(S(source["scope"])=="aosp-pinned-hidl-source-evidence" && (source["api_level"] is not null || source["platform_api_version"] is not null))
                    throw Failure("source-metadata","HIDL release context does not establish Android applicability");
                var path=ArtifactPaths.Resolve(sourceRoot,S(source["local_path"]));var bytes=await File.ReadAllBytesAsync(path,cancellationToken);
                if(Sha(bytes)!=S(source["sha256"])) throw Failure("source-sha-mismatch","SHA256 mismatch "+S(source["path"]));
                if(ContentHash.GitBlobSha1Legacy(bytes)!=S(source["git_blob"])) throw Failure("git-blob-mismatch","Git blob mismatch "+S(source["path"]));
                if(source["metadata_cache"] is JsonObject cache)
                {
                    var cached=await File.ReadAllBytesAsync(ArtifactPaths.Resolve(sourceRoot,S(cache["path"])),cancellationToken);
                    if(Sha(cached)!=S(cache["sha256"])) throw Failure("cache-sha-mismatch","Metadata cache SHA256 mismatch "+S(source["path"]));
                    // The declared bytes are verified and retained as unused audit evidence.
                    // Native declarations are extracted from raw source, never from this cache.
                    source["x-native-cache-audit"]=new JsonObject{["disposition"]="verified-unused/native-raw-reextraction",["historical_cache"]=cache.DeepClone(),
                        ["historical_context"]=source["java_file_context"]?.DeepClone(),["records_consumed"]=false,["historical_identity_claimed"]=false};
                }
                var acquisition=new JsonObject();foreach(var key in new[]{"url","revision","sha256","retrieved_at","path","api_level","channel","kind","license","scope"}) acquisition[key]=source[key]?.DeepClone();
                source["source_id"]=Hash(new JsonObject{["acquisition"]=acquisition,["processing_sha256"]=producerFingerprint});
                source["x-source-identity-version"]=3;source["x-native-producer"]=producer.DeepClone();
                var context=new SourceContext(S(source["repository"]),S(source["revision"]),S(source["source_id"]),source["platform_api_version"]?.GetValue<string>());
                var extracted=dispatcher.Extract(new(S(source["path"]),bytes,context),S(source["kind"]),S(source["scope"]));
                inputs.Add(new(source,bytes,extracted));
            }
            using var db=CatalogDatabase.CreateInMemory(catalogRoot,CatalogSchemaProfile.Entry);using var transaction=db.Connection.BeginTransaction(deferred:false);
            void Execute(string sql,params (string Name,object? Value)[] values){using var command=db.Command(sql,transaction,values);command.ExecuteNonQuery();}
            long Count(string table){using var command=db.Command("SELECT count(*) FROM "+table,transaction);return (long)command.ExecuteScalar()!;}
            var sources=inputs.ToDictionary(input=>S(input.Source["source_id"]),input=>input.Source,StringComparer.Ordinal);
            var sourceSet=Hash(new JsonArray(sources.Keys.Order(StringComparer.Ordinal).Select(key=>(JsonNode)JsonValue.Create(key)!).ToArray()));
            var registry=Hash(new JsonObject{["source_set_hash"]=sourceSet,["metadata"]=metadata.DeepClone()});
            Execute("INSERT INTO registries VALUES($id,$version,'1.2.0',$android,$api,$set,$now,'staged','{}')",("$id",registry),("$version",version),("$android",S(metadata["android_version"])),("$api",Positive(metadata["api_level"],"api_level")),("$set",sourceSet),("$now",DateTimeOffset.Now.ToString("O",CultureInfo.InvariantCulture)));
            Execute("UPDATE catalog_view SET registry_generation=$version WHERE singleton=1",("$version",version));
            foreach(var input in inputs)
            {
                var source=input.Source;Execute("INSERT INTO sources VALUES($id,$path,$url,$revision,$at,$sha,$api,$channel,$kind,$license)",
                    ("$id",S(source["source_id"])),("$path",S(source["path"])),("$url",S(source["url"])),("$revision",S(source["revision"])),("$at",S(source["retrieved_at"])),("$sha",S(source["sha256"])),("$api",source["api_level"] is null ? null : Positive(source["api_level"],"api_level")),("$channel",S(source["channel"])),("$kind",S(source["kind"])),("$license",S(source["license"])));
                Execute("INSERT INTO source_details VALUES($id,$doc)",("$id",S(source["source_id"])),("$doc",Text(source)));
            }
            var symbolValues=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
            foreach(var input in inputs)
            foreach(var row in input.Extraction.Records.Where(row=>S(row["kind"])=="field" && row["extras"]?["constant_raw"] is not null))
            {
                string? value;try{value=JsonSerializer.Deserialize<string>(S(row["extras"]!["constant_raw"]));}catch(JsonException){continue;}
                if(value is null || !value.Contains(".permission.",StringComparison.Ordinal) && !S(row["owner"]).EndsWith(".permission",StringComparison.Ordinal)) continue;
                var symbol=S(row["owner"])+"."+S(row["name"]);if(!symbolValues.TryGetValue(symbol,out var set)){set=new(StringComparer.Ordinal);symbolValues.Add(symbol,set);}set.Add(value);
                Execute("INSERT OR IGNORE INTO permission_symbols VALUES($symbol,$value,$source,$line)",("$symbol",symbol),("$value",value),("$source",S(input.Source["source_id"])),("$line",row["line"]!.GetValue<int>()));
            }
            var symbols=symbolValues.Where(item=>item.Value.Count==1).ToDictionary(item=>item.Key,item=>item.Value.Single(),StringComparer.Ordinal);
            var astSchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(catalogRoot,"contracts/PermissionExpression.schema.json")));
            void ValidatePermission(JsonObject value)
            {if(!astSchema.Evaluate(Element(value),new(){OutputFormat=OutputFormat.Flag}).IsValid) throw Failure("invalid-permission-ast","Permission schema rejected source declaration");}
            var declarations=new List<CatalogDeclaration>();var reports=new JsonArray();
            foreach(var input in inputs)
            {
                var source=input.Source;var sid=S(source["source_id"]);var kind=S(source["kind"]);var count=0;
                foreach(var issue in input.Extraction.Issues) Execute("INSERT INTO issues(source_id,line,category,reason,raw) VALUES($source,NULL,$category,$reason,$raw)",("$source",sid),("$category",issue.Code),("$reason",issue.Reason),("$raw",Convert.ToBase64String(input.Bytes)));
                if(kind=="manifest")
                {
                    foreach(var row in input.Extraction.Records.Where(row=>S(row["kind"])=="manifest-permission" && row["name"] is not null))
                    {
                        var definition=new JsonObject{["attributes"]=row["attributes"]?.DeepClone(),["children"]=new JsonArray((row["children"] as JsonArray ?? []).OfType<JsonObject>().Select(child=>(JsonNode)JsonValue.Create(xml.Serialize(child))!).ToArray())};
                        Execute("INSERT OR IGNORE INTO permission_definitions VALUES($name,$source,$level,$attributes)",("$name",S(row["name"])),("$source",sid),("$level",row["protection_level_raw"]?.GetValue<string>()),("$attributes",Text(definition)));
                    }
                }
                else foreach(var row in input.Extraction.Records)
                {
                    if(row["canonical"] is null) throw Failure("source-concern-unadmitted","Source concern is retained but has no declaration normalizer for "+kind);
                    var language=kind switch{"sdk-api-index"=>"jvm","java-source"=>"java-source","aidl"=>"aidl",_=>S(source["scope"])=="aosp-pinned-hidl-source-evidence" ? "hidl" : "java"};
                    var signature=language+":"+(S(row["kind"])=="package" ? "package:" : "")+S(row["canonical"]);var bid=ContentHash.Sha256(Encoding.UTF8.GetBytes(signature));var line=row["line"]!.GetValue<int>();
                    var ast=permissions.Permission((JsonArray)row["annotations"]!.DeepClone(),symbols,language=="aidl");ValidatePermission(ast);
                    var extra=new JsonObject{["language"]=language,["source_kind"]=kind,["source_path"]=source["path"]?.DeepClone(),["source_scope"]=source["scope"]?.DeepClone(),["annotations"]=row["all_annotations"]!.DeepClone(),["parameters"]=row["parameters"]!.DeepClone()};
                    foreach(var property in row["extras"]!.AsObject()) extra[property.Key]=property.Value?.DeepClone();
                    foreach(var parameter in extra["parameters"]!.AsArray().OfType<JsonObject>())
                    {var requirement=permissions.Permission((JsonArray)parameter["annotations"]!.DeepClone(),symbols,language=="aidl");ValidatePermission(requirement);parameter["permission"]=requirement;}
                    JsonObject Reference(string text)=>new(){["text"]=text,["status"]="unknown",["codec_status"]="unknown",["reason"]="Source type retained; runtime resolver/codec not verified"};
                    var document=new JsonObject{["schema_version"]=S(row["kind"]) is "aidl-parcelable" or "aidl-union" or "aidl-enum" or "type" ? "1.1.0" : "1.0.0",["entry_id"]=bid,["canonical_signature"]=signature,["kind"]=row["kind"]!.DeepClone(),["name"]=row["name"]!.DeepClone(),["package"]=row["package"]!.DeepClone(),["permission"]=ast,
                        ["provenance"]=new JsonArray(new JsonObject{["source_id"]=sid,["line"]=line,["raw_signature"]=row["raw"]!.DeepClone(),["source_revision"]=source["revision"]!.DeepClone()}),["origin"]="imported",["state"]="active",["registry_version"]=version,["overlay_revision"]=0,["codec_status"]="unknown",
                        ["unresolved"]=new JsonArray("OS authority, MCP client grants and wire codecs are not established by catalog metadata"),["required_semantics"]=new JsonArray(),["extensions"]=extra,["arguments"]=new JsonArray(row["parameters"]!.AsArray().Select(parameter=>(JsonNode)Reference(S(parameter!["type"]))).ToArray())};
                    if(row["owner"] is not null) document["owner"]=row["owner"]!.DeepClone();if(row["return_type"] is not null) document["return_type"]=Reference(S(row["return_type"]));
                    Execute("INSERT OR IGNORE INTO bindings(binding_id,signature,package,owner,kind,name,return_type,arguments_json,raw_signature) VALUES($id,$signature,$package,$owner,$kind,$name,$return,$arguments,$raw)",("$id",bid),("$signature",signature),("$package",S(row["package"])),("$owner",row["owner"]?.GetValue<string>()),("$kind",S(row["kind"])),("$name",S(row["name"])),("$return",row["return_type"]?.GetValue<string>()),("$arguments",Text(new JsonArray(row["parameters"]!.AsArray().Select(parameter=>parameter!["type"]!.DeepClone()).ToArray()))),("$raw",S(row["raw"])));
                    Execute("INSERT INTO occurrences VALUES($id,$source,$line)",("$id",bid),("$source",sid),("$line",line));
                    Execute("INSERT INTO binding_declarations VALUES($id,$source,$line,$doc,$hash)",("$id",bid),("$source",sid),("$line",line),("$doc",Text(document)),("$hash",Hash(document)));
                    Execute("INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES($id,$source,$line,$annotation,$logic,$expression,$raw)",("$id",bid),("$source",sid),("$line",line),("$annotation",string.Join(',',row["annotations"]!.AsArray().Select(annotation=>S(annotation!["name"])))),("$logic",S(ast["op"])),("$expression",Text(ast)),("$raw",Text(row["annotations"])));
                    declarations.Add(new(sid,line,document));count++;
                }
                var report=new JsonObject{["source_id"]=sid,["kind"]=kind,["input_bytes"]=input.Bytes.Length,["declarations"]=count,["unsupported"]=input.Extraction.Records.Count(row=>S(row["kind"])=="unsupported"),["structural_status"]=input.Extraction.StructuralStatus.ToString(),["source_metadata"]=input.Extraction.Metadata?.DeepClone()};reports.Add(report);
                Execute("INSERT INTO parse_reports VALUES($source,$doc)",("$source",sid),("$doc",Text(report)));
            }
            var contracts=new CatalogContracts(canonical);var projection=new CatalogProjection(sources,declarations,formals,headers,contracts,canonical);
            var entries=new List<JsonObject>();var inventory=new List<JsonObject>();
            foreach(var group in declarations.GroupBy(row=>S(row.Document["entry_id"])).OrderBy(group=>group.Key,StringComparer.Ordinal))
            {
                var rows=group.Select(row=>row with{Document=projection.Project(row.Document,row.SourceId)}).OrderBy(row=>SourceRank(sources[row.SourceId])).ThenBy(row=>S(sources[row.SourceId]["path"]),Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray();
                var document=(JsonObject)rows[0].Document.DeepClone();document["provenance"]=new JsonArray(rows.SelectMany(row=>row.Document["provenance"]!.AsArray()).Select(value=>value!.DeepClone()).ToArray());
                document["extensions"]!["occurrence_count"]=rows.Length;
                var entity=projection.Entity(group.Key,rows);document["extensions"]!["device_resolution_variants"]=entity["variants"]!.DeepClone();document["extensions"]!["device_resolution_contract_version"]="2.1.0";
                entries.Add(document);inventory.Add(entity);
                Execute("INSERT INTO registry_entries VALUES($registry,$id,$doc,$hash)",("$registry",registry),("$id",group.Key),("$doc",Text(document)),("$hash",Hash(document)));
                Execute("INSERT INTO registry_inventory VALUES($registry,$id,$doc,$hash)",("$registry",registry),("$id",group.Key),("$doc",Text(entity)),("$hash",Hash(entity)));
            }
            var entrySchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(catalogRoot,"contracts/Entry.schema.json")));var resolver=new DeviceResolver(canonical,Path.Combine(catalogRoot,"contracts/DeviceResolution.schema.json"));
            foreach(var entry in entries) if(!entrySchema.Evaluate(Element(entry),new(){OutputFormat=OutputFormat.Flag}).IsValid) throw Failure("invalid-entry","Entry schema rejected "+S(entry["entry_id"]));
            foreach(var item in inventory) resolver.ValidateInventoryEntity(Element(item));
            using(var integrity=db.Command("PRAGMA integrity_check",transaction)) if((string)integrity.ExecuteScalar()! !="ok") throw Failure("integrity","SQLite integrity check failed");
            using(var foreign=db.Command("PRAGMA foreign_key_check",transaction)) using(var reader=foreign.ExecuteReader()) if(reader.Read()) throw Failure("foreign-key","SQLite foreign-key check failed");
            if(Count("bindings")!=entries.Count || Count("occurrences")!=declarations.Count || inventory.Count!=entries.Count) throw Failure("reconciliation","Catalog count reconciliation failed");
            var counts=new JsonObject();foreach(var table in new[]{"sources","bindings","occurrences","binding_declarations","permission_definitions","permission_symbols","permission_relations","aidl_schemas","annotation_evidence","issues"}) counts[table]=Count(table);counts["effective_entries"]=entries.Count;
            var validation=new JsonObject{["integrity_check"]="ok",["foreign_key_check"]=new JsonArray(),["json_schema_failures"]=new JsonArray(),["counts"]=counts,["source_reports"]=reports,["complete"]=false,["native_producer"]=producer.DeepClone()};
            foreach(var source in sources.Keys) Execute("INSERT INTO registry_sources VALUES($registry,$source)",("$registry",registry),("$source",source));
            Execute("UPDATE registries SET state='validated',validation_json=$doc WHERE registry_id=$id",("$doc",Text(validation)),("$id",registry));Execute("UPDATE registries SET state='published' WHERE registry_id=$id",("$id",registry));Execute("INSERT INTO active_registry(singleton,registry_id) VALUES(1,$id)",("$id",registry));
            foreach(var pair in new Dictionary<string,string>{{"schema_version","1.2.0"},{"entry_contract_version","1.1.0"},{"device_resolution_contract_version","2.1.0"},{"source_set_hash",sourceSet},{"coverage",S(metadata["coverage"])},{"source_inventory_sha256",Sha(inventoryBytes)},{"authority","Metadata only; no OS capability or MCP client grant"},{"native_producer_id","csharp-nuke-catalog-v1"},{"native_producer_sha256",producerFingerprint}})
                Execute("INSERT INTO catalog_meta VALUES($key,$value)",("$key",pair.Key),("$value",pair.Value));
            transaction.Commit();
            async Task Write(string name,ReadOnlyMemory<byte> bytes,string media="application/json")=>artifacts.Add(await store.WriteAsync(new(name,bytes,media),cancellationToken));
            foreach(var table in new[]{"sources","source_details","bindings","occurrences","binding_declarations","permission_definitions","permission_symbols","permission_relations","aidl_schemas","annotation_evidence","issues","parse_reports"})
            {
                var lines=new StringBuilder();using(var command=db.Command("SELECT * FROM "+table+" ORDER BY "+(table=="issues" ? "id" : "1,2"))) using(var reader=command.ExecuteReader()) while(reader.Read())
                {var value=new JsonObject();for(var index=0;index<reader.FieldCount;index++) value[reader.GetName(index)]=reader.IsDBNull(index) ? null : JsonSerializer.SerializeToNode(reader.GetValue(index));lines.Append(Text(value)).Append('\n');}
                await Write(table+".jsonl",Encoding.UTF8.GetBytes(lines.ToString()),"application/x-ndjson");
            }
            var entryBytes=Encoding.UTF8.GetBytes(string.Concat(entries.Select(entry=>Text(entry)+"\n")));var inventoryBytesOut=Encoding.UTF8.GetBytes(string.Concat(inventory.Select(item=>Text(item)+"\n")));
            await Write("entries.jsonl",entryBytes,"application/x-ndjson");await Write("device_inventory.jsonl",inventoryBytesOut,"application/x-ndjson");
            await Write("device_inventory_manifest.json",canonical.Serialize(Element(new JsonObject{["contract"]="urn:android-binding-catalog:2.1.0:DeviceResolution",["registry_id"]=registry,["entries"]=entries.Count,["inventory_sha256"]=Sha(inventoryBytesOut),["semantic_scope"]="Full versioned metadata inventory; no device observation or permission grant"})));
            await Write("catalog.json",canonical.Serialize(Element(new JsonObject{["schema_version"]="1.2.0",["complete"]=false,["entries"]=new JsonArray(entries.Select(entry=>(JsonNode)entry.DeepClone()).ToArray())})));
            await Write("dispatch_manifest.json",canonical.Serialize(Element(new CatalogAliases(canonical,contracts).Generate(entries))));await Write("validation.json",canonical.Serialize(Element(validation)));
            // Retained source database lives outside the final artifact set. Never move/delete it.
            var stage=ArtifactPaths.Resolve(privateStageDirectory,Guid.NewGuid().ToString("N")+"/catalog.staging.sqlite");Directory.CreateDirectory(Path.GetDirectoryName(stage)!);
            using(var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=stage,Pooling=false}.ToString())){target.Open();db.Connection.BackupDatabase(target);}
            foreach(var input in inputs)
                if(!(await File.ReadAllBytesAsync(ArtifactPaths.Resolve(sourceRoot,S(input.Source["local_path"])),cancellationToken)).AsSpan().SequenceEqual(input.Bytes)) throw Failure("input-changed","Source changed before final publication marker");
            if(!(await File.ReadAllBytesAsync(inventoryFull,cancellationToken)).AsSpan().SequenceEqual(inventoryBytes)) throw Failure("input-changed","Inventory changed before final publication marker");
            var finalBytes=await File.ReadAllBytesAsync(stage,cancellationToken);await Write("catalog.sqlite",finalBytes,"application/vnd.sqlite3");
            if(!(await store.ReadAsync("catalog.sqlite",cancellationToken)).Span.SequenceEqual(finalBytes)) throw Failure("final-copy-mismatch","Final database copy differs from retained staging source");
            return new(registry,producerFingerprint,artifacts.ToArray(),validation);
        }
        catch(Exception error) when(error is not OperationCanceledException)
        {
            var code=error is CatalogBuildException failure ? failure.Code : "catalog-build-failed";
            var receipt=new JsonObject{["published"]=false,["code"]=code,["error_type"]=error.GetType().Name,["error"]=error.Message,["native_producer"]=producer.DeepClone()};
            await store.WriteAsync(new("validation-failure.json",canonical.Serialize(Element(receipt)),"application/json"),cancellationToken);
            if(error is CatalogBuildException) throw;throw new CatalogBuildException(code,error.Message,error);
        }
    }
    private static int SourceRank(JsonObject source)=>S(source["kind"])=="sdk-signature" ? 0 : S(source["kind"])=="source-signature" ? Path.GetFileName(S(source["path"]))=="current.txt" ? 1 : 2 : 3;
}
