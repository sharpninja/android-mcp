using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record SourceProducerValueCase(string Id, SourceFactCase Contract, JsonNode? Input, JsonNode? Owner = null, JsonNode? Types = null, string? Error = null);
public static class SourceProducerValueContractCases
{
    private static SourceFactCase Clone(string id, string baseline) => SourceFactContractCases.Get(baseline) with { Id = id, Input = (JsonObject)SourceFactContractCases.Get(baseline).Input.DeepClone(), Expected = SourceFactContractCases.Get(baseline).Expected.DeepClone() };
    private static JsonObject Types() => new(SourceFactContractCases.Types().Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value.DeepClone())));
    public static IReadOnlyList<SourceProducerValueCase> All { get; } = Create();
    private static IReadOnlyList<SourceProducerValueCase> Create()
    {
        var result = new List<SourceProducerValueCase>();
        foreach (var baseline in new[] { "FACT-CB03", "FACT-CB05", "FACT-CB06", "FACT-FN01" })
            foreach (var input in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0), JsonValue.Create(""), new JsonArray(), new JsonArray(new JsonObject()) })
            {
                var id = "VALUE-INVALID-DOCUMENT-" + result.Count; result.Add(new(id, Clone(id, baseline), input, Types: Types(), Error: "object"));
            }
        foreach (var owner in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0), JsonValue.Create(""), new JsonArray(), new JsonObject() })
        {
            var id = "VALUE-ENDPOINT-FALSEY-OWNER-" + result.Count; var test = Clone(id, "FACT-CB05"); result.Add(new(id, test, test.Input, owner));
        }
        foreach (var owner in new JsonNode[] { JsonValue.Create(true)!, JsonValue.Create(1)!, JsonValue.Create("x")!, new JsonArray(1) })
        {
            var id = "VALUE-ENDPOINT-TRUTHY-INVALID-OWNER-" + result.Count; var test = Clone(id, "FACT-CB05"); result.Add(new(id, test, test.Input, owner, Error: "object"));
        }
        foreach (var target in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0), JsonValue.Create(-0d), JsonValue.Create(""), new JsonArray(), new JsonObject() })
        {
            var id = "VALUE-REGISTRATION-FALSEY-TARGET-" + result.Count;
            var test = CallbackProducerCompletionContractCases.Get("CB-COMP01") with { Id = id, Input = SourceFactContractCases.Method() };
            result.Add(new(id, test, test.Input, Types: new JsonObject { [SourceFactContractCases.TypeB] = target }));
        }
        foreach (var target in new JsonNode[] { JsonValue.Create(true)!, JsonValue.Create(1)!, JsonValue.Create("x")!, new JsonArray(1) })
        {
            var id = "VALUE-REGISTRATION-TRUTHY-INVALID-TARGET-" + result.Count; var test = Clone(id, "FACT-CB06");
            result.Add(new(id, test, test.Input, Types: new JsonObject { [SourceFactContractCases.TypeB] = target }, Error: "object"));
        }
        foreach (var resolved in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0), JsonValue.Create(""), new JsonArray() })
        {
            var id = "VALUE-REGISTRATION-UNUSED-RESOLVER-" + result.Count; var test = Clone(id, "FACT-CB06") with { Expected = new JsonArray(), IdentityInputs = [], IdentityHashes = [] };
            test.Input["arguments"]![0]!["status"] = "primitive"; result.Add(new(id, test, test.Input, Types: resolved));
            var badId = "VALUE-REGISTRATION-REACHED-RESOLVER-" + result.Count; var reached = Clone(badId, "FACT-CB06"); result.Add(new(badId, reached, reached.Input, Types: resolved?.DeepClone(), Error: "object"));
        }
        foreach (var rows in new JsonNode?[] { null, JsonValue.Create(false), JsonValue.Create(0), JsonValue.Create(""), new JsonObject(), new JsonArray(), JsonValue.Create("x"), new JsonObject { ["role"] = "ignored" }, new JsonArray((JsonNode?)null) })
        {
            var id = "VALUE-PAIR-ITERATION-" + result.Count; var test = Clone(id, "FACT-CB18") with { Expected = new JsonArray() };
            var error = rows is null || rows is JsonValue value && !value.TryGetValue<string>(out _) ? "iterable"
                : rows is JsonArray { Count: > 0 } or JsonObject { Count: > 0 } || rows is JsonValue text && text.GetValue<string>().Length > 0 ? "object" : null;
            result.Add(new(id, test, rows, Error: error));
        }
        foreach (var baseline in new[] { "FACT-CB03", "FACT-CB05", "FACT-CB06", "FACT-FN01", "FACT-CB18" })
        {
            var id = "VALUE-SAME-TYPED-CASE-" + baseline; var test = Clone(id, baseline);
            result.Add(new(id, test, test.Operation == FactOperation.Pairs ? test.Additional?.DeepClone() : test.Input, Types: Types()));
        }
        return result;
    }
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static SourceProducerValueCase Get(string id) => All.Single(test => test.Id == id);
    public static JsonNode Invoke(SourceProducerValueCase test, ISourceFactValueProducer producer) => test.Contract.Operation switch
    {
        FactOperation.TypeCandidate => producer.TypeCandidateValue(test.Input), FactOperation.Endpoint => producer.EndpointMethodValue(test.Input, test.Owner),
        FactOperation.Registration => producer.RegistrationCandidatesValue(test.Input, test.Types), FactOperation.Pairs => producer.PossiblePairsValue(test.Input),
        FactOperation.Taxonomy => producer.TaxonomyValue(test.Input), _ => throw new InvalidOperationException("Not a source value case")
    };
    public static void AssertContract(SourceProducerValueCase test, ISourceFactValueProducer producer)
    {
        var before = new JsonArray(test.Input?.DeepClone(), test.Owner?.DeepClone(), test.Types?.DeepClone());
        if (test.Error is null) Assert.True(JsonNode.DeepEquals(test.Contract.Expected, Invoke(test, producer)), test.Id);
        else Assert.Contains(test.Error, Assert.Throws<ArgumentException>(() => Invoke(test, producer)).Message, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(before, new JsonArray(test.Input?.DeepClone(), test.Owner?.DeepClone(), test.Types?.DeepClone())), "Value input/context was mutated");
    }
}
public sealed class ScriptedSourceValueProducer(SourceProducerValueCase test, RecordingFactCanonical canonical, bool wrong = false, bool mutate = false) : ISourceFactValueProducer
{
    private JsonNode Invoke(JsonNode? input)
    {
        if (mutate && input is JsonObject obj) obj["unexpected-edit"] = true;
        if (mutate && input is JsonArray array) array.Add("unexpected-edit");
        if (test.Error is not null && !wrong) throw new ArgumentException(test.Error);
        if (wrong) return test.Contract.Operation is FactOperation.Registration or FactOperation.Pairs ? new JsonArray("wrong") : new JsonObject { ["wrong"] = true };
        var scripted = new ScriptedSourceFacts(test.Contract, canonical);
        return SourceFactContractCases.Invoke(test.Contract, scripted, scripted, scripted);
    }
    public JsonObject TypeCandidateValue(JsonNode? declaration) => Invoke(declaration).AsObject();
    public JsonObject EndpointMethodValue(JsonNode? declaration, JsonNode? owner = null) => Invoke(declaration).AsObject();
    public JsonArray RegistrationCandidatesValue(JsonNode? declaration, JsonNode? resolvedTypes) => Invoke(declaration).AsArray();
    public JsonArray PossiblePairsValue(JsonNode? candidates) => Invoke(candidates).AsArray();
    public JsonObject TaxonomyValue(JsonNode? declaration) => Invoke(declaration).AsObject();
}
