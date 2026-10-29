using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Declared source token evidence only. This service does not infer compiled
// endpoints, preprocessor conditions, transactions or runtime registration.
public interface INativeDescriptorSourceFacts
{
    JsonObject Parse(string text, string language);
}
