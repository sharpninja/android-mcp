using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

public sealed record SourcesInventoryReadRequest(RehydrationSourceProjection Projection,StoredArtifact InventoryManifest);
public interface ISourcesInventoryReader
{
    // Stores/reader are bound by the host to explicit inventory and raw acquisition roots.
    // All returned artifact paths remain relative; no absolute source root is inferred from the inventory's location.
    // Admission uses IPinnedArtifactReader, then typed projection equality and the existing ResumeOnly verifier.
    Task<SourcesInventoryResult> ReadAsync(SourcesInventoryReadRequest request,CancellationToken cancellationToken=default);
}
