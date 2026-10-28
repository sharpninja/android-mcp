using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogEffectiveInventoryProvider:ICatalogEffectiveInventoryCaseProvider
{
    public Task<CatalogEffectiveInventoryReply> CaptureAsync(CatalogEffectiveInventoryCall call)=>Task.FromResult(Capture(call));
    private static CatalogEffectiveInventoryReply Capture(CatalogEffectiveInventoryCall call)
    {
        using var producer=new NativeCatalogBuildProvider{SourceValues=values=>values[2]=("aidl","IFoo.aidl",CatalogEffectiveInventoryCases.Aidl)};
        var built=producer.Build(new("effective-capture-fixture"));Assert.Equal("ok",built.Outcome);
        var original=Path.Combine(producer.EvidenceRoot,"out/catalog.sqlite");var path=Path.Combine(producer.EvidenceRoot,"capture-fixture.sqlite");
        File.Copy(original,path);var originalHash=FileHash(original);Assert.Equal(originalHash,FileHash(path));
        using var database=CatalogDatabase.OpenExisting(path,false);var canonical=new CanonicalJson();var root=NativeSchemaProvider.Root();
        // The preserved device_resolution.validator uses DeviceView for the
        // effective catalog-state/document-pin shape. DeviceResolution is the
        // historical immutable inventory contract and rejects those fields.
        var viewSchema=Path.Combine(root,"contracts/DeviceView.schema.json");
        Assert.Equal("706d34871610f7e97ed0a00edd63bfb8961d39747a705835ed0b942ff27f807f",ContentHash.Sha256(File.ReadAllBytes(viewSchema)));
        var resolver=new DeviceResolver(canonical,viewSchema);
        var inventory=new CatalogInventory(database,new CatalogContracts(canonical),resolver,canonical,Path.Combine(root,"contracts/Entry.schema.json"));
        void Execute(string sql,params (string Name,object? Value)[] args){using var command=database.Command(sql,null,args);command.ExecuteNonQuery();}
        string Scalar(string sql,params (string Name,object? Value)[] args){using var query=database.Command(sql,null,args);return (string)query.ExecuteScalar()!;}
        JsonObject Doc(string signature)=>JsonNode.Parse(Scalar("SELECT document_json FROM effective_entries WHERE json_extract(document_json,'$.canonical_signature')=$signature",("$signature",signature)))!.AsObject();
        string Text(JsonNode value)=>Encoding.UTF8.GetString(canonical.Serialize(JsonSerializer.SerializeToElement(value)));
        string ImportedRows(SqliteTransaction? active=null)
        {
            using var query=database.Command("SELECT entry_id,document_json,record_sha256 FROM registry_inventory ORDER BY registry_id,entry_id",active);using var rows=query.ExecuteReader();var values=new List<string>();
            while(rows.Read()) values.Add(rows.GetString(0)+"\n"+rows.GetString(1)+"\n"+rows.GetString(2));return string.Join("\n",values);
        }
        void Overlay(JsonObject doc,string state="active",bool local=false)
        {
            var key=doc["entry_id"]!.GetValue<string>();var registry=Scalar("SELECT registry_id FROM active_registry");
            var baseHash=local ? null : Scalar("SELECT record_sha256 FROM effective_entries WHERE entry_id=$id",("$id",key));
            using var transaction=database.Connection.BeginTransaction();
            using(var insert=database.Command("INSERT INTO overlays VALUES($id,$import,$signature,1,$registry,$base,$state,$doc,$time,$time)",transaction,
                ("$id",key),("$import",local ? null : key),("$signature",doc["canonical_signature"]!.GetValue<string>()),("$registry",local ? null : registry),("$base",baseHash),("$state",state),("$doc",Text(doc)),("$time","2026-10-27T03:00:00Z")))insert.ExecuteNonQuery();
            using(var history=database.Command("INSERT INTO entry_history VALUES($id,1,$operation,$doc,'fixture',$time)",transaction,("$id",key),("$operation",local ? "create" : state=="suppressed" ? "suppress" : "update"),("$doc",Text(doc)),("$time","2026-10-27T03:00:00Z")))history.ExecuteNonQuery();
            transaction.Commit();
        }
        void RewriteImported(string signature,Action<JsonObject> mutate)
        {
            var doc=Doc(signature);mutate(doc);Execute("DROP TRIGGER registry_entries_no_update");var raw=Text(doc);
            Execute("UPDATE registry_entries SET document_json=$raw,record_sha256=$hash WHERE entry_id=$id",("$raw",raw),("$hash",ContentHash.Sha256(Encoding.UTF8.GetBytes(raw))),("$id",doc["entry_id"]!.GetValue<string>()));
        }
        if(call.Variant=="missing-version") Execute("DELETE FROM catalog_meta WHERE key='device_resolution_contract_version'");
        if(call.Variant=="missing-projection"){Execute("DROP TRIGGER registry_inventory_no_delete");Execute("DELETE FROM registry_inventory WHERE entry_id=(SELECT min(entry_id) FROM registry_inventory)");}
        if(call.Variant is "wrong-digest" or "wrong-identity")
        {
            Execute("DROP TRIGGER registry_inventory_no_update");var id=Scalar("SELECT min(entry_id) FROM registry_inventory");
            if(call.Variant=="wrong-digest") Execute("UPDATE registry_inventory SET record_sha256=$hash WHERE entry_id=$id",("$hash",new string('0',64)),("$id",id));
            else
            {
                var doc=JsonNode.Parse(Scalar("SELECT document_json FROM registry_inventory WHERE entry_id=$id",("$id",id)))!;doc["entry_id"]=new string('e',64);var raw=Text(doc);
                Execute("UPDATE registry_inventory SET document_json=$raw,record_sha256=$hash WHERE entry_id=$id",("$raw",raw),("$hash",ContentHash.Sha256(Encoding.UTF8.GetBytes(raw))),("$id",id));
            }
        }
        if(call.Variant=="missing-structured-shape")
        {RewriteImported("aidl:a.I.P",doc=>doc["extensions"]!.AsObject().Remove("declared_type_shape"));var field=Doc("aidl:a.I.P#x");field["return_type"]!["text"]="long";Overlay(field);}
        if(call.Variant=="invalid-overlay-identity")
        {
            var local=Doc("aidl:a.I#take(P)");local["origin"]="local";local["overlay_revision"]=1;local["canonical_signature"]="aidl:a.I#added(int)";
            local["entry_id"]=ContentHash.Sha256(Encoding.UTF8.GetBytes("aidl:a.I#other(int)"));Overlay(local,local:true);
        }
        var baseline=ImportedRows();SqliteTransaction? outer=null;CatalogInventoryCapture? captured=null;
        if(call.Variant=="caller-transaction") outer=database.Connection.BeginTransaction(deferred:true);
        try
        {
            if(call.Variant is "missing-version" or "missing-projection" or "wrong-digest" or "wrong-identity" or "caller-transaction" or "invalid-overlay-identity" or "missing-structured-shape")
            {
                try{captured=inventory.Capture();throw new InvalidOperationException("Expected checked capture failure");}
                catch(Exception error) when(error is InvalidDataException or InvalidOperationException)
                {
                    bool closed;
                    if(outer is null){using var check=database.Connection.BeginTransaction(deferred:true);check.Rollback();closed=true;}
                    else{Assert.IsType<InvalidOperationException>(Record.Exception(()=>database.Connection.BeginTransaction(deferred:true)));closed=false;}
                    var reply=new CatalogEffectiveInventoryReply(error.Message,ImportedInventoryUnchanged:ImportedRows(outer)==baseline,TransactionClosed:closed,InputsRetained:built.OriginalInputsRetained && FileHash(original)==originalHash,NoPartialCapture:captured is null);
                    return reply;
                }
            }
            var before=inventory.Capture();var facts=ResolverCases.Facts();facts["platform"]=new JsonObject{["major"]=37,["minor"]=0};
            JsonElement Resolve(CatalogInventoryCapture capture)=>resolver.Resolve(capture.Inventory,JsonSerializer.SerializeToElement(facts),new(capture.RegistryId,capture.CatalogGeneration,capture.OverlayGeneration,0,1));
            JsonElement Row(JsonElement snapshot,string key)=>snapshot.GetProperty("entries").EnumerateArray().Single(row=>row.GetProperty("entry_id").GetString()==key);
            var method=Doc("aidl:a.I#take(P)");var key=method["entry_id"]!.GetValue<string>();var old=Resolve(before);var oldRow=Row(old,key);
            string Admit(JsonElement snapshot,CatalogInventoryCapture pins,bool authority)
            {
                var row=Row(snapshot,key);var schema=row.GetProperty("schema_hash").GetString()!;var targets=new HashSet<string>(["platform:local"],StringComparer.Ordinal);
                var rule=new PolicyRule("allow",schema,targets,1);var entry=new PolicyRule("allow","tool",targets,1);
                return CatalogViewPolicy.Evaluate([new(key,schema,"platform:local",1)],new(snapshot,new(null,false,0,1,"fixture")),new Dictionary<string,PolicyRule>{{key,rule}},entry,"tool",authority,pins.DocumentHashes,resolver,canonical);
            }
            string? admission=null,noAuthority=null,changedAdmission=null;bool? schemaChanged=null,documentChanged=null,hashChanged=null;long? overlayDelta=null;string? dependencyState=null;
            if(call.Variant=="suppressed-binding") Overlay(method,"suppressed");
            if(call.Variant=="local-addition")
            {
                method["canonical_signature"]="aidl:a.I#added(int)";method["name"]="added";method["origin"]="local";method["overlay_revision"]=1;
                key=ContentHash.Sha256(Encoding.UTF8.GetBytes(method["canonical_signature"]!.GetValue<string>()));method["entry_id"]=key;
                method["arguments"]=new JsonArray(new JsonObject{["text"]="int",["status"]="primitive",["codec_status"]="unknown"});Overlay(method,local:true);
            }
            if(call.Variant=="field-override"){var field=Doc("aidl:a.I.P#x");field["return_type"]!["text"]="long";Overlay(field);}
            if(call.Variant=="document-pin")
            {admission=Admit(old,before,true);noAuthority=Admit(old,before,false);method["unresolved"]!.AsArray().Add("New metadata evidence");Overlay(method);}
            if(call.Variant=="suppressed-type") Overlay(Doc("aidl:a.I.P"),"suppressed");
            captured=inventory.Capture();var snapshot=Resolve(captured);var current=Row(snapshot,key);
            if(call.Variant is "suppressed-binding" or "suppressed-type") admission=Admit(snapshot,captured,true);
            if(call.Variant=="field-override"){schemaChanged=oldRow.GetProperty("schema_hash").GetString()!=current.GetProperty("schema_hash").GetString();hashChanged=before.InventorySha256!=captured.InventorySha256;overlayDelta=captured.OverlayGeneration-before.OverlayGeneration;}
            if(call.Variant=="document-pin"){schemaChanged=oldRow.GetProperty("schema_hash").GetString()!=current.GetProperty("schema_hash").GetString();documentChanged=oldRow.GetProperty("catalog_document_sha256").GetString()!=current.GetProperty("catalog_document_sha256").GetString();changedAdmission=Admit(old,captured,true);}
            if(call.Variant=="suppressed-type") dependencyState=Row(snapshot,Doc("aidl:a.I.P")["entry_id"]!.GetValue<string>()).GetProperty("catalog_state").GetString();
            var membership=before.Inventory.All(item=>captured.Inventory.Any(value=>value.GetProperty("entry_id").GetString()==item.GetProperty("entry_id").GetString()));
            using(var check=database.Connection.BeginTransaction(deferred:true))check.Rollback();
            return new("ok",captured.Inventory.Count-captured.BaseInventoryCount,membership,current.GetProperty("state").GetString(),current.GetProperty("catalog_state").GetString(),admission,noAuthority,changedAdmission,schemaChanged,documentChanged,hashChanged,overlayDelta,dependencyState,ImportedRows()==baseline,true,built.OriginalInputsRetained && FileHash(original)==originalHash,true);
        }
        finally{outer?.Rollback();outer?.Dispose();}
    }
    private static string FileHash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogEffectiveInventoryRealTests
{
    [Theory,MemberData(nameof(CatalogEffectiveInventoryCases.All),MemberType=typeof(CatalogEffectiveInventoryCases))]
    public Task NativeCheckedCaptureUsesSameTwelveAssertions(CatalogEffectiveInventoryCase test)=>CatalogEffectiveInventoryCases.AssertCase(test,new NativeCatalogEffectiveInventoryProvider());
}
