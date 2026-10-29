using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public enum SoongModuleFamily { Interfaces, Apex }
public interface ISoongSourceFacts
{
    JsonArray Modules(string text, SoongModuleFamily family);
    JsonNode? Literal(JsonObject node, string? property = null);
    IReadOnlyList<string> MatchSources(string buildPath, IReadOnlyList<string> patterns, IReadOnlyList<string> excluded, IReadOnlyList<string> declaredPaths);
    string? ExplicitManifestPath(string buildPath, JsonObject apexModule);
}
