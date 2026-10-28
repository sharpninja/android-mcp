using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

public sealed class ManifestSourceExtractor : ISourceExtractor
{
    private const string Android = "{http://schemas.android.com/apk/res/android}";
    private static readonly IReadOnlyDictionary<string,string> Evidence = new Dictionary<string,string>(StringComparer.Ordinal)
    { ["permission"] = "definition", ["permission-tree"] = "tree-definition", ["permission-group"] = "group-definition", ["uses-permission"] = "reference", ["uses-permission-sdk-23"] = "reference" };
    public string Kind => "manifest";
    public ExtractionResult Extract(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bytes = source.Bytes.Span;
        if (bytes.Length == 0) return new([], [], StructuralParseStatus.Clean,
            new() { ["status"] = "empty-source-no-declarations", ["permission_elements"] = 0, ["valid_android_manifest"] = false });
        if (bytes.Length >= 4 && bytes[0] == 3 && bytes[1] == 0 && bytes[2] == 8 && bytes[3] == 0)
            throw new FormatException("Compiled Android binary XML requires a separate resource-table decoder");
        JsonObject? normalization = null; XElement root; var status = "parsed";
        try
        {
            var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            var text = new UTF8Encoding(false, true).GetString(bytes[offset..]); var position = 0;
            while (true)
            {
                var leading = SourceSyntax.Rx(@"\G(?:\s+|<!--[\s\S]*?-->)").Match(text, position);
                if (!leading.Success) break; position += leading.Length;
            }
            if (position > 0 && text.AsSpan(position).StartsWith("<?xml ", StringComparison.Ordinal))
            {
                var declaration = SourceSyntax.Rx("\\G<\\?xml\\s+version=[\"']1\\.0[\"'](?:\\s+encoding=[\"'](?:utf-8|UTF-8)[\"'])?(?:\\s+standalone=[\"'](?:yes|no)[\"'])?\\s*\\?>").Match(text, position);
                if (declaration.Success)
                {
                    var end = position + declaration.Length;
                    normalization = new() { ["kind"] = "late-utf8-xml-declaration-masked-after-comments", ["character_span"] = new JsonArray(SourceSyntax.CodePointOffset(text, position), SourceSyntax.CodePointOffset(text, end)),
                        ["raw_declaration"] = declaration.Value, ["raw_source_unchanged"] = true };
                    text = text[..position] + string.Concat(declaration.Value.Select(x => x == '\n' ? '\n' : ' ')) + text[end..];
                }
            }
            root = SourceXml.Load(text);
        }
        catch (DecoderFallbackException) { root = SourceXml.Load(source.Bytes); status = "parsed-xml-declared-encoding"; }
        if (root.Name != "manifest") throw new FormatException("Root is not an Android manifest element");
        var records = new List<JsonObject>(); var issues = new List<SourceIssue>(); var definitions = new HashSet<string>(StringComparer.Ordinal); var permissionElements = 0;
        foreach (var element in root.Descendants())
        {
            if (element.Name.NamespaceName.Length > 0 || !Evidence.TryGetValue(element.Name.LocalName, out var evidence)) continue;
            var tag = element.Name.LocalName; var attributes = SourceXml.Json(SourceXml.Attributes(element)); var name = attributes[Android + "name"]?.GetValue<string>();
            if (tag == "permission")
            {
                permissionElements++;
                if (name is null) { evidence = "definition-name-unresolved"; issues.Add(new("manifest-permission-name", "Permission element lacks Android-namespace name", null, null)); }
                else if (!definitions.Add(name)) issues.Add(new("manifest-duplicate-permission", "Duplicate permission name in one manifest; raw element retained", null, null));
            }
            records.Add(new() { ["kind"] = "manifest-" + tag, ["name"] = name, ["line"] = SourceXml.Line(element), ["attributes"] = attributes,
                ["children"] = new JsonArray(element.Elements().Select(x => (JsonNode)SourceXml.ElementEvidence(x)).ToArray()), ["evidence_kind"] = evidence,
                ["protection_level_raw"] = attributes[Android + "protectionLevel"]?.DeepClone(), ["os_authority"] = "unknown", ["mcp_client_grants"] = "not-inferred" });
        }
        return new(records, issues, issues.Count > 0 ? StructuralParseStatus.Partial : StructuralParseStatus.Clean,
            new() { ["status"] = status, ["normalization"] = normalization, ["permission_elements"] = permissionElements, ["valid_android_manifest"] = true });
    }
}
