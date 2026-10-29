using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Catalog;

public sealed record CatalogPipelineWorkflowRead(CatalogPipelineWorkflowFile File,CatalogPipelineWorkflowRequest Request);

/// <summary>One caller-pinned external Catalog workflow reader, also used by native fixture adapters.</summary>
public sealed class CatalogPipelineWorkflowReader(IPinnedArtifactReader requests,ISourceWorkflowInputsReader inputs,ICanonicalJson canonical)
{
    public async Task<CatalogPipelineWorkflowRead> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        var bytes=await requests.ReadAsync(callerPin,cancellationToken);
        CatalogPipelineWorkflowFile? file;
        try{file=JsonSerializer.Deserialize<CatalogPipelineWorkflowFile>(canonical.Serialize(bytes));}
        catch(Exception error)when(error is JsonException or CanonicalJsonException or NotSupportedException)
        {throw new CatalogBuildException("catalog-pipeline-request","Malformed or ambiguous native Catalog workflow",error);}
        if(file?.Sources is null || file.Stores is null || file.Metadata is null || file.InventoryManifest is null || file.SourceGenerationCommit is null ||
            string.IsNullOrWhiteSpace(file.CatalogRoot) || string.IsNullOrWhiteSpace(file.OutputRoot) || string.IsNullOrWhiteSpace(file.StageRoot))
            throw new CatalogBuildException("catalog-pipeline-request","Required Catalog workflow binding is absent");
        var recipe=await inputs.ReadAsync(file.Sources,cancellationToken);
        return new(file,new(recipe,file.InventoryManifest,file.SourceGenerationCommit,file.Metadata,file.OutputRoot,file.StageRoot,file.ProfileId,file.Java));
    }
}
