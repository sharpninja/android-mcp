using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Compares literal entity DATA without importing or evaluating a source module.</summary>
public sealed class HtmlEntityDataAudit(IStrictJsonInspector inspector) : IHtmlEntityDataAudit
{
    public HtmlEntityDataAudit() : this(new StrictJsonInspector()) { }

    public JsonObject Compare(ReadOnlyMemory<byte> officialJson, string legacySourceText)
    {
        var official = ReadOfficial(officialJson, inspector);
        var legacy = ReadLegacy(legacySourceText);
        var missing = official.Keys.Except(legacy.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var extra = legacy.Keys.Except(official.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var changed = official.Keys.Intersect(legacy.Keys, StringComparer.Ordinal).Where(name => official[name] != legacy[name]).Order(StringComparer.Ordinal).ToArray();
        return new JsonObject
        {
            ["official_entries"] = official.Count, ["legacy_entries"] = legacy.Count,
            ["equivalent"] = missing.Length + extra.Length + changed.Length == 0,
            ["missing_in_legacy"] = Strings(missing), ["extra_in_legacy"] = Strings(extra),
            ["changed_values"] = new JsonArray(changed.Select(name => (JsonNode)new JsonObject { ["name"] = name, ["official"] = official[name], ["legacy"] = legacy[name] }).ToArray())
        };
    }

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

    internal static IReadOnlyDictionary<string, string> ReadOfficial(ReadOnlyMemory<byte> bytes, IStrictJsonInspector inspector)
    {
        var inspected = inspector.Inspect(bytes);
        if (inspected.Diagnostics.Count != 0 || !inspected.Value.HasValue || inspected.Value.Value.ValueKind != JsonValueKind.Object)
            throw new FormatException("Invalid entity JSON data");
        var root = inspected.Value.Value;
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;
            if (!name.StartsWith('&') || name.Length < 2 || property.Value.ValueKind != JsonValueKind.Object
                || !property.Value.TryGetProperty("characters", out var characters) || characters.ValueKind != JsonValueKind.String
                || !property.Value.TryGetProperty("codepoints", out var points) || points.ValueKind != JsonValueKind.Array)
                throw new FormatException("Invalid entity JSON entry");
            var expected = new StringBuilder();
            foreach (var point in points.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Number || !point.TryGetInt32(out var scalar) || !Rune.IsValid(scalar)) throw new FormatException("Invalid entity JSON code point");
                expected.Append(char.ConvertFromUtf32(scalar));
            }
            var text = characters.GetString()!;
            if (expected.ToString() != text) throw new FormatException("Inconsistent entity JSON character data");
            if (!entries.TryAdd(name[1..], text)) throw new FormatException("Duplicate entity JSON name");
        }
        return entries;
    }

    private static IReadOnlyDictionary<string, string> ReadLegacy(string source)
    {
        var starts = Regex.Matches(source, @"(?m)^\s*html5\s*=\s*\{");
        if (starts.Count != 1) throw LegacyError();
        var offset = starts[0].Index + starts[0].Length;
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            SkipTrivia(source, ref offset);
            if (offset >= source.Length) throw LegacyError();
            if (source[offset] == '}') return entries;
            var name = ReadString(source, ref offset);
            SkipTrivia(source, ref offset);
            if (offset >= source.Length || source[offset++] != ':') throw LegacyError();
            SkipTrivia(source, ref offset);
            var value = ReadString(source, ref offset);
            if (!entries.TryAdd(name, value)) throw LegacyError();
            SkipTrivia(source, ref offset);
            if (offset < source.Length && source[offset] == ',') { offset++; continue; }
            if (offset < source.Length && source[offset] == '}') return entries;
            throw LegacyError();
        }
    }

    private static void SkipTrivia(string source, ref int offset)
    {
        while (offset < source.Length)
        {
            if (char.IsWhiteSpace(source[offset])) { offset++; continue; }
            if (source[offset] != '#') return;
            while (offset < source.Length && source[offset] != '\n') offset++;
        }
    }

    private static string ReadString(string source, ref int offset)
    {
        if (offset >= source.Length || source[offset] is not ('\'' or '"')) throw LegacyError();
        var quote = source[offset++];
        var output = new StringBuilder();
        while (offset < source.Length)
        {
            var character = source[offset++];
            if (character == quote)
            {
                var result = output.ToString();
                if (!StrictJsonInspector.IsUnicodeScalarString(result)) throw LegacyError();
                return result;
            }
            if (character is '\n' or '\r') throw LegacyError();
            if (character != '\\') { output.Append(character); continue; }
            if (offset >= source.Length) throw LegacyError();
            var escaped = source[offset++];
            switch (escaped)
            {
                case '\\': output.Append('\\'); break;
                case '\'': output.Append('\''); break;
                case '"': output.Append('"'); break;
                case 'n': output.Append('\n'); break;
                case 'r': output.Append('\r'); break;
                case 't': output.Append('\t'); break;
                case 'b': output.Append('\b'); break;
                case 'f': output.Append('\f'); break;
                case 'v': output.Append('\v'); break;
                case 'a': output.Append('\a'); break;
                case 'x': case 'u': case 'U':
                    var length = escaped == 'x' ? 2 : escaped == 'u' ? 4 : 8;
                    if (offset + length > source.Length || !int.TryParse(source.AsSpan(offset, length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var scalar) || !Rune.IsValid(scalar)) throw LegacyError();
                    output.Append(char.ConvertFromUtf32(scalar)); offset += length; break;
                default: throw LegacyError();
            }
        }
        throw LegacyError();
    }

    private static FormatException LegacyError() => new("Invalid restricted legacy table data");
}
