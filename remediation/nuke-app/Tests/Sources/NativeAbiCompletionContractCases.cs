using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeAbiCompletionFailure(string Id, string Input);
public static class NativeAbiCompletionContractCases
{
    private const string Bucket = "functions:nonobject-records";
    public static IReadOnlyList<NativeAbiCase> All { get; } = Create();
    private static IReadOnlyList<NativeAbiCase> Create()
    {
        var result = new List<NativeAbiCase>();
        void Add(JsonObject root, JsonObject expected) => result.Add(new("ABI-COMP-" + result.Count, root.ToJsonString(), expected));
        var preexisting = new JsonArray(new JsonObject { ["opaque"] = 1 });
        var extended = (JsonArray)preexisting.DeepClone(); extended.Add(new JsonObject { ["ordinal"] = 0, ["raw"] = null });
        Add(new JsonObject { [Bucket] = preexisting, ["functions"] = new JsonArray((JsonNode?)null) }, NativeAbiContractCases.Result(unknown: new JsonObject { [Bucket] = extended }));
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(7), JsonValue.Create(true), JsonValue.Create("opaque"), new JsonObject() })
        {
            // Later root fields replace earlier generated buckets in original root order.
            Add(new JsonObject { ["functions"] = new JsonArray((JsonNode?)null), [Bucket] = value?.DeepClone() }, NativeAbiContractCases.Result(unknown: new JsonObject { [Bucket] = value?.DeepClone() }));
            // An earlier nonarray bucket is harmless until a nonobject row is reached.
            Add(new JsonObject { [Bucket] = value?.DeepClone(), ["functions"] = new JsonArray(new JsonObject()) }, NativeAbiContractCases.Result(
                records: new JsonArray(NativeAbiContractCases.Record("functions", 0, null, null, new JsonObject())), unknown: new JsonObject { [Bucket] = value?.DeepClone() }));
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create(7), JsonValue.Create(true), JsonValue.Create("opaque"), new JsonObject { ["referenced_type"] = "PTR_CB" } })
        {
            var baseline = NativeAbiContractCases.Get("ABI-01"); var root = JsonNode.Parse(baseline.Input)!.AsObject();
            root["functions"]![0]!["parameters"] = value?.DeepClone();
            var expected = (JsonObject)baseline.Expected.DeepClone(); expected["records"]![3]!["metadata"]!["parameters"] = value?.DeepClone();
            expected["callback_parameter_candidates"] = new JsonArray(); expected["references"]!.AsArray().RemoveAt(3); expected["reference_counts"]!["resolved"] = 3;
            if (value is JsonObject)
            {
                expected["references"]!.AsArray().Add(NativeAbiContractCases.Reference(new JsonArray("functions", 0, "parameters", "referenced_type"), "PTR_CB"));
                expected["reference_counts"]!["resolved"] = 4;
            }
            Add(root, expected);
        }
        {
            var baseline = NativeAbiContractCases.Get("ABI-01"); var root = JsonNode.Parse(baseline.Input)!.AsObject();
            var parameters = new JsonArray(null, 7, "opaque", new JsonObject(), new JsonObject { ["referenced_type"] = null }, new JsonObject { ["referenced_type"] = 7 },
                new JsonObject { ["referenced_type"] = new JsonObject { ["underlying_type"] = "VOID" } },
                new JsonObject { ["referenced_type"] = new JsonArray("VOID", new JsonObject { ["return_type"] = "MISSING" }) });
            root["functions"]![0]!["parameters"] = parameters.DeepClone();
            var expected = (JsonObject)baseline.Expected.DeepClone(); expected["records"]![3]!["metadata"]!["parameters"] = parameters.DeepClone();
            expected["callback_parameter_candidates"] = new JsonArray(); expected["references"]!.AsArray().RemoveAt(3);
            expected["references"]!.AsArray().Add(NativeAbiContractCases.Reference(new JsonArray("functions", 0, "parameters", 6, "referenced_type", "underlying_type"), "VOID"));
            expected["references"]!.AsArray().Add(NativeAbiContractCases.Reference(new JsonArray("functions", 0, "parameters", 7, "referenced_type", 1, "return_type"), "MISSING", "unresolved"));
            expected["reference_counts"] = new JsonObject { ["resolved"] = 4, ["unresolved"] = 1 }; Add(root, expected);
        }
        {
            var rows = new JsonArray(); var records = new JsonArray();
            foreach (var key in new[] { "𐀀", "\ue000", "𐀀", "\ue000" })
            {
                var metadata = new JsonObject { ["linker_set_key"] = key }; var ordinal = rows.Count; rows.Add(metadata.DeepClone());
                records.Add(NativeAbiContractCases.Record("builtin_types", ordinal, JsonValue.Create(key), null, metadata));
            }
            Add(new JsonObject { ["builtin_types"] = rows }, NativeAbiContractCases.Result(records: records, duplicates: new JsonArray("\ue000", "𐀀")));
        }
        foreach (var (input, key) in new[] { ("{\"functions\":[],\"functions\":[{}]}", "functions"), ("{\"f\\u0075nctions\":[],\"functions\":[]}", "functions"),
            ("{\"functions\":[{\"name\":1,\"name\":2}],\"functions\":[]}", "name"), ("{\"\":1,\"\":2}", "") })
            result.Add(new("ABI-DUPLICATE-DIAGNOSTIC-" + result.Count, input, new JsonObject { ["status"] = "unsupported-json", ["reason"] = "Duplicate JSON key " + key, ["raw"] = input, ["wire_or_runtime_proven"] = false }));
        return result;
    }
    public static IReadOnlyList<NativeAbiCompletionFailure> Failures { get; } = new JsonNode?[] { null, JsonValue.Create(7), JsonValue.Create(true), JsonValue.Create("opaque"), new JsonObject() }
        .Select((value, index) => new NativeAbiCompletionFailure("ABI-COLLISION-ERROR-" + index,
            new JsonObject { [Bucket] = value?.DeepClone(), ["functions"] = new JsonArray((JsonNode?)null) }.ToJsonString())).ToArray();
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static IEnumerable<object[]> ErrorCases => Failures.Select(test => new object[] { test.Id });
    public static NativeAbiCase Get(string id) => All.Single(test => test.Id == id);
    public static NativeAbiCompletionFailure GetFailure(string id) => Failures.Single(test => test.Id == id);
    public static void AssertFailure(NativeAbiCompletionFailure test, INativeAbiSourceFacts producer) =>
        Assert.Contains("Expected ABI nonobject-record bucket array", Assert.Throws<ArgumentException>(() => producer.Parse(test.Input)).Message, StringComparison.Ordinal);
}
