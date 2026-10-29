using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

// Equality of values already admitted by the shared JSON reader. This service
// neither parses input nor changes JSON/schema admission or canonical bytes.
public interface ILegacyJsonValueEquality
{
    bool Equal(JsonElement left, JsonElement right);
}
