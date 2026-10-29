using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public sealed record SdkAvailabilityEvidence(IReadOnlyList<JsonObject> Records, IReadOnlyList<JsonObject> SdkDefinitions);
public interface ISdkAvailabilitySourceFacts
{
    SdkAvailabilityEvidence Enumerate(ReadOnlyMemory<byte> xml);
}
