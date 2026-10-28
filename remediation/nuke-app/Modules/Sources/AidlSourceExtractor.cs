using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class AidlSourceExtractor(Func<ReadOnlyMemory<byte>, string> contentHash) : ISourceExtractor
{
    public string Kind => "aidl";

    private sealed class Declaration
    {
        internal required JsonObject Record { get; init; }
        internal List<JsonObject> Members { get; } = [];
        internal List<Declaration> Children { get; } = [];
        internal List<SourceIssue> Issues { get; } = [];
        internal StructuralParseStatus Status { get; set; } = StructuralParseStatus.Clean;
    }

    public ExtractionResult Extract(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var text = new UTF8Encoding(false, true).GetString(source.Bytes.Span);
        var code = SourceSyntax.Mask(text, true);
        var package = SourceSyntax.Rx(@"\bpackage\s+([\w.]+)\s*;").Match(code).Groups[1].Value;
        var imports = SourceSyntax.Rx(@"\bimport\s+([\w.]+)\s*;").Matches(code).Select(x => x.Groups[1].Value).ToArray();
        var spans = SourceSyntax.DeclarationSpans(text);
        if (spans.Count == 0)
        {
            var reason = "No supported top-level AIDL declaration";
            var row = SourceSyntax.Row("unsupported", "unparsed-source:" + source.Context.Repository + ":" + source.RelativePath + ":" + contentHash(source.Bytes),
                null, package, Path.GetFileName(source.RelativePath), text, 1, extras: new() { ["parse_error"] = reason, ["source_bytes_retained"] = true });
            return new([row], [new("aidl-semantic-gap", reason, null, null)], StructuralParseStatus.Unsupported);
        }
        var roots = new List<Declaration>();
        foreach (var span in spans) roots.Add(Parse(source, text, span.Start, span.End, package, imports, null, 0));
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>(); var ordinal = 0;
        var status = StructuralParseStatus.Clean;
        foreach (var declaration in roots) Flatten(declaration);
        return new(records, issues, status);

        void Flatten(Declaration declaration)
        {
            declaration.Record["extras"]!["declaration_ordinal"] = ordinal++;
            records.Add(declaration.Record); records.AddRange(declaration.Members); issues.AddRange(declaration.Issues);
            if (Rank(declaration.Status) > Rank(status)) status = declaration.Status;
            foreach (var child in declaration.Children) Flatten(child);
        }
    }

    private Declaration Parse(SourceDocument source, string text, int start, int end, string package, string[] imports, string? parent, int depth)
    {
        var region = text[start..end]; var code = SourceSyntax.Mask(region, true);
        var matched = SourceSyntax.Rx(@"\b(interface|parcelable|union|enum)\s+([\w]+(?:\.[\w]+)*)").Match(code);
        var kind = matched.Groups[1].Value; var name = matched.Groups[2].Value;
        var descriptor = parent is not null && !name.StartsWith(parent + ".", StringComparison.Ordinal) ? parent + "." + name
            : package.Length > 0 && !name.StartsWith(package + ".", StringComparison.Ordinal) ? package + "." + name : name;
        AnnotatedSource topAnnotations;
        try { topAnnotations = SourceSyntax.ParseAnnotations(SourceSyntax.Mask(region[..matched.Index], false)); }
        catch (FormatException error) { return Failed(error.Message); }
        var oneway = kind == "interface" && SourceSyntax.Rx(@"\boneway\b").IsMatch(SourceSyntax.Mask(topAnnotations.Masked, true));
        var opening = code.IndexOf('{', matched.Index + matched.Length);
        var rawHeader = region[..(opening < 0 ? region.Length : opening)];
        if (parent is not null) rawHeader = SourceSyntax.Mask(rawHeader, false);
        rawHeader = rawHeader.Trim();
        var extras = new JsonObject
        {
            ["aidl_kind"] = kind, ["imports"] = SourceSyntax.Strings(imports), ["parse_status"] = "declaration-only",
            ["oneway"] = oneway, ["declaration_ordinal"] = 0, ["parent_descriptor"] = parent, ["declaration_depth"] = depth,
            ["schema_resolution_reason"] = "All source bytes retained; generated transaction IDs, resolved imports and wire codecs unknown"
        };
        var declaration = new Declaration
        {
            Record = SourceSyntax.Row("aidl-" + kind, descriptor, descriptor, package, name[(name.LastIndexOf('.') + 1)..], rawHeader,
                SourceSyntax.Line(text, start + matched.Index), annotations: topAnnotations.Annotations, extras: extras)
        };
        if (opening < 0) return declaration;
        var closing = SourceSyntax.ClosingDelimiter(region, opening, '{', '}');
        if (closing < 0 || depth > 128)
        {
            var reason = closing < 0 ? "Unbalanced declaration body" : "Nested AIDL declaration depth exceeds128";
            declaration.Record["kind"] = "unsupported"; extras["parse_status"] = "unsupported"; extras["parse_error"] = reason;
            declaration.Status = StructuralParseStatus.Malformed;
            declaration.Issues.Add(Issue("aidl-semantic-gap", reason, text, start, end));
            declaration.Issues.Add(Issue("aidl-reconciliation", "AIDL lexical reconciliation unresolved; full source retained", text, start, end));
            return declaration;
        }
        var bodyStart = start + opening + 1;
        var body = SourceSyntax.Mask(region[(opening + 1)..closing], false);
        var nested = SourceSyntax.DeclarationSpans(body);
        foreach (var child in nested)
            declaration.Children.Add(Parse(source, text, bodyStart + child.Start, bodyStart + child.End, package, imports, descriptor, depth + 1));
        var remaining = SourceSyntax.MaskSpans(body, nested); var ordinal = 0; var unsupported = false;
        foreach (var chunk in SourceSyntax.Chunks(remaining, kind == "enum" ? ',' : ';'))
        {
            var line = SourceSyntax.Line(text, bodyStart + chunk.Start);
            JsonObject member;
            try
            {
                var clean = SourceSyntax.ParseAnnotations(chunk.Raw).Masked.Trim();
                if (kind == "interface" && !clean.StartsWith("const ", StringComparison.Ordinal))
                {
                    if (!clean.Contains('(')) throw new FormatException("AIDL non-method statement preserved but not interpreted");
                    member = SourceSyntax.Callable(chunk.Raw, descriptor, package, line, true);
                    member["extras"]!["declaration_ordinal"] = ordinal;
                }
                else member = Field(chunk.Raw, descriptor, package, kind, ordinal, line);
            }
            catch (FormatException error)
            {
                unsupported = true; IReadOnlyList<SourceAnnotation> annotations;
                try { annotations = SourceSyntax.ParseAnnotations(chunk.Raw).Annotations; }
                catch (FormatException) { annotations = []; }
                member = SourceSyntax.Row("unsupported", descriptor + "#unparsed:" + contentHash(Encoding.UTF8.GetBytes(chunk.Raw)), descriptor,
                    package, "unparsed", chunk.Raw, line, annotations: annotations,
                    extras: new() { ["parse_error"] = error.Message, ["declared_ordinal"] = ordinal });
            }
            member["extras"]!["imports"] = SourceSyntax.Strings(imports);
            member["extras"]!["interface_oneway"] = oneway;
            member["extras"]!["interface_annotations"] = SourceSyntax.Annotations(topAnnotations.Annotations);
            declaration.Members.Add(member); ordinal++;
        }
        extras["parse_status"] = kind == "interface" ? unsupported ? "partial" : "methods-parsed" : unsupported ? "field-metadata-partial" : "field-metadata-parsed";
        if (unsupported)
        {
            declaration.Status = StructuralParseStatus.Partial;
            declaration.Issues.Add(Issue("aidl-semantic-gap", "Unsupported source statement retained; wire layout is unknown", text, start, end));
        }
        return declaration;

        Declaration Failed(string reason)
        {
            var bad = SourceSyntax.Row("unsupported", "unparsed-source:" + source.Context.Repository + ":" + source.RelativePath + ":" + contentHash(source.Bytes), null,
                package, Path.GetFileName(source.RelativePath), region, SourceSyntax.Line(text, start),
                extras: new() { ["parse_error"] = reason, ["source_bytes_retained"] = true });
            var result = new Declaration { Record = bad, Status = StructuralParseStatus.Malformed };
            result.Issues.Add(Issue("aidl-semantic-gap", reason, text, start, end));
            result.Issues.Add(Issue("aidl-reconciliation", "AIDL annotation/syntax unresolved; full source retained", text, start, end));
            return result;
        }
    }

    private static JsonObject Field(string raw, string descriptor, string package, string kind, int ordinal, int line)
    {
        var annotated = SourceSyntax.ParseAnnotations(raw); var clean = annotated.Masked.Trim().TrimEnd(';').Trim();
        var constant = SourceSyntax.Rx(@"^const\b").IsMatch(clean);
        clean = SourceSyntax.Rx(@"^const\s+").Replace(clean, "");
        string name; string type; string? value;
        if (kind == "enum")
        {
            var match = SourceSyntax.Rx(@"^([A-Za-z_]\w*)\s*(?:=\s*(.*))?$").Match(clean);
            if (!match.Success) throw new FormatException("Unresolved AIDL enumerant");
            name = match.Groups[1].Value; type = descriptor; value = match.Groups[2].Success ? match.Groups[2].Value : null;
        }
        else
        {
            var match = SourceSyntax.Rx(@"^(.+?)(?:\s+|(?<=[\]>]))([A-Za-z_]\w*)\s*(?:=\s*(.*))?$").Match(clean);
            if (!match.Success) throw new FormatException("Unresolved AIDL field declaration");
            type = match.Groups[1].Value;
            if (type.IndexOfAny([';', '=', '{', '}', '(', ')']) >= 0) throw new FormatException("Unsupported nested AIDL field syntax");
            type = SourceSyntax.TypeName(type); name = match.Groups[2].Value; value = match.Groups[3].Success ? match.Groups[3].Value : null;
        }
        return SourceSyntax.Row("field", descriptor + "#" + name, descriptor, package, name, raw, line, type, annotations: annotated.Annotations,
            extras: new()
            {
                ["aidl_declaration_kind"] = kind, ["enum_constant"] = kind == "enum", ["is_constant"] = constant,
                ["constant_raw"] = value, ["declared_ordinal"] = ordinal, ["wire_tag"] = null,
                ["wire_layout_status"] = "unknown; declaration order/value is not verified wire metadata"
            });
    }

    private static SourceIssue Issue(string code, string reason, string text, int start, int end) =>
        new(code, reason, SourceSyntax.ByteOffset(text, start), SourceSyntax.ByteOffset(text, end));

    private static int Rank(StructuralParseStatus status) => status switch
    { StructuralParseStatus.Clean => 0, StructuralParseStatus.Partial => 1, StructuralParseStatus.Unsupported => 2, StructuralParseStatus.Malformed => 3, _ => 4 };
}
