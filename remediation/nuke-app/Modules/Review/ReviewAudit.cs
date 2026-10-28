using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewAuditBootstrapEvidence(string Head, string ReviewerAgent,
    string RequestedModel, string RequestedEffort, string PluginName, string PluginVersion,
    string PluginManifestSha256, string PrivateMcpConfigPath, string PrivateMcpConfigSha256,
    IReadOnlyList<string> AllowedMcpTools, bool ScopedSessionLogWrites,
    bool ProjectFilesReadOnly, bool ArbitraryShellOrNetworkTools);
public sealed record ReviewAuditCompletionEvidence(string Head, string RunId, string ReviewerAgent,
    string ActualModel, string ActualEffort, string SessionId, string RequestId,
    string ParentSessionId, string ParentRequestId, string FullVisibleVerdict,
    IReadOnlyList<string> RelatedCallIds, bool Persisted);
public sealed record ReviewAuditObservation<T>(T Evidence, StoredArtifact Proof);

/// <summary>Implemented only through the root-owned typed official-plugin purpose invoker.
/// It observes capability and reads the actual reviewer's own turn; it never impersonates that reviewer.</summary>
public interface IReviewAuditTransport
{
    Task<ReviewAuditBootstrapEvidence> ObserveBootstrapAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<ReviewAuditCompletionEvidence> ReadReviewerOwnTurnAsync(ReviewSnapshot snapshot, string expectedFullVerdict,
        IReadOnlyList<StoredArtifact> receipts, CancellationToken cancellationToken = default);
}

public sealed class RequiredPluginReviewAudit(IReviewAuditTransport transport, IArtifactStore store, ICanonicalJson canonical) : IReviewAudit
{
    public async Task<ReviewAuditObservation<ReviewAuditBootstrapEvidence>> VerifyReadyAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var evidence = await transport.ObserveBootstrapAsync(snapshot, cancellationToken);
        ReviewAuditPolicy.VerifyBootstrap(snapshot, evidence);
        var bytes = canonical.Serialize(ReviewPrepareService.Json(evidence));
        var proof = await store.WriteAsync(new($"reviews/{snapshot.RunId}/audit/bootstrap-{canonical.Hash(bytes)}.json",
            bytes, "application/json"), cancellationToken);
        return new(evidence, proof);
    }
    public async Task<ReviewAuditObservation<ReviewAuditCompletionEvidence>> RecordAsync(ReviewSnapshot snapshot, string fullVisibleVerdict,
        IReadOnlyList<StoredArtifact> receipts, CancellationToken cancellationToken = default)
    {
        // The actual reviewer authors its turn. This adapter only verifies its persisted read-back.
        var evidence = await transport.ReadReviewerOwnTurnAsync(snapshot, fullVisibleVerdict, receipts, cancellationToken);
        var bytes = canonical.Serialize(ReviewPrepareService.Json(evidence));
        var proof = await store.WriteAsync(new($"reviews/{snapshot.RunId}/audit/read-back-{canonical.Hash(bytes)}.json",
            bytes, "application/json"), cancellationToken);
        return new(evidence, proof);
    }
}

public sealed class UnavailableReviewAudit : IReviewAudit
{
    private static ReviewException Missing() => new("REVIEWER_AUDIT_UNAVAILABLE", "Official reviewer-owned MCP transport is not configured. Provider execution remains disabled.");
    public Task<ReviewAuditObservation<ReviewAuditBootstrapEvidence>> VerifyReadyAsync(ReviewSnapshot snapshot, CancellationToken cancellationToken = default) => throw Missing();
    public Task<ReviewAuditObservation<ReviewAuditCompletionEvidence>> RecordAsync(ReviewSnapshot snapshot, string fullVisibleVerdict,
        IReadOnlyList<StoredArtifact> receipts, CancellationToken cancellationToken = default) => throw Missing();
}

public static class ReviewAuditPolicy
{
    public const string ReviewerAgent = "Claude";
    public const string PluginName = "mcpserver-claude-code-plugin";
    public static void VerifyBootstrap(ReviewSnapshot snapshot, ReviewAuditBootstrapEvidence proof)
    {
        ReviewPrepareService.Require(proof.Head == snapshot.Head, "HEAD_MISMATCH", "Audit bootstrap head differs.");
        ReviewPrepareService.Require(proof.ReviewerAgent == ReviewerAgent && proof.RequestedModel == ReviewLaunchService.Model &&
            proof.RequestedEffort == ReviewLaunchService.Effort && proof.PluginName == PluginName && !string.IsNullOrWhiteSpace(proof.PluginVersion) &&
            Regex.IsMatch(proof.PluginManifestSha256, "^[0-9a-fA-F]{64}$") && Path.IsPathRooted(proof.PrivateMcpConfigPath) &&
            Regex.IsMatch(proof.PrivateMcpConfigSha256, "^[0-9a-fA-F]{64}$") && proof.ScopedSessionLogWrites && proof.ProjectFilesReadOnly &&
            !proof.ArbitraryShellOrNetworkTools && proof.AllowedMcpTools.Count > 0 &&
            proof.AllowedMcpTools.All(x => Regex.IsMatch(x, "^mcp__[A-Za-z0-9_-]+__sessionlog_[A-Za-z0-9_]+$")),
            "REVIEWER_AUDIT_UNAVAILABLE", "Installed official plugin identity, private configuration pin or scoped tool policy is unverified.");
    }
    public static void VerifyCompletion(ReviewSnapshot snapshot, string report, IReadOnlyList<StoredArtifact> receipts, ReviewAuditCompletionEvidence proof)
    {
        var related = CallIds(receipts);
        ReviewPrepareService.Require(proof.Persisted && proof.Head == snapshot.Head && proof.RunId == snapshot.RunId &&
            proof.ReviewerAgent == ReviewerAgent && proof.ActualModel == ReviewLaunchService.Model && proof.ActualEffort == ReviewLaunchService.Effort &&
            Regex.IsMatch(proof.SessionId, "^Claude-[0-9]{8}T[0-9]{6}Z-[a-z0-9-]+$") && Regex.IsMatch(proof.RequestId, "^req-[0-9]{8}T[0-9]{6}Z-[a-z0-9-]+$") &&
            proof.ParentSessionId == snapshot.ParentSessionId && proof.ParentRequestId == snapshot.ParentRequestId && proof.FullVisibleVerdict == report &&
            proof.RelatedCallIds.Distinct(StringComparer.Ordinal).Count() == related.Count && proof.RelatedCallIds.ToHashSet(StringComparer.Ordinal).SetEquals(related),
            "REVIEWER_AUDIT_UNAVAILABLE", "Actual reviewer-own MCP turn/read-back, identity, full verdict or call coverage is unverified.");
    }
    public static IReadOnlyList<string> CallIds(IReadOnlyList<StoredArtifact> receipts) => receipts
        .Select(x => Regex.Match(x.RelativePath, @"/calls/([0-9]{8})/response\.visible\.jsonl$"))
        .Where(x => x.Success).Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
