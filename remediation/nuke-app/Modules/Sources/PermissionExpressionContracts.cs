using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// The caller supplies only same-snapshot proven symbols. No grants or runtime
// authority are established by declared annotation expressions.
public interface IPermissionExpressionSourceFacts
{
    JsonObject Permission(JsonArray annotations, IReadOnlyDictionary<string, string>? symbols = null, bool aidl = false);
}

public interface IPermissionReferenceSourceFacts
{
    JsonObject Parse(ReadOnlyMemory<byte> html);
}
