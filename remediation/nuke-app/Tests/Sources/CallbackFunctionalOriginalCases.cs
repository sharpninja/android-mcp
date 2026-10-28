using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record OriginalFactMutation(int Step,string Path,JsonNode? Value);
public sealed record CallbackFunctionalOriginalCase(string Id,string Function,IReadOnlyList<string> Steps,IReadOnlyList<OriginalFactMutation> Mutations);
public sealed record CallbackFunctionalOriginalRig(ICallbackSourceFacts Callbacks,IFunctionalTaxonomySourceFacts Functional,Action Complete);

// This suite preserves the19 published test FUNCTION bodies. Existing33
// SourceFactContractCases remain the sole full-output fixture authority.
public static class CallbackFunctionalOriginalCases
{
    private static OriginalFactMutation M(int step,string path,JsonNode? value)=>new(step,path,value);
    public static readonly IReadOnlyList<CallbackFunctionalOriginalCase> All=
    [
        new("ORIG-CB01","test_missing_sdk_parameter_name_is_preserved_without_crash",["FACT-CB11"],[M(0,"0.parameter_name",JsonValue.Create("invented"))]),
        new("ORIG-CB02","test_name_never_proves_callback_lifecycle",["FACT-CB01"],[M(0,"is_candidate",JsonValue.Create(false)),M(0,"callback_lifecycle_proven",JsonValue.Create(true))]),
        new("ORIG-CB03","test_parameter_position_and_permission_are_retained",["FACT-CB06"],[M(0,"0.parameter_index",JsonValue.Create(1)),M(0,"0.permission.op",JsonValue.Create("wrong")),M(0,"0.registration_guaranteed",JsonValue.Create(true))]),
        new("ORIG-CB04","test_binder_endpoint_candidate_does_not_require_callback_name",["FACT-CB17"],[M(0,"",new JsonArray())]),
        new("ORIG-CB05","test_callback_executor_annotation_alone_is_not_callback_parameter",["FACT-CB12"],[M(0,"",new JsonArray(new JsonObject()))]),
        new("ORIG-CB06","test_overloads_and_parameter_positions_have_different_ids",["FACT-CB06","FACT-CB14"],[M(1,"0.candidate_id",JsonValue.Create(SourceFactContractCases.RegistrationHash))]),
        new("ORIG-CB07","test_unregistration_pair_remains_candidate",["FACT-CB06","FACT-CB20","FACT-CB18"],[M(2,"",new JsonArray()),M(2,"0.lifecycle_pair_proven",JsonValue.Create(true))]),
        new("ORIG-CB08","test_inherited_oneway_controls_endpoint_reply_mode",["FACT-CB07"],[M(0,"reply_mode",JsonValue.Create("unknown"))]),
        new("ORIG-CB09","test_missing_inherited_aidl_flag_does_not_prove_synchronous",["FACT-CB08"],[M(0,"reply_mode",JsonValue.Create("synchronous"))]),
        new("ORIG-CB10","test_explicit_false_both_aidl_flags_prove_reply_mode",["FACT-CB09"],[M(0,"reply_mode",JsonValue.Create("unknown"))]),
        new("ORIG-CB11","test_hidl_method_flag_has_no_interface_oneway_inheritance",["FACT-CB10"],[M(0,"reply_mode",JsonValue.Create("oneway"))]),
        new("ORIG-CB12","test_missing_oneway_not_invented_synchronous",["FACT-CB05"],[M(0,"reply_mode",JsonValue.Create("synchronous"))]),
        new("ORIG-CB13","test_same_short_name_does_not_prove_type_resolution",["FACT-CB13"],[M(0,"0.type_id",JsonValue.Create(SourceFactContractCases.TypeB)),M(0,"0.confidence",JsonValue.Create("wrong"))]),
        new("ORIG-FN01","test_explicit_service_annotation_retained_without_automatic_group_policy",["FACT-FN01"],[M(0,"functional_evidence.0.service_string_literal",JsonValue.Create("wrong")),M(0,"functional_evidence.0.annotation_namespace_proven",JsonValue.Create(false)),M(0,"functional_evidence.0.functional_group_membership_proven",JsonValue.Create(true))]),
        new("ORIG-FN02","test_custom_annotation_name_not_borrowed_as_android_annotation",["FACT-FN02"],[M(0,"functional_evidence.0.annotation_namespace_proven",JsonValue.Create(true))]),
        new("ORIG-FN03","test_short_annotation_requires_import",["FACT-FN03","FACT-FN04"],[M(0,"functional_evidence.0.annotation_namespace_proven",JsonValue.Create(true)),M(1,"functional_evidence.0.annotation_namespace_proven",JsonValue.Create(false))]),
        new("ORIG-FN04","test_symbol_kept_separate_from_string_literal",["FACT-FN05"],[M(0,"functional_evidence.0.service_string_literal",JsonValue.Create("evaluated")),M(0,"functional_evidence.0.service_symbol_expression",JsonValue.Create("wrong"))]),
        new("ORIG-FN05","test_package_namespace_does_not_become_functional_group",["FACT-FN06"],[M(0,"classification",JsonValue.Create("evidence-present")),M(0,"imported_group_memberships",new JsonArray("invented")),M(0,"namespace_search_evidence.functional_group_inferred",JsonValue.Create(true))]),
        new("ORIG-FN06","test_expression_not_evaluated",["FACT-FN07"],[M(0,"functional_evidence.0.service_string_literal",JsonValue.Create("evaluated")),M(0,"functional_evidence.0.service_symbol_expression",JsonValue.Create("evaluated"))])
    ];
    public static IEnumerable<object[]> Cases=>All.Select(test=>new object[]{test.Id});
    public static CallbackFunctionalOriginalCase Get(string id)=>All.Single(test=>test.Id==id);
    private static JsonObject Method(string name="registerListener")=>SourceFactContractCases.Method(name);
    private static Dictionary<string,JsonObject> Types()=>SourceFactContractCases.Types();
    private static string Text(JsonNode? node)=>node!.GetValue<string>();
    private static bool Flag(JsonNode? node)=>node!.GetValue<bool>();
    private static JsonObject FunctionalDocument(string name="android.annotation.SystemService",string arguments="\"clipboard\"")
    {
        var value=(JsonObject)SourceFactContractCases.Get("FACT-FN01").Input.DeepClone();value["extensions"]!["annotations"]![0]!["name"]=name;value["extensions"]!["annotations"]![0]!["arguments"]=arguments;return value;
    }
    public static void AssertCase(CallbackFunctionalOriginalCase test,CallbackFunctionalOriginalRig rig)
    {
        var callbacks=rig.Callbacks;var functional=rig.Functional;
        switch(test.Id)
        {
            case "ORIG-CB01":
                var missing=Method();missing["extensions"]!["parameters"]![0]!["name"]=null;Assert.Null(callbacks.RegistrationCandidates(missing,Types())[0]!["parameter_name"]);break;
            case "ORIG-CB02":
                var disposition=callbacks.TypeCandidate(Types()[SourceFactContractCases.TypeB]);Assert.True(Flag(disposition["is_candidate"]));Assert.False(Flag(disposition["callback_lifecycle_proven"]));break;
            case "ORIG-CB03":
                var parameter=callbacks.RegistrationCandidates(Method(),Types())[0]!;Assert.Equal(0,parameter["parameter_index"]!.GetValue<int>());Assert.True(JsonNode.DeepEquals(SourceFactContractCases.Permission(),parameter["permission"]));Assert.False(Flag(parameter["registration_guaranteed"]));break;
            case "ORIG-CB04":
                var binder=Method();binder["arguments"]![0]!["text"]="a.IEndpoint";binder["extensions"]!["parameters"]![0]!["name"]="endpoint";var endpoints=Types();endpoints[SourceFactContractCases.TypeB]=new(){["name"]="IEndpoint",["kind"]="aidl-interface",["extensions"]=new JsonObject{["language"]="aidl"}};Assert.Single(callbacks.RegistrationCandidates(binder,endpoints));break;
            case "ORIG-CB05":
                var executor=Method();executor["arguments"]=new JsonArray(new JsonObject{["text"]="Executor",["status"]="unknown"});executor["extensions"]!["parameters"]=new JsonArray(new JsonObject{["name"]="executor",["annotations"]=new JsonArray(new JsonObject{["name"]="CallbackExecutor"})});Assert.Empty(callbacks.RegistrationCandidates(executor,new Dictionary<string,JsonObject>()));break;
            case "ORIG-CB06":
                var first=Method();var second=Method();second["entry_id"]=SourceFactContractCases.BindingC;Assert.NotEqual(Text(callbacks.RegistrationCandidates(first,Types())[0]!["candidate_id"]),Text(callbacks.RegistrationCandidates(second,Types())[0]!["candidate_id"]));break;
            case "ORIG-CB07":
                var registrations=callbacks.RegistrationCandidates(Method(),Types());var removal=Method("unregisterListener");removal["entry_id"]=SourceFactContractCases.BindingC;var removals=callbacks.RegistrationCandidates(removal,Types());var rows=new JsonArray(registrations.Concat(removals).Select(node=>node?.DeepClone()).ToArray());var pairs=callbacks.PossiblePairs(rows);Assert.Single(pairs);Assert.False(Flag(pairs[0]!["lifecycle_pair_proven"]));break;
            case "ORIG-CB08":case "ORIG-CB09":case "ORIG-CB10":case "ORIG-CB11":case "ORIG-CB12":
                var method=Method("onEvent");if(test.Id!="ORIG-CB12"){method["extensions"]!["oneway"]=false;method["extensions"]!["language"]=test.Id=="ORIG-CB11"?"hidl":"aidl";}if(test.Id=="ORIG-CB10")method["extensions"]!["interface_oneway"]=false;
                var owner=test.Id=="ORIG-CB08"?new JsonObject{["extensions"]=new JsonObject{["oneway"]=true}}:null;Assert.Equal(test.Id=="ORIG-CB08"?"oneway":test.Id is "ORIG-CB10" or "ORIG-CB11"?"synchronous":"unknown",Text(callbacks.EndpointMethod(method,owner)["reply_mode"]));break;
            case "ORIG-CB13":
                var unresolved=Method();unresolved["arguments"]![0]=new JsonObject{["text"]="Listener",["status"]="unknown"};var candidate=callbacks.RegistrationCandidates(unresolved,Types())[0]!;Assert.Null(candidate["type_id"]);Assert.Equal("name-heuristic-unresolved-type",Text(candidate["confidence"]));break;
            case "ORIG-FN01":
                var declaration=functional.Taxonomy(FunctionalDocument())["functional_evidence"]![0]!;Assert.Equal("clipboard",Text(declaration["service_string_literal"]));Assert.True(Flag(declaration["annotation_namespace_proven"]));Assert.False(Flag(declaration["functional_group_membership_proven"]));break;
            case "ORIG-FN02":Assert.False(Flag(functional.Taxonomy(FunctionalDocument("custom.SystemService"))["functional_evidence"]![0]!["annotation_namespace_proven"]));break;
            case "ORIG-FN03":
                var shortName=FunctionalDocument("SystemService","Context.CLIPBOARD_SERVICE");Assert.False(Flag(functional.Taxonomy(shortName)["functional_evidence"]![0]!["annotation_namespace_proven"]));shortName["extensions"]!["imports"]=new JsonArray("android.annotation.SystemService");Assert.True(Flag(functional.Taxonomy(shortName)["functional_evidence"]![0]!["annotation_namespace_proven"]));break;
            case "ORIG-FN04":
                var symbol=functional.Taxonomy(FunctionalDocument(arguments:"value=Context.CLIPBOARD_SERVICE"))["functional_evidence"]![0]!;Assert.Null(symbol["service_string_literal"]);Assert.Equal("Context.CLIPBOARD_SERVICE",Text(symbol["service_symbol_expression"]));break;
            case "ORIG-FN05":
                var empty=FunctionalDocument();empty["extensions"]!["annotations"]=new JsonArray();var result=functional.Taxonomy(empty);Assert.Equal("unclassified-searchable",Text(result["classification"]));Assert.Empty(result["imported_group_memberships"]!.AsArray());Assert.False(Flag(result["namespace_search_evidence"]!["functional_group_inferred"]));break;
            case "ORIG-FN06":
                var expression=functional.Taxonomy(FunctionalDocument(arguments:"selectService()"))["functional_evidence"]![0]!;Assert.Null(expression["service_string_literal"]);Assert.Null(expression["service_symbol_expression"]);break;
            default:throw new InvalidOperationException("Unknown original function "+test.Id);
        }
        rig.Complete();
    }
}
