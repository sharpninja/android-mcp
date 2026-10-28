using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Shared external workflow inputs. The file supplies pinned files, never an admitted projection or producer.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceWorkflowInputs(StoredArtifact Recipe,StoredArtifact RecipeLock,string? Release=null,
    int? SourceLimit=null,AcquisitionCompatibilityProfile AcquisitionProfile=AcquisitionCompatibilityProfile.CanonicalRestore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceWorkflowStoreBindings(string InventoryRoot,string AcquisitionRawRoot,string GenerationStoreRoot);
public interface ISourceWorkflowInputsReader
{
    // Two reads through the same project-root-bound IPinnedArtifactReader, then the existing typed request.
    // Recipe/lock parsing remains solely in IRehydrationPlanner.Plan.
    Task<RehydrationPlanRequest> ReadAsync(SourceWorkflowInputs inputs,CancellationToken cancellationToken=default);
}
