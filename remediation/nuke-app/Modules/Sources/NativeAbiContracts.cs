using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public interface INativeAbiSourceFacts { JsonObject Parse(string text); }
