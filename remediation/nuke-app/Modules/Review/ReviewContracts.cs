using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed class ReviewException(string code, string message) : InvalidOperationException(message)
{ public string Code { get; } = code; }
public sealed record ReviewEvidence(string Path, string Sha256, string Kind,
    string? Project = null, string? RunId = null, string? Host = null,
    string? Head = null, string? Command = null);
public sealed record ReviewSnapshot(string RunId, string ParentSessionId, string ParentRequestId,
    string Head, string PullRequestUrl, IReadOnlyList<string> UnitProjects,
    IReadOnlyList<string> IntegrationProjects, IReadOnlyList<string> RequirementIds,
    IReadOnlyList<ReviewEvidence> Evidence);
public sealed record ReviewReadiness(string Head, bool IsDraft, bool IsOpen,
    string PullRequestUrl, ReadOnlyMemory<byte> Receipt);
public interface IReviewReadinessSource
{ Task<ReviewReadiness> ObserveAsync(string pullRequestUrl, CancellationToken cancellationToken = default); }
public sealed record PreparedReview(ReviewSnapshot Snapshot, string PromptPath, string PromptSha256,
    string ManifestSha256, IReadOnlyList<StoredArtifact> Artifacts, ReviewPrerequisites? Prerequisites = null);
public sealed record ReviewCapability(ToolIdentity Tool, string Model, string Effort,
    string VerificationReceiptPath, string VerificationReceiptSha256);
public sealed record ReviewTerminal(bool KnownTerminal, int ExitCode, bool Cancelled = false);
public sealed record ReviewAssessment(string Status, string Report, string? Reason,
    IReadOnlyList<string> Models, IReadOnlyList<string> Efforts, bool ContainsHiddenReasoning,
    IReadOnlyList<string> VisibleLines);
public sealed record ReviewRunResult(string Status, string Report,
    IReadOnlyList<StoredArtifact> Receipts, IReadOnlyList<string> Coverage, bool Unified);
public sealed record ReviewRecoveryInput(bool Active, bool KnownTerminal, ReadOnlyMemory<byte> Response,
    int ExitCode, string Marker, IReadOnlyList<string> Coverage, string ExpectedHead, string ObservedHead);
public interface IReviewAudit
{
    Task<ReviewAuditObservation<ReviewAuditBootstrapEvidence>> VerifyReadyAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default);
    // The FULL visible verdict must already be authored by the actual reviewer in its own MCP turn.
    Task<ReviewAuditObservation<ReviewAuditCompletionEvidence>> RecordAsync(ReviewSnapshot snapshot,
        string fullVisibleVerdict, IReadOnlyList<StoredArtifact> receipts, CancellationToken cancellationToken = default);
}
