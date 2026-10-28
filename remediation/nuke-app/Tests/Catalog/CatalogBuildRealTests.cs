using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Json.Schema;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogBuildInvocation(CatalogBuild Service,string InventoryPath,string OutputDirectory,string StageDirectory);
public sealed record CatalogIntakeFixture(string Root,string InventoryPath,string OutputDirectory,string StageDirectory,JsonObject Inventory,IDictionary<string,byte[]> Originals);
public sealed class NativeCatalogBuildProvider:ICatalogBuildCaseProvider
{
    // Keep generated source, staging and output evidence for inspection. These
    // temporary roots are owned by this provider and never hold prior inputs.
    private readonly string root=Path.Combine(Path.GetTempPath(),"native-catalog-build-"+Guid.NewGuid().ToString("N"));
    internal string EvidenceRoot=>root;
    internal Func<string,IStreamingArtifactStore>? StoreFactory {get;init;}
    internal Func<string,CatalogDatabase>? StageFactory {get;init;}
    internal Func<CatalogBuildInvocation,Task<CatalogBuildException?>>? Execution {get;init;}
    internal Action<List<(string Kind,string Path,string Text)>>? SourceValues {get;init;}
    internal Action<JsonObject,string,IDictionary<string,byte[]>>? InventorySetup {get;init;}
    internal ISourcesExtractionDispatcher? SourceDispatcher {get;init;}
    internal Action<CatalogImportObservation>? ImportObserver {get;init;}
    internal Func<CatalogIntakeFixture,Task<CatalogSourceIntake>>? IntakeFactory {get;init;}
    private static string Root()=>NativeSchemaProvider.Root();
    private static string Hash(byte[] bytes)=>ContentHash.Sha256(bytes);
    public CatalogBuildReply Build(CatalogBuildCall call)
    {
        Directory.CreateDirectory(root);var output=Path.Combine(root,"out");var stage=Path.Combine(root,"private-stage");var values=new List<(string Kind,string Path,string Text)>
        {("sdk-signature","api.txt",CatalogBuildCases.Signature),("manifest","AndroidManifest.xml",CatalogBuildCases.Manifest),("aidl","IFoo.aidl",CatalogBuildCases.Aidl)};
        if(call.Variant=="invalid-permission-ast") values[0]=(values[0].Kind,values[0].Path,values[0].Text.Replace("@RequiresPermission(android.Manifest.permission.CAMERA)","@RequiresPermission(allOf={})",StringComparison.Ordinal));
        if(call.Variant is "duplicate-source" or "verified-unused-cache-duplicate-source") values.Add(("sdk-signature","second.txt",CatalogBuildCases.Signature));
        SourceValues?.Invoke(values);
        var sources=new JsonArray();var original=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var (kind,path,text) in values)
        {
            var bytes=Encoding.UTF8.GetBytes(text);File.WriteAllBytes(Path.Combine(root,path),bytes);original.Add(path,bytes);
            sources.Add(new JsonObject{["kind"]=kind,["path"]=path,["local_path"]=path,["status"]="downloaded",["sha256"]=Hash(bytes),["git_blob"]=ContentHash.GitBlobSha1Legacy(bytes),["size_bytes"]=bytes.Length,
                ["url"]="https://android.googlesource.com/mock/"+path,["revision"]=new string('1',40),["retrieved_at"]="2026-10-26T00:00:00Z",["license"]="fixture",["scope"]="source",["repository"]="fixture",["api_level"]=37,["platform_api_version"]="37.0",["channel"]="stable"});
        }
        if(call.Variant=="source-sha-mismatch") sources[0]!["sha256"]=new string('0',64);
        if(call.Variant=="git-blob-mismatch") sources[0]!["git_blob"]=new string('0',40);
        if(call.Variant=="cache-sha-mismatch")
        {
            var cached=Encoding.UTF8.GetBytes("retained cache bytes");File.WriteAllBytes(Path.Combine(root,"cache.jsonl.gz"),cached);original.Add("cache.jsonl.gz",cached);
            sources[0]!["metadata_cache"]=new JsonObject{["path"]="cache.jsonl.gz",["sha256"]=new string('0',64),["parser_sha256"]=new string('2',64),["records"]=8};
        }
        if(call.Variant is "verified-unused-cache" or "verified-unused-cache-duplicate-source")
        {
            var cached=Encoding.UTF8.GetBytes(CatalogBuildCacheCases.CacheText);File.WriteAllBytes(Path.Combine(root,"cache.jsonl.gz"),cached);original.Add("cache.jsonl.gz",cached);
            sources[0]!["metadata_cache"]=new JsonObject{["path"]="cache.jsonl.gz",["sha256"]=Hash(cached),["parser_sha256"]=CatalogBuildCacheCases.Producer,["records"]=999};
            sources[0]!["java_file_context"]=CatalogBuildCacheCases.HistoricalContext();
        }
        var inventory=new JsonObject{["catalog"]=new JsonObject{["registry_version"]=1,["android_version"]="fixture",["api_level"]=37,["coverage"]="fixture partial"},["sources"]=sources};
        InventorySetup?.Invoke(inventory,root,original);
        var inventoryPath=Path.Combine(root,"inventory.json");var inventoryBytes=JsonSerializer.SerializeToUtf8Bytes(inventory);File.WriteAllBytes(inventoryPath,inventoryBytes);original.Add("inventory.json",inventoryBytes);
        if(call.Variant=="nonempty-output"){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"prior.txt"),"immutable previous generation");}
        var canonical=new CanonicalJson();var facts=new SourceTypeFacts(new JvmSourceLinker(),canonical);
        var dispatcher=SourceDispatcher ?? new SourcesExtractionDispatcher(new ISourceExtractor[]{new SignatureSourceExtractor(value=>ContentHash.Sha256(value.Span)),new ManifestSourceExtractor(),new AidlSourceExtractor(value=>ContentHash.Sha256(value.Span))},SourceExtractionProfile.CurrentCandidate);
        var intake=IntakeFactory?.Invoke(new(root,inventoryPath,output,stage,inventory,original)).GetAwaiter().GetResult();
        var service=new CatalogBuild(Root(),dispatcher,new PermissionExpressionSourceFacts(),facts,facts,canonical,outputStores:StoreFactory,stagingDatabases:StageFactory,observeImport:ImportObserver,sourceIntake:intake);
        CatalogBuildException? failure=null;
        try
        {
            if(Execution is not null) failure=Execution(new(service,inventoryPath,output,stage)).GetAwaiter().GetResult();
            else service.BuildAsync(inventoryPath,output,stage).GetAwaiter().GetResult();
        }
        catch(CatalogBuildException error){failure=error;}
        var preserved=original.All(pair=>File.ReadAllBytes(Path.Combine(root,pair.Key)).AsSpan().SequenceEqual(pair.Value));
        var final=File.Exists(Path.Combine(output,"catalog.sqlite"));var failurePath=Path.Combine(output,"validation-failure.json");
        if(failure is not null)
        {
            var unpublished=File.Exists(failurePath) && JsonNode.Parse(File.ReadAllText(failurePath))!["published"]!.GetValue<bool>()==false;
            var native=CatalogBuild.ProducerId=="csharp-nuke-catalog-v1" && service.ProducerFingerprint==Hash(File.ReadAllBytes(typeof(CatalogBuild).Assembly.Location));
            if(File.Exists(failurePath)) native &= JsonNode.Parse(File.ReadAllText(failurePath))!["native_producer"]!["id"]!.GetValue<string>()==CatalogBuild.ProducerId;
            var untouched=call.Variant=="nonempty-output" && Directory.EnumerateFileSystemEntries(output).Count()==1 && File.ReadAllText(Path.Combine(output,"prior.txt"))=="immutable previous generation";
            return new(failure.Code,final,unpublished,0,0,0,0,0,0,0,0,null,null,null,null,null,null,null,null,null,native,preserved,untouched);
        }
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(output,"catalog.sqlite"),ForeignKeys=true,Pooling=false}.ToString());connection.Open();
        long Scalar(string sql){using var command=connection.CreateCommand();command.CommandText=sql;return (long)command.ExecuteScalar()!;}
        string[] Documents(string sql){using var command=connection.CreateCommand();command.CommandText=sql;using var reader=command.ExecuteReader();var rows=new List<string>();while(reader.Read()) rows.Add(reader.GetString(0));return rows.ToArray();}
        var entries=File.ReadAllLines(Path.Combine(output,"entries.jsonl")).Where(value=>value.Length!=0).Select(value=>JsonNode.Parse(value)!.AsObject()).ToArray();
        var entities=File.ReadAllLines(Path.Combine(output,"device_inventory.jsonl")).Where(value=>value.Length!=0).Select(value=>JsonNode.Parse(value)!.AsObject()).ToArray();
        var entrySchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(Root(),"contracts/Entry.schema.json")));var astSchema=JsonSchema.FromText(File.ReadAllText(Path.Combine(Root(),"contracts/PermissionExpression.schema.json")));
        bool Schema(JsonSchema schema,JsonNode node)=>schema.Evaluate(JsonSerializer.SerializeToElement(node),new(){OutputFormat=OutputFormat.Flag}).IsValid;
        var entitySchema=JsonNode.Parse(File.ReadAllText(Path.Combine(Root(),"contracts/DeviceResolution.schema.json")))!.AsObject();entitySchema["$ref"]="#/$defs/InventoryEntity";var inventorySchema=JsonSchema.FromText(entitySchema.ToJsonString());
        var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"device_inventory_manifest.json")))!;
        var exported=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"catalog.json")))!["entries"]!.AsArray();
        var exact=entries.Select(entry=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(entry)))).ToArray();
        var agree=exact.SequenceEqual(Documents("SELECT document_json FROM effective_entries ORDER BY entry_id"),StringComparer.Ordinal) &&
            exact.SequenceEqual(exported.Select(entry=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(entry)))),StringComparer.Ordinal) &&
            entities.Select(entity=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(entity)))).SequenceEqual(Documents("SELECT document_json FROM registry_inventory ORDER BY entry_id"),StringComparer.Ordinal);
        bool immutable;using(var transaction=connection.BeginTransaction())
        {using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="UPDATE bindings SET name='changed' WHERE binding_id=(SELECT binding_id FROM bindings LIMIT 1)";immutable=Record.Exception(()=>command.ExecuteNonQuery()) is SqliteException;transaction.Rollback();}
        var aliases=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"dispatch_manifest.json")))!["aliases"]!.AsArray();
        var deny=aliases.Count!=0 && aliases.All(alias=>alias!["default_grant"]!.GetValue<string>()=="deny" && alias["dispatch_state"]!.GetValue<string>()=="unavailable");
        using var integrity=connection.CreateCommand();integrity.CommandText="PRAGMA integrity_check";var valid=(string)integrity.ExecuteScalar()! =="ok";
        using var fk=connection.CreateCommand();fk.CommandText="PRAGMA foreign_key_check";using var fkRows=fk.ExecuteReader();var foreign=!fkRows.Read();fkRows.Close();
        var validation=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation.json")))!;
        return new("ok",final,false,(int)Scalar("SELECT count(*) FROM sources"),(int)Scalar("SELECT count(*) FROM bindings"),(int)Scalar("SELECT count(*) FROM occurrences"),(int)Scalar("SELECT count(*) FROM binding_declarations"),
            (int)Scalar("SELECT count(*) FROM permission_definitions"),entries.Length,entities.Length,(int)Scalar("SELECT count(*) FROM occurrences o JOIN bindings b USING(binding_id) WHERE b.signature='java:a.A#go(int)'"),valid,foreign,
            entries.All(entry=>Schema(entrySchema,entry)),Documents("SELECT expression_json FROM permission_relations").All(value=>Schema(astSchema,JsonNode.Parse(value)!)),entities.All(entity=>Schema(inventorySchema,entity)),
            Hash(File.ReadAllBytes(Path.Combine(output,"device_inventory.jsonl")))==manifest["inventory_sha256"]!.GetValue<string>(),agree,immutable,deny,
            validation["native_producer"]!["id"]!.GetValue<string>()=="csharp-nuke-catalog-v1" && validation["native_producer"]!["historical_python_identity"]!.GetValue<bool>()==false,preserved,false,
            entries.Any(entry=>entry["permission"]?["name"]?.GetValue<string>()=="android.permission.CAMERA"));
    }
    public void Dispose() { }
}
[Trait("Gate","Real")]
public sealed class CatalogBuildRealTests
{
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public void NativeBuildUsesSameInputsAndAssertions(CatalogBuildCase test)
    {using var provider=new NativeCatalogBuildProvider();CatalogBuildCases.AssertCase(test,provider);}
}
