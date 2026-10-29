using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Json.Schema;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Read-only permission export. SQLite, JSON, hashing, path admission
/// and immutable streaming writes remain owned by their existing authorities.</summary>
public sealed class PermissionExportOperation(string contractRoot,IProfiledCanonicalJson canonical,IContentHash hashes,
    Func<string,bool,CatalogDatabase>? openCatalog=null,Func<string,ICreateNewStreamingArtifactStore>? createStore=null):IPermissionExportOperation
{
    // Presentation order from the original export, not a second AST validator.
    private static readonly string[] SampleOrder=["permission","all","any","access","conditional","manual","explicit-none","unknown"];
    public async Task<PermissionExportResult> RunAsync(PermissionExportRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string source,output,databasePath;
        try
        {
            source=Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.SourceCatalogRoot));
            output=Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.OutputRoot));
            databasePath=ArtifactPaths.Resolve(source,"catalog.sqlite");
            _=ArtifactPaths.Resolve(output,"permissions.jsonl");
            var relative=Path.GetRelativePath(source,output);
            if(relative=="."||!Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                throw new PermissionExportException("PERMISSION_EXPORT_SCOPE","Output must be outside the sealed release tree");
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())
                throw new PermissionExportException("PERMISSION_EXPORT_SCOPE","Output must be empty; existing files are retained");
        }
        catch(ArgumentException error){throw new PermissionExportException("PERMISSION_EXPORT_SCOPE",error.Message);}

        JsonSchema summarySchema;string version;
        var evaluation=new EvaluationOptions{OutputFormat=OutputFormat.Flag,RequireFormatValidation=true};
        evaluation.SchemaRegistry.Fetch=_=>throw new InvalidDataException("Permission export does not download schema dependencies");
        try
        {
            var bytes=await File.ReadAllBytesAsync(ArtifactPaths.Resolve(contractRoot,"contracts/PermissionSummary.schema.json"),cancellationToken);
            _=canonical.Serialize(bytes);
            using var document=JsonDocument.Parse(bytes);
            if(!CatalogSourceMetadata.IsValidSchema(document.RootElement))throw new InvalidDataException("Invalid summary schema");
            version=document.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString()
                ??throw new InvalidDataException("Summary schema version is not a string");
            summarySchema=JsonSchema.FromText(Encoding.UTF8.GetString(bytes));
        }
        catch(Exception error)when(error is not OperationCanceledException)
        {throw new PermissionExportException("PERMISSION_EXPORT_SCHEMA",error.Message);}
        if(!File.Exists(databasePath))throw new PermissionExportException("PERMISSION_EXPORT_SOURCE","Selected catalog.sqlite does not exist; no database was created");
        CatalogDatabase database;
        try{database=(openCatalog??CatalogDatabase.OpenExisting)(databasePath,true);}
        catch(Exception error)when(error is not OperationCanceledException)
        {throw new PermissionExportException("PERMISSION_EXPORT_SOURCE",error.Message);}
        using(database)
        {
            var outputStore=createStore?.Invoke(output)??new FileArtifactStore(output,value=>hashes.Sha256(value.Span));
            var artifacts=new List<StoredArtifact>();var names=new List<string>();
            using(var command=database.Command("SELECT name FROM permission_definitions UNION SELECT value FROM permission_symbols ORDER BY 1"))
            using(var rows=command.ExecuteReader())while(rows.Read())names.Add(rows.GetString(0));
            JsonNode? Parse(string raw)
            {
                try{var bytes=Encoding.UTF8.GetBytes(raw);_=canonical.Serialize(bytes);return JsonNode.Parse(bytes);}
                catch(Exception error)when(error is JsonException or CanonicalJsonException)
                {throw new PermissionExportException("PERMISSION_EXPORT_JSON",error.Message);}
            }
            byte[] Json(JsonNode value)=>canonical.Serialize(JsonSerializer.SerializeToElement(value),JsonSerializationProfile.LegacyAsciiIdentity);
            try
            {
                artifacts.Add(await outputStore.WriteNewStreamAsync("permissions.jsonl","application/x-ndjson",async(stream,token)=>
                {
                    foreach(var name in names)
                    {
                        token.ThrowIfCancellationRequested();var definitions=new JsonArray();var symbols=new JsonArray();
                        using(var query=database.Command("SELECT source_id,protection_level,attributes_json FROM permission_definitions WHERE name=$name",null,("$name",name)))
                        using(var rows=query.ExecuteReader())while(rows.Read())definitions.Add(new JsonObject{["source_id"]=rows.GetString(0),["protection_level"]=rows.IsDBNull(1)?null:rows.GetString(1),["metadata"]=Parse(rows.GetString(2))});
                        using(var query=database.Command("SELECT symbol,source_id,line FROM permission_symbols WHERE value=$name ORDER BY symbol,source_id,line",null,("$name",name)))
                        using(var rows=query.ExecuteReader())while(rows.Read())symbols.Add(new JsonObject{["symbol"]=rows.GetString(0),["source_id"]=rows.GetString(1),["line"]=rows.GetInt64(2)});
                        var doc=new JsonObject{["schema_version"]=version,["permission_id"]=hashes.Sha256(Encoding.UTF8.GetBytes("permission:"+name)),["name"]=name,
                            ["manifest_definitions"]=definitions,["symbols"]=symbols,["metadata_status"]=definitions.Count>0?"manifest-evidence-present":"constant-only; protection-level-and-attributes-unknown",
                            ["os_authority"]="unknown",["mcp_client_grants"]="not-inferred"};
                        if(!summarySchema.Evaluate(JsonSerializer.SerializeToElement(doc),evaluation).IsValid)
                            throw new PermissionExportException("PERMISSION_EXPORT_SCHEMA","Permission summary failed its canonical schema");
                        await stream.WriteAsync(Json(doc),token);await stream.WriteAsync("\n"u8.ToArray(),token);
                    }
                },cancellationToken));
                var samples=new JsonArray();
                foreach(var logic in SampleOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var command=database.Command("SELECT binding_id,source_id,line,expression_json,raw_annotation FROM permission_relations WHERE logic=$logic LIMIT 3",null,("$logic",logic));
                    using var rows=command.ExecuteReader();
                    while(rows.Read())samples.Add(new JsonObject{["logic"]=logic,["binding_id"]=rows.GetString(0),["source_id"]=rows.GetString(1),["line"]=rows.GetInt64(2),
                        ["expression"]=Parse(rows.GetString(3)),["raw_annotation"]=rows.IsDBNull(4)||rows.GetString(4).Length==0?null:Parse(rows.GetString(4))});
                }
                artifacts.Add(await outputStore.WriteNewAsync(new("permission-expression-samples.json",Json(samples),"application/json"),cancellationToken));
                using var count=database.Command("SELECT count(*) FROM permission_definitions");var definitionCount=(long)count.ExecuteScalar()!;
                var coverage=new JsonObject{["permission_names"]=(long)names.Count,["manifest_definition_records"]=definitionCount,["source_catalog"]=source,["complete"]=false};
                artifacts.Add(await outputStore.WriteNewAsync(new("permission-coverage.json",Json(coverage),"application/json"),cancellationToken));
                return new(new(names.Count,definitionCount,source,false),artifacts.AsReadOnly());
            }
            catch(IOException error){throw new PermissionExportException("PERMISSION_EXPORT_OUTPUT",error.Message);}
        }
    }
}
