using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class NativeAbiSourceFacts(IStrictJsonInspector inspector) : INativeAbiSourceFacts
{
    public NativeAbiSourceFacts() : this(new StrictJsonInspector()) { }
    private static readonly HashSet<string> TypeCollections = new(["array_types", "builtin_types", "enum_types", "function_types", "lvalue_reference_types", "pointer_types", "qualified_types", "record_types", "rvalue_reference_types"], StringComparer.Ordinal);
    private static readonly HashSet<string> ValueCollections = new(["functions", "global_vars", "elf_functions", "elf_objects"], StringComparer.Ordinal);
    private static readonly HashSet<string> ReferenceNames = new(["referenced_type", "return_type", "underlying_type"], StringComparer.Ordinal);
    private static readonly HashSet<string> IndirectCollections = new(["pointer_types", "qualified_types", "lvalue_reference_types", "rvalue_reference_types"], StringComparer.Ordinal);

    public JsonObject Parse(string text)
    {
        StrictJsonInspection inspection;
        try { inspection = inspector.Inspect(new UTF8Encoding(false, true).GetBytes(text)); }
        catch (EncoderFallbackException) { return Unsupported(text, "Invalid Unicode in ABI JSON"); }
        if (inspection.Diagnostics.Count > 0 || !inspection.Value.HasValue) return Unsupported(text, inspection.Diagnostics.FirstOrDefault()?.Message ?? "Invalid ABI JSON");
        if (inspection.Value.Value.ValueKind != JsonValueKind.Object) return Unsupported(text, "Expected ABI dump object");
        var root = JsonNode.Parse(inspection.Value.Value.GetRawText(), null, new JsonDocumentOptions { MaxDepth = int.MaxValue })!.AsObject();
        var records = new JsonArray(); var unknown = new JsonObject();
        var types = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        foreach (var (collection, items) in root)
        {
            if ((!TypeCollections.Contains(collection) && !ValueCollections.Contains(collection)) || items is not JsonArray values)
            { unknown[collection] = items?.DeepClone(); continue; }
            for (var ordinal = 0; ordinal < values.Count; ordinal++)
            {
                if (values[ordinal] is not JsonObject item)
                {
                    var key = collection + ":nonobject-records";
                    if (unknown[key] is not JsonArray) unknown[key] = new JsonArray();
                    unknown[key]!.AsArray().Add(new JsonObject { ["ordinal"] = ordinal, ["raw"] = values[ordinal]?.DeepClone() }); continue;
                }
                var localKey = String(item["linker_set_key"]);
                var displayName = item.ContainsKey("function_name") ? item["function_name"] : item["name"];
                var record = new JsonObject { ["collection"] = collection, ["ordinal"] = ordinal, ["local_key"] = localKey, ["display_name"] = displayName?.DeepClone(), ["metadata"] = item.DeepClone() };
                records.Add(record);
                if (TypeCollections.Contains(collection) && localKey is not null)
                { if (!types.TryGetValue(localKey, out var matches)) types[localKey] = matches = []; matches.Add(record); }
            }
        }
        var references = new JsonArray(); var counts = new JsonObject();
        foreach (var record in records.OfType<JsonObject>())
            References(record["metadata"], new JsonArray(record["collection"]!.DeepClone(), record["ordinal"]!.DeepClone()), types, references, counts);
        var callbacks = new JsonArray();
        foreach (var record in records.OfType<JsonObject>())
        {
            if (String(record["collection"]) != "functions" || record["metadata"] is not JsonObject metadata || metadata["parameters"] is not JsonArray parameters) continue;
            for (var index = 0; index < parameters.Count; index++)
            {
                if (parameters[index] is not JsonObject parameter) continue;
                var target = FunctionTarget(String(parameter["referenced_type"]), types);
                if (target is null) continue;
                callbacks.Add(new JsonObject
                {
                    ["function_local_key"] = record["local_key"]?.DeepClone(), ["function_name"] = record["display_name"]?.DeepClone(), ["parameter_index"] = index,
                    ["parameter_metadata"] = parameter.DeepClone(), ["function_type_key"] = target["function_type_key"]!.DeepClone(), ["chain"] = target["chain"]!.DeepClone(), ["signature"] = target["signature"]!.DeepClone(),
                    ["role"] = "structural-function-pointer-parameter-candidate", ["lifecycle_proven"] = false, ["registration_proven"] = false,
                    ["streaming_semantics"] = "unknown", ["direction"] = "unknown", ["permission_metadata"] = "unknown; absence is not permission-free"
                });
            }
        }
        return new JsonObject
        {
            ["status"] = "parsed-source-abi-json", ["records"] = records, ["references"] = references, ["reference_counts"] = counts,
            ["duplicate_type_keys"] = SourceSyntax.Strings(types.Where(x => x.Value.Count > 1).Select(x => x.Key).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars))),
            ["unknown_root_fields"] = unknown, ["callback_parameter_candidates"] = callbacks, ["wire_or_runtime_proven"] = false, ["permission_authority"] = "unknown", ["compiled_or_installed_version_range"] = null
        };
    }

    private static string? String(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;

    private static void References(JsonNode? value, JsonArray path, IReadOnlyDictionary<string, List<JsonObject>> types, JsonArray references, JsonObject counts)
    {
        if (value is JsonObject item)
            foreach (var (key, target) in item)
            {
                var child = (JsonArray)path.DeepClone(); child.Add(key);
                if (ReferenceNames.Contains(key) && String(target) is { } localKey)
                {
                    var count = types.TryGetValue(localKey, out var matches) ? matches.Count : 0;
                    var status = count == 1 ? "resolved" : count > 1 ? "ambiguous" : "unresolved";
                    counts[status] = (counts[status]?.GetValue<int>() ?? 0) + 1;
                    references.Add(new JsonObject { ["path"] = child, ["target_local_key"] = localKey, ["status"] = status });
                }
                else References(target, child, types, references, counts);
            }
        else if (value is JsonArray array)
            for (var index = 0; index < array.Count; index++) { var child = (JsonArray)path.DeepClone(); child.Add(index); References(array[index], child, types, references, counts); }
    }

    private static JsonObject? FunctionTarget(string? key, IReadOnlyDictionary<string, List<JsonObject>> types)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal); var chain = new JsonArray();
        while (key is not null && seen.Add(key))
        {
            if (!types.TryGetValue(key, out var entries) || entries.Count != 1) return null;
            var entry = entries[0]; var collection = String(entry["collection"])!;
            chain.Add(new JsonObject { ["collection"] = collection, ["local_key"] = key });
            if (collection == "function_types") return new JsonObject { ["function_type_key"] = key, ["chain"] = chain, ["signature"] = entry["metadata"]!.DeepClone() };
            if (!IndirectCollections.Contains(collection)) return null;
            key = String(entry["metadata"]!["referenced_type"]);
        }
        return null;
    }

    private static JsonObject Unsupported(string text, string reason) => new() { ["status"] = "unsupported-json", ["reason"] = reason, ["raw"] = text, ["wire_or_runtime_proven"] = false };
}
