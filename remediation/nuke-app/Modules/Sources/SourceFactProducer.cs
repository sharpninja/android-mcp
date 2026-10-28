using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class SourceFactProducer(IProfiledCanonicalJson canonical) : ICallbackSourceFacts, IFunctionalTaxonomySourceFacts, IInterfaceApplicabilitySourceFacts
{
    private static readonly Regex CallbackName = new(@"(?:Callback|Listener|Observer|DeathRecipient|OutcomeReceiver|ResultReceiver|Runnable|Callable|Consumer|Function|Predicate|Continuation)(?:$|[<$])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private const string CallbackUnknown = "Callback invocation direction, registration effects, executor/threading, lifetime, cancellation, ordering, buffering and backpressure require contract evidence";
    private const string HashUnknown = "Published hashes require compiler hash-selection and source-schema equivalence evidence before activation";

    public JsonObject TypeCandidate(JsonObject declaration)
    {
        var extensions = Object(declaration["extensions"]); var reasons = new JsonArray();
        if (Named(declaration["name"])) reasons.Add("callback-like declared type name");
        if (Objects(extensions["annotations"]).Any(x => String(x["name"])?.Split('.')[^1] == "FunctionalInterface"))
            reasons.Add("FunctionalInterface annotation retained; invocation role not established");
        if ((String(extensions["language"]) is "aidl" or "hidl") && (String(declaration["kind"]) is "aidl-interface" or "interface"))
            reasons.Add("Binder/HIDL interface can be passed as an endpoint; callback direction unproven");
        return new()
        {
            ["is_candidate"] = reasons.Count > 0, ["role"] = reasons.Count > 0 ? "candidate" : "unknown", ["reasons"] = reasons,
            ["callback_lifecycle_proven"] = false, ["inheritance"] = Property(Object(extensions["declared_type_shape"]), "supertypes", new JsonArray())
        };
    }

    public JsonObject EndpointMethod(JsonObject declaration, JsonObject? owner = null)
    {
        var extension = Object(declaration["extensions"]); var ownerExtension = Object(owner?["extensions"]);
        var own = extension["oneway"]; var inherited = extension.ContainsKey("interface_oneway") ? extension["interface_oneway"] : ownerExtension["oneway"];
        var language = String(extension["language"]);
        bool? asynchronous = language == "aidl" ? Boolean(own) == true || Boolean(inherited) == true ? true
            : Boolean(own) == false && Boolean(inherited) == false ? false : null : language == "hidl" ? Boolean(own) : null;
        return new()
        {
            ["entry_id"] = CopyRequired(declaration, "entry_id"), ["canonical_signature"] = CopyRequired(declaration, "canonical_signature"),
            ["parameters"] = Property(extension, "parameters", new JsonArray()), ["arguments"] = Property(declaration, "arguments", new JsonArray()),
            ["return_type"] = declaration["return_type"]?.DeepClone(), ["hidl_results"] = Property(extension, "results", new JsonArray()),
            ["source_language"] = language, ["explicit_oneway"] = own?.DeepClone(), ["inherited_interface_oneway"] = inherited?.DeepClone(),
            ["reply_mode"] = asynchronous == true ? "oneway" : asynchronous == false ? "synchronous" : "unknown",
            ["permission"] = declaration["permission"]?.DeepClone(), ["provenance"] = Property(declaration, "provenance", new JsonArray()),
            ["version_evidence"] = Property(extension, "device_resolution_variants", new JsonArray()), ["runtime_authority_proven"] = false
        };
    }

    public JsonArray RegistrationCandidates(JsonObject declaration, IReadOnlyDictionary<string, JsonObject> resolvedTypes)
    {
        var result = new JsonArray(); var extensions = Object(declaration["extensions"]); var parameters = Array(extensions["parameters"]);
        var arguments = Array(declaration["arguments"]);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = Object(arguments[index]);
            if (String(argument["status"]) == "primitive") continue;
            var parameter = index < parameters.Count ? Object(parameters[index]) : new JsonObject();
            JsonObject? target = null;
            if (String(argument["resolved_binding_id"]) is { } typeId) resolvedTypes.TryGetValue(typeId, out target);
            var classification = target is null ? null : TypeCandidate(target);
            var spelled = Named(argument["text"]) || Named(parameter["name"]);
            if (!spelled && Boolean(classification?["is_candidate"]) != true) continue;
            var methodName = String(declaration["name"]) ?? "";
            var role = Regex.IsMatch(methodName, @"^(?:unregister|remove|clear|unset|unsubscribe|cancel)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                ? "unregistration-candidate" : Regex.IsMatch(methodName, @"^(?:register|add|set|subscribe|request|start|bind)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? "registration-candidate" : "callback-parameter-candidate";
            var identity = new JsonObject
            {
                ["binding_id"] = CopyRequired(declaration, "entry_id"), ["parameter_index"] = index,
                ["type_id"] = argument["resolved_binding_id"]?.DeepClone(), ["type_spelling"] = argument["text"]?.DeepClone()
            };
            var candidateId = canonical.Hash(JsonSerializer.SerializeToElement(identity), JsonSerializationProfile.LegacyAsciiIdentity);
            var reasons = classification?["reasons"] is JsonArray classified ? (JsonArray)classified.DeepClone() : new JsonArray();
            if (spelled) reasons.Add("callback-like parameter/type spelling");
            result.Add(new JsonObject
            {
                ["candidate_id"] = candidateId, ["binding_id"] = identity["binding_id"]?.DeepClone(), ["parameter_index"] = index,
                ["type_id"] = identity["type_id"]?.DeepClone(), ["type_spelling"] = identity["type_spelling"]?.DeepClone(),
                ["owner"] = declaration["owner"]?.DeepClone(), ["language"] = extensions["language"]?.DeepClone(), ["parameter_name"] = parameter["name"]?.DeepClone(),
                ["direction"] = Property(parameter, "direction", JsonValue.Create("unknown")),
                ["parameter_annotations"] = parameter.ContainsKey("all_annotations") ? parameter["all_annotations"]?.DeepClone() : Property(parameter, "annotations", new JsonArray()),
                ["role"] = role, ["confidence"] = target is not null ? "source-structure-plus-role-heuristic" : "name-heuristic-unresolved-type",
                ["evidence_reasons"] = reasons, ["lifecycle_proven"] = false, ["registration_guaranteed"] = false, ["matching_unregistration_guaranteed"] = false,
                ["permission"] = declaration["permission"]?.DeepClone(), ["provenance"] = Property(declaration, "provenance", new JsonArray()),
                ["applicability"] = Property(extensions, "device_resolution_variants", new JsonArray()), ["unresolved"] = new JsonArray(CallbackUnknown)
            });
        }
        return result;
    }

    public JsonArray PossiblePairs(JsonArray candidates)
    {
        var rows = Objects(candidates).ToArray(); var result = new JsonArray();
        foreach (var registration in rows.Where(x => String(x["role"]) == "registration-candidate"))
            foreach (var removal in rows.Where(x => String(x["role"]) == "unregistration-candidate"))
                if (new[] { "owner", "language", "type_spelling", "type_id" }.All(key => JsonNode.DeepEquals(registration[key], removal[key])))
                    result.Add(new JsonObject
                    {
                        ["registration_candidate_id"] = CopyRequired(registration, "candidate_id"), ["unregistration_candidate_id"] = CopyRequired(removal, "candidate_id"),
                        ["status"] = "candidate", ["basis"] = "same declared owner/language/callback parameter type", ["lifecycle_pair_proven"] = false
                    });
        return result;
    }

    public JsonObject Taxonomy(JsonObject declaration)
    {
        var extensions = Object(declaration["extensions"]); var rows = new JsonArray();
        foreach (var annotation in Objects(extensions["annotations"]))
        {
            var name = String(annotation["name"]) ?? "";
            if (name.Split('.')[^1] != "SystemService") continue;
            var qualified = name == "android.annotation.SystemService" || name == "SystemService" &&
                Array(extensions["imports"]).Any(x => String(x) == "android.annotation.SystemService");
            var value = Regex.Replace((String(annotation["arguments"]) ?? "").Trim(), @"^value\s*=\s*", "", RegexOptions.CultureInvariant);
            string? literal = null;
            if (value.StartsWith('"'))
                try { literal = JsonSerializer.Deserialize<string>(value); }
                catch (JsonException) { }
            var symbol = Regex.IsMatch(value, @"^[A-Za-z_$][\w$]*(?:\.[A-Za-z_$][\w$]*)*$", RegexOptions.CultureInvariant) ? value : null;
            rows.Add(new JsonObject
            {
                ["dimension"] = "android-system-service-declaration", ["owner_entry_id"] = CopyRequired(declaration, "entry_id"), ["owner"] = declaration["owner"]?.DeepClone(),
                ["service_string_literal"] = literal, ["service_symbol_expression"] = symbol, ["annotation_namespace_proven"] = qualified, ["source_annotation"] = annotation.DeepClone(),
                ["confidence"] = qualified ? "explicit-android-annotation" : "annotation-namespace-unresolved", ["provenance"] = Property(declaration, "provenance", new JsonArray()),
                ["functional_group_membership_proven"] = false,
                ["unresolved"] = new JsonArray(symbol is not null ? "Symbolic service values require exact same-snapshot constant resolution" : "Service selector is literal or unresolved",
                    "Small functional groups, member roles, descriptions and dependency closure require separate curated/evidenced mapping")
            });
        }
        return new()
        {
            ["entry_id"] = CopyRequired(declaration, "entry_id"), ["functional_evidence"] = rows,
            ["namespace_search_evidence"] = new JsonObject { ["package"] = declaration["package"]?.DeepClone(), ["owner"] = declaration["owner"]?.DeepClone(), ["functional_group_inferred"] = false },
            ["imported_group_memberships"] = new JsonArray(), ["local_group_overrides_separate"] = true, ["classification"] = rows.Count > 0 ? "evidence-present" : "unclassified-searchable"
        };
    }

    public JsonObject Produce(JsonObject source, JsonObject linkage, JsonArray? hidlHashes = null)
    {
        var identity = new JsonObject();
        foreach (var key in new[] { "repository", "revision", "path", "sha256" }) identity[key] = CopyRequired(source, key);
        var interfaces = new JsonArray(); var modules = new JsonArray(); var unknown = new JsonArray();
        var result = new JsonObject
        {
            ["contract"] = "source-interface-evidence/1", ["source"] = identity, ["declared_module"] = null,
            ["interface_candidates"] = interfaces, ["module_candidates"] = modules,
            ["applicability"] = new JsonObject { ["unknown_reason"] = "No verified interface-to-generated-hash or installed-module version mapping", ["x-raw"] = new JsonObject { ["source"] = identity.DeepClone() } },
            ["unknown_reasons"] = unknown, ["runtime_observation"] = false, ["os_authority_granted"] = false, ["client_authority_granted"] = false
        };
        if (linkage["candidates"] is JsonValue candidateCount && candidateCount.TryGetValue<int>(out var count) && count > 1)
            unknown.Add("Ambiguous build-module source membership");
        if (linkage["build_evidence"] is JsonObject { Count: > 0 } build)
        {
            SameRevision(source, build);
            result["declared_module"] = new JsonObject
            {
                ["domain"] = "soong-build-module", ["name"] = CopyRequired(linkage, "build_module_name"), ["kind"] = CopyRequired(linkage, "module_kind"),
                ["source"] = build.DeepClone(), ["frozen_dump_version"] = linkage["frozen_dump_version"]?.DeepClone(), ["declared_versions"] = Property(linkage, "declared_versions", new JsonArray())
            };
            if (Object(linkage["declared_flags"])["apex_available"] is JsonArray selectors)
                foreach (var selector in selectors)
                    if (String(selector) is { } selected)
                        modules.Add(new JsonObject { ["name"] = selected, ["domain"] = "soong-apex-availability-selector", ["installed_identity_proven"] = false, ["version_range"] = null, ["source"] = build.DeepClone() });
            unknown.Add("Soong build/module/APEX selectors do not establish installed package identity or version range");
        }
        else unknown.Add("No unique declared build module linked");
        foreach (var fact in Objects(linkage["published_hash_lines"]))
        {
            var proofSource = Object(fact["source"]); SameRevision(source, proofSource);
            interfaces.Add(new JsonObject
            {
                ["domain"] = "stable-aidl-frozen-dump", ["version_literal"] = linkage["frozen_dump_version"]?.DeepClone(),
                ["published_hash"] = CopyRequired(fact, "literal"), ["hash_format"] = CopyRequired(fact, "format"), ["descriptor"] = null,
                ["source"] = proofSource.DeepClone(), ["line"] = CopyRequired(fact, "line"), ["generated_hash_selection_proven"] = false, ["source_schema_equivalence_proven"] = false
            });
        }
        foreach (var fact in Objects(hidlHashes))
        {
            var proofSource = Object(fact["source"]); SameRevision(source, proofSource);
            interfaces.Add(new JsonObject
            {
                ["domain"] = "hidl-published-identifier", ["descriptor"] = CopyRequired(fact, "declared_identifier"), ["minimum_version"] = null,
                ["published_hash"] = CopyRequired(fact, "literal"), ["hash_format"] = CopyRequired(fact, "format"), ["source"] = proofSource.DeepClone(),
                ["line"] = CopyRequired(fact, "line"), ["generated_hash_selection_proven"] = false, ["source_schema_equivalence_proven"] = false
            });
        }
        unknown.Add(interfaces.Count > 0 ? HashUnknown : "No published hash linked to this declaration");
        return result;
    }

    private static void SameRevision(JsonObject source, JsonObject proof)
    {
        foreach (var key in new[] { "repository", "revision" })
            if (!source.ContainsKey(key) || !proof.ContainsKey(key) || !JsonNode.DeepEquals(source[key], proof[key]))
                throw new ArgumentException("Proof and declaration must share repository/revision");
    }
    private static bool Named(JsonNode? value) => String(value) is { } name && CallbackName.IsMatch(name);
    private static string? String(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;
    private static bool? Boolean(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean) ? boolean : null;
    private static JsonObject Object(JsonNode? value) => value is null ? new() : value as JsonObject ?? throw new ArgumentException("Expected source fact object");
    private static JsonArray Array(JsonNode? value) => value is null ? new() : value as JsonArray ?? throw new ArgumentException("Expected source fact array");
    private static IEnumerable<JsonObject> Objects(JsonNode? value) => Array(value).Select(x => x as JsonObject ?? throw new ArgumentException("Expected source fact object row"));
    private static JsonNode? Property(JsonObject value, string key, JsonNode? fallback) => value.ContainsKey(key) ? value[key]?.DeepClone() : fallback?.DeepClone();
    private static JsonNode? CopyRequired(JsonObject value, string key) => value.ContainsKey(key) ? value[key]?.DeepClone() : throw new ArgumentException("Required source fact field: " + key);
}
