using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public enum FactOperation { TypeCandidate, Endpoint, Registration, Pairs, Taxonomy, Applicability }
public sealed record SourceFactCase(string Id, FactOperation Operation, JsonObject Input, JsonNode Expected, string Evidence,
    JsonObject? Owner = null, IReadOnlyDictionary<string, JsonObject>? Types = null, JsonObject? Linkage = null,
    JsonArray? Additional = null, IReadOnlyList<JsonObject>? IdentityInputs = null, IReadOnlyList<string>? IdentityHashes = null)
{
    public SourceExtractionCase Extraction() => new(Id, "source-facts", Id, StructuralParseStatus.Clean,
        [new JsonObject { ["output"] = Expected.DeepClone() }], [], Evidence);
}

public static class SourceFactContractCases
{
    public const string BindingA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string BindingC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    public const string TypeB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    public const string RegistrationHash = "73165e818d12b3b3dea8204898228406c2cd295ff110463ea0b7c08215bf1fb3";
    public const string OverloadHash = "54cd797c2e53467073cd0c8b89ab8dca89f7c3390ad8c4ff457519fcc480cc5d";
    public const string PositionHash = "5093d4317b892ecd37397f9a1cf83327a969141c2f5fc0bbd62c47bbfdaac8c5";
    public const string UnresolvedHash = "627b379f4a083e102df73ac257e20b00d12f01388cbc51aa6f8c273738a4da48";
    public const string UnicodeHash = "bcd2dfda224c6c0b87137dfe21fac39419118bb1c390cb2895a858aa738341b8";
    public const string BinderHash = "395abf9c125205339a34e60631bb1e6f2ffa325349724e7dee4d0b6f8380f23a";

