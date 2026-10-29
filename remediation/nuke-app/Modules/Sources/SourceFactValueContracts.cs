using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Additive boundary for original JSON values. Typed Catalog callers keep the
// existing concern interfaces; both entries must use one producer authority.
public interface ISourceFactValueProducer
{
    JsonObject TypeCandidateValue(JsonNode? declaration);
    JsonObject EndpointMethodValue(JsonNode? declaration, JsonNode? owner = null);
    JsonArray RegistrationCandidatesValue(JsonNode? declaration, JsonNode? resolvedTypes);
    JsonArray PossiblePairsValue(JsonNode? candidates);
    JsonObject TaxonomyValue(JsonNode? declaration);
}
