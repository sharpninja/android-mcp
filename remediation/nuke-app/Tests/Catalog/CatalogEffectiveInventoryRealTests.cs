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
        using var producer=new NativeCatalogBuildProvider{SourceValues=values=>
        {
            values[2]=("aidl","IFoo.aidl",CatalogEffectiveInventoryCases.Aidl);
            if(call.Variant=="owner-ambiguous")values[0]=(values[0].Kind,values[0].Path,values[0].Text+CatalogLocalOwnerCases.SecondOwnerSource);
        }};
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
        string ImportedRows(SqliteTransaction? active=null,string table="registry_inventory")
        {
            using var query=database.Command("SELECT entry_id,document_json,record_sha256 FROM "+table+" ORDER BY registry_id,entry_id",active);using var rows=query.ExecuteReader();var values=new List<string>();
            while(rows.Read()) values.Add(rows.GetString(0)+"\n"+rows.GetString(1)+"\n"+rows.GetString(2));return string.Join("\n",values);
        }
        string LocalRows(string table)
        {
            using var query=database.Command("SELECT * FROM "+table+" ORDER BY entry_id,revision");using var rows=query.ExecuteReader();var values=new List<object?[]>();
            while(rows.Read()){var row=new object?[rows.FieldCount];for(var index=0;index<row.Length;index++)row[index]=rows.IsDBNull(index) ? null : rows.GetValue(index);values.Add(row);}return JsonSerializer.Serialize(values);
        }
        string LocalAddition(JsonObject doc)
        {
            doc["canonical_signature"]=CatalogTombstoneCases.Signature;doc["name"]="added";doc["origin"]="local";doc["overlay_revision"]=1;
            var id=ContentHash.Sha256(Encoding.UTF8.GetBytes(doc["canonical_signature"]!.GetValue<string>()));doc["entry_id"]=id;
            doc["arguments"]=new JsonArray(new JsonObject{["text"]="int",["status"]="primitive",["codec_status"]="unknown"});return id;
        }
        void Overlay(JsonObject doc,string state="active",bool local=false,string? rawDocument=null,string? rowKey=null)
        {
            var key=rowKey ?? doc["entry_id"]!.GetValue<string>();var registry=Scalar("SELECT registry_id FROM active_registry");
            var baseHash=local ? null : Scalar("SELECT record_sha256 FROM effective_entries WHERE entry_id=$id",("$id",key));
            var document=rawDocument ?? Text(doc);
            using var transaction=database.Connection.BeginTransaction();
            using(var insert=database.Command("INSERT INTO overlays VALUES($id,$import,$signature,1,$registry,$base,$state,$doc,$time,$time)",transaction,
                ("$id",key),("$import",local ? null : key),("$signature",doc["canonical_signature"]!.GetValue<string>()),("$registry",local ? null : registry),("$base",baseHash),("$state",state),("$doc",document),("$time","2026-10-27T03:00:00Z")))insert.ExecuteNonQuery();
            using(var history=database.Command("INSERT INTO entry_history VALUES($id,1,$operation,$doc,'fixture',$time)",transaction,("$id",key),("$operation",local ? "create" : state=="suppressed" ? "suppress" : "update"),("$doc",document),("$time","2026-10-27T03:00:00Z")))history.ExecuteNonQuery();
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
        if(call.Variant is "invalid-active-schema" or "overlay-row-key-mismatch")
        {
            var local=Doc("aidl:a.I#take(P)");var rowKey=LocalAddition(local);
            if(call.Variant=="invalid-active-schema")local.Remove("permission");
            else local["entry_id"]=new string('0',64); // SHA(signature)==SQL key; only document key guard rejects.
            Overlay(local,local:true,rowKey:rowKey);
        }
        var baseline=ImportedRows();SqliteTransaction? outer=null;CatalogInventoryCapture? captured=null;
        if(call.Variant=="caller-transaction") outer=database.Connection.BeginTransaction(deferred:true);
        try
        {
            if(call.Variant is "missing-version" or "missing-projection" or "wrong-digest" or "wrong-identity" or "caller-transaction" or "invalid-overlay-identity" or "missing-structured-shape" or "invalid-active-schema" or "overlay-row-key-mismatch")
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
                // The original suppression test omits current_document_hashes.
                // Matching-pin and suppressed-type cases explicitly supply it.
                var hashes=call.Variant=="suppressed-binding" ? null : pins.DocumentHashes;
                return CatalogViewPolicy.Evaluate([new(key,schema,"platform:local",1)],new(snapshot,new(null,false,0,1,"fixture")),new Dictionary<string,PolicyRule>{{key,rule}},entry,"tool",authority,hashes,resolver,canonical);
            }
            if(call.Variant.StartsWith("structured-",StringComparison.Ordinal))
            {
                var variant=call.Variant["structured-".Length..];var parent=Doc("aidl:a.I.P");var field=Doc("aidl:a.I.P#x");
                var parentId=parent["entry_id"]!.GetValue<string>();var fieldId=field["entry_id"]!.GetValue<string>();
                var parentBefore=(JsonObject)parent.DeepClone();var fieldBefore=(JsonObject)field.DeepClone();
                var hasParent=variant is "parent-field" or "parent-only";var hasField=variant!="parent-only";
                if(hasParent)parent["extensions"]!["annotations"]=new JsonArray(new JsonObject{["name"]=CatalogStructuredOverlayCases.ParentAnnotation,["arguments"]=""});
                if(hasField)
                {
                    field["return_type"]!["text"]=variant=="constant-only" ? "int" : "long";
                    field["extensions"]!["constant_raw"]=variant=="constant-only" ? CatalogStructuredOverlayCases.Constant : null;
                }
                // Expectations are authored from retained input documents before
                // capture. The counterfactual drops the local parent's wire
                // annotation (or the field-only change), so equality cannot
                // accidentally accept the imported-parent fallback.
                var expectedParent=(JsonObject)parent.DeepClone();var counterfactual=(JsonObject)parentBefore.DeepClone();
                if(hasField)
                {
                    var expectedField=expectedParent["extensions"]!["declared_type_shape"]!["fields"]!.AsArray().OfType<JsonObject>().Single(value=>value["name"]!.GetValue<string>()=="x");
                    expectedField["type"]=field["return_type"]!.DeepClone();expectedField["constant"]=field["extensions"]!["constant_raw"]?.DeepClone();
                    if(hasParent)
                    {
                        var wrongField=counterfactual["extensions"]!["declared_type_shape"]!["fields"]!.AsArray().OfType<JsonObject>().Single(value=>value["name"]!.GetValue<string>()=="x");
                        wrongField["type"]=field["return_type"]!.DeepClone();wrongField["constant"]=field["extensions"]!["constant_raw"]?.DeepClone();
                    }
                }
                var contracts=new CatalogContracts(canonical);string Contract(JsonObject document)=>contracts.Digest(JsonSerializer.SerializeToElement(document));
                var expectedParentHash=Contract(expectedParent);var wrongParentHash=Contract(counterfactual);Assert.NotEqual(expectedParentHash,wrongParentHash);
                var expectedFieldHash=Contract(hasField ? field : fieldBefore);
                var expectedParentDocument=hasParent ? ContentHash.Sha256(Encoding.UTF8.GetBytes(Text(parent))) : before.DocumentHashes[parentId];
                var expectedFieldDocument=hasField ? ContentHash.Sha256(Encoding.UTF8.GetBytes(Text(field))) : before.DocumentHashes[fieldId];
                var historicalId=ContentHash.Sha256(Encoding.UTF8.GetBytes(CatalogStructuredOverlayCases.HistoricalSignature));
                if(variant=="historical-parent")Execute("INSERT INTO bindings SELECT $id,$signature,package,owner,kind,name,return_type,arguments_json,raw_signature,codec_status FROM bindings WHERE binding_id=$parent",
                    ("$id",historicalId),("$signature",CatalogStructuredOverlayCases.HistoricalSignature),("$parent",parentId));
                if(hasParent)Overlay(parent);if(hasField)Overlay(field);
                var overlays=LocalRows("overlays");var history=LocalRows("entry_history");var importedEntries=ImportedRows(table:"registry_entries");
                captured=inventory.Capture();var resolved=Resolve(captured);
                JsonElement Item(CatalogInventoryCapture value,string id)=>value.Inventory.Single(row=>row.GetProperty("entry_id").GetString()==id);
                var parentItem=Item(captured,parentId);var fieldItem=Item(captured,fieldId);var methodItem=Item(captured,key);var methodBefore=Item(before,key);
                var parents=parentItem.GetProperty("variants").EnumerateArray().ToArray();var fields=fieldItem.GetProperty("variants").EnumerateArray().ToArray();
                var expectedDependencies=methodBefore.GetProperty("variants").EnumerateArray().Select(value=>
                {
                    var dependencies=JsonNode.Parse(value.GetProperty("dependencies").GetRawText())!.AsArray();
                    Assert.Contains(dependencies.OfType<JsonObject>(),dependency=>dependency["entry_id"]!.GetValue<string>()==parentId);
                    foreach(var dependency in dependencies.OfType<JsonObject>().Where(value=>value["entry_id"]!.GetValue<string>()==parentId))dependency["schema_hash"]=expectedParentHash;
                    return Text(dependencies);
                }).ToArray();
                var ids=captured.Inventory.Select(value=>value.GetProperty("entry_id").GetString()!).ToArray();
                var bytes=Encoding.UTF8.GetBytes(string.Concat(captured.Inventory.OrderBy(value=>value.GetProperty("entry_id").GetString(),StringComparer.Ordinal).Select(value=>Encoding.UTF8.GetString(canonical.Serialize(value))+"\n")));
                var historicalPresent=Scalar("SELECT CAST(count(*) AS TEXT) FROM bindings WHERE binding_id=$id",("$id",historicalId))=="1";
                var historicalHasEntry=Scalar("SELECT CAST(count(*) AS TEXT) FROM registry_entries WHERE entry_id=$id",("$id",historicalId))!="0";
                var observation=new CatalogStructuredOverlayObservation(
                    parents.Length==Item(before,parentId).GetProperty("variants").GetArrayLength() && parents.All(value=>value.GetProperty("schema_hash").GetString()==expectedParentHash),
                    parents.All(value=>value.GetProperty("schema_hash").GetString()!=wrongParentHash),
                    fields.Length==Item(before,fieldId).GetProperty("variants").GetArrayLength() && fields.All(value=>value.GetProperty("schema_hash").GetString()==expectedFieldHash),
                    parentItem.GetProperty("catalog_document_sha256").GetString()==expectedParentDocument && captured.DocumentHashes[parentId]==expectedParentDocument,
                    fieldItem.GetProperty("catalog_document_sha256").GetString()==expectedFieldDocument && captured.DocumentHashes[fieldId]==expectedFieldDocument,
                    parentItem.GetProperty("catalog_state").GetString()=="active" && parentItem.GetProperty("catalog_revision").GetInt64()==(hasParent ? 1 : 0),
                    fieldItem.GetProperty("catalog_state").GetString()=="active" && fieldItem.GetProperty("catalog_revision").GetInt64()==(hasField ? 1 : 0),
                    expectedDependencies.SequenceEqual(methodItem.GetProperty("variants").EnumerateArray().Select(value=>Text(JsonNode.Parse(value.GetProperty("dependencies").GetRawText())!)),StringComparer.Ordinal),
                    parents.All(value=>value.GetProperty("dependencies").GetArrayLength()==0),
                    historicalPresent==(variant=="historical-parent") && !historicalHasEntry && !ids.Contains(historicalId,StringComparer.Ordinal),
                    overlays==LocalRows("overlays") && history==LocalRows("entry_history"),importedEntries==ImportedRows(table:"registry_entries"),ImportedRows()==baseline,
                    captured.Inventory.Count==before.Inventory.Count && before.Inventory.All(value=>ids.Contains(value.GetProperty("entry_id").GetString()!,StringComparer.Ordinal)),
                    captured.RegistryId==before.RegistryId && captured.CatalogGeneration==before.CatalogGeneration && captured.BaseInventoryCount==before.BaseInventoryCount,
                    captured.OverlayGeneration-before.OverlayGeneration,captured.InventorySha256==ContentHash.Sha256(bytes),ids.SequenceEqual(ids.Order(StringComparer.Ordinal)),
                    Row(resolved,key).GetProperty("state").GetString()!,Admit(resolved,captured,true),Admit(resolved,captured,false)=="os-unavailable");
                using(var check=database.Connection.BeginTransaction(deferred:true))check.Rollback();
                return new("ok",ImportedInventoryUnchanged:ImportedRows()==baseline,InputsRetained:built.OriginalInputsRetained && FileHash(original)==originalHash,Structured:observation);
            }
            string? admission=null,noAuthority=null,changedAdmission=null;bool? schemaChanged=null,documentChanged=null,hashChanged=null;long? overlayDelta=null;string? dependencyState=null;
            if(call.Variant=="suppressed-binding") Overlay(method,"suppressed");
            var tombstone=call.Variant.StartsWith("tombstone-",StringComparison.Ordinal);
            var localOwner=call.Variant.StartsWith("owner-",StringComparison.Ordinal);
            if(call.Variant=="local-addition" || tombstone || localOwner)
            {
                key=LocalAddition(method);
                if(call.Variant=="owner-absent")method["owner"]="a.Missing";
                var state=tombstone ? call.Variant.Split('-')[1] : "active";
                Overlay(method,state,local:true,rawDocument:tombstone ? CatalogTombstoneCases.Document(call).Raw : null);
            }
            if(tombstone || localOwner)
            {
                var overlays=LocalRows("overlays");var history=LocalRows("entry_history");
                var expectedHash=(tombstone ? CatalogTombstoneCases.Document(call).CanonicalHash : null) ?? ContentHash.Sha256(Encoding.UTF8.GetBytes(Text(method)));
                captured=inventory.Capture();var tombstoneSnapshot=Resolve(captured);var tombstoneRow=Row(tombstoneSnapshot,key);
                var item=captured.Inventory.Single(row=>row.GetProperty("entry_id").GetString()==key);
                var keys=captured.Inventory.Select(row=>row.GetProperty("entry_id").GetString()!).ToArray();
                var digestBytes=Encoding.UTF8.GetBytes(string.Concat(captured.Inventory.OrderBy(row=>row.GetProperty("entry_id").GetString(),StringComparer.Ordinal).Select(row=>Encoding.UTF8.GetString(canonical.Serialize(row))+"\n")));
                if(localOwner)
                {
                    var variant=item.GetProperty("variants").EnumerateArray().Single();var dependencies=variant.GetProperty("dependencies").EnumerateArray().ToArray();
                    using var ownerQuery=database.Command("SELECT binding_id FROM bindings WHERE owner=$owner",null,("$owner",method["owner"]!.GetValue<string>()));
                    using var ownerRows=ownerQuery.ExecuteReader();var owners=new HashSet<string>(StringComparer.Ordinal);
                    while(ownerRows.Read()){var owner=ownerRows.GetString(0);if(captured.Inventory.Any(value=>value.GetProperty("entry_id").GetString()==owner && value.GetProperty("entity_kind").GetString()=="type"))owners.Add(owner);}
                    var contracts=new CatalogContracts(canonical);var contract=contracts.Digest(JsonSerializer.SerializeToElement(method));
                    var localHash=ContentHash.Sha256(Encoding.UTF8.GetBytes(Text(method)));
                    var expectedOwnerHash=contracts.Digest(JsonSerializer.SerializeToElement(Doc("aidl:a.I")));
                    var ownerObservation=new CatalogLocalOwnerObservation(item.GetProperty("entity_kind").GetString()!,item.GetProperty("variants").GetArrayLength(),variant.GetProperty("availability").GetProperty("unknown_reason").GetString()!,owners.Count,dependencies.Length,
                        string.Join(";",dependencies.Select(value=>value.GetProperty("entry_id").GetString()).Order(StringComparer.Ordinal)),variant.GetProperty("schema_hash").GetString()==contract,
                        dependencies.All(value=>value.GetProperty("schema_hash").GetString()==expectedOwnerHash),
                        item.GetProperty("catalog_document_sha256").GetString()==localHash && captured.DocumentHashes.GetValueOrDefault(key)==localHash && tombstoneRow.GetProperty("catalog_document_sha256").GetString()==localHash && tombstoneRow.GetProperty("catalog_revision").GetInt64()==1 && tombstoneRow.GetProperty("catalog_state").GetString()=="active",
                        tombstoneRow.GetProperty("schema_hash").ValueKind==JsonValueKind.Null,tombstoneRow.GetProperty("contract_hash").ValueKind==JsonValueKind.Null,tombstoneRow.GetProperty("graph_digest").ValueKind==JsonValueKind.Null,
                        string.Join(";",tombstoneRow.GetProperty("reason_codes").EnumerateArray().Select(value=>value.GetString())),keys.SequenceEqual(keys.Order(StringComparer.Ordinal)),
                        before.Inventory.All(row=>captured.Inventory.Any(value=>canonical.Serialize(value).AsSpan().SequenceEqual(canonical.Serialize(row)))),
                        LocalRows("overlays")==overlays && LocalRows("entry_history")==history && captured.CatalogGeneration==before.CatalogGeneration && long.Parse(Scalar("SELECT CAST(overlay_generation AS TEXT) FROM catalog_view WHERE singleton=1"),System.Globalization.CultureInfo.InvariantCulture)==captured.OverlayGeneration);
                    var targets=new HashSet<string>(["platform:local"],StringComparer.Ordinal);var allow=new PolicyRule("allow",contract,targets,1);var entryRule=new PolicyRule("allow","tool",targets,1);
                    var decision=CatalogViewPolicy.Evaluate([new(key,contract,"platform:local",1)],new(tombstoneSnapshot,new(null,false,0,1,"fixture")),new Dictionary<string,PolicyRule>{{key,allow}},entryRule,"tool",true,captured.DocumentHashes,resolver,canonical);
                    using(var check=database.Connection.BeginTransaction(deferred:true))check.Rollback();
                    return new("ok",captured.Inventory.Count-captured.BaseInventoryCount,before.Inventory.All(row=>keys.Contains(row.GetProperty("entry_id").GetString()!,StringComparer.Ordinal)),tombstoneRow.GetProperty("state").GetString(),item.GetProperty("catalog_state").GetString(),Admission:decision,
                        InventoryHashChanged:captured.InventorySha256!=before.InventorySha256,OverlayGenerationDelta:captured.OverlayGeneration-before.OverlayGeneration,ImportedInventoryUnchanged:ImportedRows()==baseline,InputsRetained:built.OriginalInputsRetained && FileHash(original)==originalHash,LocalOwner:ownerObservation);
                }
                var observation=new CatalogTombstoneObservation(item.GetProperty("entry_id").GetString()!,item.GetProperty("entity_kind").GetString()!,item.GetProperty("variants").GetArrayLength(),item.GetProperty("catalog_revision").GetInt64(),
                    string.Join(";",tombstoneRow.GetProperty("reason_codes").EnumerateArray().Select(reason=>reason.GetString())),
                    tombstoneRow.GetProperty("schema_hash").ValueKind==JsonValueKind.Null,tombstoneRow.GetProperty("contract_hash").ValueKind==JsonValueKind.Null,tombstoneRow.GetProperty("graph_digest").ValueKind==JsonValueKind.Null,tombstoneRow.GetProperty("dependencies").GetArrayLength(),
                    item.GetProperty("catalog_document_sha256").GetString()==expectedHash && captured.DocumentHashes.GetValueOrDefault(key)==expectedHash,
                    tombstoneRow.GetProperty("entry_id").GetString()==key && tombstoneRow.GetProperty("catalog_document_sha256").GetString()==expectedHash && tombstoneRow.GetProperty("catalog_revision").GetInt64()==1 && tombstoneRow.GetProperty("catalog_state").GetString()==call.Variant.Split('-')[1],
                    captured.InventorySha256==ContentHash.Sha256(digestBytes),keys.SequenceEqual(keys.Order(StringComparer.Ordinal)),
                    before.Inventory.All(row=>captured.Inventory.Any(value=>canonical.Serialize(value).AsSpan().SequenceEqual(canonical.Serialize(row)))),
                    LocalRows("overlays")==overlays,LocalRows("entry_history")==history,captured.CatalogGeneration-before.CatalogGeneration,
                    long.Parse(Scalar("SELECT CAST(overlay_generation AS TEXT) FROM catalog_view WHERE singleton=1"),System.Globalization.CultureInfo.InvariantCulture)-captured.OverlayGeneration);
                using(var check=database.Connection.BeginTransaction(deferred:true))check.Rollback();
                return new("ok",captured.Inventory.Count-captured.BaseInventoryCount,before.Inventory.All(row=>keys.Contains(row.GetProperty("entry_id").GetString()!,StringComparer.Ordinal)),tombstoneRow.GetProperty("state").GetString(),item.GetProperty("catalog_state").GetString(),
                    InventoryHashChanged:captured.InventorySha256!=before.InventorySha256,OverlayGenerationDelta:captured.OverlayGeneration-before.OverlayGeneration,
                    ImportedInventoryUnchanged:ImportedRows()==baseline,InputsRetained:built.OriginalInputsRetained && FileHash(original)==originalHash,Tombstone:observation);
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
