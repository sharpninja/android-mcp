using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Offline aliases remain unavailable and default deny. No execution authority is inferred.</summary>
public sealed class CatalogAliases(ICanonicalJson canonical,CatalogContracts contracts)
{
    public JsonObject Generate(IEnumerable<JsonObject> entries)=>new(){["format_version"]="1.1.0",["contract_format_version"]=3,["aliases"]=new JsonArray(EnumerateOrdered(entries.OrderBy(entry=>entry["entry_id"]!.GetValue<string>(),StringComparer.Ordinal)).Select(alias=>(JsonNode)alias).ToArray())};
    public IEnumerable<JsonObject> EnumerateOrdered(IEnumerable<JsonObject> entries)
    {
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in entries)
        {
            var id=entry["entry_id"]!.GetValue<string>();var signature=entry["canonical_signature"]!.GetValue<string>();
            if(ContentHash.Sha256(Encoding.UTF8.GetBytes(signature))!=id) throw new ArgumentException("Canonical identity mismatch");
            if(!seen.Add(id)) throw new ArgumentException("Duplicate effective entry");
            var element=JsonSerializer.SerializeToElement(entry);if(contracts.EntityKind(element)!="binding") continue;
            yield return new JsonObject{["tool_name"]="binding_"+id,["policy_tool_id"]="binding.invoke",["binding_id"]=id,["schema_hash"]=contracts.Digest(element),["document_hash"]=canonical.Hash(element),
                ["permission_hash"]=canonical.Hash(JsonSerializer.SerializeToElement(entry["permission"])),["generic_binding_id"]=id,["default_grant"]="deny",["device_view_id"]=null,["device_state"]="unknown",["type_graph_digest"]=null,
                ["dispatch_state"]="unavailable",["reason"]="Offline catalog only; runtime authority, target resolver and validated codec required"};
        }
    }
}
