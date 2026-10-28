using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

/// <summary>Trusted sealed-view composition around the single base admission algebra.</summary>
public static class CatalogViewPolicy
{
    // Shared by view policy and selected aliases. This is the one existing
    // schema/hash/freshness authority, not a second snapshot validator.
    internal static JsonElement RequireFreshSnapshot(DeviceViewRead trusted,DeviceResolver resolver,ICanonicalJson canonical)
    {
        if(trusted.Snapshot is not { } view || trusted.Status.Stale || trusted.Status.Generation!=view.GetProperty("view_generation").GetInt64() || trusted.Status.Epoch!=view.GetProperty("capability_epoch").GetInt64())
            throw new StaleDeviceViewException("Fresh active device view required");
        resolver.ValidateSnapshot(view);
        var body=view.EnumerateObject().Where(property=>property.Name!="view_id").ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
        if(canonical.Hash(JsonSerializer.SerializeToElement(body))!=view.GetProperty("view_id").GetString())throw new InvalidDataException("Device view hash mismatch");
        return view;
    }
    public static string Evaluate(IReadOnlyList<CanonicalBinding> plan,DeviceViewRead trusted,IReadOnlyDictionary<string,PolicyRule> bindingRules,
        PolicyRule? entryRule,string entrySchema,bool authority,IReadOnlyDictionary<string,string>? currentDocumentHashes,
        DeviceResolver resolver,ICanonicalJson canonical,bool emergency=false)
    {
        JsonElement view;
        try{view=RequireFreshSnapshot(trusted,resolver,canonical);}catch(Exception error)when(error is InvalidDataException or StaleDeviceViewException){return "deny";}
        var entries=view.GetProperty("entries").EnumerateArray().ToDictionary(row=>row.GetProperty("entry_id").GetString()!,row=>row,StringComparer.Ordinal);var pending=new Stack<string>();
        foreach(var binding in plan)
        {
            if(!entries.TryGetValue(binding.BindingId,out var row) || row.GetProperty("entity_kind").GetString()!="binding" || row.GetProperty("schema_hash").GetString()!=binding.SchemaHash || binding.Target!=view.GetProperty("target_key").GetString() || row.GetProperty("catalog_document_sha256").ValueKind==JsonValueKind.Null || currentDocumentHashes is null || currentDocumentHashes.GetValueOrDefault(binding.BindingId)!=row.GetProperty("catalog_document_sha256").GetString())return "deny";
            pending.Push(binding.BindingId);
        }
        var seen=new HashSet<string>(StringComparer.Ordinal);
        while(pending.TryPop(out var key))
        {
            if(!seen.Add(key))continue;if(!entries.TryGetValue(key,out var row) || row.GetProperty("state").GetString()!="available" || row.GetProperty("catalog_state").GetString()!="active")return "deny";
            foreach(var dependency in row.GetProperty("dependencies").EnumerateArray())pending.Push(dependency.GetProperty("entry_id").GetString()!);
        }
        return PolicyReference.Evaluate(plan,bindingRules,entryRule,entrySchema,authority,emergency);
    }
}
