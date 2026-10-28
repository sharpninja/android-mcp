using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public interface ISourceHtmlDecoder { string Decode(string text); }
public interface IHtmlEntityDataAudit
{
    // This reads restricted literal DATA syntax. It must never evaluate code,
    // import a module, create a process or depend on an interpreter.
    JsonObject Compare(ReadOnlyMemory<byte> officialJson, string legacySourceText);
}
