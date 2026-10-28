using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Sources;

public interface IAnnotationTargetSourceFacts
{
    string? XmlJvmKey(string itemName, IReadOnlyDictionary<string, string> owners);
    IReadOnlySet<string> FormalNames(string raw);
}
public sealed record StructuralTypeHeader(JsonNode? TypeParameters, IReadOnlyList<JsonObject> Supertypes);
public interface IStructuralTypeHeaderSourceFacts
{
    StructuralTypeHeader StructuralHeader(JsonObject document);
}
