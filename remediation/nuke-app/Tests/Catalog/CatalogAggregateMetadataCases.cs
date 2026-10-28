using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogAggregateMetadataCall(string Variant,JsonObject Declaration);
public sealed record CatalogAggregateMetadataCase(string Id,string LegacyMethod,CatalogAggregateMetadataCall Call,JsonObject Expected);
public interface ICatalogAggregateMetadataCaseProvider { Task<JsonObject> ExecuteAsync(CatalogAggregateMetadataCall call); }
public static class CatalogAggregateMetadataCases
{
    // Exact doc() fixture from test_source_metadata_contract.py. Production
    // bindings use shared Source callbacks/taxonomy and Catalog aggregate/schema
    // validation, not another source-fact producer or a runtime authority.
    public static JsonObject Declaration()=>new()
    {
        ["entry_id"]=new string('a',64),["kind"]="interface",["name"]="Listener",["owner"]="p.Listener",["package"]="p",
        ["canonical_signature"]="java:p.Listener",["arguments"]=new JsonArray(),["permission"]=new JsonObject{["op"]="unknown",["reason"]="fixture"},
        ["provenance"]=new JsonArray(),["extensions"]=new JsonObject{["language"]="java",["annotations"]=new JsonArray()}
    };
    public static JsonObject Source()=>new(){["repository"]="fixture",["revision"]="r1",["path"]="IFoo.aidl",["sha256"]=new string('b',64)};
    public static JsonObject ExactContext()=>new(){["kind"]="exact-occurrence",["source_ids"]=new JsonArray(new string('b',64)),["declaration_record_hash"]=new string('c',64),["unknown_reason"]=null};
    public static JsonObject InterfaceLinkage()
    {
        var build=Source();build["path"]="Android.bp";
        JsonObject HashLine(int line,char literal){var source=Source();source["path"]=".hash";return new(){["source"]=source,["line"]=line,["literal"]=new string(literal,40),["format"]="published-aidl-hash-hex40"};}
        return new(){["build_evidence"]=build,["build_module_name"]="foo",["module_kind"]="aidl_interface",["declared_flags"]=new JsonObject{["apex_available"]=new JsonArray("//apex_available:platform")},["published_hash_lines"]=new JsonArray(HashLine(1,'d'),HashLine(2,'e'))};
    }
    public static JsonObject NativeRecord()
    {
        var source=Source();source["source_id"]=new string('d',64);
        return new(){["evidence_record_id"]=new string('c',64),["identity_scope"]="source-file-local ABI evidence, not a stable MCP binding ID",["permission_authority"]="unknown",
            ["record"]=new JsonObject{["collection"]="pointer_types",["display_name"]="fixture pointer",["local_key"]="p",["ordinal"]=0,["metadata"]=new JsonObject()},
            ["runtime_applicability"]="unknown",["source"]=source,["source_registry_id"]=new string('e',64),["wire_codec_proven"]=false};
    }
    public static JsonObject HidlConstraint()=>new(){["source"]=Source(),["declared_descriptor"]="p@1.0::IFoo",["constraint"]=new JsonObject{["unknown_reason"]="No matching same-revision published source hash"}};
    public static JsonObject NativeDescriptor()=>new(){["source"]=Source(),["source_registry_id"]=new string('d',64),["fact"]=new JsonObject
    {
        ["family"]="AIBinder_Class_define",["line"]=1,["character_span"]=new JsonArray(0,40),["declared_symbol_token"]=null,["descriptor_expression"]="chooseDescriptor()",
        ["descriptor_literal"]=null,["status"]="computed-or-unresolved-argument",["raw"]="AIBinder_Class_define(chooseDescriptor())",["compiled_endpoint_proven"]=false,
        ["wire_transaction_schema"]=null,["preprocessor_or_build_conditions"]="unresolved",["runtime_registration"]="unknown"
    }};
    private static CatalogAggregateMetadataCase Case(string id,string method,string variant,JsonObject expected,Action<JsonObject>? edit=null)
    {var doc=Declaration();edit?.Invoke(doc);return new(id,method,new(variant,doc),expected);}
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<CatalogAggregateMetadataCase> Generate()
    {
        yield return Case("AGG-01-schema", "test_generated_schema_is_available","schema",new(){["schema_exists"]=true});
        yield return Case("AGG-02-unknown-searchable","test_all_unclassified_entries_remain_unknown_and_searchable","unclassified",
            new(){["role"]="unknown",["classification"]="unclassified-searchable"},doc=>doc["name"]="Ordinary");
        yield return Case("AGG-03-candidate-no-authority","test_named_listener_is_candidate_never_registrable","candidate",
            new(){["role"]="candidate",["registration_allowed"]=false});
        yield return Case("AGG-04-evidence-order","test_producer_dto_retains_selector_and_ordered_hash_candidates","interface-evidence",
            new(){["interface_evidence_exact"]=true,["version_range"]=null});
        yield return Case("AGG-05-runtime-claim","test_source_metadata_cannot_claim_runtime_or_client_authority","runtime-claim",new(){["rejected"]=true});
        yield return Case("AGG-06-entry-identity","test_entry_identity_mismatch_rejected_before_attach","identity",new(){["identity_rejected"]=true});
        yield return Case("AGG-07-copy-extension","test_attach_preserves_inputs_and_uses_one_canonical_extension","attach",
            new(){["input_equal"]=true,["single_canonical_extension"]=true,["variants_exact"]=true});
        yield return Case("AGG-08-version-isolation","test_aggregate_metadata_does_not_borrow_oneway_from_another_version","oneway",
            new(){["aggregate_reply_mode"]="unknown",["exact_reply_mode"]="oneway"},doc=>{doc["kind"]="method";doc["name"]="changed";doc["extensions"]!["oneway"]=true;doc["extensions"]!["language"]="aidl";});
        yield return Case("AGG-09-duplicate-context","test_duplicate_source_variant_context_cannot_be_flattened_into_one_entry","duplicate-context",new(){["duplicate_rejected"]=true});
        yield return Case("AGG-10-unknown-contract-field","test_unknown_fields_are_rejected_in_known_contract","unknown-field",new(){["unknown_field_rejected"]=true});
        yield return Case("AGG-11-native-not-binding","test_native_evidence_identity_is_not_a_binding_id","native-record",
            new(){["confidence"]="structural-only",["runtime_observation"]=false,["wire_claim_rejected"]=true});
        yield return Case("AGG-12-abstract-no-invention","test_materialization_does_not_auto_subclass_abstract_types","abstract-materialization",
            new(){["materialization_strategy"]="unsupported_abstract_class",["unknown_reason"]="ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS",["unpinned_adapter_rejected"]=true});
        yield return Case("AGG-13-hidl-unknown-constraint","test_unknown_hidl_source_constraint_is_retained_without_positive_clause","hidl-constraint",
            new(){["unknown_constraint_valid"]=true,["unknown_plus_positive_rejected"]=true});
        yield return Case("AGG-14-direction-unknown","test_null_source_parameter_direction_means_unresolved_not_input","null-direction",
            new(){["direction"]=null,["registration_guaranteed"]=false},doc=>{doc["kind"]="method";doc["name"]="registerListener";doc["arguments"]=new JsonArray(new JsonObject{["status"]="unknown",["text"]="Listener",["reason"]="fixture",["codec_status"]="unknown"});doc["extensions"]!["parameters"]=new JsonArray(new JsonObject{["name"]="listener",["direction"]=null});});
        yield return Case("AGG-15-owner-flag-unknown","test_reply_mode_cannot_invent_synchronous_from_missing_owner_flag","reply-mode",
            new(){["invented_synchronous_rejected"]=true},doc=>{doc["kind"]="method";doc["extensions"]!["language"]="aidl";doc["extensions"]!["oneway"]=false;});
        yield return Case("AGG-16-computed-descriptor","test_computed_native_descriptor_keeps_unresolved_expression","native-descriptor",
            new(){["document_exact"]=true,["compiled_endpoint_claim_rejected"]=true});
        yield return Case("AGG-17-immutable-occurrence-hook","test_immutable_builder_occurrences_attach_before_effective_record_hash","occurrences",
            new(){["entry_schema_valid"]=true,["variant_count_matches_occurrences"]=true,["hash_changed"]=true,["idempotent"]=true,["raw_whitespace_tamper_rejected"]=true});
    }
    public static async Task AssertCase(CatalogAggregateMetadataCase test,ICatalogAggregateMetadataCaseProvider provider)
    {
        var before=test.Call.Declaration.DeepClone();var actual=await provider.ExecuteAsync(test.Call);
        Assert.True(JsonNode.DeepEquals(test.Expected,actual),$"Expected {test.Expected}; actual {actual}");
        Assert.True(JsonNode.DeepEquals(before,test.Call.Declaration));
    }
}
public sealed class ScriptedCatalogAggregateMetadata(CatalogAggregateMetadataCase test,string? wrong=null):ICatalogAggregateMetadataCaseProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogAggregateMetadataCall call)
    {
        Assert.Same(test.Call,call);var result=test.Expected.DeepClone().AsObject();
        if(wrong is not null)
        {
            var value=result[wrong];result[wrong]=value is JsonValue scalar && scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("wrong");
        }
        return Task.FromResult(result);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogAggregateMetadataMockTests
{
    [Theory,MemberData(nameof(CatalogAggregateMetadataCases.All),MemberType=typeof(CatalogAggregateMetadataCases))]
    public Task OriginalAggregateContractAssertionsPass(CatalogAggregateMetadataCase test)=>CatalogAggregateMetadataCases.AssertCase(test,new ScriptedCatalogAggregateMetadata(test));
    [Theory,MemberData(nameof(CatalogAggregateMetadataCases.All),MemberType=typeof(CatalogAggregateMetadataCases))]
    public async Task EachOriginalOutputIdentityAndAuthorityAssertionRejectsItsMutation(CatalogAggregateMetadataCase test)
    {foreach(var key in test.Expected.Select(pair=>pair.Key))Assert.NotNull(await Record.ExceptionAsync(()=>CatalogAggregateMetadataCases.AssertCase(test,new ScriptedCatalogAggregateMetadata(test,key))));}
}
