using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceProducerDomainFailure(SourceFactCase Case, string Error);

// Expected values are the existing independent full-output goldens plus the
// exact deepcopy/default/short-circuit clause in the pinned original bodies.
public static class SourceProducerDomainContractCases
{
    private static SourceFactCase Copy(string id, string baseline) => SourceFactContractCases.Get(baseline) with
    {
        Id = id, Input = (JsonObject)SourceFactContractCases.Get(baseline).Input.DeepClone(),
        Expected = SourceFactContractCases.Get(baseline).Expected.DeepClone(),
        Additional = (JsonArray?)SourceFactContractCases.Get(baseline).Additional?.DeepClone(),
        Evidence = "Pinned callback_source_facts.py / functional_source_facts.py JSON-domain reached operations and deepcopy semantics"
    };
    private static JsonObject Extensions(SourceFactCase value) => value.Input["extensions"]!.AsObject();
    private static IEnumerable<JsonNode?> Values() => new JsonNode?[] { null, JsonValue.Create(7), JsonValue.Create(true), new JsonArray(), new JsonObject(), JsonValue.Create("") };
    public static IReadOnlyList<SourceFactCase> All { get; } = Create();
    private static IReadOnlyList<SourceFactCase> Create()
    {
        var result = new List<SourceFactCase>(); var n = 0;
        foreach (var value in Values())
        {
            var endpoint = Copy("DOMAIN-ENDPOINT-LANGUAGE-" + n, "FACT-CB05");
            Extensions(endpoint)["language"] = value?.DeepClone(); endpoint.Expected["source_language"] = value?.DeepClone(); result.Add(endpoint);
            foreach (var (input, output) in new[] { ("parameters", "parameters"), ("results", "hidl_results"), ("device_resolution_variants", "version_evidence") })
            {
                var retained = Copy("DOMAIN-ENDPOINT-" + input + "-" + n, "FACT-CB05");
                Extensions(retained)[input] = value?.DeepClone(); retained.Expected[output] = value?.DeepClone(); result.Add(retained);
            }
            foreach (var field in new[] { "arguments", "provenance", "permission", "return_type" })
            {
                var retained = Copy("DOMAIN-ENDPOINT-" + field + "-" + n, "FACT-CB05");
                retained.Input[field] = value?.DeepClone(); retained.Expected[field] = value?.DeepClone(); result.Add(retained);
            }
            var inheritance = Copy("DOMAIN-TYPE-INHERITANCE-" + n, "FACT-CB03");
            inheritance.Input["extensions"] = new JsonObject { ["declared_type_shape"] = new JsonObject { ["supertypes"] = value?.DeepClone() } };
            inheritance.Expected["inheritance"] = value?.DeepClone(); result.Add(inheritance);
            var name = Copy("DOMAIN-TYPE-NAME-" + n, "FACT-CB03"); name.Input["name"] = value?.DeepClone(); result.Add(name);
            // parameters is fetched eagerly but len/index is never reached for primitive arguments.
            var primitive = Copy("DOMAIN-REGISTRATION-UNUSED-PARAMETERS-" + n, "FACT-CB06") with { Expected = new JsonArray(), IdentityInputs = [], IdentityHashes = [] };
            primitive.Input["arguments"]![0]!["status"] = "primitive"; Extensions(primitive)["parameters"] = value?.DeepClone(); result.Add(primitive);
            n++;
        }
        foreach (var empty in new JsonNode[] { new JsonArray(), new JsonObject(), JsonValue.Create("")! })
        {
            var type = Copy("DOMAIN-TYPE-EMPTY-ANNOTATIONS-" + result.Count, "FACT-CB03"); type.Input["extensions"] = new JsonObject { ["annotations"] = empty.DeepClone() }; result.Add(type);
            var taxonomy = Copy("DOMAIN-TAXONOMY-EMPTY-ANNOTATIONS-" + result.Count, "FACT-FN06"); taxonomy.Input["extensions"] = new JsonObject { ["annotations"] = empty.DeepClone() }; result.Add(taxonomy);
            var registration = Copy("DOMAIN-REGISTRATION-EMPTY-ARGUMENTS-" + result.Count, "FACT-CB06") with { Expected = new JsonArray(), IdentityInputs = [], IdentityHashes = [] };
            registration.Input["arguments"] = empty.DeepClone(); result.Add(registration);
        }
        var shortCircuit = Copy("DOMAIN-TYPE-ANNOTATION-ANY-SHORT-CIRCUIT", "FACT-CB04"); Extensions(shortCircuit)["annotations"]!.AsArray().Add((JsonNode?)null); result.Add(shortCircuit);
        var unusedName = Copy("DOMAIN-REGISTRATION-UNUSED-METHOD-NAME", "FACT-CB06") with { Expected = new JsonArray(), IdentityInputs = [], IdentityHashes = [] };
        unusedName.Input["name"] = null; unusedName.Input["arguments"]![0]!["status"] = "primitive"; result.Add(unusedName);
        // Full-qualified annotations never evaluate membership in imports.
        var qualified = SourceFactContractCases.All.First(x => x.Operation == FactOperation.Taxonomy && x.Input["extensions"]?["annotations"]?[0]?["name"]?.GetValue<string>() == "android.annotation.SystemService");
        foreach (var imports in Values())
        {
            var retained = Copy("DOMAIN-TAXONOMY-QUALIFIED-UNUSED-IMPORTS-" + result.Count, qualified.Id);
            Extensions(retained)["imports"] = imports?.DeepClone(); result.Add(retained);
        }
        foreach (var imports in new JsonNode[] { JsonValue.Create("prefix.android.annotation.SystemService.suffix")!, new JsonObject { ["android.annotation.SystemService"] = null } })
        {
            var retained = Copy("DOMAIN-TAXONOMY-IMPORT-MEMBERSHIP-" + result.Count, "FACT-FN04"); Extensions(retained)["imports"] = imports; result.Add(retained);
        }
        foreach (var value in new JsonNode[] { new JsonObject(), JsonValue.Create("")! })
        {
            var missingParameter = Copy("DOMAIN-REGISTRATION-ZERO-LENGTH-PARAMETERS-" + result.Count, "FACT-CB06");
            Extensions(missingParameter)["parameters"] = value; missingParameter.Expected[0]!["parameter_name"] = null; result.Add(missingParameter);
        }
        var missingMethod = Copy("DOMAIN-REGISTRATION-MISSING-METHOD-NAME", "FACT-CB06");
        missingMethod.Input.Remove("name"); missingMethod.Expected[0]!["role"] = "callback-parameter-candidate"; result.Add(missingMethod);
        var missingAnnotationName = Copy("DOMAIN-TYPE-MISSING-ANNOTATION-NAME", "FACT-CB03");
        missingAnnotationName.Input["extensions"] = new JsonObject { ["annotations"] = new JsonArray(new JsonObject()) }; result.Add(missingAnnotationName);
        foreach (var key in new[] { "owner", "language", "type_spelling", "type_id" })
        {
            var pair = Copy("DOMAIN-PAIR-BOOL-NUMBER-" + key, "FACT-CB18");
            pair.Additional![0]![key] = true; pair.Additional[1]![key] = 1; result.Add(pair);
            var nested = Copy("DOMAIN-PAIR-NESTED-BOOL-NUMBER-" + key, "FACT-CB18");
            nested.Additional![0]![key] = new JsonObject { ["x"] = new JsonArray(false, 9007199254740992L) };
            nested.Additional[1]![key] = new JsonObject { ["x"] = new JsonArray(0, 9007199254740992d) }; result.Add(nested);
        }
        return result;
    }
    public static IReadOnlyList<SourceProducerDomainFailure> Failures { get; } = CreateFailures();
    private static IReadOnlyList<SourceProducerDomainFailure> CreateFailures()
    {
        var result = new List<SourceProducerDomainFailure>();
        void Add(string id, string baseline, string error, Action<SourceFactCase> change)
        {
            var test = Copy(id, baseline); change(test); result.Add(new(test, error));
        }
        foreach (var baseline in new[] { "FACT-CB03", "FACT-CB05", "FACT-CB06", "FACT-FN01" })
            Add("DOMAIN-ERROR-NULL-EXTENSIONS-" + baseline, baseline, "object", x => x.Input["extensions"] = null);
        foreach (var baseline in new[] { "FACT-CB03", "FACT-FN01" })
            foreach (var value in new JsonNode?[] { null, JsonValue.Create(true), JsonValue.Create(7) })
            {
                var copy = value?.DeepClone();
                Add("DOMAIN-ERROR-ANNOTATIONS-" + result.Count, baseline, "iterable", x => x.Input["extensions"] = new JsonObject { ["annotations"] = copy });
            }
        foreach (var baseline in new[] { "FACT-CB03", "FACT-FN01" })
            foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), new JsonArray(), JsonValue.Create("x") })
            {
                var copy = value?.DeepClone();
                Add("DOMAIN-ERROR-ANNOTATION-ROW-" + result.Count, baseline, "object", x => x.Input["extensions"] = new JsonObject { ["annotations"] = new JsonArray(copy) });
            }
        foreach (var baseline in new[] { "FACT-CB03", "FACT-FN01" })
            foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false), new JsonArray(), new JsonObject() })
            {
                var copy = value?.DeepClone();
                Add("DOMAIN-ERROR-ANNOTATION-NAME-" + result.Count, baseline, "string", x => x.Input["extensions"] = new JsonObject { ["annotations"] = new JsonArray(new JsonObject { ["name"] = copy }) });
            }
        Add("DOMAIN-ERROR-TYPE-NULL-SHAPE", "FACT-CB03", "object", x => x.Input["extensions"] = new JsonObject { ["declared_type_shape"] = null });
        Add("DOMAIN-ERROR-ENDPOINT-OWNER-NULL-EXTENSIONS", "FACT-CB05", "object", _ => { });
        result[^1] = result[^1] with { Case = result[^1].Case with { Owner = new JsonObject { ["extensions"] = null } } };
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false) })
        {
            var copy = value?.DeepClone(); Add("DOMAIN-ERROR-ARGUMENTS-" + result.Count, "FACT-CB06", "iterable", x => x.Input["arguments"] = copy);
        }
        Add("DOMAIN-ERROR-NULL-ARGUMENT", "FACT-CB06", "object", x => x.Input["arguments"] = new JsonArray((JsonNode?)null));
        Add("DOMAIN-ERROR-NULL-PARAMETER", "FACT-CB06", "object", x => { Extensions(x)["parameters"]![0] = null; x.Input["arguments"]![0]!["text"] = "Ordinary"; });
        Add("DOMAIN-ERROR-PARAMETERS-DICTIONARY-INDEX", "FACT-CB06", "index", x => Extensions(x)["parameters"] = new JsonObject { ["0"] = new JsonObject() });
        foreach (var value in new JsonNode[] { new JsonArray(), new JsonObject() })
        {
            var copy = value.DeepClone(); Add("DOMAIN-ERROR-UNHASHABLE-RESOLVED-ID-" + result.Count, "FACT-CB06", "key", x => x.Input["arguments"]![0]!["resolved_binding_id"] = copy);
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false) })
        {
            var copy = value?.DeepClone(); Add("DOMAIN-ERROR-REACHED-PARAMETERS-" + result.Count, "FACT-CB06", "length", x => Extensions(x)["parameters"] = copy);
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false), new JsonArray(), new JsonObject() })
        {
            var copy = value?.DeepClone(); Add("DOMAIN-ERROR-REACHED-METHOD-NAME-" + result.Count, "FACT-CB06", "string", x => x.Input["name"] = copy);
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false), new JsonArray(), new JsonObject() })
        {
            var copy = value?.DeepClone();
            Add("DOMAIN-ERROR-TAXONOMY-ARGUMENTS-" + result.Count, "FACT-FN01", "string", x => x.Input["extensions"] = new JsonObject { ["annotations"] = new JsonArray(new JsonObject { ["name"] = "android.annotation.SystemService", ["arguments"] = copy }) });
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(3), JsonValue.Create(false) })
        {
            var copy = value?.DeepClone();
            Add("DOMAIN-ERROR-TAXONOMY-IMPORTS-" + result.Count, "FACT-FN01", "membership", x => x.Input["extensions"] = new JsonObject { ["annotations"] = new JsonArray(new JsonObject { ["name"] = "SystemService" }), ["imports"] = copy });
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> ErrorCases => Failures.Select(x => new object[] { x.Case.Id });
    public static SourceFactCase Get(string id) => All.Single(x => x.Id == id);
    public static SourceProducerDomainFailure GetFailure(string id) => Failures.Single(x => x.Case.Id == id);
    public static void AssertFailure(SourceProducerDomainFailure test, ICallbackSourceFacts callbacks, IFunctionalTaxonomySourceFacts taxonomy, IInterfaceApplicabilitySourceFacts applicability)
    {
        var input = test.Case.Input.DeepClone(); var owner = test.Case.Owner?.DeepClone();
        var error = Assert.Throws<ArgumentException>(() => SourceFactContractCases.Invoke(test.Case, callbacks, taxonomy, applicability));
        Assert.Contains(test.Error, error.Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(input, test.Case.Input), "Rejected input was mutated");
        Assert.True(JsonNode.DeepEquals(owner, test.Case.Owner), "Rejected owner was mutated");
    }
}
