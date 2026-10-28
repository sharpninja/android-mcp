using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class AidlSourceExtractor(Func<ReadOnlyMemory<byte>, string> contentHash) : ISourceExtractor
{
    public string Kind => "aidl";
    private const string GenericReason = "All source bytes retained; generated transaction IDs, resolved imports and wire codecs unknown";
    private const string StructuredReason = "Source fields/enumerants inventoried; imports, runtime type resolution, wire tags/layouts and codecs remain unverified";

    private sealed class Declaration
    {
        internal required JsonObject Record { get; init; }
        internal JsonObject Schema { get; init; } = new();
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
        // The legacy whole-file parser rejects an unclosed annotation before
        // declaration selection. Preserve that source as one unknown schema.
        try { _ = SourceSyntax.ParseAnnotations(SourceSyntax.Mask(text, false)); }
        catch (FormatException error) { return Unknown(source, text, "", [], "AIDL annotation/syntax unresolved: " + error.Message, true); }
        var spans = SourceSyntax.DeclarationSpans(text);
        if (spans.Count == 0)
            return Unknown(source, text, package, imports, "No supported top-level AIDL declaration", false);
        var roots = new List<Declaration>();
        foreach (var span in spans) roots.Add(Parse(source, text, span.Start, span.End, package, imports, null, 0));
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>(); var ordinal = 0; var declarations = new List<Declaration>();
        var status = StructuralParseStatus.Clean;
        foreach (var declaration in roots) Flatten(declaration);
        var first = roots[0].Schema;
        var states = declarations.Select(item => item.Schema["parse_status"]!.GetValue<string>()).ToArray();
        var schemaStatus = declarations.Count > 1 && states.Any(value => value is "unsupported" or "partial" or "field-metadata-partial") ? "partial"
            : roots.Count == 1 ? first["parse_status"]!.GetValue<string>() : "declarations-inventoried";
        var rootDescriptor = roots.Count == 1 && first["declaration_kind"]?.GetValue<string>() == "interface" ? first["descriptor"]?.GetValue<string>() : null;
        var rootKind = roots.Count == 1 ? first["declaration_kind"]?.GetValue<string>() : "multiple";
        return new(records, issues, status, new JsonObject
        { ["aidl_schema"] = SchemaPayload(package, rootDescriptor, rootKind, text, schemaStatus, imports, declarations.Select(item => item.Schema)) });

        void Flatten(Declaration declaration)
        {
            declaration.Record["extras"]!["declaration_ordinal"] = ordinal;
            declaration.Schema["declaration_ordinal"] = ordinal++;
            declarations.Add(declaration);
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
            ["schema_resolution_reason"] = GenericReason
        };
        var declaration = new Declaration
        {
            Record = SourceSyntax.Row("aidl-" + kind, descriptor, descriptor, package, name[(name.LastIndexOf('.') + 1)..], rawHeader,
                SourceSyntax.Line(text, start + matched.Index), annotations: topAnnotations.Annotations, extras: extras),
            Schema = DeclarationMetadata(descriptor, kind, "declaration-only", GenericReason, SourceSyntax.Line(text, start + matched.Index), 0,
                SourceSyntax.CodePointOffset(text, start), SourceSyntax.CodePointOffset(text, end), parent, depth)
        };
        if (opening < 0) return declaration;
        var closing = SourceSyntax.ClosingDelimiter(region, opening, '{', '}');
        if (closing < 0 || depth > 128)
        {
            var reason = closing < 0 ? "Unbalanced declaration body" : "Nested AIDL declaration depth exceeds128";
            declaration.Record["kind"] = "unsupported"; extras["parse_status"] = "unsupported"; extras["parse_error"] = reason;
            extras["schema_resolution_reason"] = reason;
            declaration.Schema["parse_status"] = "unsupported"; declaration.Schema["reason"] = reason;
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
        var schemaReason = kind == "interface" ? GenericReason : StructuredReason;
        extras["schema_resolution_reason"] = schemaReason;
        declaration.Schema["parse_status"] = extras["parse_status"]!.DeepClone(); declaration.Schema["reason"] = schemaReason;
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
            var result = new Declaration { Record = bad, Status = StructuralParseStatus.Malformed,
                Schema = DeclarationMetadata(null, null, "unsupported", reason) };
            result.Issues.Add(Issue("aidl-semantic-gap", reason, text, start, end));
            result.Issues.Add(Issue("aidl-reconciliation", "AIDL annotation/syntax unresolved; full source retained", text, start, end));
            return result;
        }
    }

    private ExtractionResult Unknown(SourceDocument source, string text, string package, string[] imports, string reason, bool syntaxFailure)
    {
        var row = SourceSyntax.Row("unsupported", "unparsed-source:" + source.Context.Repository + ":" + source.RelativePath + ":" + contentHash(source.Bytes),
            null, package, Path.GetFileName(source.RelativePath), text, 1, extras: new() { ["parse_error"] = reason, ["source_bytes_retained"] = true });
        var issues = new List<SourceIssue> { new("aidl-semantic-gap", reason, null, null) };
        if (syntaxFailure) issues.Add(new("aidl-reconciliation", "AIDL lexical reconciliation unresolved; full source retained", null, null));
        return new([row], issues, syntaxFailure ? StructuralParseStatus.Malformed : StructuralParseStatus.Unsupported,
            new JsonObject { ["aidl_schema"] = SchemaPayload(package, null, null, text, "unsupported", imports, [DeclarationMetadata(null, null, "unsupported", reason)]) });
    }

    private static JsonObject DeclarationMetadata(string? descriptor, string? kind, string status, string reason,
        int? line = null, int? ordinal = null, int? start = null, int? end = null, string? parent = null, int? depth = null) => new()
    {
        ["descriptor"] = descriptor, ["declaration_kind"] = kind, ["parse_status"] = status, ["reason"] = reason,
        ["declaration_line"] = line, ["declaration_ordinal"] = ordinal,
        ["declaration_span"] = start.HasValue ? new JsonArray(start.Value, end!.Value) : null,
        ["parent_descriptor"] = parent, ["declaration_depth"] = depth
    };
    private static JsonObject SchemaPayload(string package, string? descriptor, string? kind, string text, string status, string[] imports, IEnumerable<JsonObject> declarations) => new()
    {
        ["package"] = package, ["descriptor"] = descriptor, ["declaration_kind"] = kind, ["raw_schema"] = text, ["parse_status"] = status,
        ["transaction_metadata"] = new JsonObject { ["imports"] = SourceSyntax.Strings(imports), ["wire_status"] = "unknown", ["declarations"] = new JsonArray(declarations.Select(item => (JsonNode)item.DeepClone()).ToArray()) },
        ["codec_status"] = "unknown"
    };

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
