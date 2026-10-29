using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

public sealed class DocumentationGenerationManifestFactory:IArtifactGenerationManifestFactory
{
    public string RelativePath=>DocumentationAssetPaths.Manifest;
    public Artifact Create(string generationId,IReadOnlyList<StoredArtifact> inputs,IReadOnlyList<StoredArtifact> outputs)
    {
        var json=new JsonObject{{"schema_version","1.0.0"},{"kind","android-mcp-design-generation"},{"generation_id",generationId},{"inputs",Rows(inputs)},{"artifacts",Rows(outputs)}};
        return new(RelativePath,Encoding.UTF8.GetBytes(json.ToJsonString()+"\n"),"application/json");
    }
    private static JsonArray Rows(IEnumerable<StoredArtifact> artifacts)=>new(artifacts.Select(a=>(JsonNode?)new JsonObject
    {{"path",a.RelativePath},{"sha256",a.Sha256},{"size_bytes",a.Length},{"media_type",a.MediaType}}).ToArray());
}
