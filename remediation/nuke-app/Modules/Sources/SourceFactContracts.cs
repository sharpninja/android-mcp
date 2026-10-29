using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

// Parent-approved concern seams. One eventual service implements all three;
// Catalog validates/stores these facts and never copies their producer rules.
public interface ICallbackSourceFacts
{
    JsonObject TypeCandidate(JsonObject declaration);
    JsonObject EndpointMethod(JsonObject declaration, JsonObject? owner = null);
    JsonArray RegistrationCandidates(JsonObject declaration, IReadOnlyDictionary<string, JsonObject> resolvedTypes);
    JsonArray PossiblePairs(JsonArray candidates);
}

public interface IFunctionalTaxonomySourceFacts
{
    JsonObject Taxonomy(JsonObject declaration);
}

public interface IInterfaceApplicabilitySourceFacts
{
    JsonObject Produce(JsonObject source, JsonObject linkage, JsonArray? hidlHashes = null);
}
