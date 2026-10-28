using System.Text.Json;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Declared source requirements are facts, never runtime grants. Symbol inputs
// are caller-proven exact-snapshot constants; this service performs no lookup.
public sealed class PermissionExpressionSourceFacts : IPermissionExpressionSourceFacts
{
    private const string Symbolic = "Unresolved symbolic permission expression";
    public JsonObject Permission(JsonArray annotations, IReadOnlyDictionary<string, string>? symbols = null, bool aidl = false)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var clauses = new List<JsonObject>();
        foreach (var annotation in annotations.OfType<JsonObject>())
        {
            var name = annotation["name"]!.GetValue<string>(); var shortName = name.Split('.')[^1];
            if (shortName is not ("RequiresPermission" or "EnforcePermission" or "PermissionManuallyEnforced" or "RequiresNoPermission" or "Read" or "Write")) continue;
            if (shortName is "Read" or "Write" && !name.Contains("RequiresPermission", StringComparison.Ordinal)) continue;
            var arguments = annotation["arguments"]!.GetValue<string>();
            if (aidl) arguments = SourceSyntax.Rx(@"(allOf|anyOf)\s*=\s*\[([^\]]*)\]").Replace(arguments, "$1={$2}");
            clauses.Add(Resolve(Ast(name, arguments), symbols));
        }
        return clauses.Count == 1 ? clauses[0] : clauses.Count > 0 ? Group("all", clauses)
            : Unknown("No declared recognized permission metadata; absence is not permission-free");
    }
    private static JsonObject Unknown(string reason, string? raw = null)
    { var value = new JsonObject { ["op"] = "unknown", ["reason"] = reason }; if (raw is not null) value["raw"] = raw; return value; }
    private static JsonObject Group(string operation, IEnumerable<JsonObject> values) => new()
    { ["op"] = operation, ["children"] = new JsonArray(values.Select(x => (JsonNode)x).ToArray()) };
    private static JsonObject Leaf(string value) => SourceSyntax.Rx("^\"[^\"\\\\]+\"$").IsMatch(value)
        ? new() { ["op"] = "permission", ["name"] = JsonSerializer.Deserialize<string>(value) }
        : Unknown(Symbolic, value);
    private static JsonObject Ast(string name, string arguments)
    {
        var shortName = name.Split('.')[^1];
        if (shortName == "RequiresNoPermission") return new() { ["op"] = "explicit-none" };
        if (shortName == "PermissionManuallyEnforced") return new() { ["op"] = "manual", ["reason"] = "Manual enforcement declared; backend checks unknown" };
        if (shortName is "Read" or "Write")
        {
            var nested = SourceSyntax.ParseAnnotations(arguments).Annotations;
            return new() { ["op"] = "access", ["access"] = shortName.ToLowerInvariant(),
                ["requirement"] = nested.Count == 1 ? Ast(nested[0].Document["name"]!.GetValue<string>(), nested[0].Document["arguments"]!.GetValue<string>()) : Unknown("Unresolved nested annotation", arguments) };
        }
        var pairs = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var item in SourceSyntax.Chunks(arguments, ',', true))
        {
            var at = item.Raw.IndexOf('=');
            if (at >= 0) pairs[item.Raw[..at].Trim()] = item.Raw[(at + 1)..].Trim(); else if (item.Raw.Length > 0) pairs["value"] = item.Raw;
        }
        var clauses = new List<JsonObject>();
        foreach (var key in new[] { "value", "allOf", "anyOf" })
            if (pairs.TryGetValue(key, out var value)) clauses.Add(key == "value" ? Leaf(value) : Group(key == "allOf" ? "all" : "any", SourceSyntax.Chunks(value.Trim('{', '}'), ',', true).Select(x => Leaf(x.Raw))));
        var ast = clauses.Count == 1 ? clauses[0] : clauses.Count > 0 ? Group("all", clauses) : Unknown("Permission expression absent or unsupported", arguments);
        if (pairs.GetValueOrDefault("conditional") == "true") ast = new() { ["op"] = "conditional", ["condition"] = Unknown("Annotation marks conditional but does not identify runtime predicate"), ["requirement"] = ast };
        if (pairs.Keys.Any(x => x is not ("value" or "allOf" or "anyOf" or "conditional"))) ast = Group("all", [ast, Unknown("Unrecognized annotation attributes", arguments)]);
        return ast;
    }
    private static JsonObject Resolve(JsonObject value, IReadOnlyDictionary<string,string>? symbols)
    {
        var operation = value["op"]?.GetValue<string>();
        if (operation == "unknown" && value["reason"]?.GetValue<string>() == Symbolic && value["raw"]?.GetValue<string>() is { } raw && symbols?.TryGetValue(raw, out var resolved) == true)
            return new() { ["op"] = "permission", ["name"] = resolved, ["x-symbol"] = raw };
        if (operation == "permission")
        {
            var name = value["name"]!.GetValue<string>();
            if (symbols?.TryGetValue(name, out var resolvedName) == true) return new() { ["op"] = "permission", ["name"] = resolvedName, ["x-symbol"] = name };
            if (name.StartsWith("Manifest.permission.", StringComparison.Ordinal) || name.Contains(".Manifest.permission.", StringComparison.Ordinal) || name.Contains(".Permission.", StringComparison.Ordinal) || SourceSyntax.Rx(@"\s").IsMatch(name))
                return Unknown("Source annotation contains a symbolic reference or non-permission predicate, not a proven literal permission name", name);
            if (!name.Contains('.')) return Unknown("Unqualified permission literal; namespace not proven", name);
        }
        var result = new JsonObject();
        foreach (var property in value)
            result[property.Key] = property.Value is JsonObject child ? Resolve(child, symbols) : property.Value is JsonArray array
                ? new JsonArray(array.Select(x => x is JsonObject item ? (JsonNode)Resolve(item, symbols) : x?.DeepClone()).ToArray()) : property.Value?.DeepClone();
        return result;
    }
}
