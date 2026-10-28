using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Recursive evidence has tag, attributes, children, text and tail. No ambient
// namespace registration or source rewrite occurs during legacy rendering.
public interface ISourceXmlFormatter
{
    string Serialize(JsonObject element);
}
