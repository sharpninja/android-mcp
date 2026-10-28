using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record ViewCall(string Operation="read",JsonObject? Snapshot=null,JsonObject? Facts=null,long ExpectedGeneration=0,
    string? Reason=null,string? ETag=null,string? Sql=null,JsonObject[]? Inventory=null,object? InventoryCount=null);
public sealed record ViewReply(string Outcome,long Epoch,long Generation,bool Stale,string? Snapshot,string? Header,string ETag,
    int Views,int Members,int Edges,int Resolved,int BindingProjection,string Reasons,string? Error=null,int WireContracts=0,
    string WireEvidenceColumns="flags|interface_hash|interface_version|oneway|proof_source_json|proof_state",int InventorySets=1,int InventoryMembers=2,
    int FactsRows=0,bool ForeignKeysClean=true,int ScopeRows=1);
public sealed record ViewCase(string Id,ViewCall[] Calls,ViewReply[] Expected,string ScopeProfile="fixture",long FixtureEpoch=0,JsonObject[]? TrustedInventory=null);
public interface IViewCaseProvider:IDisposable { ViewReply Execute(ViewCall call); }

public static class ViewStorageCases
{
    public static string Hash(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string H(char value)=>ResolverCases.H(value);
    // Independent literal sorted compact fixtures; never produced by DeviceResolver.
    public const string FactsText="{\"android_user_id\":0,\"backend_instance_id\":\"backend-fixture\",\"build_fingerprint\":\"fixture-build\",\"capability_epoch\":0,\"device_instance_id\":\"boot-fixture\",\"evidence_ids\":[\"fixture-observation\"],\"extensions\":{\"30\":4},\"identity_verified\":true,\"interfaces\":{\"a.I\":{\"authoritative\":true,\"hash\":\"interface-hash-v2\",\"state\":\"reachable\",\"version\":2}},\"modules\":{\"m\":{\"authoritative\":true,\"state\":\"present\",\"version\":\"12\"}},\"observed_at\":\"2026-10-27T01:00:00Z\",\"oem\":\"fixture-oem\",\"platform\":{\"major\":37,\"minor\":1},\"profile_id\":\"fixture\",\"target_key\":\"platform:local\"}";
    public static string FactsBody(long epoch=0,string target="platform:local",string profile="fixture")=>FactsText.Replace("\"capability_epoch\":0","\"capability_epoch\":"+epoch,StringComparison.Ordinal)
        .Replace("\"target_key\":\"platform:local\"","\"target_key\":\""+target+"\"",StringComparison.Ordinal)
        .Replace("\"profile_id\":\"fixture\"","\"profile_id\":\""+profile+"\"",StringComparison.Ordinal);
    public static JsonObject Facts(long epoch=0,string target="platform:local",string profile="fixture")=>JsonNode.Parse(FactsBody(epoch,target,profile))!.AsObject();
    public static string InventoryEntity(char key,string kind,char schema,bool dependency)=>
        "{\"catalog_document_sha256\":\""+ResolverCases.DocumentHash(key,schema)+"\",\"entity_kind\":\""+kind+"\",\"entry_id\":\""+H(key)+
        "\",\"variants\":[{\"availability\":{\"alternatives\":[{\"platform\":{\"introduced\":[37,0],\"removed\":null}}],\"evidence_ids\":[\"fixture-source\"]},\"dependencies\":["+
        (dependency ? "{\"entry_id\":\""+H('b')+"\",\"schema_hash\":\""+H('b')+"\"}" : "")+"],\"schema_hash\":\""+H(schema)+"\"}]}";
    public static JsonObject[] Inventory()=>[JsonNode.Parse(InventoryEntity('a',"binding",'a',true))!.AsObject(),JsonNode.Parse(InventoryEntity('b',"type",'b',false))!.AsObject()];
    public static string InventoryHash=>Hash(InventoryEntity('a',"binding",'a',true)+"\n"+InventoryEntity('b',"type",'b',false)+"\n");
    public static string UnknownEntity=>InventoryEntity('a',"binding",'a',true).Replace(
        "{\"alternatives\":[{\"platform\":{\"introduced\":[37,0],\"removed\":null}}],\"evidence_ids\":[\"fixture-source\"]}",
        "{\"unknown_reason\":\"OEM lifecycle not acquired\",\"x-raw\":{\"vendor\":1}}",StringComparison.Ordinal);
    public static JsonObject[] UnknownInventory()=>[JsonNode.Parse(UnknownEntity)!.AsObject(),JsonNode.Parse(InventoryEntity('b',"type",'b',false))!.AsObject()];
    public static string UnknownInventoryHash=>Hash(UnknownEntity+"\n"+InventoryEntity('b',"type",'b',false)+"\n");
    public static string ScopeHash(string profile="fixture")=>Hash("[\""+profile+"\",\"platform:local\",0,\"backend-fixture\",\"boot-fixture\"]");
    private static string Row(char key,string kind,char contract,string graph,string schema,bool dependency)=>
        "{\"catalog_document_sha256\":\""+ResolverCases.DocumentHash(key,contract)+"\",\"catalog_revision\":0,\"catalog_state\":\"active\",\"contract_hash\":\""+H(contract)+
        "\",\"dependencies\":["+(dependency ? "{\"contract_hash\":\""+H('b')+"\",\"entry_id\":\""+H('b')+"\"}" : "")+
        "],\"entity_kind\":\""+kind+"\",\"entry_id\":\""+H(key)+"\",\"graph_digest\":\""+graph+
        "\",\"reason_codes\":[\"APPLICABLE_EVIDENCE\"],\"schema_hash\":\""+schema+"\",\"state\":\"available\"}";
    private static string Body(long epoch,long generation,string target,bool entries,string profile="fixture",bool partial=false)=>
        "{\"android_user_id\":0,\"backend_instance_id\":\"backend-fixture\",\"capability_epoch\":"+epoch+",\"catalog_generation\":1,\"device_instance_id\":\"boot-fixture\","+
        (entries ? "\"entries\":["+(partial ? Row('a',"binding",'a',ResolverCases.GraphAA,ResolverCases.SchemaAA,false) :
            Row('a',"binding",'a',ResolverCases.GraphAB,ResolverCases.SchemaA_AB,true)+","+Row('b',"type",'b',ResolverCases.GraphBB,ResolverCases.SchemaBB,false))+"]," : "")+
        "\"facts_id\":\""+Hash(FactsBody(epoch,target,profile))+"\",\"inventory_count\":"+(partial ? 1 : 2)+",\"inventory_sha256\":\""+
        (partial ? Hash(InventoryEntity('a',"binding",'a',false)+"\n") : InventoryHash)+
        "\",\"overlay_generation\":0,\"profile_id\":\""+profile+"\",\"registry_id\":\""+H('f')+"\",\"schema_version\":\"3.0.0\",\"target_key\":\""+target+"\",\"view_generation\":"+generation+"}";
    public static string SnapshotBody(long epoch=0,long generation=1,string target="platform:local",bool header=false,string profile="fixture",bool partial=false)
    {
        var full=Body(epoch,generation,target,true,profile,partial);var body=header ? Body(epoch,generation,target,false,profile,partial) : full;
        return body[..^1]+",\"view_id\":\""+Hash(full)+"\"}";
    }
    public static JsonObject Snapshot(long epoch=0,long generation=1,string target="platform:local",string profile="fixture",bool partial=false)=>JsonNode.Parse(SnapshotBody(epoch,generation,target,profile:profile,partial:partial))!.AsObject();
    public static string UnknownSnapshotBody(bool header=false)
    {
        var row="{\"catalog_document_sha256\":\""+ResolverCases.DocumentHash('a')+
            "\",\"catalog_revision\":0,\"catalog_state\":\"active\",\"contract_hash\":null,\"dependencies\":[],\"entity_kind\":\"binding\",\"entry_id\":\""+H('a')+
            "\",\"graph_digest\":null,\"reason_codes\":[\"APPLICABILITY_UNKNOWN\"],\"schema_hash\":null,\"state\":\"unknown\"}";
        string Selected(bool entries)=>Body(0,1,"platform:local",entries).Replace(InventoryHash,UnknownInventoryHash,StringComparison.Ordinal)
            .Replace(Row('a',"binding",'a',ResolverCases.GraphAB,ResolverCases.SchemaA_AB,true),row,StringComparison.Ordinal);
        var full=Selected(true);var body=header ? Selected(false) : full;return body[..^1]+",\"view_id\":\""+Hash(full)+"\"}";
    }
    public static string Tag(long epoch=0,long generation=0,string profile="fixture",string? inventoryHash=null)=>$"\"dv2:{ScopeHash(profile)}:{generation}:{epoch}:1:0:{inventoryHash ?? InventoryHash}\"";
    public static ViewReply State(long epoch=0,long generation=0,bool stale=true,string? active=null,string reasons="startup",bool status=false,string outcome="ok",string? error=null)=>
        new(outcome,epoch,generation,stale,status ? null : active,active is null ? null : SnapshotBody(epoch,generation,header:true),
            Tag(epoch,generation),active is null ? 0 : 1,active is null ? 0 : 2,active is null ? 0 : 1,active is null || stale ? 0 : 2,active is null || stale ? 0 : 1,reasons,error,FactsRows:active is null ? 0 : 1);
    public static ViewCall Publish(long epoch=0,long generation=1,long expected=0,string target="platform:local")=>new("publish",Snapshot(epoch,generation,target),Facts(epoch,target),expected);
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<object[]> WithMutableInputs=>Generate().Where(test=>test.Calls.Any(call=>call.Snapshot is not null || call.Facts is not null || call.Inventory is not null))
        .Select(test=>new object[]{test});
    public static void AssertCapturedFixtureContracts(ViewCase test)
    {
        var schemaText=File.ReadAllText(Path.Combine(NativeSchemaProvider.Root(),"contracts","DeviceView.schema.json"));
        Assert.Equal("706d34871610f7e97ed0a00edd63bfb8961d39747a705835ed0b942ff27f807f",Hash(schemaText));
        JsonSchema Schema(string root)
        {
            var schema=JsonNode.Parse(schemaText)!.AsObject();schema["$ref"]="#/$defs/"+root;return JsonSchema.FromText(schema.ToJsonString());
        }
        var snapshotSchema=Schema("Snapshot");var factsSchema=Schema("Facts");
        foreach(var call in test.Calls)
        {
            if(call.Facts is not null) Assert.True(factsSchema.Evaluate(JsonSerializer.SerializeToElement(call.Facts)).IsValid,"Captured Facts fixture must satisfy the retained contract");
            if(call.Snapshot is null) continue;
            Assert.True(snapshotSchema.Evaluate(JsonSerializer.SerializeToElement(call.Snapshot)).IsValid,"Snapshot fixture must satisfy the retained contract");
            var body=call.Snapshot.DeepClone().AsObject();var pinned=body["view_id"]!.GetValue<string>();body.Remove("view_id");
            // Every member of these literal fixtures is ASCII and was written in
            // canonical property order; this standard-library hash is independent
            // of the service's canonicalizer and resolver.
            var actual=Hash(body.ToJsonString());
            if(test.Id=="VIEW-11-snapshot-hash-mismatch-rollback") Assert.NotEqual(pinned,actual);else Assert.Equal(pinned,actual);
            if(call.Facts is null) continue;
            var factsHash=Hash(call.Facts.ToJsonString());
            if(test.Id=="VIEW-10-facts-hash-mismatch-rollback") Assert.NotEqual(call.Snapshot["facts_id"]!.GetValue<string>(),factsHash);
            else Assert.Equal(call.Snapshot["facts_id"]!.GetValue<string>(),factsHash);
        }
    }
    public static IEnumerable<ViewCase> Generate()
    {
        var initial=State();var active=State(generation:1,stale:false,active:SnapshotBody());
        var invalidated=active with{Epoch=1,Stale=true,ETag=Tag(1,1),Resolved=0,BindingProjection=0,Reasons="startup|module-replaced"};
        yield return new("VIEW-01-startup-stale-complete-inventory",[new()],[initial]);
        yield return new("VIEW-02-complete-atomic-activation-and-type-edge",[Publish(),new()],[active,active]);
        yield return new("VIEW-03-invalidation-preserves-old-snapshot",[Publish(),new("invalidate",Reason:"module-replaced"),new(),Publish(expected:1)],
            [active,invalidated,invalidated,invalidated with{Outcome="stale-view",Error="Captured facts epoch changed"}]);
        yield return new("VIEW-04-cross-target-valid-hash-no-partial-view",[Publish(target:"other-target"),new()],
            [initial with{Outcome="invalid",Error="Cross-target/profile"},initial]);
        yield return new("VIEW-05-cross-profile-no-partial-view",[new("publish",Snapshot(profile:"other-profile"),Facts(profile:"other-profile")),new()],
            [initial with{Outcome="invalid",Error="Cross-target/profile"},initial]);
        yield return new("VIEW-06-stale-generation-cas-no-partial-view",[Publish(),Publish(generation:2,expected:0),new()],
            [active,active with{Outcome="stale-view",Error="generation"},active]);
        yield return new("VIEW-07-old-epoch-refresh-rejected-then-fresh-activation",[new("invalidate",Reason:"reconnect"),Publish(),Publish(epoch:1)],
            [initial with{Epoch=1,ETag=Tag(1),Reasons="startup|reconnect"},initial with{Outcome="stale-view",Epoch=1,ETag=Tag(1),Reasons="startup|reconnect",Error="Captured facts epoch changed"},
             State(epoch:1,generation:1,stale:false,active:SnapshotBody(1),reasons:"startup|reconnect")]);
        yield return new("VIEW-08-partial-inventory-cannot-activate",[new("publish",Snapshot(partial:true),Facts()),new()],
            [initial with{Outcome="invalid",Error="inventory"},initial]);
        foreach(var (id,sql,error) in new[]{("pointer-delete","DELETE FROM device_view_active","Invalidate device scope"),
            ("pointer-cas","UPDATE device_view_active SET generation=generation+1","Device view CAS"),
            ("sealed-entry","UPDATE device_view_entries SET state='unknown'","Device view entries are immutable"),
            ("sealed-header","UPDATE device_views SET inventory_count=0","Immutable device view"),
            ("sealed-edge","UPDATE device_type_edges SET type_contract_hash='fixture'","Graph edges are immutable")})
            yield return new("VIEW-09-sql-guard-"+id,[Publish(),new("sql-attack",Sql:sql),new()],[active,active with{Outcome="constraint",Error=error},active]);
        var wrongFacts=Facts();wrongFacts["observed_at"]="2026-10-27T02:00:00Z";
        yield return new("VIEW-10-facts-hash-mismatch-rollback",[new("publish",Snapshot(),wrongFacts),new()],
            [initial with{Outcome="invalid",Error="Facts hash mismatch"},initial]);
        var wrongHash=Snapshot();wrongHash["view_id"]=new string('d',64);
        yield return new("VIEW-11-snapshot-hash-mismatch-rollback",[new("publish",wrongHash,Facts()),new()],
            [initial with{Outcome="invalid",Error="Snapshot hash mismatch"},initial]);
        yield return new("VIEW-12-empty-reason-no-epoch-change",[new("invalidate",Reason:""),new()],
            [initial with{Outcome="invalid",Error="Invalidation reason required"},initial]);
        yield return new("VIEW-13-bounded-status-header",[Publish(),new("status")],[active,active with{Snapshot=null}]);
        yield return new("VIEW-14-refresh-precondition-order",[new("refresh"),new("refresh",ETag:"*"),new("refresh",ETag:Tag(generation:1)),new("refresh",ETag:Tag())],
            [initial with{Outcome="precondition-428"},initial with{Outcome="precondition-400"},initial with{Outcome="precondition-412"},initial with{Epoch=1,ETag=Tag(1),Reasons="startup|owner-refresh"}]);
        yield return new("VIEW-15-transaction-owned",[new("begin-fixture-transaction"),Publish(),new("rollback-fixture-transaction"),new()],
            [initial,initial with{Outcome="invalid",Error="owns its transaction"},initial,initial]);
        var second=State(generation:2,stale:false,active:SnapshotBody(generation:2)) with{Views=2,Members=4,Edges=2};
        yield return new("VIEW-16-second-activation-retains-first-view",[Publish(),Publish(generation:2,expected:1),new()],[active,second,second]);
        yield return new("VIEW-17-entry-json-cannot-override-normalized-state",[Publish(),new("sql-entry-json-mismatch",Snapshot(generation:2),Facts(),1),new()],
            [active,active with{Outcome="constraint",Error="JSON disagrees"},active]);
        yield return new("VIEW-18-reopen-same-inventory-is-idempotent",[new("reopen",Inventory:Inventory()),new()],[initial,initial]);
        yield return new("VIEW-19-explicit-count-mismatch-no-partial-inventory",[new("reopen",Inventory:Inventory(),InventoryCount:1L),new()],
            [initial with{Outcome="invalid",Error="inventory count"},initial]);
        yield return new("VIEW-20-boolean-count-not-an-integer",[new("reopen",Inventory:[Inventory()[0]],InventoryCount:true),new()],
            [initial with{Outcome="invalid",Error="inventory count"},initial]);
        yield return new("VIEW-20-floating-count-not-an-integer",[new("reopen",Inventory:Inventory(),InventoryCount:2.0),new()],
            [initial with{Outcome="invalid",Error="inventory count"},initial]);
        yield return new("VIEW-21-duplicate-inventory-identity-no-partial-set",[new("reopen",Inventory:[Inventory()[0],Inventory()[0]]),new()],
            [initial with{Outcome="invalid",Error="Duplicate inventory"},initial]);
        var malformed=Inventory();malformed[0].Remove("variants");
        yield return new("VIEW-22-malformed-inventory-no-partial-set",[new("reopen",Inventory:malformed),new()],
            [initial with{Outcome="invalid",Error="InventoryEntity"},initial]);
        var changed=Inventory();changed[0]=JsonNode.Parse(InventoryEntity('a',"binding",'a',false))!.AsObject();
        yield return new("VIEW-23-reopen-different-inventory-rolls-back-new-pin",[new("reopen",Inventory:changed),new()],
            [initial with{Outcome="invalid",Error="Reopened scope inventory differs"},initial]);
        yield return new("VIEW-24-etag-includes-distinct-scope-profile",[new()],[initial with{ETag=Tag(profile:"other-fixture")}],ScopeProfile:"other-fixture");
        var exhausted=initial with{Epoch=9007199254740991,ETag=Tag(9007199254740991),Reasons="startup|fixture-max-epoch"};
        yield return new("VIEW-25-epoch-exhaustion-before-state-mutation",[new("invalidate",Reason:"module-replaced"),new()],
            [exhausted with{Outcome="overflow",Error="Capability epoch exhausted"},exhausted],FixtureEpoch:9007199254740991);
        var unknown=active with{Snapshot=UnknownSnapshotBody(),Header=UnknownSnapshotBody(true),ETag=Tag(generation:1,inventoryHash:UnknownInventoryHash),Edges=0};
        yield return new("VIEW-26-unknown-remains-visible-with-lossless-source-evidence",
            [new("publish",JsonNode.Parse(UnknownSnapshotBody())!.AsObject(),Facts()),new("reopen",Inventory:UnknownInventory()),new()],
            [unknown,unknown,unknown],TrustedInventory:UnknownInventory());
    }
    public static void AssertCase(ViewCase test,IViewCaseProvider provider)
    {
        Assert.Equal(test.Calls.Length,test.Expected.Length);
        for(var index=0;index<test.Calls.Length;index++)
        {
            var call=test.Calls[index];var factsBefore=call.Facts?.ToJsonString();var snapshotBefore=call.Snapshot?.ToJsonString();var inventoryBefore=call.Inventory?.Select(item=>item.ToJsonString()).ToArray();
            var actual=provider.Execute(call);var expected=test.Expected[index];
            Assert.Equal(expected with{Error=null},actual with{Error=null});
            if(expected.Error is null) Assert.Null(actual.Error);else Assert.Contains(expected.Error,actual.Error ?? "",StringComparison.OrdinalIgnoreCase);
            Assert.Equal(factsBefore,call.Facts?.ToJsonString());Assert.Equal(snapshotBefore,call.Snapshot?.ToJsonString());
            Assert.Equal(inventoryBefore,call.Inventory?.Select(item=>item.ToJsonString()).ToArray());
        }
    }
}
public sealed class ScriptedViewProvider(ViewCase expected,int corruptAt=-1,int field=0,int mutateAt=-1):IViewCaseProvider
{
    private int index;
    public ViewReply Execute(ViewCall call)
    {
        Assert.Same(expected.Calls[index],call);var position=index++;var row=expected.Expected[position];
        if(position==mutateAt)
        {
            var input=call.Snapshot ?? call.Facts ?? call.Inventory![0];input["x-mock-mutation"]=true;
        }
        if(position!=corruptAt) return row;
        return field switch
        {
            0=>row with{Outcome="wrong"},1=>row with{Epoch=row.Epoch+1},2=>row with{Generation=row.Generation+1},3=>row with{Stale=!row.Stale},
            4=>row with{Snapshot="wrong"},5=>row with{Header="wrong"},6=>row with{ETag="wrong"},7=>row with{Views=row.Views+1},8=>row with{Members=row.Members+1},
            9=>row with{Edges=row.Edges+1},10=>row with{Resolved=row.Resolved+1},11=>row with{BindingProjection=row.BindingProjection+1},
            12=>row with{Reasons="wrong"},13=>row with{Error="wrong"},14=>row with{WireContracts=row.WireContracts+1},15=>row with{WireEvidenceColumns="wrong"},
            16=>row with{InventorySets=row.InventorySets+1},17=>row with{InventoryMembers=row.InventoryMembers+1},18=>row with{FactsRows=row.FactsRows+1},
            19=>row with{ForeignKeysClean=!row.ForeignKeysClean},_=>row with{ScopeRows=row.ScopeRows+1}
        };
    }
    public void AssertConsumed()=>Assert.Equal(expected.Calls.Length,index);
    public void Dispose() { }
}
[Trait("Gate","Mock")]
public sealed class DeviceViewStorageMockTests
{
    [Theory,MemberData(nameof(ViewStorageCases.All),MemberType=typeof(ViewStorageCases))]
    public void CorrectRepliesPass(ViewCase test)
    { ViewStorageCases.AssertCapturedFixtureContracts(test);using var provider=new ScriptedViewProvider(test);ViewStorageCases.AssertCase(test,provider);provider.AssertConsumed(); }
    [Theory,MemberData(nameof(ViewStorageCases.All),MemberType=typeof(ViewStorageCases))]
    public void EveryFieldAtEveryStepRejectsWrongReplies(ViewCase test)
    {
        for(var position=0;position<test.Calls.Length;position++)
        for(var field=0;field<21;field++)
        { using var provider=new ScriptedViewProvider(test,position,field);Assert.NotNull(Record.Exception(()=>ViewStorageCases.AssertCase(test,provider))); }
    }
    [Theory,MemberData(nameof(ViewStorageCases.WithMutableInputs),MemberType=typeof(ViewStorageCases))]
    public void InputMutationIsRejected(ViewCase test)
    {
        var position=Array.FindIndex(test.Calls,call=>call.Snapshot is not null || call.Facts is not null || call.Inventory is not null);
        using var provider=new ScriptedViewProvider(test,mutateAt:position);
        Assert.NotNull(Record.Exception(()=>ViewStorageCases.AssertCase(test,provider)));
    }
}
