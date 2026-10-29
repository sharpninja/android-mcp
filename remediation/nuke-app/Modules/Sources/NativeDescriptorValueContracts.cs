using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// JSON boundary for original parse inputs. String callers keep their existing
// service contract; accepted empty JSON containers must not be silently lost.
public interface INativeDescriptorValueSourceFacts
{
    JsonObject ParseValue(JsonNode? text, JsonNode? language);
}
