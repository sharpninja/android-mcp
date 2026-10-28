using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>Named documentation sections, never compiled definitions or grants.</summary>
public sealed class PermissionReferenceSourceFacts(IProfiledSourceHtmlDecoder decoder) : IPermissionReferenceSourceFacts
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly Regex TagName = new(@"^/?\s*([A-Za-z][^\t\n\r\f />]*)", RegexOptions.CultureInvariant);
    private static readonly Regex Attributes = new("""([^\t\n\r\f />=]+)(?:[\t\n\r\f ]*=[\t\n\r\f ]*(?:"([^"]*)"|'([^']*)'|([^>\t\n\r\f ]*)))?""", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"[\s\u001c-\u001f]+", RegexOptions.CultureInvariant);
    private static readonly Regex Constants = new("""Constant\s+Value\s*:\s*("(?:[^"\\]|\\.)*")""", RegexOptions.CultureInvariant);
    private static readonly Regex Protection = new(@"Protection\s+level\s*:\s*([^\r\n]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private const string Semantics = "Unversioned official API reference snapshot; raw introduced/deprecated/removed and protection metadata preserved separately";
    private const string Authority = "Documentation fact only; not a compiled definition, OS grant or MCP client permission";
    private sealed class Section(int depth, Dictionary<string, string?> attributes, int line)
    {
        public int Depth { get; } = depth;
        public Dictionary<string, string?> Attributes { get; } = attributes;
        public int Line { get; } = line;
        public List<string?> Names { get; } = [];
        public StringBuilder Text { get; } = new();
        public StringBuilder Signature { get; } = new();
    }
    public JsonObject Parse(ReadOnlyMemory<byte> html)
    {
        var text = Utf8.GetString(html.Span);
        var active = new List<Section>(); var rows = new List<JsonObject>(); var other = new List<JsonObject>();
        var depth = 0; var signature = false; var script = 0;
        void Append(string value, bool inSignature = false)
        { foreach (var section in active) { section.Text.Append(value); if (inSignature) section.Signature.Append(value); } }
        void Finish(Section section)
        {
            if (section.Names.Count != 1) return;
            var name = section.Names[0]; var declaration = Spaces.Replace(section.Signature.ToString().Trim(), " ");
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(declaration, @"\bpublic\s+static\s+final\s+(?:java\.lang\.)?String\s+" + Regex.Escape(name) + @"\b", RegexOptions.CultureInvariant))
            {
                other.Add(new() { ["name"] = name, ["line"] = section.Line, ["signature"] = declaration, ["reason"] = "Not a recognized documented String constant section" }); return;
            }
            var body = section.Text.ToString(); var matches = Constants.Matches(body); string? value = null; string? unknown = null;
            if (matches.Count == 1)
            {
                try { value = JsonSerializer.Deserialize<string>(matches[0].Groups[1].Value); }
                catch (JsonException) { unknown = "Documented literal is not a recognized string encoding"; }
            }
            else unknown = "Missing or ambiguous documented constant literal";
            var protection = Protection.Match(body);
            rows.Add(new()
            {
                ["name"] = name, ["symbol"] = "android.Manifest.permission." + name, ["value"] = value, ["line"] = section.Line,
                ["introduced_raw"] = section.Attributes.GetValueOrDefault("data-version-added"), ["deprecated_raw"] = section.Attributes.GetValueOrDefault("data-version-deprecated"),
                ["removed_raw"] = section.Attributes.GetValueOrDefault("data-version-removed"), ["protection_level_raw"] = protection.Success ? protection.Groups[1].Value.Trim() : null,
                ["signature"] = declaration, ["value_status"] = value is null ? "unknown" : "documented-constant", ["unknown_reason"] = unknown, ["authority"] = Authority
            });
        }
        void Start(string tag, Dictionary<string, string?> attributes, int line)
        {
            if (tag is "script" or "style") script++;
            if (script != 0) return;
            if (tag == "div")
            { depth++; if (attributes.ContainsKey("data-version-added")) active.Add(new(depth, attributes, line)); }
            if (tag is "p" or "div" or "br" or "h3" or "pre" or "li") Append("\n");
            var classes = Spaces.Split(attributes.GetValueOrDefault("class") ?? "");
            if (tag == "h3" && classes.Contains("api-name", StringComparer.Ordinal))
                foreach (var section in active) section.Names.Add(attributes.GetValueOrDefault("id"));
            if (tag == "pre" && classes.Contains("api-signature", StringComparer.Ordinal)) signature = true;
        }
        void End(string tag)
        {
            if (tag is "script" or "style") { script = Math.Max(0, script - 1); return; }
            if (script != 0) return;
            if (tag == "pre") signature = false;
            if (tag is "p" or "div" or "br" or "h3" or "pre" or "li") Append("\n");
            if (tag != "div") return;
            var completed = active.Where(x => x.Depth == depth).ToArray(); active.RemoveAll(x => x.Depth == depth);
            foreach (var section in completed) Finish(section);
            depth = Math.Max(0, depth - 1);
        }

        // Single feed, quote-aware events. Comments and raw-text elements are
        // never scanned as nested markup. Entity expansion is shared once.
        var offset = 0; string? rawTag = null; var rawEscapable = false;
        while (offset < text.Length)
        {
            if (rawTag is not null)
            {
                var closing = Regex.Match(text[offset..], @"</\s*" + Regex.Escape(rawTag) + @"\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var count = closing.Success ? closing.Index : text.Length - offset;
                if (script == 0) { var data = text.Substring(offset, count); Append(rawEscapable ? decoder.Decode(data) : data, signature); }
                offset += count;
                if (!closing.Success) break;
                End(rawTag); offset += closing.Length; rawTag = null; continue;
            }
            if (text[offset] != '<')
            {
                var next = text.IndexOf('<', offset); if (next < 0) next = text.Length;
                if (script == 0) Append(decoder.Decode(text[offset..next]), signature);
                offset = next; continue;
            }
            if (text.AsSpan(offset).StartsWith("<!--", StringComparison.Ordinal))
            { var close = text.IndexOf("-->", offset + 4, StringComparison.Ordinal); offset = close < 0 ? text.Length : close + 3; continue; }
            var end = TagEnd(text, offset + 1);
            if (end < 0) { if (script == 0) Append(decoder.Decode(text[offset..]), signature); break; }
            var token = text[(offset + 1)..end]; var match = TagName.Match(token);
            if (!match.Success) { offset = end + 1; continue; }
            var tag = match.Groups[1].Value.ToLowerInvariant(); var ending = token.StartsWith('/');
            if (ending) End(tag);
            else
            {
                var attributes = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (Match attribute in Attributes.Matches(token[match.Length..].TrimEnd('/')))
                {
                    var value = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Success ? attribute.Groups[4].Value : null;
                    attributes[attribute.Groups[1].Value.ToLowerInvariant()] = value is null ? null : decoder.Decode(value, SourceHtmlDecodingProfile.Attribute);
                }
                Start(tag, attributes, 1 + text.AsSpan(0, offset).Count('\n'));
                if (token.TrimEnd().EndsWith('/')) End(tag);
                else if (tag is "script" or "style" or "xmp" or "iframe" or "noembed" or "noframes" or "textarea" or "title")
                { rawTag = tag; rawEscapable = tag is "textarea" or "title"; }
            }
            offset = end + 1;
        }
        var duplicates = rows.GroupBy(x => x["name"]!.GetValue<string>(), StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        duplicates.Sort(StrictJsonInspector.CompareScalars);
        return new()
        {
            ["fields"] = new JsonArray(rows.Cast<JsonNode>().ToArray()), ["duplicate_field_names"] = new JsonArray(duplicates.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            ["nonconstant_or_unsupported_sections"] = new JsonArray(other.Cast<JsonNode>().ToArray()), ["unclosed_version_sections"] = active.Count,
            ["known_values"] = rows.Count(x => x["value"] is not null), ["unknown_values"] = rows.Count(x => x["value"] is null), ["source_semantics"] = Semantics
        };
    }
    private static int TagEnd(string text, int start)
    {
        var quote = '\0';
        for (var i = start; i < text.Length; i++)
        { var ch = text[i]; if (quote != '\0') { if (ch == quote) quote = '\0'; } else if (ch is '\'' or '"') quote = ch; else if (ch == '>') return i; }
        return -1;
    }
}
