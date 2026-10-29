using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class SoongSourceFacts(IGlobMatcher glob) : ISoongSourceFacts
{
    private sealed record Token(string Raw, int Start, int End);
    private static readonly Regex QuotedToken = new(@"\G""(?:\\.|[^""\\])*""", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Interfaces = new(["aidl_interface", "hidl_interface"], StringComparer.Ordinal);
    private static readonly HashSet<string> Apex = new(["apex", "apex_defaults", "prebuilt_apex", "apex_set"], StringComparer.Ordinal);
    private static readonly string[] ApexProperties = ["name", "manifest", "defaults", "apex_name", "version", "min_sdk_version", "updatable", "package_name"];

    public JsonArray Modules(string text, SoongModuleFamily family)
    {
        var kinds = family switch { SoongModuleFamily.Interfaces => Interfaces, SoongModuleFamily.Apex => Apex, _ => throw new ArgumentOutOfRangeException(nameof(family)) };
        var tokens = Tokenize(text);
        var result = new JsonArray(); var index = 0;
        while (index < tokens.Length - 1)
        {
            if (!kinds.Contains(tokens[index].Raw) || tokens[index + 1].Raw != "{") { index++; continue; }
            var start = index; var position = index + 1;
            var properties = Value(tokens, ref position, text, false);
            var module = new JsonObject { ["kind"] = tokens[start].Raw, ["line"] = SourceSyntax.Line(text, tokens[start].Start), ["properties"] = properties,
                ["raw"] = text[tokens[start].Start..tokens[position - 1].End] };
            if (family == SoongModuleFamily.Interfaces)
            {
                module["status"] = properties["complete"]?.GetValue<bool>() == true ? "static-properties-parsed" : "unclosed-module-block";
                module["runtime_module_identity_proven"] = false; module["android_applicability_proven"] = false;
            }
            else
            {
                var literals = new JsonObject(); foreach (var name in ApexProperties) literals[name] = Literal(properties, name);
                module["literal_properties"] = literals; module["installed_module_version_proven"] = false;
            }
            result.Add(module); index = Math.Max(position, index + 1);
        }
        return result;
    }

    private static bool Decimal(Rune scalar) => Rune.GetUnicodeCategory(scalar) == UnicodeCategory.DecimalDigitNumber;
    private static Token[] Tokenize(string text)
    {
        var masked = SourceText.MaskBuildComments(text); var result = new List<Token>();
        for (var index = 0; index < masked.Length;)
        {
            var start = index; var scalar = Rune.GetRuneAt(masked, index);
            if (SourceText.Strip(scalar.ToString()).Length == 0) { index += scalar.Utf16SequenceLength; continue; }
            if (masked[index] == '"')
            {
                var quoted = QuotedToken.Match(masked, index);
                if (quoted.Success) { index += quoted.Length; result.Add(new(quoted.Value, start, index)); continue; }
            }
            index += scalar.Utf16SequenceLength;
            if (scalar.Value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_')
                while (index < masked.Length && SourceText.OriginalWord(Rune.GetRuneAt(masked, index))) index += Rune.GetRuneAt(masked, index).Utf16SequenceLength;
            else if (Decimal(scalar))
                while (index < masked.Length && Decimal(Rune.GetRuneAt(masked, index))) index += Rune.GetRuneAt(masked, index).Utf16SequenceLength;
            result.Add(new(masked[start..index], start, index));
        }
        return result.ToArray();
    }

    private static JsonObject Value(IReadOnlyList<Token> tokens, ref int index, string text, bool expression = true)
    {
        if (index >= tokens.Count) return new JsonObject { ["kind"] = "missing-value", ["raw"] = "" };
        var start = index; var token = tokens[index].Raw; JsonObject node;
        if (token is "[" or "{")
        {
            var closing = token == "[" ? "]" : "}"; index++; var children = new JsonArray(); var complete = true;
            while (index < tokens.Count && tokens[index].Raw != closing)
            {
                if (token == "{")
                {
                    if (index + 1 >= tokens.Count || tokens[index + 1].Raw != ":") { complete = false; break; }
                    var key = tokens[index].Raw; index += 2; var child = Value(tokens, ref index, text);
                    children.Add(new JsonObject { ["name"] = key, ["value"] = child });
                }
                else children.Add(Value(tokens, ref index, text));
                if (index < tokens.Count && tokens[index].Raw == ",") index++;
                else if (index < tokens.Count && tokens[index].Raw != closing) { complete = false; break; }
            }
            if (index >= tokens.Count || tokens[index].Raw != closing) complete = false; else index++;
            node = new JsonObject { ["kind"] = token == "{" ? "object" : "array", ["items"] = children, ["complete"] = complete };
        }
        else if (token.StartsWith('"'))
        {
            try { node = new JsonObject { ["kind"] = "literal", ["value"] = JsonSerializer.Deserialize<string>(token) }; }
            catch (JsonException) { node = new JsonObject { ["kind"] = "unknown-string", ["raw"] = token }; }
            index++;
        }
        else if (token is "true" or "false") { node = new JsonObject { ["kind"] = "literal", ["value"] = token == "true" }; index++; }
        else if (token.EnumerateRunes().All(Decimal))
        {
            var digits = string.Concat(token.EnumerateRunes().Select(scalar => CharUnicodeInfo.GetDecimalDigitValue(scalar.ToString(), 0).ToString(CultureInfo.InvariantCulture)));
            var integer = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
            node = new JsonObject { ["kind"] = "literal", ["value"] = JsonNode.Parse(integer.ToString(CultureInfo.InvariantCulture)) }; index++;
        }
        else { node = new JsonObject { ["kind"] = "unresolved-expression" }; index++; }
        if (expression && index < tokens.Count && tokens[index].Raw is not ("," or "}" or "]"))
        {
            var depth = 0;
            while (index < tokens.Count)
            {
                var next = tokens[index].Raw;
                if (depth == 0 && next is "," or "}" or "]") break;
                if (next is "(" or "[" or "{") depth++;
                else if (next is ")" or "]" or "}") depth = Math.Max(0, depth - 1);
                index++;
            }
            node = new JsonObject { ["kind"] = "unresolved-expression" };
        }
        node["raw"] = index > start ? text[tokens[start].Start..tokens[index - 1].End] : "";
        return node;
    }

    public JsonNode? Literal(JsonObject node, string? property = null)
    {
        if (property is not null)
        {
            var matches = (node["items"] as JsonArray ?? []).OfType<JsonObject>().Where(x => x["name"]?.GetValue<string>() == property).ToArray();
            return matches.Length == 1 && matches[0]["value"] is JsonObject value ? Literal(value) : null;
        }
        var kind = node["kind"]?.GetValue<string>();
        if (kind == "literal") return node["value"]?.DeepClone();
        if (node["complete"]?.GetValue<bool>() != true) return null;
        if (kind == "array" && node["items"] is JsonArray items)
        {
            var values = items.OfType<JsonObject>().Select(x => Literal(x)).ToArray();
            return values.Length == items.Count && values.All(x => x is not null) ? new JsonArray(values) : null;
        }
        if (kind == "object" && node["items"] is JsonArray properties)
        {
            var result = new JsonObject();
            foreach (var item in properties)
            {
                if (item is not JsonObject pair || pair["name"] is null || pair["value"] is not JsonObject value) return null;
                var name = pair["name"]!.GetValue<string>(); var literal = Literal(value);
                if (result.ContainsKey(name) || literal is null) return null;
                result.Add(name, literal);
            }
            return result;
        }
        return null;
    }

    public IReadOnlyList<string> MatchSources(string buildPath, IReadOnlyList<string> patterns, IReadOnlyList<string> excluded, IReadOnlyList<string> declaredPaths)
    {
        foreach (var pattern in patterns.Concat(excluded))
            if (!SafeSelector(pattern)) throw new ArgumentException("Nonliteral, external or escaping source selection requires separate proof");
        var build = GlobMatcher.NormalizePosixPath(buildPath); var slash = build.LastIndexOf('/'); var parent = slash < 0 ? "" : build[..slash];
        var result = new SortedSet<string>(Comparer<string>.Create(StrictJsonInspector.CompareScalars));
        foreach (var path in declaredPaths)
        {
            var normalized = GlobMatcher.NormalizePosixPath(path);
            string relative;
            if (parent.Length == 0) { if (normalized.StartsWith('/')) continue; relative = normalized; }
            else { if (!normalized.StartsWith(parent + "/", StringComparison.Ordinal)) continue; relative = normalized[(parent.Length + 1)..]; }
            if (patterns.Any(pattern => glob.Matches(relative, pattern, GlobMatchingProfile.PosixFullPath)) && !excluded.Any(pattern => glob.Matches(relative, pattern, GlobMatchingProfile.PosixFullPath))) result.Add(path);
        }
        return result.ToArray();
    }

    public string? ExplicitManifestPath(string buildPath, JsonObject apexModule)
    {
        if (apexModule["literal_properties"]?["manifest"] is not JsonValue scalar || !scalar.TryGetValue<string>(out var value) || !SafeSelector(value)) return null;
        var build = GlobMatcher.NormalizePosixPath(buildPath); var slash = build.LastIndexOf('/'); var parent = slash < 0 ? "" : build[..slash];
        var joined = GlobMatcher.NormalizePosixPath(parent.Length == 0 ? value : parent + "/" + value);
        return joined.Length == 0 ? "." : joined;
    }

    private static bool SafeSelector(string value) => !value.StartsWith('/') && !value.StartsWith(':') && !value.Split('/').Contains("..", StringComparer.Ordinal);
}
