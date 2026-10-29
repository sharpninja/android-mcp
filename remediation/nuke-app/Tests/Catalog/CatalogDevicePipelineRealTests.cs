using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogDevicePipelineProvider:ICatalogDevicePipelineCaseProvider
{
    internal Action<JsonArray>? SourceIndexExtensionsObserved {get;init;}
    private static string S(JsonNode? value)=>value!.GetValue<string>();
    private static string B(bool value)=>value?"true":"false";
    private static JsonElement Element(JsonNode value)=>JsonSerializer.SerializeToElement(value);
    private static string FileHash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
    private static string Id(string signature)=>ContentHash.Sha256(Encoding.UTF8.GetBytes(signature));
    public Task<CatalogDevicePipelineReply> RunAsync(CatalogDevicePipelineCall call)=>Task.FromResult(Run(call));
    private CatalogDevicePipelineReply Run(CatalogDevicePipelineCall call)
    {
        var facts=new Dictionary<string,string>(StringComparer.Ordinal);
        if(call.Variant=="source-index")
        {
            var parsed=new SdkApiIndexExtractor().Extract(new("api.xml",Encoding.UTF8.GetBytes(CatalogDevicePipelineCases.HistoryXml),new("fixture",new string('1',40),"fixture-recipe","37.2")));
            var records=parsed.Records.ToDictionary(row=>S(row["canonical"]),StringComparer.Ordinal);
            SourceIndexExtensionsObserved?.Invoke((JsonArray)records["a/Backport"]["extras"]!["sdk_extensions"]!.DeepClone());
            facts["retains.a/Old"]=B(records.ContainsKey("a/Old"));facts["retains.a/Future#f()V"]=B(records.ContainsKey("a/Future#f()V"));
            facts["future.member.container.since"]=S(records["a/Future#f()V"]["extras"]!["container_api_lifecycle"]!["since"]);
            facts["backport.extensions"]=string.Join(',',records["a/Backport"]["extras"]!["sdk_extensions"]!.AsArray().Select(value=>S(value!["sdk_id"])+":"+S(value["minimum_version"])));
            return new("ok",facts);
        }
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        using var producer=new NativeCatalogBuildProvider
        {
            SourceDispatcher=runtime.Dispatcher,
            SourceValues=values=>
            {
                if(call.Variant=="history-build")values.Add(("sdk-api-index","api.xml",CatalogDevicePipelineCases.HistoryXml));
                if(call.Variant=="structured-contract")values[2]=("aidl","IFoo.aidl",CatalogDevicePipelineCases.StructuredAidl);
            },
            InventorySetup=(inventory,root,originals)=>
            {
                if(call.Variant!="history-build")return;
                var source=inventory["sources"]!.AsArray().OfType<JsonObject>().Single(row=>S(row["kind"])=="sdk-api-index");source["platform_api_version"]="37.2";
                // Explicit verified-unused fixture evidence, never an old
                // producer baseline. The unchanged raw XML is re-extracted by
                // the one native SDK provider, as in prior approved imports.
                var bytes=Encoding.UTF8.GetBytes("synthetic unused SDK fixture cache; raw XML remains authoritative");
                File.WriteAllBytes(Path.Combine(root,"index.gz"),bytes);originals.Add("index.gz",bytes);
                source["metadata_cache"]=new JsonObject{["path"]="index.gz",["sha256"]=ContentHash.Sha256(bytes),["records"]=999};
            }
        };
        var built=producer.Build(new("device-pipeline"));Assert.Equal("ok",built.Outcome);
        var output=Path.Combine(producer.EvidenceRoot,"out");var originalDatabase=Path.Combine(output,"catalog.sqlite");var originalHash=FileHash(originalDatabase);
        var databasePath=originalDatabase;
        if(call.Variant=="missing-projection")
        {
            databasePath=Path.Combine(producer.EvidenceRoot,"missing-projection.sqlite");File.Copy(originalDatabase,databasePath);
            Assert.Equal(originalHash,FileHash(databasePath));
        }
        using var database=CatalogDatabase.OpenExisting(databasePath,call.Variant!="missing-projection");
        var canonical=new CanonicalJson();var rootPath=NativeSchemaProvider.Root();
        var resolver=new DeviceResolver(canonical,Path.Combine(rootPath,"contracts/DeviceView.schema.json"));
        var contracts=new CatalogContracts(canonical);
        var inventory=new CatalogInventory(database,contracts,resolver,canonical,Path.Combine(rootPath,"contracts/Entry.schema.json"));
        string Scalar(string sql){using var query=database.Command(sql);return (string)query.ExecuteScalar()!;}
        long Count(string sql){using var query=database.Command(sql);return (long)query.ExecuteScalar()!;}
        JsonObject[] Documents()
        {
            using var query=database.Command("SELECT document_json FROM effective_entries ORDER BY entry_id");using var rows=query.ExecuteReader();var docs=new List<JsonObject>();
            while(rows.Read())docs.Add(JsonNode.Parse(rows.GetString(0))!.AsObject());return docs.ToArray();
        }
        var registry=Scalar("SELECT registry_id FROM active_registry");
        if(call.Variant=="missing-projection")
        {
            using(var command=database.Command("DROP TRIGGER registry_inventory_no_delete"))command.ExecuteNonQuery();
            using(var command=database.Command("DELETE FROM registry_inventory WHERE entry_id=(SELECT min(entry_id) FROM registry_inventory)"))command.ExecuteNonQuery();
            var before=Count("SELECT count(*) FROM registry_inventory");
            var error=Assert.Throws<InvalidDataException>(()=>inventory.LoadChecked(registry).ToArray());
            facts["missing-projection.error"]=error.Message;
            facts["missing-projection.not-repaired"]=B(before==Count("SELECT count(*) FROM registry_inventory"));
        }
        else if(call.Variant=="mutated-export")
        {
            // Only a copied export is corrupted. Produced DB, export and
            // manifest originals remain untouched and inspectable.
            var copy=Path.Combine(producer.EvidenceRoot,"invalid-export");Directory.CreateDirectory(copy);
            foreach(var name in new[]{"device_inventory.jsonl","device_inventory_manifest.json"})
            {File.Copy(Path.Combine(output,name),Path.Combine(copy,name));Assert.Equal(FileHash(Path.Combine(output,name)),FileHash(Path.Combine(copy,name)));}
            var exportHash=FileHash(Path.Combine(output,"device_inventory.jsonl"));
            var validator=new CatalogInventoryValidator(database,inventory,canonical);
            Assert.True(validator.Validate(output).Complete);
            File.AppendAllText(Path.Combine(copy,"device_inventory.jsonl"),"{}\n",new UTF8Encoding(false));
            var error=Assert.Throws<InvalidDataException>(()=>validator.Validate(copy));
            Assert.Contains("Inventory stream/manifest",error.Message,StringComparison.Ordinal);
            facts["appended-export-object.error"]="Inventory stream/manifest";
            facts["original.db.retained"]=B(originalHash==FileHash(originalDatabase) && exportHash==FileHash(Path.Combine(output,"device_inventory.jsonl")));
        }
        else if(call.Variant=="structured-contract")
        {
            var documents=Documents().ToDictionary(doc=>S(doc["canonical_signature"]),StringComparer.Ordinal);var method=documents["aidl:a.I#take(P)"];
            var target=S(documents["aidl:a.I.P"]["entry_id"]);var argument=method["arguments"]![0]!;
            var rows=inventory.LoadChecked(registry).ToDictionary(row=>row.GetProperty("entry_id").GetString()!,StringComparer.Ordinal);
            var variant=rows[S(method["entry_id"])].GetProperty("variants")[0];
            facts["take(P).argument.status"]=S(argument["status"]);
            facts["take(P).argument.resolved-id.equals-aidl:a.I.P"]=B(S(argument["resolved_binding_id"])==target);
            facts["take(P).variant.schema.equals-contract-digest"]=B(variant.GetProperty("schema_hash").GetString()==contracts.Digest(Element(method)));
            facts["take(P).variant.dependencies.contains-aidl:a.I.P"]=B(variant.GetProperty("dependencies").EnumerateArray().Any(row=>row.GetProperty("entry_id").GetString()==target));
        }
        else if(call.Variant=="history-build")
        {
            var raw=inventory.LoadChecked(registry).ToArray();
            string State(string signature,int major,int minor,int? extension=null)
            {
                var observation=ResolverCases.Facts();observation["platform"]=new JsonObject{["major"]=major,["minor"]=minor};observation["extensions"]=extension is null?new JsonObject():new JsonObject{["30"]=extension.Value};
                var resolved=resolver.Resolve(raw,Element(observation),new(ResolverCases.H('f'),1,0,0,1));
                return resolved.GetProperty("entries").EnumerateArray().Single(row=>row.GetProperty("entry_id").GetString()==Id(signature)).GetProperty("state").GetString()!;
            }
            facts["jvm:a/Old@35.0"]=State("jvm:a/Old",35,0);facts["jvm:a/Old@36.0"]=State("jvm:a/Old",36,0);
            facts["jvm:a/Future#f()V@37.2"]=State("jvm:a/Future#f()V",37,2);facts["jvm:a/Future#f()V@38.0"]=State("jvm:a/Future#f()V",38,0);
            facts["jvm:a/Backport@33.0:30=4"]=State("jvm:a/Backport",33,0,4);facts["jvm:a/Backport@33.0:30=3"]=State("jvm:a/Backport",33,0,3);facts["jvm:a/Backport@33.0:no-extension"]=State("jvm:a/Backport",33,0);
        }
        else if(call.Variant=="atomic-store")
        {
            var raw=inventory.LoadChecked(registry).ToArray();var observation=ResolverCases.Facts();observation["platform"]=new JsonObject{["major"]=36,["minor"]=0};var observed=Element(observation);
            var snapshot=resolver.Resolve(raw,observed,new(registry,1,0,0,1));
            facts["inventory.count.equals-effective-entries"]=B(raw.Length==Count("SELECT count(*) FROM effective_entries"));
            foreach(var item in raw)resolver.ValidateInventoryEntity(item);facts["inventory.every-entity-valid"]="true";
            facts["snapshot.inventory-count.equals-raw"]=B(snapshot.GetProperty("inventory_count").GetInt64()==raw.Length);
            facts["java:a.A#go(int).state@36.0"]=snapshot.GetProperty("entries").EnumerateArray().Single(row=>row.GetProperty("entry_id").GetString()==Id("java:a.A#go(int)")).GetProperty("state").GetString()!;
            using var storeDatabase=CatalogDatabase.CreateInMemory(rootPath,CatalogSchemaProfile.Bridge);
            using(var command=storeDatabase.Command("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES('fixture','self_adb','fixture',0,1,0,'{}','fixture')"))command.ExecuteNonQuery();
            var store=new DeviceViewStore(storeDatabase,resolver,canonical,observed,registry,1,0,raw,raw.Length);
            store.Publish(snapshot,observed,0);facts["published.snapshot.equals-resolved"]=B(canonical.Hash(store.Read().Snapshot!.Value)==canonical.Hash(snapshot));
            store.Invalidate("device-build-change");facts["read.stale.after-device-build-change"]=B(store.Read().Status.Stale);
        }
        else if(call.Variant=="selected-aliases")
        {
            var capture=inventory.Capture();var observation=ResolverCases.Facts();observation["platform"]=new JsonObject{["major"]=37,["minor"]=0};
            var snapshot=resolver.Resolve(capture.Inventory,Element(observation),new(capture.RegistryId,capture.CatalogGeneration,capture.OverlayGeneration,0,1));
            var trusted=new DeviceViewRead(snapshot,new(null,false,0,1,"fixture"));var aliases=new CatalogAliases(canonical,contracts);var manifest=aliases.Generate(Documents(),trusted,resolver);
            var key=Id("java:a.A#go(int)");var row=snapshot.GetProperty("entries").EnumerateArray().Single(value=>value.GetProperty("entry_id").GetString()==key);
            var alias=manifest["aliases"]!.AsArray().OfType<JsonObject>().Single(value=>S(value["binding_id"])==key);var schema=row.GetProperty("schema_hash").GetString()!;
            facts["java:a.A#go(int).state@37.0"]=row.GetProperty("state").GetString()!;facts["alias.schema.equals-selected-schema"]=B(S(alias["schema_hash"])==schema);
            facts["alias.graph.equals-selected-graph"]=B(S(alias["type_graph_digest"])==row.GetProperty("graph_digest").GetString());
            var request=new CatalogRouteRequest(S(alias["tool_name"]),key,schema,"fixture",1);
            var mapping=new Dictionary<string,(string BindingId,string SchemaHash)>{{S(alias["tool_name"]),(key,schema)}};var schemas=new Dictionary<string,string>{{key,schema}};
            facts["generated.plan.equals-generic-plan"]=B(CatalogCanonicalRoute.Resolve("generated",request,mapping,schemas).SequenceEqual(CatalogCanonicalRoute.Resolve("generic",request,mapping,schemas)));
            var error=Assert.Throws<StaleDeviceViewException>(()=>aliases.Generate(Documents(),trusted with{Status=trusted.Status with{Stale=true}},resolver));
            Assert.Contains("Fresh active",error.Message,StringComparison.Ordinal);facts["stale.alias-generation.error"]="Fresh active";
        }
        else throw new InvalidOperationException("Unknown original pipeline fixture");
        Assert.Equal(originalHash,FileHash(originalDatabase));
        return new("ok",facts,built.OriginalInputsRetained);
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogDevicePipelineRealTests
{
    [Theory,MemberData(nameof(CatalogDevicePipelineCases.All),MemberType=typeof(CatalogDevicePipelineCases))]
    public Task NativePipelineUsesSameSevenOriginalAssertions(CatalogDevicePipelineCase test)=>
        CatalogDevicePipelineCases.AssertCase(test,new NativeCatalogDevicePipelineProvider());
}
