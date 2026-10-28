using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Seed metadata is additive and belongs only to this new request's canonical identity.
// Existing Plan, SourcePin and SourceBatchRequest serialization remains unchanged.
public sealed record SourceBatchSeedRequest(SourceBatchRequest Acquisition,RehydrationSeedProjection SeedProjection);
public enum SourceSeedStatus { Imported,Reused,Absent }
public sealed record SourceSeedCounts(int Imported,int Reused,int Absent);
public sealed record SourceSeedEntry(int PhysicalSourceIndex,SourceSeedStatus Status,StoredArtifact? Artifact,string? BundleCandidate);
public sealed record SourceSeedReceipt(string RequestSha256,SourceBatchRequest Acquisition,IReadOnlyList<SourceSeedEntry> Entries,
    SourceSeedCounts OriginalCounts,IReadOnlyList<StoredArtifact> Outputs,bool RawComplete,bool HistoricalByteIdentityClaimed,
    RehydrationSeedProjection SeedProjection);
public sealed record SourceSeedResult(SourceSeedReceipt Receipt,StoredArtifact Manifest,SourceSeedCounts CurrentCounts,bool Reused);
public interface ISourceBatchSeeder
{
    // Pins, exact optional snapshot and candidate order come from the winning row's typed SeedProjection.
    // There is no fallback to the base projection's first pin or to OriginalRow JSON.
    Task<SourceSeedResult> SeedOnlyAsync(SourceBatchSeedRequest request,CancellationToken cancellationToken=default);
}
