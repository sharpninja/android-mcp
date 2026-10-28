using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Seed is local-only intent within the existing batch engine; acquisition request serialization stays unchanged.
public sealed record SourceBatchSeedRequest(SourceBatchRequest Acquisition);
public enum SourceSeedStatus { Imported,Reused,Absent }
public sealed record SourceSeedCounts(int Imported,int Reused,int Absent);
public sealed record SourceSeedEntry(int PhysicalSourceIndex,SourceSeedStatus Status,StoredArtifact? Artifact,string? BundleCandidate);
public sealed record SourceSeedReceipt(string RequestSha256,SourceBatchRequest Acquisition,IReadOnlyList<SourceSeedEntry> Entries,
    SourceSeedCounts OriginalCounts,IReadOnlyList<StoredArtifact> Outputs,bool RawComplete,bool HistoricalByteIdentityClaimed);
// Current counts describe this invocation; replay never rewrites the sealed original receipt/counts.
public sealed record SourceSeedResult(SourceSeedReceipt Receipt,StoredArtifact Manifest,SourceSeedCounts CurrentCounts,bool Reused);
public interface ISourceBatchSeeder
{
    // Existing target, then bundle LocalPath, then declared RequiredLocalSnapshot. First existing candidate must verify.
    // Missing candidates are Absent, never HTTP fallback. Only raw target bytes and the last receipt are published.
    Task<SourceSeedResult> SeedOnlyAsync(SourceBatchSeedRequest request,CancellationToken cancellationToken=default);
}
