using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Publication supplies the validated projection. This boundary does not parse a recipe.
// Historical cache import is explicit and preserves its original producer identity.
public enum SourceBatchCachePolicy { PreserveAliases, ImportLockedHistorical }
public sealed record SourceBatchRequest(string GenerationId, string ReceiptPath,
    RehydrationSourceProjection Projection,
    SourceBatchCachePolicy CachePolicy = SourceBatchCachePolicy.PreserveAliases);
public sealed record SourceBatchSource(int PhysicalSourceIndex, AcquiredSource Source);
public sealed record SourceBatchHistoricalCache(int CacheAliasIndex, HistoricalCachePin Pin, StoredArtifact Artifact);
public sealed record SourceBatchReceipt(string GenerationId, string RequestSha256,
    RehydrationSourceProjection Projection, SourceBatchCachePolicy CachePolicy,
    int TotalRows, int SelectedRows, int ProcessedRows, int TotalPhysicalSources,
    int SelectedPhysicalSources, bool Complete, bool HistoricalByteIdentityClaimed,
    IReadOnlyList<SourceBatchSource> Sources,
    IReadOnlyList<SourceBatchHistoricalCache> HistoricalCaches,
    IReadOnlyList<StoredArtifact> Outputs);
public sealed record SourceBatchResult(SourceBatchReceipt Receipt, StoredArtifact Manifest, bool Reused);
public interface ISourceBatch
{
    Task<SourceBatchResult> RunAsync(SourceBatchRequest request, CancellationToken cancellationToken = default);
}
