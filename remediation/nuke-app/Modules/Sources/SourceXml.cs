using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace AndroidMcp.BuildTool.Sources;

internal static class SourceXml
{
    internal static XElement Load(ReadOnlyMemory<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), false);
        return LoadReader(XmlReader.Create(stream, Settings()));
    }
    internal static XElement Load(string text)
    {
        using var input = new StringReader(text);
        return LoadReader(XmlReader.Create(input, Settings()));
    }
    private static XmlReaderSettings Settings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
    private static XElement LoadReader(XmlReader input)
    {
        using (input)
        {
            try { return XDocument.Load(input, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace).Root ?? throw new FormatException("XML document has no root"); }
            catch (XmlException error) { throw new FormatException("XML parse failed: " + error.Message, error); }
        }
    }
    // Raw qualified names preserve the legacy Expat locator pass separately
    // from namespace-expanded XElement selection. One secure reader policy.
    internal static IReadOnlyList<(string Name, int Line)> StartElementLocations(ReadOnlyMemory<byte> bytes, IReadOnlyList<string> names)
    {
        using var stream = new MemoryStream(bytes.ToArray(), false);
        using var reader = XmlReader.Create(stream, Settings());
        var result = new List<(string, int)>();
        try
        {
            while (reader.Read()) if (reader.NodeType == XmlNodeType.Element && names.Contains(reader.Name, StringComparer.Ordinal))
                result.Add((reader.Name, ((IXmlLineInfo)reader).LineNumber));
        }
        catch (XmlException error) { throw new FormatException("XML parse failed: " + error.Message, error); }
        return result;
    }
    internal static Dictionary<string,string> Attributes(XElement element) => element.Attributes().Where(x => !x.IsNamespaceDeclaration)
        .ToDictionary(x => x.Name.ToString(), x => x.Value, StringComparer.Ordinal);
    internal static JsonObject Json(IReadOnlyDictionary<string,string> values) => new(values.Select(x => new KeyValuePair<string,JsonNode?>(x.Key, JsonValue.Create(x.Value))));
    internal static int Line(XElement element) => ((IXmlLineInfo)element).LineNumber;
    internal static JsonObject ElementEvidence(XElement element)
    {
        var children = element.Elements().ToArray();
        var text = string.Concat(element.Nodes().TakeWhile(x => x is not XElement).OfType<XText>().Select(x => x.Value));
        var tail = string.Concat(element.NodesAfterSelf().TakeWhile(x => x is not XElement).OfType<XText>().Select(x => x.Value));
        return new() { ["tag"] = element.Name.ToString(), ["attributes"] = Json(Attributes(element)),
            ["children"] = new JsonArray(children.Select(x => (JsonNode)ElementEvidence(x)).ToArray()),
            ["text"] = text.Length > 0 ? text : null, ["tail"] = tail.Length > 0 ? tail : null };
    }
}
