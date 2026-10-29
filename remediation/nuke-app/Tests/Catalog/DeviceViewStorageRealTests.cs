using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeViewProvider:IViewCaseProvider
{
    private readonly ViewCase test;
    private readonly CatalogDatabase database;
    private readonly CanonicalJson canonical=new();
    private readonly DeviceResolver resolver;
    private DeviceViewStore store;
    private SqliteTransaction? fixtureTransaction;
    private ViewReply? beforeOwnedTransaction;
    private DeviceViewControl? beforeOwnedControl;
    public NativeViewProvider(ViewCase test)
    {
        this.test=test;var root=NativeSchemaProvider.Root();database=CatalogDatabase.CreateInMemory(root,CatalogSchemaProfile.Bridge);
        resolver=new(canonical,Path.Combine(root,"contracts","DeviceView.schema.json"));
        using(var profile=database.Command("INSERT INTO connection_profiles(profile_id,backend_kind,display_name,enabled,row_version,android_user_id,endpoint_metadata_json,verification_strength) VALUES($profile,'self_adb','fixture',0,1,0,'{}','fixture')",null,("$profile",test.ScopeProfile))) profile.ExecuteNonQuery();
        store=Create(test.TrustedInventory ?? ViewStorageCases.Inventory());
        if(test.FixtureEpoch!=0) SeedEpochBoundary(test.FixtureEpoch);
    }
    private static JsonElement Element(JsonObject value)=>JsonSerializer.SerializeToElement(value);
    private string Text(JsonElement value)=>Encoding.UTF8.GetString(canonical.Serialize(value));
    private DeviceViewStore Create(JsonObject[] inventory,object? count=null)
    {
        var control=store?.Control();
        return new(database,resolver,canonical,Element(ViewStorageCases.Facts(profile:test.ScopeProfile)),
            control?.RegistryId ?? ViewStorageCases.H('f'),control?.CatalogGeneration ?? 1,control?.OverlayGeneration ?? 0,
            inventory.Select(Element).ToArray(),count,()=>"fixture-time");
    }
    private int Count(string table)
    { using var query=database.Command("SELECT count(*) FROM "+table,fixtureTransaction);return checked((int)(long)query.ExecuteScalar()!); }
    private DeviceViewControl ObservedControl()
    {
        using var query=database.Command("SELECT view_id,registry_id,catalog_generation,overlay_generation,capability_epoch,generation,stale,expected_inventory_count,inventory_sha256 FROM device_view_active WHERE scope_id=$scope",fixtureTransaction,("$scope",store.ScopeId));
        using var row=query.ExecuteReader();Assert.True(row.Read());return new(row.IsDBNull(0) ? null : row.GetString(0),row.GetString(1),row.GetInt64(2),row.GetInt64(3),row.GetInt64(4),row.GetInt64(5),row.GetInt64(6)!=0,row.GetInt64(7),row.GetString(8));
    }
    private ViewReply Observe(string outcome="ok",string? error=null,bool statusOnly=false)
    {
        string? snapshot;string? header;string tag;long epoch;long generation;bool stale;
        if(fixtureTransaction is null)
        {
            DeviceViewStatus status;
            if(statusOnly) { status=store.Status();snapshot=null; }
            else { var read=store.Read();status=read.Status;snapshot=read.Snapshot is null ? null : Text(read.Snapshot.Value); }
            header=status.Header is null ? null : Text(status.Header.Value);tag=status.ETag;epoch=status.Epoch;generation=status.Generation;stale=status.Stale;
        }
        else
        {
            // Observe transaction side effects with the actual caller transaction.
            // Production read APIs are not asked to enlist in a caller-owned unit.
            var control=ObservedControl();var unchanged=control==beforeOwnedControl;
            snapshot=unchanged ? beforeOwnedTransaction!.Snapshot : "fixture-observed-pointer-change";
            header=unchanged ? beforeOwnedTransaction!.Header : "fixture-observed-pointer-change";
            tag=unchanged ? beforeOwnedTransaction!.ETag : "fixture-observed-pointer-change";
            epoch=control.Epoch;generation=control.Generation;stale=control.Stale;
        }
        var reasons=new List<string>();
        using(var query=database.Command("SELECT reason FROM device_view_invalidations WHERE scope_id=$scope ORDER BY capability_epoch",fixtureTransaction,("$scope",store.ScopeId)))
        using(var rows=query.ExecuteReader()) while(rows.Read()) reasons.Add(rows.GetString(0));
        var requiredColumns=new HashSet<string>("flags|interface_hash|interface_version|oneway|proof_source_json|proof_state".Split('|'),StringComparer.Ordinal);
        var columns=new List<string>();
        using(var query=database.Command("PRAGMA table_info(binding_wire_contracts)",fixtureTransaction))
        using(var rows=query.ExecuteReader()) while(rows.Read()) if(requiredColumns.Contains(rows.GetString(1))) columns.Add(rows.GetString(1));
        bool clean;
        using(var query=database.Command("PRAGMA foreign_key_check",fixtureTransaction)) using(var rows=query.ExecuteReader()) clean=!rows.Read();
        return new(outcome,epoch,generation,stale,snapshot,header,tag,Count("device_views"),Count("device_view_entries"),Count("device_type_edges"),
            Count("resolved_device_catalog"),Count("binding_availability"),string.Join('|',reasons),error,Count("binding_wire_contracts"),
            string.Join('|',columns.Order(StringComparer.Ordinal)),Count("device_inventory_sets"),Count("device_inventory_members"),Count("device_capability_facts"),clean,Count("device_view_scopes"));
    }
    private void SeedEpochBoundary(long epoch)
    {
        // Private fixture seeding represents the state after many valid invalidations.
        // The exact trigger is restored in the SAME transaction; no artifact DDL changes.
        using var read=database.Command("SELECT sql FROM sqlite_master WHERE name='device_active_update'");var trigger=(string)read.ExecuteScalar()!;
        using var transaction=database.Connection.BeginTransaction(deferred:false);
        using(var drop=database.Command("DROP TRIGGER device_active_update",transaction)) drop.ExecuteNonQuery();
        using(var insert=database.Command("INSERT INTO device_view_invalidations VALUES($scope,$epoch,'fixture-max-epoch','fixture-time')",transaction,("$scope",store.ScopeId),("$epoch",epoch))) insert.ExecuteNonQuery();
        using(var update=database.Command("UPDATE device_view_active SET capability_epoch=$epoch WHERE scope_id=$scope",transaction,("$epoch",epoch),("$scope",store.ScopeId))) update.ExecuteNonQuery();
        using(var restore=database.Command(trigger,transaction)) restore.ExecuteNonQuery();transaction.Commit();
    }
    private void AttackNormalizedEntry(ViewCall call)
    {
        var snapshot=Element(call.Snapshot!);var row=snapshot.GetProperty("entries")[0];
        var header=snapshot.EnumerateObject().Where(property=>property.Name!="entries").ToDictionary(property=>property.Name,property=>property.Value.Clone(),StringComparer.Ordinal);
        using var transaction=database.Connection.BeginTransaction(deferred:false);
        using(var stage=database.Command("INSERT INTO device_views VALUES($view,$scope,$registry,$catalog,$overlay,$epoch,$generation,$count,$inventory,$facts,$body,'staged')",transaction,
            ("$view",snapshot.GetProperty("view_id").GetString()),("$scope",store.ScopeId),("$registry",snapshot.GetProperty("registry_id").GetString()),
            ("$catalog",snapshot.GetProperty("catalog_generation").GetInt64()),("$overlay",snapshot.GetProperty("overlay_generation").GetInt64()),("$epoch",snapshot.GetProperty("capability_epoch").GetInt64()),
            ("$generation",snapshot.GetProperty("view_generation").GetInt64()),("$count",snapshot.GetProperty("inventory_count").GetInt64()),("$inventory",snapshot.GetProperty("inventory_sha256").GetString()),
            ("$facts",snapshot.GetProperty("facts_id").GetString()),("$body",Text(JsonSerializer.SerializeToElement(header))))) stage.ExecuteNonQuery();
        using var attack=database.Command("INSERT INTO device_view_entries VALUES($view,$entry,$kind,'unknown',$reasons,$schema,$contract,$dependencies,$graph,$body)",transaction,
            ("$view",snapshot.GetProperty("view_id").GetString()),("$entry",row.GetProperty("entry_id").GetString()),("$kind",row.GetProperty("entity_kind").GetString()),
            ("$reasons",Text(row.GetProperty("reason_codes"))),("$schema",row.GetProperty("schema_hash").GetString()),("$contract",row.GetProperty("contract_hash").GetString()),
            ("$dependencies",Text(row.GetProperty("dependencies"))),("$graph",row.GetProperty("graph_digest").GetString()),("$body",Text(row)));
        attack.ExecuteNonQuery();throw new InvalidOperationException("Fixture expected the normalized-entry JSON guard to reject");
    }
    public ViewReply Execute(ViewCall call)
    {
        try
        {
            switch(call.Operation)
            {
                case "read":return Observe();
                case "status":return Observe(statusOnly:true);
                case "publish":store.Publish(Element(call.Snapshot!),Element(call.Facts!),call.ExpectedGeneration);break;
                case "invalidate":store.Invalidate(call.Reason!);break;
                case "refresh":store.RequestRefresh(call.ETag);break;
                case "reopen":store=Create(call.Inventory!,call.InventoryCount);break;
                case "sql-attack":using(var command=database.Command(call.Sql!)) command.ExecuteNonQuery();break;
                case "sql-entry-json-mismatch":AttackNormalizedEntry(call);break;
                case "begin-fixture-transaction":beforeOwnedTransaction=Observe();beforeOwnedControl=store.Control();fixtureTransaction=database.Connection.BeginTransaction(deferred:false);return beforeOwnedTransaction;
                case "rollback-fixture-transaction":fixtureTransaction!.Rollback();fixtureTransaction.Dispose();fixtureTransaction=null;break;
                default:throw new ArgumentException("Unknown fixture view operation",nameof(call));
            }
            return Observe();
        }
        catch(CatalogException error) { return Observe("precondition-"+error.Status); }
        catch(StaleDeviceViewException error) { return Observe("stale-view",error.Message); }
        catch(SqliteException error) when(error.SqliteErrorCode==19) { return Observe("constraint",error.Message); }
        catch(OverflowException error) { return Observe("overflow",error.Message); }
        catch(InvalidDataException error) { return Observe("invalid",error.Message); }
        catch(ArgumentException error) { return Observe("invalid",error.Message); }
        catch(InvalidOperationException error) when(error.Message=="Reference storage owns its transaction") { return Observe("invalid",error.Message); }
    }
    public void Dispose() { fixtureTransaction?.Dispose();database.Dispose(); }
}
[Trait("Gate","Real")]
public sealed class DeviceViewStorageRealTests
{
    [Theory,MemberData(nameof(ViewStorageCases.All),MemberType=typeof(ViewStorageCases))]
    public void NativeStorageUsesSameCasesAndAssertions(ViewCase test)
    { using var provider=new NativeViewProvider(test);ViewStorageCases.AssertCase(test,provider); }
}
