using System.Text;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Compatibility facade over the single shared generation protocol.</summary>
public sealed class DocumentationAssetOperation(IArtifactStore source,IArtifactStore output,IArtifactLeaseProvider leases,
    IDocumentationProjector projector,IContentHash hashes,string outputRootIdentity):IDocumentationAssetOperation
{
    public async Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootIdentity);
        var scope="design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(outputRootIdentity));
        var runtime=new ArtifactGenerationRuntime(source,output,leases,new ArtifactReceiptVerifier(hashes),new DocumentationGenerationManifestFactory(),scope);
        var recipe=new ArtifactGenerationRecipe([new(DocumentationAssetPaths.Blueprint,"text/markdown"),new(DocumentationAssetPaths.Design,"text/markdown")],inputs=>
        {
            var projection=projector.Project(new(inputs[0],inputs[1]));
            return new(projection.Artifacts.Select(a=>a.RelativePath).ToArray(),_=>Task.FromResult(projection.Artifacts));
        });
        try{var result=await runtime.ExecuteAsync(generationId,recipe,cancellationToken);return new(result.GenerationId,result.Inputs,result.Outputs,result.Manifest);}
        catch(ArtifactGenerationException error){throw new DocumentationAssetException(error.Code,error.Message,error);}
    }
}
