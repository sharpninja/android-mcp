using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Json.Schema;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogInventoryCapture(string RegistryId,long CatalogGeneration,long OverlayGeneration,int BaseInventoryCount,
    IReadOnlyDictionary<string,string> DocumentHashes,IReadOnlyList<JsonElement> Inventory,string InventorySha256);

/// <summary>One checked immutable loader and atomic effective overlay composition. No device authority.</summary>
public sealed class CatalogInventory(CatalogDatabase database,CatalogContracts contracts,DeviceResolver resolver,ICanonicalJson canonical,string entrySchemaPath)
{
    private readonly JsonSchema entrySchema=JsonSchema.FromText(File.ReadAllText(entrySchemaPath));
    private static string S(JsonNode? value)=>value?.GetValue<string>() ?? "";
    private static JsonElement Element(JsonNode? value)=>JsonSerializer.SerializeToElement(value);
    private object? Scalar(string sql,SqliteTransaction? transaction,params (string Name,object? Value)[] args)
    {using var command=database.Command(sql,transaction,args);return command.ExecuteScalar();}
    public IEnumerable<JsonElement> LoadChecked(string registryId,SqliteTransaction? transaction=null)
    {
        if(Scalar("SELECT value FROM catalog_meta WHERE key='device_resolution_contract_version'",transaction) is not string version || version!="2.1.0")
            throw new InvalidDataException("No supported complete device inventory contract");
        var expected=(long)Scalar("SELECT count(*) FROM registry_entries WHERE registry_id=$registry",transaction,("$registry",registryId))!;
        var actual=(long)Scalar("SELECT count(*) FROM registry_inventory WHERE registry_id=$registry",transaction,("$registry",registryId))!;
        if(actual!=expected) throw new InvalidDataException("Incomplete registry inventory");
        using var command=database.Command("SELECT entry_id,document_json,record_sha256 FROM registry_inventory WHERE registry_id=$registry ORDER BY entry_id",transaction,("$registry",registryId));
        using var rows=command.ExecuteReader();
        while(rows.Read())
        {
            var raw=rows.GetString(1);using var parsed=JsonDocument.Parse(raw);var item=parsed.RootElement;resolver.ValidateInventoryEntity(item);
            if(item.GetProperty("entry_id").GetString()!=rows.GetString(0) || ContentHash.Sha256(Encoding.UTF8.GetBytes(raw))!=rows.GetString(2))
                throw new InvalidDataException("Inventory identity or digest mismatch");
            yield return item.Clone();
        }
    }
    public CatalogInventoryCapture Capture()
    {
        SqliteTransaction transaction;
        try{transaction=database.Connection.BeginTransaction(deferred:true);}
        catch(InvalidOperationException error){throw new InvalidOperationException("Reference inventory capture owns its read transaction",error);}
        using(transaction)
        {
            var registry=(string?)Scalar("SELECT registry_id FROM active_registry WHERE singleton=1",transaction) ?? throw new InvalidDataException("Active registry required");
            long generation,overlay;
            using(var query=database.Command("SELECT registry_generation,overlay_generation FROM catalog_view WHERE singleton=1",transaction))
            using(var row=query.ExecuteReader()){if(!row.Read()) throw new InvalidDataException("Catalog view required");generation=row.GetInt64(0);overlay=row.GetInt64(1);}
            var items=LoadChecked(registry,transaction).Select(item=>JsonNode.Parse(item.GetRawText())!.AsObject()).ToDictionary(item=>S(item["entry_id"]),StringComparer.Ordinal);var baseCount=items.Count;
            var changed=new Dictionary<string,JsonObject>(StringComparer.Ordinal);var states=new Dictionary<string,string>(StringComparer.Ordinal);var revisions=new Dictionary<string,long>(StringComparer.Ordinal);
            var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
            using(var query=database.Command("SELECT entry_id,record_sha256 FROM registry_entries WHERE registry_id=$registry",transaction,("$registry",registry)))
            using(var rows=query.ExecuteReader()) while(rows.Read()) hashes.Add(rows.GetString(0),rows.GetString(1));
            using(var query=database.Command("SELECT entry_id,state,revision,document_json FROM overlays ORDER BY entry_id",transaction))
            using(var rows=query.ExecuteReader()) while(rows.Read())
            {
                var key=rows.GetString(0);var state=rows.GetString(1);var doc=JsonNode.Parse(rows.GetString(3))!.AsObject();states[key]=state;revisions[key]=rows.GetInt64(2);hashes[key]=canonical.Hash(Element(doc));
                if(state=="active")
                {
                    if(!entrySchema.Evaluate(Element(doc),new(){OutputFormat=OutputFormat.Flag}).IsValid) throw new InvalidDataException("Invalid local overlay Entry contract");
                    if(S(doc["entry_id"])!=key || ContentHash.Sha256(Encoding.UTF8.GetBytes(S(doc["canonical_signature"])))!=key) throw new InvalidDataException("Local overlay canonical identity mismatch");
                    changed.Add(key,doc);
                }
                if(!items.ContainsKey(key)) items.Add(key,new JsonObject{["entry_id"]=key,["entity_kind"]=state=="active" ? contracts.EntityKind(Element(doc)) : "other",["variants"]=new JsonArray()});
            }
            JsonObject? Imported(string key)=>Scalar("SELECT document_json FROM registry_entries WHERE registry_id=$registry AND entry_id=$id",transaction,("$registry",registry),("$id",key)) is string raw ? JsonNode.Parse(raw)!.AsObject() : null;
            foreach(var item in changed.ToArray())
            {
                var doc=item.Value;if(S(doc["kind"])!="field" || doc["extensions"]?["aidl_declaration_kind"] is null) continue;
                using var query=database.Command("SELECT binding_id FROM bindings WHERE owner=$owner AND kind IN ('aidl-parcelable','aidl-union','aidl-enum')",transaction,("$owner",S(doc["owner"])));
                using var rows=query.ExecuteReader();while(rows.Read())
                {
                    var key=rows.GetString(0);var parent=changed.GetValueOrDefault(key) ?? Imported(key);if(parent is null) continue;
                    if(parent["extensions"]?["declared_type_shape"] is not JsonObject shape) throw new InvalidDataException("Structured type override needs a producer shape");
                    foreach(var field in shape["fields"]!.AsArray().OfType<JsonObject>()) if(S(field["name"])==S(doc["name"])) {field["type"]=doc["return_type"]?.DeepClone();field["constant"]=doc["extensions"]?["constant_raw"]?.DeepClone();}
                    changed[key]=parent;
                }
            }
            var contractHashes=items.ToDictionary(item=>item.Key,item=>item.Value["variants"]!.AsArray().Select(variant=>S(variant!["schema_hash"])).ToHashSet(StringComparer.Ordinal),StringComparer.Ordinal);
            foreach(var (key,doc) in changed){contractHashes[key]=new(StringComparer.Ordinal){contracts.Digest(Element(doc))};items[key]["entity_kind"]=contracts.EntityKind(Element(doc));}
            foreach(var (key,item) in items)
            {
                if(changed.TryGetValue(key,out var doc))
                {
                    var refs=(doc["arguments"] as JsonArray ?? []).OfType<JsonObject>().Select(value=>(JsonObject)value.DeepClone()).ToList();if(doc["return_type"] is JsonObject returned) refs.Add(returned);
                    foreach(var field in new[]{"fields","supertypes"}) foreach(var shapeItem in doc["extensions"]?["declared_type_shape"]?[field] as JsonArray ?? []) if(shapeItem?["type"] is JsonObject type) refs.Add(type);
                    var unknown=new List<string>();var dependencies=new SortedDictionary<string,string>(StringComparer.Ordinal);
                    if(S(item["entity_kind"])!="type" && S(doc["owner"]).Length!=0)
                    {
                        var owners=new HashSet<string>(StringComparer.Ordinal);using var query=database.Command("SELECT binding_id FROM bindings WHERE owner=$owner",transaction,("$owner",S(doc["owner"])));using var rows=query.ExecuteReader();
                        while(rows.Read()){var owner=rows.GetString(0);if(items.TryGetValue(owner,out var value) && S(value["entity_kind"])=="type") owners.Add(owner);}
                        if(owners.Count==1) refs.Add(new JsonObject{["status"]="resolved",["resolved_binding_id"]=owners.Single()});else unknown.Add("Containing local type is absent or ambiguous");
                    }
                    foreach(var reference in refs)
                    {
                        if(S(reference["status"])=="primitive") continue;var target=S(reference["resolved_binding_id"]);var candidates=contractHashes.GetValueOrDefault(target);
                        if(S(reference["status"])!="resolved" || !items.TryGetValue(target,out var targetItem) || S(targetItem["entity_kind"])!="type" || candidates?.Count!=1)
                        {unknown.Add("Local override requires an unambiguous declared type schema");continue;}
                        dependencies[target]=candidates.Single();
                    }
                    var variants=item["variants"]!.AsArray();if(variants.Count==0) variants.Add(new JsonObject{["availability"]=new JsonObject{["unknown_reason"]="Local addition has no imported version applicability evidence"},["dependencies"]=new JsonArray(),["schema_hash"]=contractHashes[key].Single()});
                    foreach(var variant in variants.OfType<JsonObject>())
                    {
                        variant["schema_hash"]=contractHashes[key].Single();variant["dependencies"]=new JsonArray(dependencies.Select(dependency=>(JsonNode)new JsonObject{["entry_id"]=dependency.Key,["schema_hash"]=dependency.Value}).ToArray());
                        if(unknown.Count!=0) variant["availability"]=CatalogProjection.Incomplete(variant["availability"]!.AsObject(),string.Join("; ",unknown.Distinct(StringComparer.Ordinal).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars))));
                    }
                }
                else foreach(var variant in item["variants"]!.AsArray().OfType<JsonObject>()) foreach(var dependency in variant["dependencies"]!.AsArray().OfType<JsonObject>())
                {
                    var target=S(dependency["entry_id"]);if(!changed.ContainsKey(target)) continue;
                    if(contractHashes[target].Count==1) dependency["schema_hash"]=contractHashes[target].Single();else variant["availability"]=CatalogProjection.Incomplete(variant["availability"]!.AsObject(),"Local type variants ambiguous");
                }
                item["catalog_state"]=states.GetValueOrDefault(key,"active");item["catalog_revision"]=revisions.GetValueOrDefault(key);item["catalog_document_sha256"]=hashes.GetValueOrDefault(key);resolver.ValidateInventoryEntity(Element(item));
            }
            var inventory=items.OrderBy(item=>item.Key,StringComparer.Ordinal).Select(item=>Element(item.Value)).ToArray();var digest=resolver.InventoryDigest(inventory);
            var result=new CatalogInventoryCapture(registry,generation,overlay,baseCount,hashes,inventory,digest);transaction.Commit();return result;
        }
    }
}
