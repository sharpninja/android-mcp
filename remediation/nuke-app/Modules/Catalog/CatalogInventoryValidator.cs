using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogInventoryValidation(string Status,long Entries,string? Contract,string? InventorySha256,bool Complete,string Scope);

/// <summary>Independent read-only reconciliation through the single checked inventory/schema engine.</summary>
public sealed class CatalogInventoryValidator(CatalogDatabase database,CatalogInventory inventory,ICanonicalJson canonical)
{
    public CatalogInventoryValidation Validate(string outputRoot)
    {
        var manifestPath=ArtifactPaths.Resolve(outputRoot,"device_inventory_manifest.json");
        using var transaction=database.Connection.BeginTransaction(deferred:true);
        object? Scalar(string sql,params (string Name,object? Value)[] args)
        {using var query=database.Command(sql,transaction,args);return query.ExecuteScalar();}
        if(Scalar("SELECT value FROM catalog_meta WHERE key='device_resolution_contract_version'") is null)
        {
            if(File.Exists(manifestPath))throw new InvalidDataException("Inventory export has no declared catalog contract");
            return new("absent-legacy; cannot initialize a device view",0,null,null,false,"Legacy inventory is not publishable");
        }
        var registry=(string?)Scalar("SELECT registry_id FROM active_registry WHERE singleton=1") ?? throw new InvalidDataException("Active registry required");
        var types=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);var sources=new HashSet<string>(StringComparer.Ordinal);
        using(var query=database.Command("SELECT entry_id,document_json FROM registry_inventory WHERE registry_id=$registry AND json_extract(document_json,'$.entity_kind')='type'",transaction,("$registry",registry)))
        using(var rows=query.ExecuteReader())while(rows.Read())
        {
            using var document=JsonDocument.Parse(rows.GetString(1));types.Add(rows.GetString(0),document.RootElement.GetProperty("variants").EnumerateArray().Select(value=>value.GetProperty("schema_hash").GetString()!).ToHashSet(StringComparer.Ordinal));
        }
        using(var query=database.Command("SELECT source_id FROM sources",transaction))using(var rows=query.ExecuteReader())while(rows.Read())sources.Add(rows.GetString(0));
        var exportPath=ArtifactPaths.Resolve(outputRoot,"device_inventory.jsonl");
        if(!File.Exists(exportPath)||!File.Exists(manifestPath))throw new InvalidDataException("Complete inventory export/manifest required");
        using var export=File.OpenRead(exportPath);long count=0;var buffer=new byte[65536];
        void Match(ReadOnlySpan<byte> expected)
        {
            for(var offset=0;offset<expected.Length;)
            {
                var length=Math.Min(buffer.Length,expected.Length-offset);var read=0;
                while(read<length){var chunk=export.Read(buffer,read,length-read);if(chunk==0)throw new InvalidDataException("Inventory stream/manifest reconciliation failed");read+=chunk;}
                if(!buffer.AsSpan(0,length).SequenceEqual(expected.Slice(offset,length)))throw new InvalidDataException("Inventory stream/manifest reconciliation failed");
                offset+=length;
            }
        }
        foreach(var item in inventory.LoadChecked(registry,transaction))
        {
            var id=item.GetProperty("entry_id").GetString()!;
            using var entry=JsonDocument.Parse((string?)Scalar("SELECT document_json FROM registry_entries WHERE registry_id=$registry AND entry_id=$id",("$registry",registry),("$id",id)) ?? throw new InvalidDataException("Inventory Entry is absent"));
            var extensions=entry.RootElement.GetProperty("extensions");
            if(extensions.GetProperty("device_resolution_contract_version").GetString()!="2.1.0" ||
                !canonical.Serialize(extensions.GetProperty("device_resolution_variants")).AsSpan().SequenceEqual(canonical.Serialize(item.GetProperty("variants"))))
                throw new InvalidDataException("Inventory differs from hashed Entry projection");
            foreach(var variant in item.GetProperty("variants").EnumerateArray())
            {
                var rule=variant.GetProperty("availability");
                var evidence=rule.TryGetProperty("evidence_ids",out var ids) ? ids.EnumerateArray().Select(value=>value.GetString()).ToArray() :
                    new[]{rule.TryGetProperty("x-raw",out var raw) && raw.TryGetProperty("source_id",out var source) ? source.GetString() : null};
                if(evidence.Length==0 || evidence.Any(source=>source is null || !sources.Contains(source)))throw new InvalidDataException("Inventory source evidence missing");
                foreach(var dependency in variant.GetProperty("dependencies").EnumerateArray())
                    if(!types.TryGetValue(dependency.GetProperty("entry_id").GetString()!,out var candidates) || !candidates.Contains(dependency.GetProperty("schema_hash").GetString()!))
                        throw new InvalidDataException("Inventory type dependency schema absent");
            }
            Match(canonical.Serialize(item));Match("\n"u8);count++;
        }
        if(export.ReadByte()!=-1)throw new InvalidDataException("Inventory stream/manifest reconciliation failed");
        export.Position=0;var digest=ContentHash.Sha256(export);
        var manifestBytes=File.ReadAllBytes(manifestPath);_=canonical.Serialize(manifestBytes);using var manifest=JsonDocument.Parse(manifestBytes);var root=manifest.RootElement;
        if(root.GetProperty("contract").GetString()!="urn:android-binding-catalog:2.1.0:DeviceResolution" || root.GetProperty("registry_id").GetString()!=registry ||
            !root.TryGetProperty("entries",out var entries) || entries.ValueKind!=JsonValueKind.Number || !StrictJsonInspector.IsIntegerToken(entries.GetRawText()) ||
            !entries.TryGetInt64(out var manifestCount) || manifestCount!=count || root.GetProperty("inventory_sha256").GetString()!=digest)
            throw new InvalidDataException("Inventory stream/manifest reconciliation failed");
        transaction.Commit();
        return new("validated",count,"2.1.0",digest,true,"Complete selected registry projection, not global source coverage or runtime authority");
    }
}
