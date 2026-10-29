using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

// Declared HIDL syntax only. No compiler, inherited-method expansion,
// product selection, transaction numbering or runtime registration occurs.
public sealed class HidlSourceExtractor : ISourceExtractor
{
    private const string Identifier = @"[A-Za-z_]\w*";
    public string Kind => "hidl-source";
    public ExtractionResult Extract(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var text = Encoding.UTF8.GetString(source.Bytes.Span); var mask = SourceSyntax.Mask(text, false); var code = SourceSyntax.Mask(text, true);
        var packageMatch = SourceSyntax.Rx(@"^\s*package\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)@(\d+\.\d+)\s*;").Match(mask);
        var package = packageMatch.Success ? packageMatch.Groups[1].Value + "@" + packageMatch.Groups[2].Value : "";
        var version = packageMatch.Success ? packageMatch.Groups[2].Value : null;
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>(); var unsupported = new JsonArray(); var imports = new JsonArray();
        var seen = new HashSet<(string Canonical, int Line)>(); var unterminated = false;
        JsonObject Add(string kind, string name, string? owner, int start, int end, JsonArray? parameters = null,
            string? returned = null, JsonArray? annotations = null, JsonObject? extra = null)
        {
            var canonical = kind == "package" ? package : kind is "type" or "interface" or "enum" && owner is not null ? owner : (owner ?? (package.Length > 0 ? package : "unresolved-package")) + "::" + name;
            if (kind == "method") canonical += "(" + string.Join(',', (parameters ?? new()).Select(x => x!["type"]!.GetValue<string>())) + ")";
            var visible = SourceSyntax.Mask(text[start..end], false); var leading = visible.Length - visible.TrimStart().Length;
            var line = SourceSyntax.Line(text, start + leading); extra ??= new();
            if (!seen.Add((canonical, line)))
            {
                kind = "unsupported"; canonical += "::duplicate-locator:" + SourceSyntax.CodePointOffset(text, start);
                extra["parse_error"] = "Duplicate declaration at one physical locator; raw syntax retained";
            }
            var extensions = new JsonObject
            {
                ["hidl_package_version"] = version, ["android_availability"] = "unknown", ["wire_transaction_id"] = null, ["wire_codec_status"] = "unknown",
                ["source_character_span"] = new JsonArray(SourceSyntax.CodePointOffset(text, start), SourceSyntax.CodePointOffset(text, end)),
                ["semantic_status"] = "Declared-source structure only; compiler validity, imports and runtime registration unverified"
            };
            foreach (var property in extra) extensions[property.Key] = property.Value?.DeepClone();
            var row = SourceSyntax.Row(kind, canonical, owner, package, name, text[start..end], line, returned, parameters, extras: extensions);
            row["all_annotations"] = annotations ?? new(); records.Add(row);
            if (kind == "unsupported")
            {
                var reason = extensions["parse_error"]?.GetValue<string>() ?? "Unknown HIDL structure";
                unsupported.Add(new JsonObject { ["line"] = line, ["raw"] = text[start..end], ["reason"] = reason });
                issues.Add(new("hidl-unsupported", reason, SourceSyntax.ByteOffset(text, start), SourceSyntax.ByteOffset(text, end)));
            }
            return row;
        }
        void Unknown(string name, string? owner, int start, int end, string reason, JsonArray? annotations = null) =>
            Add("unsupported", name, owner, start, end, annotations: annotations, extra: new() { ["parse_error"] = reason });
        if (!packageMatch.Success)
        {
            Unknown("unresolved-hidl-source", null, 0, text.Length, "Required HIDL package/version declaration absent or malformed");
            return new(records, issues, StructuralParseStatus.Unsupported, new() { ["unsupported"] = unsupported, ["imports"] = imports, ["source_complete"] = false });
        }
        Add("package", package, null, packageMatch.Index, packageMatch.Index + packageMatch.Length);
        void Scope(int start, int end, string? owner, string context)
        {
            foreach (var span in Spans(text, start, end, ';', false, code))
            {
                var a = span.Start; var b = span.End; var (initial, annotations) = PrefixAnnotations(text, a, b, code);
                while (initial < b && char.IsWhiteSpace(mask[initial])) initial++;
                var raw = mask[initial..b].Trim(); if (raw.Length == 0) continue;
                if (b >= end || code[b] != ';')
                { unterminated = true; Unknown("unterminated-hidl-statement", owner, a, b, "Required statement semicolon absent; raw syntax retained", annotations); continue; }
                var import = SourceSyntax.Rx(@"^import\s+([^;{}]+)$").Match(raw);
                if (import.Success && context == "package")
                { imports.Add(new JsonObject { ["raw"] = text[a..b], ["target"] = import.Groups[1].Value.Trim(), ["line"] = SourceSyntax.Line(text, initial) }); continue; }
                var declaration = SourceSyntax.Rx(@"^(interface|struct|union|safe_union|enum)\s+(" + Identifier + @")\b").Match(raw);
                var brace = code.IndexOf('{', initial, b - initial);
                if (declaration.Success && declaration.Groups[1].Value == "interface" && context == "compound" && brace < 0) declaration = Match.Empty;
                if (declaration.Success)
                {
                    var head = declaration.Groups[1].Value; var name = declaration.Groups[2].Value;
                    if (head == "interface" && (context != "package" || !name.StartsWith('I')))
                    { Unknown(name, owner, a, b, "HIDL interfaces require global scope and I-prefixed name", annotations); continue; }
                    var close = code.LastIndexOf('}', Math.Max(initial, b - 1), b - initial);
                    if (brace < 0 || close < brace) { Unknown(name, owner, a, b, "Missing HIDL declaration body", annotations); continue; }
                    var suffix = mask[(close + 1)..b].Trim(); var full = owner is null ? package + "::" + name : owner + "." + name;
                    var header = mask[initial..brace]; var extra = new JsonObject
                    { ["hidl_declaration_kind"] = head, ["enclosing_type"] = owner, ["keyword_character_offset"] = SourceSyntax.CodePointOffset(text, initial), ["hidl_qualified_name"] = full };
                    if (head == "interface") { extra["declared_extends"] = header[declaration.Length..].Trim(); extra["descriptor_source_name"] = full; }
                    if (head == "enum") extra["storage_type_raw"] = header[declaration.Length..].Trim();
                    Add(head == "interface" ? "interface" : head == "enum" ? "enum" : "type", name, full, a, b, annotations: annotations, extra: extra);
                    if (head == "enum")
                    {
                        foreach (var valueSpan in Spans(text, brace + 1, close, ',', false, code))
                        {
                            var (valueStart, valueAnnotations) = PrefixAnnotations(text, valueSpan.Start, valueSpan.End, code);
                            var value = SourceSyntax.Mask(text[valueStart..valueSpan.End], false).Trim();
                            var member = SourceSyntax.Rx(@"^(" + Identifier + @")(?:\s*=\s*(.+))?$").Match(value);
                            if (member.Success) Add("field", member.Groups[1].Value, full, valueSpan.Start, valueSpan.End, returned: full, annotations: valueAnnotations,
                                extra: new() { ["hidl_declaration_kind"] = "enum-value", ["constant_expression_raw"] = member.Groups[2].Success ? member.Groups[2].Value : null });
                            else Unknown("unresolved-enum-value", full, valueSpan.Start, valueSpan.End, "Unknown HIDL enum value");
                        }
                    }
                    else Scope(brace + 1, close, full, head == "interface" ? "interface" : "compound");
                    if (suffix.Length > 0)
                    {
                        if (context == "compound" && SourceSyntax.Rx("^" + Identifier + "$").IsMatch(suffix)) Add("field", suffix, owner, close + 1, b, returned: full, extra: new() { ["hidl_declaration_kind"] = "inline-compound-field" });
                        else Unknown("trailing-declaration-syntax", owner, close + 1, b, "Unrecognized suffix after HIDL type body");
                    }
                    continue;
                }
                var alias = SourceSyntax.Rx(@"^typedef\s+(.+?)\s+(" + Identifier + ")$").Match(raw);
                if (alias.Success)
                { var name = alias.Groups[2].Value; Add("type", name, owner is null ? package + "::" + name : owner + "." + name, a, b, annotations: annotations, extra: new() { ["hidl_declaration_kind"] = "typedef", ["target_type_raw"] = alias.Groups[1].Value }); continue; }
                var method = SourceSyntax.Rx(@"^(oneway\s+)?(" + Identifier + @")\s*\((.*?)\)\s*(?:generates\s*\((.*?)\))?$").Match(raw);
                if (method.Success && context == "interface")
                {
                    try
                    {
                        if (method.Groups[1].Success && method.Groups[4].Success) throw new FormatException("HIDL oneway method cannot declare generated results");
                        var parameters = Typed(raw, method.Groups[3].Index, method.Groups[3].Index + method.Groups[3].Length);
                        var results = method.Groups[4].Success ? Typed(raw, method.Groups[4].Index, method.Groups[4].Index + method.Groups[4].Length) : new JsonArray();
                        if (method.Groups[4].Success && results.Count == 0) throw new FormatException("Empty HIDL generates clause");
                        Add("method", method.Groups[2].Value, owner, a, b, parameters, annotations: annotations,
                            extra: new() { ["hidl_declaration_kind"] = "method", ["results"] = results, ["oneway"] = method.Groups[1].Success });
                    }
                    catch (FormatException error) { Unknown(method.Groups[2].Value, owner, a, b, error.Message, annotations); }
                    continue;
                }
                if (context == "compound")
                {
                    try
                    {
                        var fields = Typed(raw, 0, raw.Length); if (fields.Count != 1) throw new FormatException("Expected exactly one HIDL field");
                        Add("field", fields[0]!["name"]!.GetValue<string>(), owner, a, b, returned: fields[0]!["type"]!.GetValue<string>(), annotations: annotations, extra: new() { ["hidl_declaration_kind"] = "field" }); continue;
                    }
                    catch (FormatException) { }
                }
                Unknown("unresolved-hidl-statement", owner, a, b, "Unsupported HIDL declaration retained verbatim", annotations);
            }
        }
        Scope(packageMatch.Index + packageMatch.Length, text.Length, null, "package");
        return new(records, issues, unterminated ? StructuralParseStatus.Malformed : issues.Count > 0 ? StructuralParseStatus.Partial : StructuralParseStatus.Clean,
            new() { ["unsupported"] = unsupported, ["imports"] = imports, ["source_complete"] = false, ["structural_statement_parse_complete"] = unsupported.Count == 0,
                ["limitation"] = "No compiler validation, complete type resolution, implicit inherited method expansion, Android/module availability or wire transaction inference" });
    }

