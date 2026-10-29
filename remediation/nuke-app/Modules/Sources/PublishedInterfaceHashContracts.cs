using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public interface IPublishedInterfaceHashSourceFacts
{
    JsonObject AidlHashes(string text);
    JsonObject HidlHashes(string text);
}
