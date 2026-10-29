using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogMetadataObligationCall(string Variant,JsonObject Document);
public sealed record CatalogMetadataObligationCase(string Id,string LegacyMethod,CatalogMetadataObligationCall Call,JsonObject Expected);
public interface ICatalogMetadataObligationCaseProvider { Task<JsonObject> ExecuteAsync(CatalogMetadataObligationCall call); }
public static class CatalogMetadataObligationCases
{
    // Exact descriptor input from MetadataCrud.test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization.
    public static JsonObject Descriptor(bool unknown=false)=>new()
    {
        ["schema_version"]="1.0.0",["callback_id"]=new string('c',64),["type_id"]=new string('a',64),["type_contract_hash"]=new string('b',64),
        ["classification"]=unknown?"unknown":"candidate",["unknown_reason"]=unknown?"Role unproven":"Role not proven",["methods"]=new JsonArray(),["registration_binding_ids"]=new JsonArray(),
        ["materialization"]=unknown?new JsonObject{["strategy"]="unknown",["unknown_reason"]="Shape not resolved"}:new JsonObject{["strategy"]="unsupported_abstract_class",["unknown_reason"]="ABSTRACT_CLASS_NO_RUNTIME_SUBCLASS"},
        ["cleanup"]=new JsonObject{["binding_id"]=null,["contract_hash"]=null,["status"]="unknown",["argument_mapping"]=new JsonArray(),["evidence"]=new JsonArray(),["unknown_reason"]=unknown?"No cleanup proof":"Cleanup unknown"},
        ["evidence"]=new JsonArray(),["source_applicability"]=new JsonObject{["unknown_reason"]=unknown?"No device evidence":"No observed device"}
    };
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<CatalogMetadataObligationCase> Generate()
    {
        yield return new("META-OB-01-lifecycle-atomic-unit","test_lifecycle_pair_is_an_atomic_group_unit",new("lifecycle",MetadataCrudCases.Group()),
            new(){["register_only_rejected"]=true,["paired_unit_valid"]=true});
        yield return new("META-OB-02-immutable-history-head","test_history_and_selected_head_cannot_be_deleted_or_rewritten",new("immutable",MetadataCrudCases.Group()),
            new(){["delete_revisions_rejected"]=true,["rewrite_revisions_rejected"]=true,["delete_heads_rejected"]=true,["row_unchanged"]=true});
        yield return new("META-OB-03-json-null-SQL","test_json_null_schema_cannot_pass_sql_check",new("sql-null",MetadataCrudCases.Group()),
            new(){["null_schema_SQL_rejected"]=true,["no_partial_row"]=true});
        yield return new("META-OB-04-exact-trusted-member-pin","test_exact_trusted_contract_pin_is_required_for_group_membership",new("member-pin",MetadataCrudCases.Group()),
            new(){["wrong_pin_status"]=422,["correct_pin_status"]=200,["saved_contract_exact"]=true});
        yield return new("META-OB-05-descriptor-trusted-type","test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization",new("descriptor",Descriptor()),
            new(){["create_status"]=200,["strategy"]="unsupported_abstract_class",["positive_unknown_materializer_status"]=422,["revision"]=1,["generation"]=1,["head_unchanged"]=true});
        yield return new("META-OB-06-unknown-shape-retained","test_unknown_descriptor_can_preserve_unknown_materialization",new("unknown-descriptor",Descriptor(true)),
            new(){["valid"]=true,["strategy"]="unknown",["input_unchanged"]=true});
        yield return new("META-OB-07-public-representation","test_public_representation_matches_local_openapi_component",new("representation",MetadataCrudCases.Group()),
            new(){["schema_valid"]=true,["contains_document_json"]=false,["document_exact"]=true});
        yield return new("META-OB-08-local-OpenAPI-references","test_metadata_openapi_has_only_resolvable_local_references",new("openapi",MetadataCrudCases.Group()),
            new(){["all_local_references_resolve"]=true,["descriptor_verbs_exact"]=true,["group_verbs_exact"]=true});
        // The original store's pin() guard applies to descriptor type and every
        // method/required type/register/cleanup binding. These cases expose the
        // existing trusted type requirement without granting runtime authority.
        yield return new("META-OB-09-missing-descriptor-type","test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization",new("missing-descriptor-type",Descriptor()),
            new(){["status"]=422,["generation"]=0,["no_partial_row"]=true});
        yield return new("META-OB-10-wrong-descriptor-type-pin","test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization",new("wrong-descriptor-pin",Descriptor()),
            new(){["status"]=422,["generation"]=0,["no_partial_row"]=true});
        yield return new("META-OB-11-nontype-descriptor-reference","test_descriptor_uses_trusted_type_pin_and_explicit_unsupported_materialization",new("nontype-descriptor-reference",Descriptor()),
            new(){["status"]=422,["generation"]=0,["no_partial_row"]=true});
    }
    public static async Task AssertCase(CatalogMetadataObligationCase test,ICatalogMetadataObligationCaseProvider provider)
    {
        var before=test.Call.Document.DeepClone();var actual=await provider.ExecuteAsync(test.Call);
        Assert.True(JsonNode.DeepEquals(test.Expected,actual),$"Expected {test.Expected}; actual {actual}");Assert.True(JsonNode.DeepEquals(before,test.Call.Document));
    }
}
public sealed class ScriptedCatalogMetadataObligation(CatalogMetadataObligationCase test,string? wrong=null):ICatalogMetadataObligationCaseProvider
{
    public Task<JsonObject> ExecuteAsync(CatalogMetadataObligationCall call)
    {
        Assert.Same(test.Call,call);var result=test.Expected.DeepClone().AsObject();
        if(wrong is not null){var value=result[wrong];result[wrong]=value is JsonValue scalar && scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("wrong");}
        return Task.FromResult(result);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMetadataObligationMockTests
{
    [Theory,MemberData(nameof(CatalogMetadataObligationCases.All),MemberType=typeof(CatalogMetadataObligationCases))]
    public Task OriginalDescriptorAndRemainingGroupAssertionsPass(CatalogMetadataObligationCase test)=>CatalogMetadataObligationCases.AssertCase(test,new ScriptedCatalogMetadataObligation(test));
    [Theory,MemberData(nameof(CatalogMetadataObligationCases.All),MemberType=typeof(CatalogMetadataObligationCases))]
    public async Task EveryTrustedPinImmutabilityAndPublicRepresentationAssertionRejectsMutation(CatalogMetadataObligationCase test)
    {foreach(var key in test.Expected.Select(pair=>pair.Key))Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMetadataObligationCases.AssertCase(test,new ScriptedCatalogMetadataObligation(test,key))));}
}
