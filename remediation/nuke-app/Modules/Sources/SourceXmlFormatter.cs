using System.Text;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Historical XML rendering is one explicit source compatibility rule set.
// It has no ambient namespace registrations and never rewrites source bytes.
public sealed class SourceXmlFormatter : ISourceXmlFormatter
{
    private static readonly IReadOnlyDictionary<string,string> KnownPrefixes = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["http://www.w3.org/XML/1998/namespace"] = "xml", ["http://www.w3.org/1999/xhtml"] = "html",
        ["http://www.w3.org/1999/02/22-rdf-syntax-ns#"] = "rdf", ["http://schemas.xmlsoap.org/wsdl/"] = "wsdl",
        ["http://www.w3.org/2001/XMLSchema"] = "xs", ["http://www.w3.org/2001/XMLSchema-instance"] = "xsi", ["http://purl.org/dc/elements/1.1/"] = "dc"
    };
    public string Serialize(JsonObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var names = new Dictionary<string,string>(StringComparer.Ordinal); var namespaces = new Dictionary<string,string>(StringComparer.Ordinal);
        void Name(string value)
        {
            if (names.ContainsKey(value)) return;
            if (!value.StartsWith('{')) { names.Add(value, value); return; }
            var boundary = value.LastIndexOf('}'); if (boundary < 2 || boundary + 1 == value.Length) throw new ArgumentException("Malformed source XML qualified name");
            var uri = value[1..boundary];
            if (!namespaces.TryGetValue(uri, out var prefix))
            {
                prefix = KnownPrefixes.GetValueOrDefault(uri) ?? "ns" + namespaces.Count;
                if (prefix != "xml") namespaces.Add(uri, prefix);
            }
            names.Add(value, prefix.Length > 0 ? prefix + ":" + value[(boundary + 1)..] : value[(boundary + 1)..]);
        }
        void Visit(JsonObject node)
        {
            Name(Tag(node)); foreach (var attribute in Attributes(node)) Name(attribute.Key);
            foreach (var child in Children(node)) Visit(child);
        }
        Visit(element); var output = new StringBuilder();
        void Write(JsonObject node, bool root)
        {
            var tag = names[Tag(node)]; output.Append('<').Append(tag);
            if (root)
                foreach (var declaration in namespaces.OrderBy(x => x.Value, StringComparer.Ordinal))
                    output.Append(" xmlns").Append(declaration.Value.Length > 0 ? ":" + declaration.Value : "").Append("=\"").Append(Escape(declaration.Key, true)).Append('"');
            foreach (var attribute in Attributes(node)) output.Append(' ').Append(names[attribute.Key]).Append("=\"").Append(Escape(attribute.Value!.GetValue<string>(), true)).Append('"');
            var children = Children(node).ToArray(); var text = node["text"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(text) || children.Length > 0)
            {
                output.Append('>'); if (text is not null) output.Append(Escape(text, false));
                foreach (var child in children) Write(child, false);
                output.Append("</").Append(tag).Append('>');
            }
            else output.Append(" />");
            if (node["tail"]?.GetValue<string>() is { Length: > 0 } tail) output.Append(Escape(tail, false));
        }
        Write(element, true); return output.ToString();
    }
    private static string Tag(JsonObject node) => node["tag"]?.GetValue<string>() ?? throw new ArgumentException("Source XML element tag is required");
    private static JsonObject Attributes(JsonObject node) => node["attributes"] as JsonObject ?? throw new ArgumentException("Source XML attributes object is required");
    private static IEnumerable<JsonObject> Children(JsonObject node) => (node["children"] as JsonArray ?? throw new ArgumentException("Source XML children array is required"))
        .Select(x => x as JsonObject ?? throw new ArgumentException("Source XML child element object is required"));
    internal static string Escape(string value, bool attribute)
    {
        value = value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        return attribute ? value.Replace("\"", "&quot;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal).Replace("\n", "&#10;", StringComparison.Ordinal).Replace("\t", "&#09;", StringComparison.Ordinal) : value;
    }
}
