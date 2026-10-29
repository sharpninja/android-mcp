using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Catalog;

// Revised proposed EXTERNAL file: never accepts an admitted projection or an
// editable producer identity. Reuse Acquisition's authoritative shared types.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CatalogPipelineWorkflowFile(SourceWorkflowInputs Sources,SourceWorkflowStoreBindings Stores,
    StoredArtifact InventoryManifest,StoredArtifact SourceGenerationCommit,CatalogPipelineMetadata Metadata,
    string CatalogRoot,string OutputRoot,string StageRoot,string? ProfileId=null,SourceJavaAdmission? Java=null);

// INTERNAL boundary only. Recipe bytes have passed the shared caller-pinned
// InputsReader. The one IRehydrationPlanner.Plan still derives the projection.
public sealed record CatalogPipelineWorkflowRequest(RehydrationPlanRequest Recipe,StoredArtifact InventoryManifest,
    StoredArtifact SourceGenerationCommit,CatalogPipelineMetadata Metadata,string OutputRoot,string StageRoot,
    string? ProfileId=null,SourceJavaAdmission? Java=null);
public interface ICatalogPipelineWorkflowOperation
{
    Task<CatalogBuildResult> RunAsync(CatalogPipelineWorkflowRequest request,CancellationToken cancellationToken=default);
}
