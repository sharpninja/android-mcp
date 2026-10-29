using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeAbiOriginalMethod(string Id, string Method, NativeAbiCase Fixture, Action<JsonObject> Assertion, IReadOnlyList<Action<JsonObject>> NegativeControls);
public static class NativeAbiOriginalContractCases
{
    // Exact default json.dumps spelling of the original ASCII dict fixture,
    // authored from that function body without invoking Python.
    private const string OriginalFixture = "{\"builtin_types\": [{\"linker_set_key\": \"VOID\", \"name\": \"void\"}], \"function_types\": [{\"linker_set_key\": \"CB\", \"return_type\": \"VOID\", \"parameters\": []}], \"pointer_types\": [{\"linker_set_key\": \"PTR_CB\", \"referenced_type\": \"CB\"}], \"functions\": [{\"linker_set_key\": \"register_a\", \"function_name\": \"register_a\", \"return_type\": \"VOID\", \"parameters\": [{\"referenced_type\": \"PTR_CB\"}]}]}";
    public static IReadOnlyList<NativeAbiOriginalMethod> All { get; } =
    [
        new("ORIG-ABI-01", "test_pointer_to_function_parameter_is_structural_callback_candidate", NativeAbiContractCases.Get("ABI-01") with { Input = OriginalFixture }, result =>
        {
            var candidate = Assert.Single(result["callback_parameter_candidates"]!.AsArray()); Assert.Equal(0, candidate!["parameter_index"]!.GetValue<int>());
            Assert.False(candidate["lifecycle_proven"]!.GetValue<bool>()); Assert.False(result["wire_or_runtime_proven"]!.GetValue<bool>());
        }, [r => r["callback_parameter_candidates"] = new JsonArray(), r => r["callback_parameter_candidates"]![0]!["parameter_index"] = 1,
            r => r["callback_parameter_candidates"]![0]!["lifecycle_proven"] = true, r => r["wire_or_runtime_proven"] = true]),
        new("ORIG-ABI-02", "test_missing_type_reference_is_retained", NativeAbiContractCases.Get("ABI-02") with { Input = OriginalFixture.Replace("\"referenced_type\": \"CB\"", "\"referenced_type\": \"MISSING\"", StringComparison.Ordinal) },
            r => Assert.Equal(1, r["reference_counts"]!["unresolved"]!.GetValue<int>()), [r => r["reference_counts"]!["unresolved"] = 0]),
        new("ORIG-ABI-03", "test_duplicate_type_identity_not_silently_selected", NativeAbiContractCases.Get("ABI-03") with { Input = OriginalFixture[..^1] + ", \"record_types\": [{\"linker_set_key\": \"CB\", \"name\": \"collision\"}]}" }, r =>
        { Assert.Contains(r["duplicate_type_keys"]!.AsArray(), key => key!.GetValue<string>() == "CB"); Assert.Empty(r["callback_parameter_candidates"]!.AsArray()); },
            [r => r["duplicate_type_keys"] = new JsonArray(), r => r["callback_parameter_candidates"] = new JsonArray(new JsonObject())]),
        new("ORIG-ABI-04", "test_unknown_root_collection_preserved", NativeAbiContractCases.Get("ABI-04"),
            r => Assert.True(JsonNode.DeepEquals(new JsonArray(new JsonObject { ["opaque"] = 7 }), r["unknown_root_fields"]!["future_collection"])),
            [r => r["unknown_root_fields"]!["future_collection"]![0]!["opaque"] = 8]),
        new("ORIG-ABI-05", "test_duplicate_json_keys_preserved_as_unsupported_source", NativeAbiContractCases.Get("ABI-05"),
            r => Assert.Equal("unsupported-json", r["status"]!.GetValue<string>()), [r => r["status"] = "parsed-source-abi-json"]),
        new("ORIG-ABI-06", "test_nonfinite_json_is_not_a_valid_interchange_number", NativeAbiContractCases.Get("ABI-06"),
            r => Assert.Equal("unsupported-json", r["status"]!.GetValue<string>()), [r => r["status"] = "parsed-source-abi-json"]),
        new("ORIG-ABI-07", "test_mangled_overloads_not_collapsed_by_display_name", NativeAbiContractCases.Get("ABI-07"), r =>
        { Assert.Equal(2, r["records"]!.AsArray().Count); Assert.Equal(new[] { "f_int", "f_string" }, r["records"]!.AsArray().Select(row => row!["local_key"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal).Order(StringComparer.Ordinal)); },
            [r => r["records"]!.AsArray().RemoveAt(1), r => r["records"]![1]!["local_key"] = "f_int"])
    ];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[] { test.Id });
    public static NativeAbiOriginalMethod Get(string id) => All.Single(test => test.Id == id);
    public static void AssertMethod(NativeAbiOriginalMethod test, INativeAbiSourceFacts producer) => test.Assertion(producer.Parse(test.Fixture.Input));
}
