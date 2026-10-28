using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogMetadataLeafCall(string Variant,JsonObject Document);
public sealed record CatalogMetadataLeafCase(string Id,string[] OriginalFunctions,CatalogMetadataLeafCall Call,JsonObject Expected);
public interface ICatalogMetadataLeafProvider { Task<JsonObject> ExecuteAsync(CatalogMetadataLeafCall call); }
public static class CatalogMetadataLeafCases
{
    private static JsonObject Reply(params (string Key,object? Value)[] fields)
    {var value=new JsonObject{["source_schema_unchanged"]=true};foreach(var (key,item) in fields)value[key]=JsonSerializer.SerializeToNode(item);return value;}
    private static CatalogMetadataLeafCase Store(string id,string variant,JsonObject expected,params string[] functions)=>
        new(id,functions,new(variant,MetadataCrudCases.Group()),expected);
    private static CatalogMetadataLeafCase Reference(string id,string variant,JsonObject expected,JsonObject? document=null,params string[] functions)=>
        new(id,functions,new(variant,document??CatalogAggregateMetadataCases.Declaration()),expected);
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<CatalogMetadataLeafCase> Generate()
    {
        yield return Reference("LEAF-01-single-schema-authority","invalid-schema",Reply(("schema_invalid",true),("composer_rejected_before_instance",true),("release_validator_rejected",true),("existing_report_absent",true)),
            functions:["validator","validate"]);
        yield return Store("LEAF-02-import-same-base-replay","import-replay",Reply(("revision",1),("generation",1L),("history",1),("bases",1),("heads",1),("etag_unchanged",true),("reason","first base")),"import_fixture","row","generation","tag");
        yield return Store("LEAF-03-clean-import-replacement","import-clean-next",Reply(("revision",2),("generation",2L),("history",2),("bases",2),("heads",1),("origin","imported"),("state","active"),("title","Next source")),"import_fixture","_append","get");
        yield return Store("LEAF-04-local-import-conflict","local-import",Reply(("revision",2),("generation",2L),("history",2),("bases",1),("origin","overlay"),("state","conflict"),("title","Local")),"create","import_fixture","get");
        yield return Store("LEAF-05-overlay-same-base-replay","overlay-replay",Reply(("revision",2),("generation",2L),("history",2),("bases",1),("etag_unchanged",true),("origin","overlay"),("title","Local"),("reason","local edit")),"put","import_fixture");
        yield return Store("LEAF-06-imported-suppression-restore","import-restore",Reply(("revision",3),("generation",3L),("history",3),("bases",1),("origin","overlay"),("state","active"),("title","Restored")),"delete","put","history");
        yield return Store("LEAF-07-history-complete-reasons","history-reasons",Reply(("reasons",new[]{"first reason","second reason","third reason"}),("revisions",new[]{1,2,3}),("all_columns_exact",true),("documents_exact",true),("history_readonly",true)),"_append","history","row");
        yield return Store("LEAF-08-exact-shared-dependency-DAG","dependency-dag",Reply(("accepted",true),("heads",4),("revisions",4),("generation",4L),("root_state","active")),"validate_group","validate_document","create");
        yield return Store("LEAF-09-stale-dependency-revision","dependency-revision",Reply(("status",422),("heads",1),("revisions",2),("generation",2L),("rejected_without_mutation",true)),"validate_group","validate_document");
        yield return Store("LEAF-10-stale-dependency-content-hash","dependency-hash",Reply(("status",422),("heads",1),("revisions",1),("generation",1L),("rejected_without_mutation",true)),"validate_group","validate_document");
        yield return Reference("LEAF-11-functional-cross-identity","functional-identity",Reply(("schema_precondition",true),("error_contains","Functional identity differs"),("candidate_schema_precondition",true),("candidate_flag_rejected",true),("candidate_error_contains","candidate flag")),functions:["validate_metadata"]);
        var endpoint=CatalogAggregateMetadataCases.Declaration();endpoint["kind"]="method";endpoint["extensions"]!["language"]="aidl";endpoint["extensions"]!["oneway"]=true;
        yield return Reference("LEAF-12-endpoint-cross-identity","endpoint-identity",Reply(("schema_precondition",true),("error_contains","Endpoint identity differs")),endpoint,"validate_metadata","validate");
        var parameter=CatalogAggregateMetadataCases.Generate().Single(test=>test.Id=="AGG-14-direction-unknown").Call.Declaration.DeepClone().AsObject();
        yield return Reference("LEAF-13-parameter-cross-identity","parameter-identity",Reply(("schema_precondition",true),("error_contains","Parameter binding identity differs")),parameter,"validate_metadata");
        yield return Reference("LEAF-14-interface-evidence-revision","interface-revision",Reply(("schema_precondition",true),("error_contains","Cross-repository/revision source evidence")),functions:["validate_metadata","produce_entry_metadata"]);
        // A supplied authored DTO is an input to contract validation, not a
        // claim that a Source parser or runtime proved this fixture constraint.
        yield return Reference("LEAF-15-HIDL-byte-and-scope-guards","hidl-guards",Reply(("positive_schema_precondition",true),("positive_preserved",true),("descriptor_schema_precondition",true),("descriptor_rejected",true),("hash_schema_precondition",true),("hash_rejected",true),("scope_schema_precondition",true),("scope_rejected",true),("empty_evidence_schema_invalid",true),("empty_evidence_rejected",true),("no_runtime_authority",true)),functions:["validate_metadata"]);
        yield return Reference("LEAF-16-materialization-entire-shape-domain","materialization",Reply(("strategies",new Dictionary<string,string>{["abstract-class"]="unsupported_abstract_class",["class"]="unsupported_class_subclass",["nonfinal-class"]="unsupported_class_subclass",["unknown"]="unknown",["java-interface"]="interface_dynamic_proxy",["aidl-interface"]="binder_generic_stub"}),("unknown_shape_rejected",true),("valid_adapter_exact_copy",true),("missing_pin_rejected",true),("extra_pin_rejected",true),("empty_pin_rejected",true),("nonstring_pin_rejected",true),("short_hash_rejected",true)),functions:["materialization"]);
        yield return Reference("LEAF-17-occurrence-full-context-and-fallback","occurrence-context",Reply(("binding_mismatch_rejected",true),("provenance_mismatch_rejected",true),("variant_identity_rejected",true),("lookup_exact_source",true),("lookup_evidence_retained",true),("fallback_variants",1),("fallback_context","aggregate-unresolved"),("no_runtime_authority",true)),functions:["attach_occurrences","attach","produce_entry_metadata"]);
        yield return Reference("LEAF-18-entry-identity-before-concerns","missing-entry",Reply(("error_contains","Missing Entry identity"),("concern_calls",0)),functions:["produce_entry_metadata"]);
        yield return Store("LEAF-19-exact-strong-header-domain","header-domain",Reply(("statuses",new[]{400,400,400,400,400,400}),("reject_before_lookup",true),("generation",1L),("revisions",1),("headers_unchanged",true)),"precondition","put","delete");
    }
    public static async Task AssertCase(CatalogMetadataLeafCase test,ICatalogMetadataLeafProvider provider)
    {
        var call=test.Call with{Document=test.Call.Document.DeepClone().AsObject()};var before=call.Document.DeepClone();
        var value=await provider.ExecuteAsync(call);Assert.True(JsonNode.DeepEquals(test.Expected,value),$"Expected {test.Expected}; actual {value}");
        Assert.True(JsonNode.DeepEquals(before,call.Document));
    }
}
public sealed class ScriptedCatalogMetadataLeaf(CatalogMetadataLeafCase test,string? mutation=null):ICatalogMetadataLeafProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogMetadataLeafCall call)
    {
        Assert.Equal(test.Call.Variant,call.Variant);Assert.True(JsonNode.DeepEquals(test.Call.Document,call.Document));var reply=test.Expected.DeepClone().AsObject();
        if(mutation=="$input")call.Document["unexpected_mutation"]=true;
        else if(mutation is not null)
        {var value=reply[mutation];reply[mutation]=value is JsonValue scalar&&scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("wrong");}
        return Task.FromResult(reply);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataLeafMockTests
{
    [Theory,MemberData(nameof(CatalogMetadataLeafCases.All),MemberType=typeof(CatalogMetadataLeafCases))]
    public Task CorrectWholeGroupClauseFixturesPass(CatalogMetadataLeafCase test)=>CatalogMetadataLeafCases.AssertCase(test,new ScriptedCatalogMetadataLeaf(test));
    [Theory,MemberData(nameof(CatalogMetadataLeafCases.All),MemberType=typeof(CatalogMetadataLeafCases))]
    public async Task EveryNewClauseAndInputRetentionAssertionDiscriminates(CatalogMetadataLeafCase test)
    {
        foreach(var key in test.Expected.Select(pair=>pair.Key).Append("$input"))
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMetadataLeafCases.AssertCase(test,new ScriptedCatalogMetadataLeaf(test,key))));
    }
}
