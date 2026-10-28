using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed record NativeAbiCase(string Id, string Input, JsonObject Expected, bool Diagnostic = false)
{
    public JsonObject CorrectResult() { var result = (JsonObject)Expected.DeepClone(); if (Diagnostic) result["reason"] = "Unsupported ABI JSON fixture diagnostic"; return result; }
    public SourceExtractionCase MutationCase() => new(Id, "native-abi", Input, StructuralParseStatus.Clean, [Expected], [], "native_abi_facts seven legacy tests plus graph/opaque-metadata boundaries");
}
public static class NativeAbiContractCases
{
    private static JsonArray Array(params JsonNode?[] values) => new(values);
    private static JsonObject Fixture() => new()
    {
        ["builtin_types"] = Array(new JsonObject { ["linker_set_key"] = "VOID", ["name"] = "void" }),
        ["function_types"] = Array(new JsonObject { ["linker_set_key"] = "CB", ["return_type"] = "VOID", ["parameters"] = new JsonArray() }),
        ["pointer_types"] = Array(new JsonObject { ["linker_set_key"] = "PTR_CB", ["referenced_type"] = "CB" }),
        ["functions"] = Array(new JsonObject { ["linker_set_key"] = "register_a", ["function_name"] = "register_a", ["return_type"] = "VOID", ["parameters"] = Array(new JsonObject { ["referenced_type"] = "PTR_CB" }) })
    };
    private static JsonObject Record(string collection, int ordinal, JsonNode? key, JsonNode? name, JsonObject metadata) => new()
    { ["collection"] = collection, ["ordinal"] = ordinal, ["local_key"] = key?.DeepClone(), ["display_name"] = name?.DeepClone(), ["metadata"] = metadata.DeepClone() };
    private static JsonObject Reference(JsonArray path, string target, string status = "resolved") => new() { ["path"] = path, ["target_local_key"] = target, ["status"] = status };
    private static JsonArray BaseRecords(JsonObject root) => Array(
        Record("builtin_types", 0, "VOID", "void", root["builtin_types"]![0]!.AsObject()),
        Record("function_types", 0, "CB", null, root["function_types"]![0]!.AsObject()),
        Record("pointer_types", 0, "PTR_CB", null, root["pointer_types"]![0]!.AsObject()),
        Record("functions", 0, "register_a", "register_a", root["functions"]![0]!.AsObject()));
    private static JsonArray BaseReferences(string pointerTarget = "CB", string pointerStatus = "resolved", string parameterTarget = "PTR_CB") => Array(
        Reference(Array("function_types", 0, "return_type"), "VOID"), Reference(Array("pointer_types", 0, "referenced_type"), pointerTarget, pointerStatus),
        Reference(Array("functions", 0, "return_type"), "VOID"), Reference(Array("functions", 0, "parameters", 0, "referenced_type"), parameterTarget));
    private static JsonObject Chain(string collection, string key) => new() { ["collection"] = collection, ["local_key"] = key };
    private static JsonObject Callback(JsonObject root, string parameterTarget = "PTR_CB", bool qualified = false) => new()
    {
        ["function_local_key"] = "register_a", ["function_name"] = "register_a", ["parameter_index"] = 0,
        ["parameter_metadata"] = new JsonObject { ["referenced_type"] = parameterTarget }, ["function_type_key"] = "CB",
        ["chain"] = qualified ? Array(Chain("qualified_types", "Q"), Chain("pointer_types", "PTR_CB"), Chain("function_types", "CB")) : Array(Chain("pointer_types", "PTR_CB"), Chain("function_types", "CB")),
        ["signature"] = root["function_types"]![0]!.DeepClone(), ["role"] = "structural-function-pointer-parameter-candidate", ["lifecycle_proven"] = false,
        ["registration_proven"] = false, ["streaming_semantics"] = "unknown", ["direction"] = "unknown", ["permission_metadata"] = "unknown; absence is not permission-free"
    };
    private static JsonObject Result(JsonArray? records = null, JsonArray? references = null, JsonObject? counts = null, JsonArray? duplicates = null, JsonObject? unknown = null, JsonArray? callbacks = null) => new()
    {
        ["status"] = "parsed-source-abi-json", ["records"] = records ?? new(), ["references"] = references ?? new(), ["reference_counts"] = counts ?? new(),
        ["duplicate_type_keys"] = duplicates ?? new(), ["unknown_root_fields"] = unknown ?? new(), ["callback_parameter_candidates"] = callbacks ?? new(),
        ["wire_or_runtime_proven"] = false, ["permission_authority"] = "unknown", ["compiled_or_installed_version_range"] = null
    };
    private static NativeAbiCase Case(string id, JsonObject root, JsonObject expected) => new(id, JsonSerializer.Serialize(root), expected);
    private static NativeAbiCase Unsupported(string id, string text) => new(id, text, new() { ["status"] = "unsupported-json", ["raw"] = text, ["wire_or_runtime_proven"] = false }, true);
    public static IReadOnlyList<NativeAbiCase> All { get; } = Create();
    private static IReadOnlyList<NativeAbiCase> Create()
    {
        var original = Fixture(); var missing = Fixture(); missing["pointer_types"]![0]!["referenced_type"] = "MISSING";
        var duplicate = Fixture(); duplicate["record_types"] = Array(new JsonObject { ["linker_set_key"] = "CB", ["name"] = "collision" });
        var duplicateRecords = BaseRecords(duplicate); duplicateRecords.Add(Record("record_types", 0, "CB", "collision", duplicate["record_types"]![0]!.AsObject()));
        var overloads = new JsonObject { ["functions"] = Array(new JsonObject { ["linker_set_key"] = "f_int", ["function_name"] = "f" }, new JsonObject { ["linker_set_key"] = "f_string", ["function_name"] = "f" }) };
        var cycle = new JsonObject { ["pointer_types"] = Array(new JsonObject { ["linker_set_key"] = "P", ["referenced_type"] = "Q" }, new JsonObject { ["linker_set_key"] = "Q", ["referenced_type"] = "P" }), ["functions"] = Array(new JsonObject { ["linker_set_key"] = "F", ["parameters"] = Array(new JsonObject { ["referenced_type"] = "P" }) }) };
        var qualified = Fixture(); qualified["functions"]![0]!["parameters"]![0]!["referenced_type"] = "Q"; qualified["qualified_types"] = Array(new JsonObject { ["linker_set_key"] = "Q", ["referenced_type"] = "PTR_CB" });
        var qualifiedRecords = BaseRecords(qualified); qualifiedRecords.Add(Record("qualified_types", 0, "Q", null, qualified["qualified_types"]![0]!.AsObject()));
        var qualifiedReferences = BaseReferences(parameterTarget: "Q"); qualifiedReferences.Add(Reference(Array("qualified_types", 0, "referenced_type"), "PTR_CB"));
        var future = new JsonObject { ["opaque"] = 7 }; var unknownRecords = Array(new JsonObject { ["ordinal"] = 0, ["raw"] = 1 }, new JsonObject { ["ordinal"] = 1, ["raw"] = "opaque" }, new JsonObject { ["ordinal"] = 2, ["raw"] = null });
        var collections = new JsonObject(); var collectionRecords = new JsonArray();
        foreach (var collection in new[] { "array_types", "enum_types", "lvalue_reference_types", "record_types", "rvalue_reference_types", "global_vars", "elf_functions", "elf_objects" })
        { collections[collection] = Array(new JsonObject()); collectionRecords.Add(Record(collection, 0, null, null, new())); }
        var references = Fixture(); references["functions"]![0]!["parameters"]![0]!["referenced_type"] = "L";
        references["lvalue_reference_types"] = Array(new JsonObject { ["linker_set_key"] = "L", ["referenced_type"] = "R" });
        references["rvalue_reference_types"] = Array(new JsonObject { ["linker_set_key"] = "R", ["referenced_type"] = "PTR_CB" });
        var referenceRecords = BaseRecords(references); referenceRecords.Add(Record("lvalue_reference_types", 0, "L", null, references["lvalue_reference_types"]![0]!.AsObject())); referenceRecords.Add(Record("rvalue_reference_types", 0, "R", null, references["rvalue_reference_types"]![0]!.AsObject()));
        var referencePaths = BaseReferences(parameterTarget: "L"); referencePaths.Add(Reference(Array("lvalue_reference_types", 0, "referenced_type"), "R")); referencePaths.Add(Reference(Array("rvalue_reference_types", 0, "referenced_type"), "PTR_CB"));
        var referenceCallback = Callback(references, "L"); referenceCallback["chain"] = Array(Chain("lvalue_reference_types", "L"), Chain("rvalue_reference_types", "R"), Chain("pointer_types", "PTR_CB"), Chain("function_types", "CB"));
        var nested = Fixture(); nested["functions"]![0]!["future"] = new JsonObject { ["referenced_type"] = "VOID", ["opaque"] = Array(1, 2, 3) };
        var nestedPaths = BaseReferences(); nestedPaths.Add(Reference(Array("functions", 0, "future", "referenced_type"), "VOID"));
        return
        [
            Case("ABI-01", original, Result(BaseRecords(original), BaseReferences(), new() { ["resolved"] = 4 }, callbacks: Array(Callback(original)))),
            Case("ABI-02", missing, Result(BaseRecords(missing), BaseReferences("MISSING", "unresolved"), new() { ["resolved"] = 3, ["unresolved"] = 1 })),
            Case("ABI-03", duplicate, Result(duplicateRecords, BaseReferences(pointerStatus: "ambiguous"), new() { ["resolved"] = 3, ["ambiguous"] = 1 }, Array("CB"))),
            Case("ABI-04", new() { ["future_collection"] = Array(future.DeepClone()) }, Result(unknown: new() { ["future_collection"] = Array(future.DeepClone()) })),
            Unsupported("ABI-05", "{\"functions\":[],\"functions\":[{}]}"), Unsupported("ABI-06", "{\"functions\":[NaN]}"),
            Case("ABI-07", overloads, Result(Array(Record("functions", 0, "f_int", "f", overloads["functions"]![0]!.AsObject()), Record("functions", 1, "f_string", "f", overloads["functions"]![1]!.AsObject())))),
            Case("ABI-08", cycle, Result(Array(Record("pointer_types", 0, "P", null, cycle["pointer_types"]![0]!.AsObject()), Record("pointer_types", 1, "Q", null, cycle["pointer_types"]![1]!.AsObject()), Record("functions", 0, "F", null, cycle["functions"]![0]!.AsObject())),
                Array(Reference(Array("pointer_types", 0, "referenced_type"), "Q"), Reference(Array("pointer_types", 1, "referenced_type"), "P"), Reference(Array("functions", 0, "parameters", 0, "referenced_type"), "P")), new() { ["resolved"] = 3 })),
            Case("ABI-09", new() { ["functions"] = Array(1, "opaque", null, new JsonObject()) }, Result(Array(Record("functions", 3, null, null, new())), unknown: new() { ["functions:nonobject-records"] = unknownRecords })),
            Case("ABI-10", new() { ["functions"] = future.DeepClone() }, Result(unknown: new() { ["functions"] = future.DeepClone() })),
            Case("ABI-11", new() { ["arch"] = "arm64", ["library"] = "libfoo", ["version"] = "9" }, Result(unknown: new() { ["arch"] = "arm64", ["library"] = "libfoo", ["version"] = "9" })),
            Unsupported("ABI-12", "[]"),
            Case("ABI-13", qualified, Result(qualifiedRecords, qualifiedReferences, new() { ["resolved"] = 5 }, callbacks: Array(Callback(qualified, "Q", true)))),
            Case("ABI-14", new() { ["builtin_types"] = Array(new JsonObject { ["linker_set_key"] = 7, ["name"] = "opaque-key" }) }, Result(Array(Record("builtin_types", 0, null, "opaque-key", new() { ["linker_set_key"] = 7, ["name"] = "opaque-key" })))),
            Case("ABI-15", new() { ["functions"] = Array(new JsonObject { ["linker_set_key"] = "F", ["function_name"] = null, ["name"] = "fallback-not-selected" }) },
                Result(Array(Record("functions", 0, "F", null, new() { ["linker_set_key"] = "F", ["function_name"] = null, ["name"] = "fallback-not-selected" })))),
            Case("ABI-16", collections, Result(collectionRecords)),
            Case("ABI-17", references, Result(referenceRecords, referencePaths, new() { ["resolved"] = 6 }, callbacks: Array(referenceCallback))),
            Case("ABI-18", nested, Result(BaseRecords(nested), nestedPaths, new() { ["resolved"] = 5 }, callbacks: Array(Callback(nested))))
        ];
    }
    public static IEnumerable<object[]> Cases => All.Select(x => new object[] { x.Id });
    public static IEnumerable<object[]> Mutations => All.SelectMany(x => SourceMutationCases.For(x.MutationCase()));
    public static NativeAbiCase Get(string id) => All.Single(x => x.Id == id);
    public static void AssertContract(NativeAbiCase test, INativeAbiSourceFacts provider)
    {
        var result = (JsonObject)provider.Parse(test.Input).DeepClone();
        if (test.Diagnostic) { Assert.True(result.ContainsKey("reason")); Assert.False(string.IsNullOrWhiteSpace(result["reason"]?.GetValue<string>())); result.Remove("reason"); }
        Assert.True(JsonNode.DeepEquals(test.Expected, result), "Native ABI source metadata differs: " + test.Id);
    }
}
