using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class SignatureSourceExtractor(Func<ReadOnlyMemory<byte>, string> contentHash) : ISourceExtractor
{
    public string Kind => "signature";

    public ExtractionResult Extract(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var text = new UTF8Encoding(false, true).GetString(source.Bytes.Span);
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>();
        var scopes = new Stack<(string Package, string? Owner)>(); var package = ""; string? owner = null;
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].Trim();
            if (raw.Length == 0 || raw.StartsWith("//", StringComparison.Ordinal)) continue;
            if (raw == "}")
            {
                (package, owner) = scopes.Count > 0 ? scopes.Pop() : ("", null);
                continue;
            }
            if (raw.EndsWith('{')) scopes.Push((package, owner));
            try
            {
                var annotated = SourceSyntax.ParseAnnotations(raw); var clean = annotated.Masked;
                JsonObject record;
                if (clean.StartsWith("package ", StringComparison.Ordinal))
                {
                    package = clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1]; owner = null;
                    record = SourceSyntax.Row("package", package, null, package, package, raw, index + 1, annotations: annotated.Annotations);
                }
                else
                {
                    var type = SourceSyntax.Rx(@"(?<![\w.])(@interface|class|interface|enum)\s+([\w.$]+)").Match(clean);
                    if (type.Success && clean.EndsWith('{'))
                    {
                        if (package.Length == 0) throw new FormatException("Type without package");
                        var kind = type.Groups[1].Value == "@interface" ? "annotation" : type.Groups[1].Value;
                        var name = type.Groups[2].Value; owner = package + "." + name;
                        record = SourceSyntax.Row(kind, owner, owner, package, name, raw, index + 1, annotations: annotated.Annotations,
                            extras: new() { ["type_header"] = clean });
                    }
                    else
                    {
                        if (owner is null) throw new FormatException("Member without owner");
                        var memberKind = clean.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (memberKind is "method" or "ctor") record = SourceSyntax.Callable(raw, owner, package, index + 1, false);
                        else record = Field(raw, owner, package, index + 1, memberKind, annotated);
                    }
                }
                records.Add(record);
            }
            catch (FormatException error)
            {
                if (raw.EndsWith('{')) owner = null;
                records.Add(SourceSyntax.Row("unsupported", "unparsed:" + contentHash(Encoding.UTF8.GetBytes(raw)), owner, package, "unparsed", raw, index + 1,
                    extras: new() { ["parse_error"] = error.Message }));
                issues.Add(new("signature-unsupported", error.Message, null, null));
            }
        }
        return new(records, issues, issues.Count == 0 ? StructuralParseStatus.Clean : StructuralParseStatus.Partial);
    }

    private static JsonObject Field(string raw, string owner, string package, int line, string memberKind, AnnotatedSource annotated)
    {
        if (memberKind is not ("field" or "enum_constant" or "property")) throw new FormatException("Unsupported signature member");
        var clean = annotated.Masked.Trim().TrimEnd(';').Trim();
        var body = clean[memberKind.Length..].Trim(); var equal = body.IndexOf('=');
        var head = (equal < 0 ? body : body[..equal]).Trim(); var name = SourceSyntax.Rx(@"([\w$]+)$").Match(head);
        if (!name.Success) throw new FormatException("Unresolved field name");
        var value = equal < 0 ? null : body[(equal + 1)..].Trim();
        return SourceSyntax.Row(memberKind == "enum_constant" ? "field" : memberKind, owner + "#" + name.Value,
            owner, package, name.Value, raw, line, SourceSyntax.ReturnType(head[..name.Index].Trim()), annotations: annotated.Annotations,
            extras: new() { ["constant_raw"] = value, ["enum_constant"] = memberKind == "enum_constant" });
    }
}
