using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record DeviceViewControl(string? ViewId,string RegistryId,long CatalogGeneration,long OverlayGeneration,
    long Epoch,long Generation,bool Stale,long InventoryCount,string InventoryHash);
public sealed record DeviceViewStatus(JsonElement? Header,bool Stale,long Epoch,long Generation,string ETag);
public sealed record DeviceViewRead(JsonElement? Snapshot,DeviceViewStatus Status);
public sealed class StaleDeviceViewException(string message):InvalidOperationException(message);

/// <summary>Private offline captured-fact and sealed-view storage; never an Android collector or dispatcher.</summary>
public sealed partial class DeviceViewStore
{
    private readonly CatalogDatabase database;
    private readonly DeviceResolver resolver;
    private readonly ICanonicalJson canonical;
    private readonly JsonElement[] scope;
    private readonly Func<string> now;
    private readonly object sync=new();
    private JsonElement[] inventory;
    public string ScopeId { get; }
    public DeviceViewStore(CatalogDatabase database,DeviceResolver resolver,ICanonicalJson canonical,JsonElement scopeFacts,
        string registryId,long catalogGeneration,long overlayGeneration,IReadOnlyList<JsonElement> inventory,object? inventoryCount=null,Func<string>? now=null)
    {
        this.database=database;this.resolver=resolver;this.canonical=canonical;
        this.now=now ?? (()=>DateTimeOffset.Now.ToString("O",CultureInfo.InvariantCulture));
        ArgumentNullException.ThrowIfNull(inventory);
        if(inventoryCount is not null && inventoryCount is not (int or long)) throw new ArgumentException("Invalid expected inventory count");
        var expected=inventoryCount is null ? inventory.Count : Convert.ToInt64(inventoryCount,CultureInfo.InvariantCulture);
        if(expected!=inventory.Count) throw new ArgumentException("Expected inventory count disagrees with supplied complete inventory");
        if(expected<0 || expected>DeviceResolver.CounterLimit) throw new ArgumentException("Invalid expected inventory count");
        scope=DeviceResolver.ScopeFields.Select(field=>scopeFacts.GetProperty(field).Clone()).ToArray();
        ScopeId=canonical.Hash(JsonSerializer.SerializeToElement(scope));
        this.inventory=inventory.Select(item=>item.Clone()).ToArray();
        using(var shape=database.Command("SELECT count(*) FROM pragma_table_info('device_view_entries') WHERE name='contract_hash'"))
            if((long)shape.ExecuteScalar()! !=1) throw new InvalidDataException("Legacy device-view storage requires checked migration; model3 cannot activate silently");
        using var transaction=Begin();
        var hash=PinInventory(this.inventory,transaction);
        using(var insertScope=database.Command("INSERT OR IGNORE INTO device_view_scopes VALUES($scope,$profile,$target,$user,$backend,$device)",transaction,
            ("$scope",ScopeId),("$profile",scope[0].GetString()),("$target",scope[1].GetString()),("$user",scope[2].GetInt64()),
            ("$backend",scope[3].GetString()),("$device",scope[4].GetString()))) insertScope.ExecuteNonQuery();
        using(var selected=database.Command("SELECT count(*) FROM device_view_active WHERE scope_id=$scope",transaction,("$scope",ScopeId)))
        {
            if((long)selected.ExecuteScalar()! ==0)
            {
                using(var invalidation=database.Command("INSERT INTO device_view_invalidations VALUES($scope,0,'startup',$time)",transaction,("$scope",ScopeId),("$time",this.now()))) invalidation.ExecuteNonQuery();
                using var initial=database.Command("INSERT INTO device_view_active VALUES($scope,NULL,$registry,$catalog,$overlay,0,0,1,$count,$inventory)",transaction,
                    ("$scope",ScopeId),("$registry",registryId),("$catalog",catalogGeneration),("$overlay",overlayGeneration),("$count",inventory.Count),("$inventory",hash));
                initial.ExecuteNonQuery();
            }
        }
        if(Control(transaction).InventoryHash!=hash) throw new InvalidDataException("Reopened scope inventory differs from pinned catalog");
        transaction.Commit();
    }
    private SqliteTransaction Begin()
    {
        try { return database.Connection.BeginTransaction(deferred:false); }
        catch(InvalidOperationException error) { throw new InvalidOperationException("Reference storage owns its transaction",error); }
    }
    private string Text(JsonElement value)=>Encoding.UTF8.GetString(canonical.Serialize(value));
    private string PinInventory(IReadOnlyList<JsonElement> items,SqliteTransaction transaction)
    {
        foreach(var item in items) resolver.ValidateInventoryEntity(item);
        if(items.Select(item=>item.GetProperty("entry_id").GetString()).Distinct(StringComparer.Ordinal).Count()!=items.Count)
            throw new InvalidDataException("Duplicate inventory membership");
        var hash=resolver.InventoryDigest(items);
        using(var existing=database.Command("SELECT entry_count,state FROM device_inventory_sets WHERE inventory_sha256=$hash",transaction,("$hash",hash)))
        using(var reader=existing.ExecuteReader())
        {
            if(reader.Read())
            {
                if(reader.GetInt64(0)!=items.Count || reader.GetString(1)!="sealed") throw new InvalidDataException("Pinned inventory digest/content mismatch");
                reader.Close();
                using var members=database.Command("SELECT document_json FROM device_inventory_members WHERE inventory_sha256=$hash ORDER BY entry_id",transaction,("$hash",hash));
                using var rows=members.ExecuteReader();var stored=new List<JsonElement>();
                while(rows.Read()) { using var document=JsonDocument.Parse(rows.GetString(0));stored.Add(document.RootElement.Clone()); }
                if(resolver.InventoryDigest(stored)!=hash) throw new InvalidDataException("Pinned inventory digest/content mismatch");
                return hash;
            }
        }
        using(var stage=database.Command("INSERT INTO device_inventory_sets VALUES($hash,$count,'staged')",transaction,("$hash",hash),("$count",items.Count))) stage.ExecuteNonQuery();
        foreach(var item in items)
        {
            using var member=database.Command("INSERT INTO device_inventory_members VALUES($hash,$entry,$kind,$body)",transaction,
                ("$hash",hash),("$entry",item.GetProperty("entry_id").GetString()),("$kind",item.GetProperty("entity_kind").GetString()),("$body",Text(item)));
            member.ExecuteNonQuery();
        }
        using(var seal=database.Command("UPDATE device_inventory_sets SET state='sealed' WHERE inventory_sha256=$hash",transaction,("$hash",hash))) seal.ExecuteNonQuery();
        return hash;
    }
    private DeviceViewControl Control(SqliteTransaction? transaction)
    {
        using var command=database.Command("SELECT view_id,registry_id,catalog_generation,overlay_generation,capability_epoch,generation,stale,expected_inventory_count,inventory_sha256 FROM device_view_active WHERE scope_id=$scope",transaction,("$scope",ScopeId));
        using var row=command.ExecuteReader();
        if(!row.Read()) throw new InvalidDataException("Device scope not initialized");
        return new(row.IsDBNull(0) ? null : row.GetString(0),row.GetString(1),row.GetInt64(2),row.GetInt64(3),row.GetInt64(4),row.GetInt64(5),row.GetInt64(6)!=0,row.GetInt64(7),row.GetString(8));
    }
    public DeviceViewControl Control() { lock(sync) return Control(null); }
    private string Tag(DeviceViewControl control)=>$"\"dv2:{ScopeId}:{control.Generation}:{control.Epoch}:{control.CatalogGeneration}:{control.OverlayGeneration}:{control.InventoryHash}\"";
    private DeviceViewStatus Status(DeviceViewControl control)
    {
        JsonElement? header=null;
        if(control.ViewId is not null)
        {
            using var command=database.Command("SELECT document_json FROM device_views WHERE view_id=$view",null,("$view",control.ViewId));
            using var document=JsonDocument.Parse((string)command.ExecuteScalar()!);header=document.RootElement.Clone();
        }
        return new(header,control.Stale,control.Epoch,control.Generation,Tag(control));
    }
    public DeviceViewStatus Status() { lock(sync) return Status(Control(null)); }
    public DeviceViewRead Read()
    {
        lock(sync)
        {
            var control=Control(null);var status=Status(control);if(control.ViewId is null) return new(null,status);
            using var command=database.Command("SELECT document_json FROM device_view_entries WHERE view_id=$view ORDER BY entry_id",null,("$view",control.ViewId));
            using var rows=command.ExecuteReader();var entries=new List<JsonElement>();
            while(rows.Read()) { using var document=JsonDocument.Parse(rows.GetString(0));entries.Add(document.RootElement.Clone()); }
            var snapshot=status.Header!.Value.EnumerateObject().ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
            snapshot.Add("entries",JsonSerializer.SerializeToElement(entries));return new(JsonSerializer.SerializeToElement(snapshot),status);
        }
    }
    public void Invalidate(string reason,string? registryId=null,long? catalogGeneration=null,long? overlayGeneration=null,IReadOnlyList<JsonElement>? newInventory=null)
    {
        if(string.IsNullOrEmpty(reason)) throw new ArgumentException("Invalidation reason required");
        lock(sync)
        {
            using var transaction=Begin();var control=Control(transaction);
            if(control.Epoch==DeviceResolver.CounterLimit) throw new OverflowException("Capability epoch exhausted");
            var epoch=control.Epoch+1;
            using(var invalidation=database.Command("INSERT INTO device_view_invalidations VALUES($scope,$epoch,$reason,$time)",transaction,
                ("$scope",ScopeId),("$epoch",epoch),("$reason",reason),("$time",now()))) invalidation.ExecuteNonQuery();
            var hash=newInventory is null ? control.InventoryHash : PinInventory(newInventory,transaction);
            using(var update=database.Command("UPDATE device_view_active SET capability_epoch=$epoch,stale=1,registry_id=$registry,catalog_generation=$catalog,overlay_generation=$overlay,expected_inventory_count=$count,inventory_sha256=$inventory WHERE scope_id=$scope",transaction,
                ("$epoch",epoch),("$registry",registryId ?? control.RegistryId),("$catalog",catalogGeneration ?? control.CatalogGeneration),
                ("$overlay",overlayGeneration ?? control.OverlayGeneration),("$count",newInventory?.Count ?? control.InventoryCount),("$inventory",hash),("$scope",ScopeId))) update.ExecuteNonQuery();
            transaction.Commit();if(newInventory is not null) inventory=newInventory.Select(item=>item.Clone()).ToArray();
        }
    }
    public DeviceViewStatus RequestRefresh(string? etag)
    {
        lock(sync)
        {
            if(etag is null) throw new CatalogException(428,"If-Match required");
            if(!StrongViewTag().IsMatch(etag)) throw new CatalogException(400,"One exact strong device-view ETag required");
            if(etag!=Tag(Control(null))) throw new CatalogException(412,"Device view ETag changed");
            Invalidate("owner-refresh");return Status();
        }
    }
    public void Publish(JsonElement snapshot,JsonElement facts,long expectedGeneration)
    {
        lock(sync)
        {
            using var transaction=Begin();var control=Control(transaction);
            resolver.ValidateSnapshot(snapshot);resolver.ValidateFacts(facts);
            if(facts.GetProperty("capability_epoch").GetInt64()!=control.Epoch || snapshot.GetProperty("capability_epoch").GetInt64()!=control.Epoch)
                throw new StaleDeviceViewException("Captured facts epoch changed");
            if(snapshot.GetProperty("facts_id").GetString()!=canonical.Hash(facts)) throw new InvalidDataException("Facts hash mismatch");
            var body=snapshot.EnumerateObject().Where(property=>property.Name!="view_id").ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
            if(snapshot.GetProperty("view_id").GetString()!=canonical.Hash(JsonSerializer.SerializeToElement(body))) throw new InvalidDataException("Snapshot hash mismatch");
            if(DeviceResolver.ScopeFields.Where((field,index)=>!canonical.Serialize(snapshot.GetProperty(field)).AsSpan().SequenceEqual(canonical.Serialize(scope[index])) ||
                !canonical.Serialize(facts.GetProperty(field)).AsSpan().SequenceEqual(canonical.Serialize(scope[index]))).Any()) throw new InvalidDataException("Cross-target/profile snapshot");
            if(snapshot.GetProperty("registry_id").GetString()!=control.RegistryId || snapshot.GetProperty("catalog_generation").GetInt64()!=control.CatalogGeneration || snapshot.GetProperty("overlay_generation").GetInt64()!=control.OverlayGeneration)
                throw new StaleDeviceViewException("Catalog changed");
            if(expectedGeneration!=control.Generation || snapshot.GetProperty("view_generation").GetInt64()!=control.Generation+1) throw new StaleDeviceViewException("Snapshot generation/capability epoch changed");
            if(snapshot.GetProperty("inventory_sha256").GetString()!=control.InventoryHash || snapshot.GetProperty("inventory_count").GetInt64()!=control.InventoryCount)
                throw new InvalidDataException("Snapshot inventory differs from trusted pin or complete membership");
            var expected=resolver.Resolve(inventory,facts,new(control.RegistryId,control.CatalogGeneration,control.OverlayGeneration,control.Epoch,control.Generation+1));
            if(!canonical.Serialize(snapshot).AsSpan().SequenceEqual(canonical.Serialize(expected))) throw new InvalidDataException("Snapshot inventory membership, selected variants or states differ from trusted resolution");
            var factsId=canonical.Hash(facts);var viewId=snapshot.GetProperty("view_id").GetString();
            using(var insertFacts=database.Command("INSERT OR IGNORE INTO device_capability_facts VALUES($facts,$scope,$epoch,$observed,$body)",transaction,
                ("$facts",factsId),("$scope",ScopeId),("$epoch",control.Epoch),("$observed",facts.GetProperty("observed_at").GetString()),("$body",Text(facts)))) insertFacts.ExecuteNonQuery();
            var header=body.Where(property=>property.Key!="entries").ToDictionary(property=>property.Key,property=>property.Value,StringComparer.Ordinal);
            header.Add("view_id",snapshot.GetProperty("view_id").Clone());
            using(var stage=database.Command("INSERT INTO device_views VALUES($view,$scope,$registry,$catalog,$overlay,$epoch,$generation,$count,$inventory,$facts,$body,'staged')",transaction,
                ("$view",viewId),("$scope",ScopeId),("$registry",control.RegistryId),("$catalog",control.CatalogGeneration),("$overlay",control.OverlayGeneration),
                ("$epoch",control.Epoch),("$generation",control.Generation+1),("$count",control.InventoryCount),("$inventory",control.InventoryHash),("$facts",factsId),("$body",Text(JsonSerializer.SerializeToElement(header))))) stage.ExecuteNonQuery();
            foreach(var row in snapshot.GetProperty("entries").EnumerateArray())
            {
                object? Nullable(string field)=>row.GetProperty(field).ValueKind==JsonValueKind.Null ? null : row.GetProperty(field).GetString();
                using var entry=database.Command("INSERT INTO device_view_entries VALUES($view,$entry,$kind,$state,$reasons,$schema,$contract,$dependencies,$graph,$body)",transaction,
                    ("$view",viewId),("$entry",row.GetProperty("entry_id").GetString()),("$kind",row.GetProperty("entity_kind").GetString()),("$state",row.GetProperty("state").GetString()),
                    ("$reasons",Text(row.GetProperty("reason_codes"))),("$schema",Nullable("schema_hash")),("$contract",Nullable("contract_hash")),
                    ("$dependencies",Text(row.GetProperty("dependencies"))),("$graph",Nullable("graph_digest")),("$body",Text(row)));
                entry.ExecuteNonQuery();
            }
            using(var seal=database.Command("UPDATE device_views SET state='sealed' WHERE view_id=$view",transaction,("$view",viewId))) seal.ExecuteNonQuery();
            using(var activate=database.Command("UPDATE device_view_active SET view_id=$view,generation=$generation,stale=0 WHERE scope_id=$scope AND generation=$old AND capability_epoch=$epoch",transaction,
                ("$view",viewId),("$generation",control.Generation+1),("$scope",ScopeId),("$old",expectedGeneration),("$epoch",control.Epoch)))
                if(activate.ExecuteNonQuery()!=1) throw new StaleDeviceViewException("Concurrent device view CAS");
            transaction.Commit();
        }
    }
    [GeneratedRegex("^\"dv2:[0-9a-f]{64}:(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):(?:0|[1-9][0-9]*):[0-9a-f]{64}\"$",RegexOptions.CultureInvariant)]
    private static partial Regex StrongViewTag();
}
