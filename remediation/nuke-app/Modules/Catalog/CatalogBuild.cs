using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Publication;
using Json.Schema;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed class CatalogBuildException(string code,string message,Exception? inner=null):IOException(message,inner)
{ public string Code {get;}=code; }
public sealed record CatalogBuildResult(string RegistryId,string ProducerFingerprint,IReadOnlyList<StoredArtifact> Artifacts,JsonObject Validation,string StagingDatabasePath,string RawSourceStagePath);
public sealed record CatalogSourceIntake(ISourceExtractGenerationReader Reader,SourceExtractGenerationRequest Request);

/// <summary>Offline native source-to-sealed-catalog generation. No runtime authority or device observations.</summary>
public sealed class CatalogBuild(string catalogRoot,ISourcesExtractionDispatcher dispatcher,IPermissionExpressionSourceFacts permissions,
    IAnnotationTargetSourceFacts formals,IStructuralTypeHeaderSourceFacts headers,ICanonicalJson canonical,ISourceXmlFormatter? xmlFormatter=null,
    Func<string,IStreamingArtifactStore>? outputStores=null,Func<string,CatalogDatabase>? stagingDatabases=null,IJvmSourceLinker? sourceJvm=null,Action<CatalogImportObservation>? observeImport=null,CatalogSourceIntake? sourceIntake=null)
{
    public const string ProducerId="csharp-nuke-catalog-v1";
    public string ProducerFingerprint {get;}=ContentHash.Sha256(File.ReadAllBytes(typeof(CatalogBuild).Assembly.Location));
    private readonly ISourceXmlFormatter xml=xmlFormatter ?? new SourceXmlFormatter();
    private readonly IJvmSourceLinker sourceJvm=sourceJvm ?? new JvmSourceLinker();
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
    private sealed record Input(JsonObject Source,long Length,bool CacheHit=false,bool Facade=false);
    public Task<CatalogBuildResult> BuildAsync(string inventoryPath,string outputDirectory,string privateStageDirectory,CancellationToken cancellationToken=default)=>
        BuildCoreAsync(inventoryPath,null,outputDirectory,privateStageDirectory,cancellationToken);
    public Task<CatalogBuildResult> BuildAsync(CatalogInventoryBridgeResult bridge,string outputDirectory,string privateStageDirectory,CancellationToken cancellationToken=default)=>
        BuildCoreAsync(null,bridge,outputDirectory,privateStageDirectory,cancellationToken);
    private async Task<CatalogBuildResult> BuildCoreAsync(string? inventoryPath,CatalogInventoryBridgeResult? bridge,string outputDirectory,string privateStageDirectory,CancellationToken cancellationToken)
    {
        var output=Path.GetFullPath(outputDirectory);var inventoryFull=inventoryPath is null ? null : Path.GetFullPath(inventoryPath);var intake=bridge?.SourceExtraction ?? sourceIntake;
        ISourceExtractGeneration? opened=null;byte[]? admittedBytes=null;JsonObject? admittedSpec=null;Dictionary<int,RehydrationSourceRow>? admittedRows=null;
        ExceptionDispatchInfo? deferredManifestError=null;
        async Task<JsonObject> ReadManifest()
        {
            admittedBytes=bridge is null ? await File.ReadAllBytesAsync(inventoryFull!,cancellationToken) : bridge.LegacyManifest.Bytes.ToArray();
            _=canonical.Serialize(admittedBytes);return JsonNode.Parse(admittedBytes)!.AsObject();
        }
        try
        {
            if(intake is not null)
            {
                opened=await intake.Reader.OpenAsync(intake.Request,cancellationToken);
                admittedSpec=await ReadManifest();
                var rows=admittedSpec["sources"]!.AsArray();var projection=intake.Request.Projection;
                if(rows.Count!=projection.Rows.Count || projection.Rows.Select(row=>row.RowIndex).Distinct().Count()!=projection.Rows.Count)
                    throw Failure("catalog-source-row-mismatch","Inventory and admitted projection have different logical membership");
                foreach(var row in projection.Rows)
                    if(row.RowIndex<0 || row.RowIndex>=rows.Count || canonical.Hash(Element(rows[row.RowIndex]))!=canonical.Hash(row.OriginalRow))
                        throw Failure("catalog-source-row-mismatch","Inventory row differs from the exact admitted OriginalRow");
                var selected=projection.SelectedPhysicalSourceIndices.ToHashSet();admittedRows=projection.Rows.Where(row=>selected.Contains(row.PhysicalSourceIndex)).ToDictionary(row=>row.RowIndex);
            }
        }
        catch(SourceExtractException error){opened?.Dispose();throw new CatalogBuildException(error.Code,error.Message,error);}
        catch{opened?.Dispose();throw;}
        using var generation=opened;
        if(intake is null)
        {
            try{admittedSpec=await ReadManifest();}
            catch(Exception error)when(error is IOException or JsonException or CanonicalJsonException or InvalidOperationException)
            {
                // Preserve ordinary legacy failure publication semantics while
                // reading/strict-parsing a valid manifest only once.
                deferredManifestError=ExceptionDispatchInfo.Capture(error);
            }
        }
        if(bridge is null && admittedSpec?["x-native-catalog-input"] is JsonObject marker &&
            marker["requires_typed_context"] is JsonValue required && required.TryGetValue<bool>(out var typedRequired) && typedRequired)
            throw Failure("catalog-typed-context-required","A Catalog bridge manifest requires its admitted typed context");
        _=ArtifactPaths.Resolve(output,"catalog.sqlite");_=ArtifactPaths.Resolve(privateStageDirectory,"generation.lock");
        await using var lease=await new FileArtifactLeaseProvider(privateStageDirectory).AcquireAsync(output,cancellationToken);
        if(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw Failure("nonempty-output","Output must be empty; preserve immutable earlier publication");
        var store=outputStores?.Invoke(output) ?? new FileArtifactStore(output,Sha);var artifacts=new List<StoredArtifact>();
        var producerFingerprint=ProducerFingerprint;
        var producer=new JsonObject{["id"]=ProducerId,["assembly_sha256"]=producerFingerprint,["historical_python_identity"]=false};
        try
        {
            var rawPath=ArtifactPaths.Resolve(privateStageDirectory,Guid.NewGuid().ToString("N")+"/source-records.sqlite");
            using var raw=new CatalogImportStage(rawPath,canonical);observeImport?.Invoke(new("raw-stage",rawPath));
            deferredManifestError?.Throw();
            var inventoryBytes=admittedBytes!;var spec=admittedSpec!;
            var metadata=spec["catalog"]?.AsObject() ?? throw Failure("catalog-metadata","Required catalog metadata is absent");
            var version=Positive(metadata["registry_version"],"registry_version");_=Positive(metadata["api_level"],"api_level");
            foreach(var name in new[]{"android_version","coverage"}) if(string.IsNullOrEmpty(S(metadata[name]))) throw Failure("catalog-metadata",name+" must be nonempty");
            var sourceRoot=bridge?.AcquisitionRawRoot ?? Path.GetDirectoryName(inventoryFull!)!;var inputs=new List<Input>();
            foreach(var candidate in spec["sources"]!.AsArray().Select((node,index)=>(Source:node!.AsObject(),Index:index)).Where(item=>(bridge is null ? S(item.Source["status"])=="downloaded" : bridge.VerifiedLogicalRows.Contains(item.Index)) && (admittedRows is null || admittedRows.ContainsKey(item.Index))).OrderBy(item=>S(item.Source["kind"])=="sdk-api-index" ? 0 : 1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Only Catalog's owned view is normalized. Admitted OriginalRow
                // and Source reader/context remain the exact native projection.
                var source=(JsonObject)(bridge?.NormalizedSources is { } normalized ? normalized[candidate.Index] : candidate.Source).DeepClone();
                if(!source.ContainsKey("api_level") || string.IsNullOrEmpty(S(source["channel"]))) throw Failure("source-metadata","Explicit source api_level/channel metadata required");
                if(source["api_level"] is not null) _=Positive(source["api_level"],"source api_level");
                if(S(source["kind"])=="sdk-annotations" && string.IsNullOrEmpty(S(source["platform_api_version"]))) throw Failure("source-metadata","Explicit platform_api_version required for sdk-annotations");
                if(S(candidate.Source["scope"])=="aosp-pinned-hidl-source-evidence" && (source["api_level"] is not null || source["platform_api_version"] is not null))
                    throw Failure("source-metadata","HIDL release context does not establish Android applicability");
                byte[]? bytes=null;
                if(generation is null)
                {
                var path=ArtifactPaths.Resolve(sourceRoot,S(source["local_path"]));bytes=await File.ReadAllBytesAsync(path,cancellationToken);
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
                }
                var acquisition=new JsonObject();foreach(var key in new[]{"url","revision","sha256","retrieved_at","path","api_level","channel","kind","license","scope"}) acquisition[key]=source[key]?.DeepClone();
                source["source_id"]=Hash(new JsonObject{["acquisition"]=acquisition,["processing_sha256"]=producerFingerprint});
                source["x-source-identity-version"]=3;source["x-native-producer"]=producer.DeepClone();
                long length;var hit=false;
                if(generation is null)
                {
                    var context=new SourceContext(S(source["repository"]),S(source["revision"]),S(source["source_id"]),source["platform_api_version"]?.GetValue<string>());
                    var extracted=dispatcher.Extract(new(S(source["path"]),bytes!,context),S(source["kind"]),S(source["scope"]));raw.Capture(S(source["source_id"]),extracted);length=bytes!.LongLength;
                }
                else
                {
                    var row=await generation.ReadAsync(admittedRows![candidate.Index],cancellationToken);
                    if(row.RowIndex!=candidate.Index || row.SourceLength<0 || row.CacheHit && (intake!.Request.TrustedIndex is null || row.CacheReceipt is null || row.CacheSeal is null))
                        throw Failure("catalog-source-row-mismatch","Source reader response does not match its admitted row/cache authority");
                    source["x-native-source-intake"]=new JsonObject{["producer"]=JsonSerializer.SerializeToNode(intake!.Request.Producer),["profile_id"]=intake.Request.Profile.Id,
                        ["context"]=JsonSerializer.SerializeToNode(row.Context),["source_length"]=row.SourceLength,["cache_hit"]=row.CacheHit,["cache_receipt"]=JsonSerializer.SerializeToNode(row.CacheReceipt),["cache_seal"]=JsonSerializer.SerializeToNode(row.CacheSeal),
                        ["historical_cache_disposition"]=source["metadata_cache"] is null ? "not-declared" : "declared-unconsumed-unverified",["historical_cache"]=source["metadata_cache"]?.DeepClone(),["historical_records_consumed"]=false,["historical_identity_claimed"]=false};
                    raw.Capture(S(source["source_id"]),row);length=row.SourceLength;hit=row.CacheHit;
                }
                observeImport?.Invoke(new("source-captured",rawPath,S(source["source_id"]),Count:raw.Count(S(source["source_id"]))));
                inputs.Add(new(source,length,hit,generation is not null));
            }
            var stage=ArtifactPaths.Resolve(privateStageDirectory,Guid.NewGuid().ToString("N")+"/catalog.staging.sqlite");
            using var db=stagingDatabases?.Invoke(stage) ?? CatalogDatabase.CreateFile(catalogRoot,CatalogSchemaProfile.Entry,stage);
            using var transaction=db.Connection.BeginTransaction(deferred:false);
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
            foreach(var row in raw.Records(S(input.Source["source_id"])).Where(row=>S(row["kind"])=="field" && row["extras"]?["constant_raw"] is not null))
            {
                string? value;try{value=JsonSerializer.Deserialize<string>(S(row["extras"]!["constant_raw"]));}catch(JsonException){continue;}
                if(value is null || !value.Contains(".permission.",StringComparison.Ordinal) && !S(row["owner"]).EndsWith(".permission",StringComparison.Ordinal)) continue;
                var symbol=S(row["owner"])+"."+S(row["name"]);if(!symbolValues.TryGetValue(symbol,out var set)){set=new(StringComparer.Ordinal);symbolValues.Add(symbol,set);}set.Add(value);
                Execute("INSERT OR IGNORE INTO permission_symbols VALUES($symbol,$value,$source,$line)",("$symbol",symbol),("$value",value),("$source",S(input.Source["source_id"])),("$line",row["line"]!.GetValue<int>()));
            }
            var symbols=symbolValues.Where(item=>item.Value.Count==1).ToDictionary(item=>item.Key,item=>item.Value.Single(),StringComparer.Ordinal);
            var currentOwners=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal);
            var currentKeys=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal);
            var currentTargets=new Dictionary<string,Dictionary<string,string?>>(StringComparer.Ordinal);
            var indexPlatforms=inputs.Where(input=>S(input.Source["kind"])=="sdk-api-index").Select(input=>S(input.Source["platform_api_version"])).Where(value=>value.Length!=0).ToHashSet(StringComparer.Ordinal);
            foreach(var input in inputs.Where(input=>S(input.Source["kind"])=="sdk-api-index"))
            {
                var detected=new HashSet<string>(StringComparer.Ordinal);
                foreach(var row in raw.Records(S(input.Source["source_id"])))
                {
                    var extra=row["extras"]!.AsObject();var status=extra["lifecycle_status"]?.GetValue<string>();
                    if(status is not (null or "current-at-target")) continue;
                    var platform=S(extra["target_platform_api"]);if(platform.Length==0) throw Failure("source-index-metadata","API index lacks target platform version");
                    if(input.Source["platform_api_version"] is not null && S(input.Source["platform_api_version"])!=platform) throw Failure("source-index-metadata","API index target platform mismatch");
                    detected.Add(platform);if(!currentOwners.TryGetValue(platform,out var owners)){owners=new(StringComparer.Ordinal);currentOwners.Add(platform,owners);}
                    if(!currentKeys.TryGetValue(platform,out var keys)){keys=new(StringComparer.Ordinal);currentKeys.Add(platform,keys);}
                    var signature=S(row["canonical"]);if(!signature.Contains('#')) owners[S(row["owner"])]=S(extra["jvm_owner"]);
                    var binding=ContentHash.Sha256(Encoding.UTF8.GetBytes("jvm:"+signature));keys[signature]=binding;
                    if(!currentTargets.TryGetValue(platform,out var targets)){targets=new(StringComparer.Ordinal);currentTargets.Add(platform,targets);}indexPlatforms.Add(platform);
                    var target=!signature.Contains('#') ? S(row["owner"]) : S(row["owner"])+"#"+S(row["name"])+(S(row["kind"]) is "method" or "constructor" ? "("+string.Join(',',row["parameters"]!.AsArray().Select(value=>S(value!["type"])))+")" : "");
                    if(targets.TryGetValue(target,out var previous) && previous!=binding) targets[target]=null;else targets.TryAdd(target,binding);
                }
                if(detected.Count>1) throw Failure("source-index-metadata","API index mixes target platform versions");
            }
            var astSchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(catalogRoot,"contracts/PermissionExpression.schema.json")));
            void ValidatePermission(JsonObject value)
            {if(!astSchema.Evaluate(Element(value),new(){OutputFormat=OutputFormat.Flag}).IsValid) throw Failure("invalid-permission-ast","Permission schema rejected source declaration");}
            var reports=new JsonArray();var linkedPermissions=0;long declarationCount=0;
            foreach(var input in inputs)
            {
                var source=input.Source;var sid=S(source["source_id"]);var kind=S(source["kind"]);var count=0;var unsupported=0;var facts=raw.Facts(sid);
                if(facts.Issues.Count!=0)
                {
                    var rawEvidence=input.Facade ? raw.RawEvidence(sid) ?? throw Failure("SOURCE_EXTRACT_RAW_EVIDENCE","Required admitted issue evidence is absent") : Convert.ToBase64String(await File.ReadAllBytesAsync(ArtifactPaths.Resolve(sourceRoot,S(source["local_path"])),cancellationToken));
                    foreach(var issue in facts.Issues) Execute("INSERT INTO issues(source_id,line,category,reason,raw) VALUES($source,NULL,$category,$reason,$raw)",("$source",sid),("$category",issue.Code),("$reason",issue.Reason),("$raw",rawEvidence));
                }
                if(kind=="aidl")
                {
                    var schema=facts.Metadata?["aidl_schema"] as JsonObject ?? throw Failure("source-aidl-metadata-missing","AIDL source producer did not supply its raw schema concern");
                    Execute("INSERT INTO aidl_schemas VALUES($source,$package,$descriptor,$kind,$raw,$status,$metadata,$codec)",("$source",sid),("$package",schema["package"]?.GetValue<string>()),("$descriptor",schema["descriptor"]?.GetValue<string>()),
                        ("$kind",schema["declaration_kind"]?.GetValue<string>()),("$raw",schema["raw_schema"]!.GetValue<string>()),("$status",schema["parse_status"]!.GetValue<string>()),("$metadata",Text(schema["transaction_metadata"])),("$codec",schema["codec_status"]!.GetValue<string>()));
                }
                if(kind=="manifest")
                {
                    foreach(var row in raw.Records(sid).Where(row=>S(row["kind"])=="manifest-permission" && row["name"] is not null))
                    {
                        var definition=new JsonObject{["attributes"]=row["attributes"]?.DeepClone(),["children"]=new JsonArray((row["children"] as JsonArray ?? []).OfType<JsonObject>().Select(child=>(JsonNode)JsonValue.Create(xml.Serialize(child))!).ToArray())};
                        Execute("INSERT OR IGNORE INTO permission_definitions VALUES($name,$source,$level,$attributes)",("$name",S(row["name"])),("$source",sid),("$level",row["protection_level_raw"]?.GetValue<string>()),("$attributes",Text(definition)));
                    }
                }
                else foreach(var row in raw.Records(sid))
                {
                    if(row["canonical"] is null) throw Failure("source-concern-unadmitted","Source concern is retained but has no declaration normalizer for "+kind);
                    var language=kind switch{"sdk-api-index"=>"jvm","java-source"=>"java-source","aidl"=>"aidl",_=>S(source["scope"])=="aosp-pinned-hidl-source-evidence" ? "hidl" : "java"};
                    var signature=language+":"+(S(row["kind"])=="package" ? "package:" : "")+S(row["canonical"]);var bid=ContentHash.Sha256(Encoding.UTF8.GetBytes(signature));var line=row["line"]!.GetValue<int>();
                    var ast=permissions.Permission((JsonArray)row["annotations"]!.DeepClone(),symbols,language=="aidl");ValidatePermission(ast);
                    var extra=new JsonObject{["language"]=language,["source_kind"]=kind,["source_path"]=source["path"]?.DeepClone(),["source_scope"]=source["scope"]?.DeepClone(),["annotations"]=row["all_annotations"]!.DeepClone(),["parameters"]=row["parameters"]!.DeepClone()};
                    foreach(var property in row["extras"]!.AsObject()) extra[property.Key]=property.Value?.DeepClone();
                    string? targetJvm=null;
                    if(language=="java-source")
                    {
                        var platform=S(source["platform_api_version"]);var owners=currentOwners.GetValueOrDefault(platform) ?? new Dictionary<string,string>(StringComparer.Ordinal);
                        var key=sourceJvm.Key(row,owners);
                        if(key is not null && currentKeys.GetValueOrDefault(platform)?.TryGetValue(key,out targetJvm)==true) extra["proven_current_sdk_jvm_identity"]=key;
                        extra["identity_scope"]="Source-spelled type identity; wildcard/type-variable resolution unknown; no JVM equivalence inferred without proof";
                    }
                    else if(language=="jvm") extra["identity_scope"]="Published JVM owner/member key; methods include descriptors; field descriptor/type and Java type kind may be unknown";
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
                    if(targetJvm is not null && S(ast["op"])!="unknown")
                    {
                        Execute("INSERT INTO permission_relations(binding_id,source_id,line,annotation,logic,expression_json,raw_annotation) VALUES($id,$source,$line,'source declaration; exact JVM descriptor link',$logic,$expression,$raw)",("$id",targetJvm),("$source",sid),("$line",line),("$logic",S(ast["op"])),("$expression",Text(ast)),("$raw",Text(row["annotations"])));linkedPermissions++;
                    }
                    count++;declarationCount++;if(S(row["kind"])=="unsupported") unsupported++;
                }
                var report=new JsonObject{["source_id"]=sid,["kind"]=kind,["input_bytes"]=input.Length,["declarations"]=count,["unsupported"]=unsupported,["structural_status"]=facts.StructuralStatus.ToString(),["source_metadata"]=facts.Metadata?.DeepClone()};reports.Add(report);
            }
            var annotationImporter=new CatalogAnnotationImporter(db,transaction,sources,currentTargets,currentKeys,currentOwners,indexPlatforms,canonical,permissions,formals,symbols);
            foreach(var input in inputs.Where(input=>S(input.Source["kind"])=="sdk-annotations"))
            {
                var sid=S(input.Source["source_id"]);var archive=raw.Facts(sid).Metadata?["annotation_archive"] as JsonObject ?? throw Failure("source-annotation-metadata-missing","SDK annotation producer did not supply its archive concern");
                var attached=annotationImporter.Attach(input.Source,archive);var report=reports.OfType<JsonObject>().Single(value=>S(value["source_id"])==sid);
                report["annotations"]=attached.Annotations;report["mapped_annotations"]=attached.Mapped;report["unmatched_annotations"]=attached.Annotations-attached.Mapped;
                report["mapping_status"]=attached.Annotations==attached.Mapped ? "complete" : "partial";report["platform_api_version"]=input.Source["platform_api_version"]?.DeepClone();
                report["xml_documents"]=archive["counts"]?["xml_documents"]?.DeepClone();report["items"]=archive["counts"]?["items"]?.DeepClone();report["status"]=archive["status"]?.DeepClone();
                var platform=S(input.Source["platform_api_version"]);
                if(!indexPlatforms.Contains(platform) && attached.Mapped<attached.Annotations)
                {report["status"]="partial";Execute("INSERT INTO issues(source_id,line,category,reason,raw) VALUES($source,NULL,'annotation-platform',$reason,$raw)",("$source",sid),("$reason","No imported SDK API index for annotation platform "+platform+" and unresolved same-snapshot signature targets remain"),("$raw",S(input.Source["path"])));}
            }
            foreach(var report in reports.OfType<JsonObject>()) Execute("INSERT INTO parse_reports VALUES($source,$doc)",("$source",S(report["source_id"])),("$doc",Text(report)));
            raw.Dispose();
            IEnumerable<CatalogDeclaration> ReadDeclarations(string? entry=null)
            {
                using var command=db.Command("SELECT source_id,line,document_json FROM binding_declarations"+(entry is null ? "" : " WHERE binding_id=$entry")+" ORDER BY source_id,line",transaction,entry is null ? [] : [("$entry",entry)]);
                using var reader=command.ExecuteReader();while(reader.Read()) yield return new(reader.GetString(0),reader.GetInt32(1),JsonNode.Parse(reader.GetString(2))!.AsObject());
            }
            IEnumerable<string> BindingIds()
            {using var command=db.Command("SELECT binding_id FROM bindings ORDER BY binding_id",transaction);using var reader=command.ExecuteReader();while(reader.Read()) yield return reader.GetString(0);}
            var contracts=new CatalogContracts(canonical);var projection=new CatalogProjection(sources,()=>ReadDeclarations(),formals,headers,contracts,canonical);
            foreach(var group in BindingIds().Select(key=>new KeyValuePair<string,int>(key,0)))
            {
                var rows=ReadDeclarations(group.Key).Select(row=>row with{Document=projection.Project(row.Document,row.SourceId)}).OrderBy(row=>SourceRank(sources[row.SourceId])).ThenBy(row=>S(sources[row.SourceId]["path"]),Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray();
                observeImport?.Invoke(new("entry-batch",rawPath,EntryId:group.Key,Count:rows.Length));
                var document=(JsonObject)rows[0].Document.DeepClone();document["provenance"]=new JsonArray(rows.SelectMany(row=>row.Document["provenance"]!.AsArray()).Select(value=>value!.DeepClone()).ToArray());
                var permissionRows=new List<(JsonObject Expression,string Source,long Line,string? Annotation)>();
                using(var query=db.Command("SELECT expression_json,source_id,line,annotation FROM permission_relations WHERE binding_id=$id ORDER BY id",transaction,("$id",group.Key)))
                using(var reader=query.ExecuteReader()) while(reader.Read()) permissionRows.Add((JsonNode.Parse(reader.GetString(0))!.AsObject(),reader.GetString(1),reader.GetInt64(2),reader.IsDBNull(3) ? null : reader.GetString(3)));
                static bool Absent(JsonObject expression)=>S(expression["op"])=="unknown" && S(expression["reason"]).StartsWith("No declared recognized",StringComparison.Ordinal);
                var known=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
                foreach(var row in permissionRows) if(!Absent(row.Expression)) known[Text(PermissionSemantic(row.Expression))]=row.Expression;
                if(known.Count==1) document["permission"]=known.Values.Single().DeepClone();
                else if(known.Count>1) document["permission"]=new JsonObject{["op"]="unknown",["reason"]="Source-dependent or conflicting permission metadata; select exact declaration evidence"};
                if(known.Count>1 || known.Count!=0 && permissionRows.Any(row=>Absent(row.Expression)))
                {
                    var variants=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
                    foreach(var row in permissionRows)
                    {
                        var key=Text(PermissionSemantic(row.Expression));if(!variants.TryGetValue(key,out var variant)){variant=(JsonObject)row.Expression.DeepClone();variant["x-occurrences"]=new JsonArray();variants.Add(key,variant);}
                        variant["x-occurrences"]!.AsArray().Add(new JsonObject{["source_id"]=row.Source,["line"]=row.Line,["annotation"]=row.Annotation});if(Absent(row.Expression)) variant["x-absence-of-annotation"]=true;
                    }
                    document["extensions"]!["permission_variants"]=new JsonArray(variants.Values.Select(value=>(JsonNode)value).ToArray());
                }
                document["extensions"]!["occurrence_count"]=rows.Length;
                var entity=projection.Entity(group.Key,rows);document["extensions"]!["device_resolution_variants"]=entity["variants"]!.DeepClone();document["extensions"]!["device_resolution_contract_version"]="2.1.0";
                Execute("INSERT INTO registry_entries VALUES($registry,$id,$doc,$hash)",("$registry",registry),("$id",group.Key),("$doc",Text(document)),("$hash",Hash(document)));
                Execute("INSERT INTO registry_inventory VALUES($registry,$id,$doc,$hash)",("$registry",registry),("$id",group.Key),("$doc",Text(entity)),("$hash",Hash(entity)));
            }
            var entrySchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(catalogRoot,"contracts/Entry.schema.json")));var resolver=new DeviceResolver(canonical,Path.Combine(catalogRoot,"contracts/DeviceResolution.schema.json"));
            IEnumerable<JsonObject> ValidationDocuments(string table)
            {using var command=db.Command("SELECT document_json FROM "+table+" ORDER BY entry_id",transaction);using var reader=command.ExecuteReader();while(reader.Read()) yield return JsonNode.Parse(reader.GetString(0))!.AsObject();}
            foreach(var entry in ValidationDocuments("registry_entries")) if(!entrySchema.Evaluate(Element(entry),new(){OutputFormat=OutputFormat.Flag}).IsValid) throw Failure("invalid-entry","Entry schema rejected "+S(entry["entry_id"]));
            foreach(var item in ValidationDocuments("registry_inventory")) resolver.ValidateInventoryEntity(Element(item));
            using(var integrity=db.Command("PRAGMA integrity_check",transaction)) if((string)integrity.ExecuteScalar()! !="ok") throw Failure("integrity","SQLite integrity check failed");
            using(var foreign=db.Command("PRAGMA foreign_key_check",transaction)) using(var reader=foreign.ExecuteReader()) if(reader.Read()) throw Failure("foreign-key","SQLite foreign-key check failed");
            var entryCount=Count("registry_entries");if(Count("bindings")!=entryCount || Count("occurrences")!=declarationCount || Count("registry_inventory")!=entryCount) throw Failure("reconciliation","Catalog count reconciliation failed");
            var counts=new JsonObject();foreach(var table in new[]{"sources","bindings","occurrences","binding_declarations","permission_definitions","permission_symbols","permission_relations","aidl_schemas","annotation_evidence","issues"}) counts[table]=Count(table);counts["effective_entries"]=entryCount;
            var validation=new JsonObject{["integrity_check"]="ok",["foreign_key_check"]=new JsonArray(),["json_schema_failures"]=new JsonArray(),["counts"]=counts,["source_reports"]=reports,["proven_jvm_source_permission_links"]=linkedPermissions,["complete"]=false,["native_producer"]=producer.DeepClone()};
            foreach(var source in sources.Keys) Execute("INSERT INTO registry_sources VALUES($registry,$source)",("$registry",registry),("$source",source));
            Execute("UPDATE registries SET state='validated',validation_json=$doc WHERE registry_id=$id",("$doc",Text(validation)),("$id",registry));Execute("UPDATE registries SET state='published' WHERE registry_id=$id",("$id",registry));Execute("INSERT INTO active_registry(singleton,registry_id) VALUES(1,$id)",("$id",registry));
            foreach(var pair in new Dictionary<string,string>{{"schema_version","1.2.0"},{"entry_contract_version","1.1.0"},{"device_resolution_contract_version","2.1.0"},{"source_set_hash",sourceSet},{"coverage",S(metadata["coverage"])},{"source_inventory_sha256",Sha(inventoryBytes)},{"authority","Metadata only; no OS capability or MCP client grant"},{"native_producer_id","csharp-nuke-catalog-v1"},{"native_producer_sha256",producerFingerprint}})
                Execute("INSERT INTO catalog_meta VALUES($key,$value)",("$key",pair.Key),("$value",pair.Value));
            transaction.Commit();
            async Task<StoredArtifact> WriteStream(string name,Func<Stream,CancellationToken,Task> produce,string media="application/json")
            {
                try{var artifact=await store.WriteStreamAsync(name,media,produce,cancellationToken);artifacts.Add(artifact);return artifact;}
                catch(IOException error){throw new CatalogBuildException("export-failure","Catalog export failed; partial output and stage retained",error);}
            }
            Task<StoredArtifact> Write(string name,ReadOnlyMemory<byte> bytes,string media="application/json")=>WriteStream(name,async (stream,token)=>
            {for(var offset=0;offset<bytes.Length;offset+=65536) await stream.WriteAsync(bytes.Slice(offset,Math.Min(65536,bytes.Length-offset)),token);},media);
            Task<StoredArtifact> WriteLines(string name,Func<IEnumerable<string>> lines)=>WriteStream(name,async (stream,token)=>
            {await using var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,true);foreach(var line in lines()){await writer.WriteAsync(line.AsMemory(),token);await writer.WriteAsync("\n".AsMemory(),token);}await writer.FlushAsync(token);},"application/x-ndjson");
            IEnumerable<string> TableLines(string table)
            {
                using var command=db.Command("SELECT * FROM "+table+" ORDER BY "+(table=="issues" ? "id" : "1,2"));using var reader=command.ExecuteReader();
                while(reader.Read()){var value=new JsonObject();for(var index=0;index<reader.FieldCount;index++) value[reader.GetName(index)]=reader.IsDBNull(index) ? null : JsonSerializer.SerializeToNode(reader.GetValue(index));yield return Text(value);}
            }
            IEnumerable<string> DocumentLines(string table)
            {using var command=db.Command("SELECT document_json FROM "+table+" ORDER BY entry_id");using var reader=command.ExecuteReader();while(reader.Read()) yield return reader.GetString(0);}
            IEnumerable<JsonObject> EntryDocuments()=>DocumentLines("registry_entries").Select(line=>JsonNode.Parse(line)!.AsObject());
            foreach(var table in new[]{"sources","source_details","bindings","occurrences","binding_declarations","permission_definitions","permission_symbols","permission_relations","aidl_schemas","annotation_evidence","issues","parse_reports"})
            {
                await WriteLines(table+".jsonl",()=>TableLines(table));
            }
            await WriteLines("entries.jsonl",()=>DocumentLines("registry_entries"));var inventoryArtifact=await WriteLines("device_inventory.jsonl",()=>DocumentLines("registry_inventory"));
            await Write("device_inventory_manifest.json",canonical.Serialize(Element(new JsonObject{["contract"]="urn:android-binding-catalog:2.1.0:DeviceResolution",["registry_id"]=registry,["entries"]=entryCount,["inventory_sha256"]=inventoryArtifact.Sha256,["semantic_scope"]="Full versioned metadata inventory; no device observation or permission grant"})));
            async Task WriteArray(Stream stream,CancellationToken token,string prefix,IEnumerable<string> items,string suffix)
            {await using var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,true);await writer.WriteAsync(prefix.AsMemory(),token);var first=true;foreach(var item in items){if(!first) await writer.WriteAsync(",".AsMemory(),token);await writer.WriteAsync(item.AsMemory(),token);first=false;}await writer.WriteAsync(suffix.AsMemory(),token);await writer.FlushAsync(token);}
            await WriteStream("catalog.json",(stream,token)=>WriteArray(stream,token,"{\"schema_version\":\"1.2.0\",\"complete\":false,\"entries\":[",DocumentLines("registry_entries"),"]}"));
            await WriteStream("dispatch_manifest.json",(stream,token)=>WriteArray(stream,token,"{\"format_version\":\"1.1.0\",\"contract_format_version\":3,\"aliases\":[",new CatalogAliases(canonical,contracts).EnumerateOrdered(EntryDocuments()).Select(Text),"]}"));
            await Write("validation.json",canonical.Serialize(Element(validation)));
            // Export cursors finish before closing the actual file-backed stage.
            // No backup database, move or deletion substitutes for this source.
            db.Dispose();
            foreach(var input in inputs.Where(input=>!input.CacheHit))
            {using var source=File.OpenRead(ArtifactPaths.Resolve(sourceRoot,S(input.Source["local_path"])));if(ContentHash.Sha256(source)!=S(input.Source["sha256"])) throw Failure("input-changed","Source changed before final publication marker");}
            if(bridge is not null ? !bridge.LegacyManifest.Bytes.Span.SequenceEqual(inventoryBytes) : !(await File.ReadAllBytesAsync(inventoryFull!,cancellationToken)).AsSpan().SequenceEqual(inventoryBytes)) throw Failure("input-changed","Inventory changed before final publication marker");
            string FileHash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
            var stageHash=FileHash(stage);var final=await WriteStream("catalog.sqlite",async (stream,token)=>
            {await using var source=File.OpenRead(stage);await source.CopyToAsync(stream,65536,token);},"application/vnd.sqlite3");
            if(final.Sha256!=stageHash || FileHash(ArtifactPaths.Resolve(output,"catalog.sqlite"))!=stageHash) throw Failure("final-copy-mismatch","Final database copy differs from retained staging source");
            return new(registry,producerFingerprint,artifacts.ToArray(),validation,stage,rawPath);
        }
        catch(Exception error) when(error is not OperationCanceledException)
        {
            var code=error switch{CatalogBuildException failure=>failure.Code,SourceExtractException source=>source.Code,_=>"catalog-build-failed"};
            var receipt=new JsonObject{["published"]=false,["code"]=code,["error_type"]=error.GetType().Name,["error"]=error.Message,["native_producer"]=producer.DeepClone()};
            await store.WriteAsync(new("validation-failure.json",canonical.Serialize(Element(receipt)),"application/json"),cancellationToken);
            if(error is CatalogBuildException) throw;throw new CatalogBuildException(code,error.Message,error);
        }
    }
    private static int SourceRank(JsonObject source)=>S(source["kind"])=="sdk-signature" ? 0 : S(source["kind"])=="source-signature" ? Path.GetFileName(S(source["path"]))=="current.txt" ? 1 : 2 : 3;
    private static JsonNode? PermissionSemantic(JsonNode? value)
    {
        if(value is JsonObject obj){var result=new JsonObject();foreach(var pair in obj) if(!pair.Key.StartsWith("x-",StringComparison.Ordinal)) result[pair.Key]=PermissionSemantic(pair.Value);return result;}
        if(value is JsonArray array) return new JsonArray(array.Select(PermissionSemantic).ToArray());return value?.DeepClone();
    }
}