    public static IReadOnlyList<SourceFactCase> All { get; } = Create();
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.Extraction()));
    public static SourceFactCase Get(string id) => All.Single(x => x.Id == id);

    public static JsonArray A(params JsonNode?[] nodes) => new(nodes);
    public static JsonObject Permission() => new() { ["op"] = "unknown" };
    public static JsonArray Provenance() => A(new JsonObject { ["source_id"] = "source" });
    public static JsonObject Method(string name = "registerListener", string binding = BindingA) => new()
    {
        ["entry_id"] = binding, ["canonical_signature"] = "java:a.A#" + name + "(a.Listener)", ["name"] = name,
        ["owner"] = "a.A", ["kind"] = "method", ["arguments"] = A(new JsonObject { ["text"] = "a.Listener", ["status"] = "resolved", ["resolved_binding_id"] = TypeB }),
        ["permission"] = Permission(), ["provenance"] = Provenance(),
        ["extensions"] = new JsonObject { ["language"] = "java", ["parameters"] = A(new JsonObject { ["name"] = "listener", ["type"] = "a.Listener" }) }
    };
    public static Dictionary<string, JsonObject> Types() => new(StringComparer.Ordinal)
    { [TypeB] = new() { ["name"] = "Listener", ["kind"] = "interface", ["extensions"] = new JsonObject { ["language"] = "java" } } };

    public static JsonObject Identity(string binding = BindingA, int position = 0, string? type = TypeB, string spelling = "a.Listener") =>
        new() { ["binding_id"] = binding, ["parameter_index"] = position, ["type_id"] = type, ["type_spelling"] = spelling };

    private static JsonObject CandidateType(bool candidate, params string[] reasons) => new()
    {
        ["is_candidate"] = candidate, ["role"] = candidate ? "candidate" : "unknown", ["reasons"] = A(reasons.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["callback_lifecycle_proven"] = false, ["inheritance"] = new JsonArray()
    };

    private static JsonObject Endpoint(string mode, string language = "java", JsonNode? own = null, JsonNode? inherited = null) => new()
    {
        ["entry_id"] = BindingA, ["canonical_signature"] = "java:a.A#onEvent(a.Listener)",
        ["parameters"] = A(new JsonObject { ["name"] = "listener", ["type"] = "a.Listener" }),
        ["arguments"] = A(new JsonObject { ["text"] = "a.Listener", ["status"] = "resolved", ["resolved_binding_id"] = TypeB }),
        ["return_type"] = null, ["hidl_results"] = new JsonArray(), ["source_language"] = language,
        ["explicit_oneway"] = own, ["inherited_interface_oneway"] = inherited, ["reply_mode"] = mode,
        ["permission"] = Permission(), ["provenance"] = Provenance(), ["version_evidence"] = new JsonArray(), ["runtime_authority_proven"] = false
    };

    private static JsonObject Registration(string hash = RegistrationHash, string binding = BindingA, int position = 0,
        string? type = TypeB, string spelling = "a.Listener", string? parameterName = "listener", string role = "registration-candidate",
        bool resolved = true, bool spelled = true, bool binder = false) => new()
    {
        ["candidate_id"] = hash, ["binding_id"] = binding, ["parameter_index"] = position, ["type_id"] = type,
        ["type_spelling"] = spelling, ["owner"] = "a.A", ["language"] = "java", ["parameter_name"] = parameterName,
        ["direction"] = "unknown", ["parameter_annotations"] = new JsonArray(), ["role"] = role,
        ["confidence"] = resolved ? "source-structure-plus-role-heuristic" : "name-heuristic-unresolved-type",
        ["evidence_reasons"] = A((resolved ? new[] { binder ? "Binder/HIDL interface can be passed as an endpoint; callback direction unproven" : "callback-like declared type name" } : [])
            .Concat(spelled ? ["callback-like parameter/type spelling"] : []).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["lifecycle_proven"] = false, ["registration_guaranteed"] = false, ["matching_unregistration_guaranteed"] = false,
        ["permission"] = Permission(), ["provenance"] = Provenance(), ["applicability"] = new JsonArray(),
        ["unresolved"] = A("Callback invocation direction, registration effects, executor/threading, lifetime, cancellation, ordering, buffering and backpressure require contract evidence")
    };

    private static IReadOnlyList<SourceFactCase> Create()
    {
        var result = new List<SourceFactCase>
        {
            new("FACT-CB01", FactOperation.TypeCandidate, Types()[TypeB], CandidateType(true, "callback-like declared type name"), "test_name_never_proves_callback_lifecycle"),
            new("FACT-CB02", FactOperation.TypeCandidate, new() { ["name"] = "IEndpoint", ["kind"] = "aidl-interface", ["extensions"] = new JsonObject { ["language"] = "aidl" } },
                CandidateType(true, "Binder/HIDL interface can be passed as an endpoint; callback direction unproven"), "test_binder_endpoint_candidate_does_not_require_callback_name"),
            new("FACT-CB03", FactOperation.TypeCandidate, new() { ["name"] = "Ordinary", ["kind"] = "class" }, CandidateType(false), "Ordinary source type retains unknown role"),
            new("FACT-CB04", FactOperation.TypeCandidate, new() { ["name"] = "Ordinary", ["extensions"] = new JsonObject { ["annotations"] = A(new JsonObject { ["name"] = "java.lang.FunctionalInterface" }) } },
                CandidateType(true, "FunctionalInterface annotation retained; invocation role not established"), "callback_source_facts.py:FunctionalInterface source annotation"),
            new("FACT-CB05", FactOperation.Endpoint, Method("onEvent"), Endpoint("unknown"), "test_missing_oneway_not_invented_synchronous"),
            new("FACT-CB06", FactOperation.Registration, Method(), A(Registration()), "test_parameter_position_and_permission_are_retained", Types: Types(),
                IdentityInputs: [Identity()], IdentityHashes: [RegistrationHash])
        };
        var inherited = Method("onEvent"); inherited["extensions"]!["language"] = "aidl"; inherited["extensions"]!["oneway"] = false;
        result.Add(new("FACT-CB07", FactOperation.Endpoint, inherited, Endpoint("oneway", "aidl", false, true), "test_inherited_oneway_controls_endpoint_reply_mode",
            Owner: new() { ["extensions"] = new JsonObject { ["oneway"] = true } }));
        result.Add(new("FACT-CB08", FactOperation.Endpoint, (JsonObject)inherited.DeepClone(), Endpoint("unknown", "aidl", false), "test_missing_inherited_aidl_flag_does_not_prove_synchronous"));
        var synchronous = (JsonObject)inherited.DeepClone(); synchronous["extensions"]!["interface_oneway"] = false;
        result.Add(new("FACT-CB09", FactOperation.Endpoint, synchronous, Endpoint("synchronous", "aidl", false, false), "test_explicit_false_both_aidl_flags_prove_reply_mode"));
        var hidl = Method("onEvent"); hidl["extensions"]!["language"] = "hidl"; hidl["extensions"]!["oneway"] = false;
        result.Add(new("FACT-CB10", FactOperation.Endpoint, hidl, Endpoint("synchronous", "hidl", false), "test_hidl_method_flag_has_no_interface_oneway_inheritance"));
        var missingName = Method(); missingName["extensions"]!["parameters"]![0]!["name"] = null;
        result.Add(new("FACT-CB11", FactOperation.Registration, missingName, A(Registration(parameterName: null)), "test_missing_sdk_parameter_name_is_preserved_without_crash", Types: Types(),
            IdentityInputs: [Identity()], IdentityHashes: [RegistrationHash]));
        var executor = Method(); executor["arguments"] = A(new JsonObject { ["text"] = "Executor", ["status"] = "unknown" });
        executor["extensions"]!["parameters"] = A(new JsonObject { ["name"] = "executor", ["annotations"] = A(new JsonObject { ["name"] = "CallbackExecutor" }) });
        result.Add(new("FACT-CB12", FactOperation.Registration, executor, new JsonArray(), "test_callback_executor_annotation_alone_is_not_callback_parameter"));
        var unresolved = Method(); unresolved["arguments"] = A(new JsonObject { ["text"] = "Listener", ["status"] = "unknown" });
        result.Add(new("FACT-CB13", FactOperation.Registration, unresolved, A(Registration(UnresolvedHash, type: null, spelling: "Listener", resolved: false)),
            "test_same_short_name_does_not_prove_type_resolution", Types: Types(), IdentityInputs: [Identity(type: null, spelling: "Listener")], IdentityHashes: [UnresolvedHash]));
        result.Add(new("FACT-CB14", FactOperation.Registration, Method(binding: BindingC), A(Registration(OverloadHash, BindingC)),
            "test_overloads_and_parameter_positions_have_different_ids", Types: Types(), IdentityInputs: [Identity(BindingC)], IdentityHashes: [OverloadHash]));
        var position = Method(); position["arguments"]!.AsArray().Insert(0, new JsonObject { ["text"] = "int", ["status"] = "primitive" });
        position["extensions"]!["parameters"]!.AsArray().Insert(0, new JsonObject { ["name"] = "count", ["type"] = "int" });
        result.Add(new("FACT-CB15", FactOperation.Registration, position, A(Registration(PositionHash, position: 1)),
            "Parameter ordinal after primitive retains distinct identity", Types: Types(), IdentityInputs: [Identity(position: 1)], IdentityHashes: [PositionHash]));
        var unicode = Method(); unicode["arguments"]![0]!["text"] = "a.Lístener😀";
        result.Add(new("FACT-CB16", FactOperation.Registration, unicode, A(Registration(UnicodeHash, spelling: "a.Lístener😀")),
            "LegacyAsciiIdentity Unicode golden vector; independent PowerShell receipt", Types: Types(), IdentityInputs: [Identity(spelling: "a.Lístener😀")], IdentityHashes: [UnicodeHash]));
        var binderMethod = Method(); binderMethod["arguments"]![0]!["text"] = "a.IEndpoint"; binderMethod["extensions"]!["parameters"]![0]!["name"] = "endpoint";
        var binderTypes = Types(); binderTypes[TypeB] = new() { ["name"] = "IEndpoint", ["kind"] = "aidl-interface", ["extensions"] = new JsonObject { ["language"] = "aidl" } };
        result.Add(new("FACT-CB17", FactOperation.Registration, binderMethod, A(Registration(BinderHash, spelling: "a.IEndpoint", parameterName: "endpoint", spelled: false, binder: true)),
            "test_binder_endpoint_candidate_does_not_require_callback_name", Types: binderTypes, IdentityInputs: [Identity(spelling: "a.IEndpoint")], IdentityHashes: [BinderHash]));
        var removal = Registration(OverloadHash, BindingC, role: "unregistration-candidate");
        var pair = new JsonObject { ["registration_candidate_id"] = RegistrationHash, ["unregistration_candidate_id"] = OverloadHash, ["status"] = "candidate",
            ["basis"] = "same declared owner/language/callback parameter type", ["lifecycle_pair_proven"] = false };
        result.Add(new("FACT-CB18", FactOperation.Pairs, new(), A(pair), "test_unregistration_pair_remains_candidate", Additional: A(Registration(), removal)));
        var differentOwner = (JsonObject)removal.DeepClone(); differentOwner["owner"] = "other.Owner";
        result.Add(new("FACT-CB19", FactOperation.Pairs, new(), new JsonArray(), "Pairing cannot borrow another owner", Additional: A(Registration(), differentOwner)));
        result.Add(new("FACT-CB20", FactOperation.Registration, Method("unregisterListener", BindingC), A(Registration(OverloadHash, BindingC, role: "unregistration-candidate")),
            "test_unregistration_pair_remains_candidate (actual removal-role producer)", Types: Types(), IdentityInputs: [Identity(BindingC)], IdentityHashes: [OverloadHash]));
        result.Add(new("FACT-CB21", FactOperation.Endpoint, (JsonObject)synchronous.DeepClone(), Endpoint("synchronous", "aidl", false, false),
            "Explicit inherited-interface false is not replaced by owner fallback true", Owner: new() { ["extensions"] = new JsonObject { ["oneway"] = true } }));
        var primitive = Method(); primitive["arguments"]![0]!["status"] = "primitive";
        result.Add(new("FACT-CB22", FactOperation.Registration, primitive, new JsonArray(), "Primitive argument cannot become callback candidate", Types: Types()));
        AddTaxonomy(result); AddApplicability(result);
        return result;
    }

    private static JsonObject TaxonomyInput(string annotationName = "android.annotation.SystemService", string arguments = "\"clipboard\"") => new()
    {
        ["entry_id"] = BindingA, ["owner"] = "android.content.ClipboardManager", ["package"] = "android.content", ["provenance"] = A(new JsonObject { ["source_id"] = "s" }),
        ["extensions"] = new JsonObject { ["annotations"] = A(new JsonObject { ["name"] = annotationName, ["arguments"] = arguments, ["raw"] = "source annotation" }), ["imports"] = new JsonArray() }
    };
    private static JsonObject TaxonomyOutput(JsonObject input, bool qualified, string? literal = null, string? symbol = null, bool hasEvidence = true)
    {
        var rows = new JsonArray();
        if (hasEvidence) rows.Add(new JsonObject
        {
            ["dimension"] = "android-system-service-declaration", ["owner_entry_id"] = BindingA, ["owner"] = "android.content.ClipboardManager",
            ["service_string_literal"] = literal, ["service_symbol_expression"] = symbol, ["annotation_namespace_proven"] = qualified,
            ["source_annotation"] = input["extensions"]!["annotations"]![0]!.DeepClone(),
            ["confidence"] = qualified ? "explicit-android-annotation" : "annotation-namespace-unresolved", ["provenance"] = A(new JsonObject { ["source_id"] = "s" }),
            ["functional_group_membership_proven"] = false,
            ["unresolved"] = A(symbol is not null ? "Symbolic service values require exact same-snapshot constant resolution" : "Service selector is literal or unresolved",
                "Small functional groups, member roles, descriptions and dependency closure require separate curated/evidenced mapping")
        });
        return new() { ["entry_id"] = BindingA, ["functional_evidence"] = rows,
            ["namespace_search_evidence"] = new JsonObject { ["package"] = "android.content", ["owner"] = "android.content.ClipboardManager", ["functional_group_inferred"] = false },
            ["imported_group_memberships"] = new JsonArray(), ["local_group_overrides_separate"] = true, ["classification"] = hasEvidence ? "evidence-present" : "unclassified-searchable" };
    }
    private static void AddTaxonomy(List<SourceFactCase> result)
    {
        var full = TaxonomyInput(); result.Add(new("FACT-FN01", FactOperation.Taxonomy, full, TaxonomyOutput(full, true, "clipboard"), "test_explicit_service_annotation_retained_without_automatic_group_policy"));
        var custom = TaxonomyInput("custom.SystemService"); result.Add(new("FACT-FN02", FactOperation.Taxonomy, custom, TaxonomyOutput(custom, false, "clipboard"), "test_custom_annotation_name_not_borrowed_as_android_annotation"));
        var shortName = TaxonomyInput("SystemService", "Context.CLIPBOARD_SERVICE"); result.Add(new("FACT-FN03", FactOperation.Taxonomy, shortName, TaxonomyOutput(shortName, false, symbol: "Context.CLIPBOARD_SERVICE"), "test_short_annotation_requires_import (no import)"));
        var imported = (JsonObject)shortName.DeepClone(); imported["extensions"]!["imports"] = A("android.annotation.SystemService");
        result.Add(new("FACT-FN04", FactOperation.Taxonomy, imported, TaxonomyOutput(imported, true, symbol: "Context.CLIPBOARD_SERVICE"), "test_short_annotation_requires_import (exact import)"));
        var symbolic = TaxonomyInput(arguments: "value=Context.CLIPBOARD_SERVICE"); result.Add(new("FACT-FN05", FactOperation.Taxonomy, symbolic, TaxonomyOutput(symbolic, true, symbol: "Context.CLIPBOARD_SERVICE"), "test_symbol_kept_separate_from_string_literal"));
        var empty = TaxonomyInput(); empty["extensions"]!["annotations"] = new JsonArray(); result.Add(new("FACT-FN06", FactOperation.Taxonomy, empty, TaxonomyOutput(empty, false, hasEvidence: false), "test_package_namespace_does_not_become_functional_group"));
        var expression = TaxonomyInput(arguments: "selectService()"); result.Add(new("FACT-FN07", FactOperation.Taxonomy, expression, TaxonomyOutput(expression, true), "test_expression_not_evaluated"));
    }

    public static JsonObject EvidenceSource(string path = "aidl_api/foo/1/p/Foo.aidl", string? hash = null) =>
        new() { ["repository"] = "r", ["revision"] = "c", ["path"] = path, ["sha256"] = hash ?? BindingA };
    public static JsonObject Linkage() => new()
    {
        ["build_evidence"] = EvidenceSource("Android.bp", TypeB), ["build_module_name"] = "foo", ["module_kind"] = "aidl_interface", ["frozen_dump_version"] = "1",
        ["declared_versions"] = A(new JsonObject { ["version"] = "1", ["imports"] = A("bar-V2") }),
        ["declared_flags"] = new JsonObject { ["apex_available"] = A("com.android.foo", "//apex_available:platform") },
        ["published_hash_lines"] = A(new JsonObject { ["source"] = EvidenceSource("aidl_api/foo/1/.hash", BindingC), ["line"] = 1, ["literal"] = new string('d', 40), ["format"] = "published-aidl-hash-hex40" })
    };
    private static JsonObject Applicability(JsonObject source) => new()
    {
        ["contract"] = "source-interface-evidence/1", ["source"] = source.DeepClone(), ["declared_module"] = null,
        ["interface_candidates"] = new JsonArray(), ["module_candidates"] = new JsonArray(),
        ["applicability"] = new JsonObject { ["unknown_reason"] = "No verified interface-to-generated-hash or installed-module version mapping", ["x-raw"] = new JsonObject { ["source"] = source.DeepClone() } },
        ["unknown_reasons"] = new JsonArray(), ["runtime_observation"] = false, ["os_authority_granted"] = false, ["client_authority_granted"] = false
    };
    private static void AddApplicability(List<SourceFactCase> result)
    {
        var source = EvidenceSource(); var linked = Linkage(); var full = Applicability(source);
        full["declared_module"] = new JsonObject { ["domain"] = "soong-build-module", ["name"] = "foo", ["kind"] = "aidl_interface", ["source"] = EvidenceSource("Android.bp", TypeB),
            ["frozen_dump_version"] = "1", ["declared_versions"] = A(new JsonObject { ["version"] = "1", ["imports"] = A("bar-V2") }) };
        full["module_candidates"] = A(new JsonObject { ["name"] = "com.android.foo", ["domain"] = "soong-apex-availability-selector", ["installed_identity_proven"] = false, ["version_range"] = null, ["source"] = EvidenceSource("Android.bp", TypeB) },
            new JsonObject { ["name"] = "//apex_available:platform", ["domain"] = "soong-apex-availability-selector", ["installed_identity_proven"] = false, ["version_range"] = null, ["source"] = EvidenceSource("Android.bp", TypeB) });
        full["interface_candidates"] = A(new JsonObject { ["domain"] = "stable-aidl-frozen-dump", ["version_literal"] = "1", ["published_hash"] = new string('d', 40), ["hash_format"] = "published-aidl-hash-hex40",
            ["descriptor"] = null, ["source"] = EvidenceSource("aidl_api/foo/1/.hash", BindingC), ["line"] = 1, ["generated_hash_selection_proven"] = false, ["source_schema_equivalence_proven"] = false });
        full["unknown_reasons"] = A("Soong build/module/APEX selectors do not establish installed package identity or version range", "Published hashes require compiler hash-selection and source-schema equivalence evidence before activation");
        result.Add(new("FACT-IF01", FactOperation.Applicability, source, full, "test_real_source_facts_preserved_without_fabricating_module_range", Linkage: linked));
        var absent = Applicability(source); absent["unknown_reasons"] = A("No unique declared build module linked", "No published hash linked to this declaration");
        result.Add(new("FACT-IF02", FactOperation.Applicability, (JsonObject)source.DeepClone(), absent, "test_no_module_and_no_hash_remain_named_unknowns", Linkage: new()));
        var hidl = new JsonObject { ["source"] = EvidenceSource("current.txt", new string('e', 64)), ["declared_identifier"] = "android.hardware.foo@1.0::IFoo", ["literal"] = new string('f', 64), ["format"] = "published-hidl-hash-hex64-and-identifier", ["line"] = 2 };
        var hidlExpected = Applicability(source); hidlExpected["interface_candidates"] = A(new JsonObject { ["domain"] = "hidl-published-identifier", ["descriptor"] = "android.hardware.foo@1.0::IFoo", ["minimum_version"] = null,
            ["published_hash"] = new string('f', 64), ["hash_format"] = "published-hidl-hash-hex64-and-identifier", ["source"] = EvidenceSource("current.txt", new string('e', 64)), ["line"] = 2, ["generated_hash_selection_proven"] = false, ["source_schema_equivalence_proven"] = false });
        hidlExpected["unknown_reasons"] = A("No unique declared build module linked", "Published hashes require compiler hash-selection and source-schema equivalence evidence before activation");
        result.Add(new("FACT-IF03", FactOperation.Applicability, (JsonObject)source.DeepClone(), hidlExpected, "test_hidl_package_version_not_android_api_or_integer_interface_version", Linkage: new(), Additional: A(hidl)));
        var ambiguous = (JsonObject)absent.DeepClone(); ambiguous["unknown_reasons"] = A("Ambiguous build-module source membership", "No unique declared build module linked", "No published hash linked to this declaration");
        result.Add(new("FACT-IF04", FactOperation.Applicability, (JsonObject)source.DeepClone(), ambiguous, "Ambiguous module remains named unknown", Linkage: new() { ["candidates"] = 2 }));
    }

    public static JsonNode Invoke(SourceFactCase test, ICallbackSourceFacts callbacks, IFunctionalTaxonomySourceFacts taxonomy, IInterfaceApplicabilitySourceFacts applicability) => test.Operation switch
    {
        FactOperation.TypeCandidate => callbacks.TypeCandidate(test.Input), FactOperation.Endpoint => callbacks.EndpointMethod(test.Input, test.Owner),
        FactOperation.Registration => callbacks.RegistrationCandidates(test.Input, test.Types ?? new Dictionary<string, JsonObject>()), FactOperation.Pairs => callbacks.PossiblePairs(test.Additional!),
        FactOperation.Taxonomy => taxonomy.Taxonomy(test.Input), FactOperation.Applicability => applicability.Produce(test.Input, test.Linkage!, test.Additional),
        _ => throw new InvalidOperationException("Unknown fixture operation")
    };

    public static void AssertContract(SourceFactCase test, ICallbackSourceFacts callbacks, IFunctionalTaxonomySourceFacts taxonomy, IInterfaceApplicabilitySourceFacts applicability)
    {
        var before = new JsonObject { ["input"] = test.Input.DeepClone(), ["owner"] = test.Owner?.DeepClone(), ["linkage"] = test.Linkage?.DeepClone(), ["additional"] = test.Additional?.DeepClone(),
            ["types"] = new JsonObject((test.Types ?? new Dictionary<string, JsonObject>()).Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value.DeepClone()))) };
        var adapter = new ScriptedSourceExtractor("source-facts", _ => new([new JsonObject { ["output"] = Invoke(test, callbacks, taxonomy, applicability) }], [], StructuralParseStatus.Clean));
        SourceExtractionContractCases.AssertContract(test.Extraction(), adapter);
        var after = new JsonObject { ["input"] = test.Input.DeepClone(), ["owner"] = test.Owner?.DeepClone(), ["linkage"] = test.Linkage?.DeepClone(), ["additional"] = test.Additional?.DeepClone(),
            ["types"] = new JsonObject((test.Types ?? new Dictionary<string, JsonObject>()).Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value.DeepClone()))) };
        Assert.True(JsonNode.DeepEquals(before, after), "Fact production changed source input or type/proof context");
    }
}
