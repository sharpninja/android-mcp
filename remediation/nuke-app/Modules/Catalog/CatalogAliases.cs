using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Offline aliases remain unavailable and default deny. No execution authority is inferred.</summary>
public sealed class CatalogAliases(ICanonicalJson canonical,CatalogContracts contracts)
{
    public JsonObject Generate(IEnumerable<JsonObject> entries)=>Manifest(EnumerateOrdered(entries.OrderBy(entry=>S(entry,"entry_id"),StringComparer.Ordinal)));
    public JsonObject Generate(IEnumerable<JsonObject> entries,DeviceViewRead trusted,DeviceResolver resolver)
    {
        var view=CatalogViewPolicy.RequireFreshSnapshot(trusted,resolver,canonical);
        return Manifest(Enumerate(entries.OrderBy(entry=>S(entry,"entry_id"),StringComparer.Ordinal),view));
    }
    private static string S(JsonObject value,string name)=>value[name]!.GetValue<string>();
    private static JsonObject Manifest(IEnumerable<JsonObject> aliases)=>new(){["format_version"]="1.1.0",["contract_format_version"]=3,
        ["aliases"]=new JsonArray(aliases.Select(alias=>(JsonNode)alias).ToArray())};
    public IEnumerable<JsonObject> EnumerateOrdered(IEnumerable<JsonObject> entries)=>Enumerate(entries,null);
    private IEnumerable<JsonObject> Enumerate(IEnumerable<JsonObject> entries,JsonElement? view)
    {
        var selected=view?.GetProperty("entries").EnumerateArray().ToDictionary(row=>row.GetProperty("entry_id").GetString()!,row=>row,StringComparer.Ordinal);
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in entries)
        {
            var id=S(entry,"entry_id");var signature=S(entry,"canonical_signature");
            if(ContentHash.Sha256(Encoding.UTF8.GetBytes(signature))!=id)throw new ArgumentException("Canonical identity mismatch");
            if(!seen.Add(id))throw new ArgumentException("Duplicate effective entry");
            var element=JsonSerializer.SerializeToElement(entry);var kind=contracts.EntityKind(element);
            var documentHash=canonical.Hash(element);var digest=contracts.Digest(element);JsonElement? resolved=null;
            if(selected is not null)
            {
                if(!selected.TryGetValue(id,out var row))throw new InvalidDataException("Device view omits inventory entry");
                resolved=row;
                if(row.GetProperty("state").GetString()!="available" ||
                    row.TryGetProperty("catalog_state",out var state) && state.GetString()!="active" || kind!="binding")continue;
                digest=row.GetProperty("schema_hash").GetString()!;
                if(row.GetProperty("catalog_document_sha256").GetString()!=documentHash)
                    throw new InvalidDataException("Generated alias document differs from pinned effective catalog");
            }
            if(kind!="binding")continue;
            yield return new JsonObject{["tool_name"]="binding_"+id,["policy_tool_id"]="binding.invoke",["binding_id"]=id,["schema_hash"]=digest,
                ["document_hash"]=documentHash,["permission_hash"]=canonical.Hash(JsonSerializer.SerializeToElement(entry["permission"])),
                ["generic_binding_id"]=id,["default_grant"]="deny",["device_view_id"]=view?.GetProperty("view_id").GetString(),
                ["device_state"]=resolved?.GetProperty("state").GetString() ?? "unknown",["type_graph_digest"]=resolved?.GetProperty("graph_digest").GetString(),
                ["dispatch_state"]="unavailable",["reason"]="Offline catalog only; runtime authority, target resolver and validated codec required"};
        }
        if(selected is not null && !seen.SetEquals(selected.Keys))throw new InvalidDataException("Device view and supplied inventory differ");
    }
}