    private static IEnumerable<SourceSpan> Spans(string text, int start, int end, char separator, bool angles, string? code = null) =>
        SourceSyntax.DelimiterSpans(text, start, end, separator, angles, true, code).Where(x => SourceSyntax.Mask(text[x.Start..x.End], false).Trim().Length > 0);

    private static (int Position, JsonArray Annotations) PrefixAnnotations(string text, int start, int end, string? masked = null)
    {
        var code = masked ?? SourceSyntax.Mask(text, true); var result = new JsonArray(); var position = start;
        while (true)
        {
            while (position < end && char.IsWhiteSpace(code[position])) position++;
            var match = SourceSyntax.Rx(@"\G@(" + Identifier + @"(?:\." + Identifier + ")*)").Match(code, position);
            if (!match.Success || position + match.Length > end) return (position, result);
            var initial = position; position += match.Length; var arguments = "";
            while (position < end && char.IsWhiteSpace(code[position])) position++;
            if (position < end && code[position] == '(')
            {
                var begin = position + 1; var depth = 1; position++;
                while (position < end && depth > 0) { if (code[position] == '(') depth++; else if (code[position] == ')') depth--; position++; }
                if (depth != 0) return (initial, result); arguments = text[begin..(position - 1)];
            }
            result.Add(new JsonObject { ["name"] = match.Groups[1].Value, ["arguments"] = arguments, ["raw"] = text[initial..position], ["semantics"] = "unknown HIDL source annotation" });
        }
    }
    private static JsonArray Typed(string text, int start, int end)
    {
        var result = new JsonArray(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in Spans(text, start, end, ',', true))
        {
            var (initial, annotations) = PrefixAnnotations(text, span.Start, span.End); var clean = SourceSyntax.Mask(text[initial..span.End], false).Trim();
            var match = SourceSyntax.Rx(@"^(.+?)(?:\s+|(?<=[>\]]))(" + Identifier + ")$").Match(clean);
            if (!match.Success) throw new FormatException("Unrecognized typed variable " + clean);
            if (!names.Add(match.Groups[2].Value)) throw new FormatException("Duplicate typed variable names");
            result.Add(new JsonObject { ["name"] = match.Groups[2].Value, ["type"] = SourceSyntax.Rx(@"\s+").Replace(match.Groups[1].Value, " ").Trim(),
                ["annotations"] = new JsonArray(), ["all_annotations"] = annotations, ["raw"] = text[span.Start..span.End] });
        }
        return result;
    }
}
