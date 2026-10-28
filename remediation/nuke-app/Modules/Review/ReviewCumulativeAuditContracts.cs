using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

// Proposed continuation extension, not a production implementation. The final provider call
// keeps its own one MCP turn, preserving separate call-report and cumulative-verdict sections.
public sealed record ReviewObservedCumulativeVerdict(ReviewObservedReviewerTurnLink Turn,
    IReadOnlyList<string> RelatedProviderCallIds,string FullVisibleVerdict,string FinalCallVisibleReport,
    StoredArtifact CallReportEntryProof);
public interface IReviewCumulativeVerdictEvidenceSource
{
    // Extract only the actual reviewer's scoped tool request/result from preserved visible captures.
    // Never create a turn as Codex or synthesize success from a supplied DTO/configuration value.
    Task<ReviewObservedCumulativeVerdict?> ObserveAsync(ReviewSnapshot snapshot,
        IReadOnlyList<StoredArtifact> providerReceipts,CancellationToken cancellationToken=default);
}
