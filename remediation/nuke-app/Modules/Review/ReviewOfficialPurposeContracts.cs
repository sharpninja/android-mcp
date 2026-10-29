using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Review;

// Proposed seam only. No implementation, executable dispatch, client creation or network access.
// Root owns its eventual approved runtime binding and shared policy.
public sealed record ReviewOfficialInstallationRequest(string WorkspacePath, string Head,
    string RunId, string ParentSessionId, string ParentRequestId);
public sealed record ReviewOfficialInstallationObservation(string WorkspacePath, string Head,
    string PackageIdentity, string ManifestName, string Version, StoredArtifact ManifestProof,
    StoredArtifact RuntimeIdentityProof, StoredArtifact ScopedConfigurationProof,
    StoredArtifact ToolDiscoveryProof, StoredArtifact NoQueuedMutationProof,
    string PrivateMcpConfigurationPath, string PrivateMcpConfigurationSha256,
    IReadOnlyList<string> ReviewerAllowedTools, string ReviewerSessionSource,
    string RequestedModel, string RequestedEffort);

// The observer remains Codex. ReviewerSessionSource is an exact query filter, not caller identity.
// The page vocabulary deliberately matches the installed QueryAsync surface, not newer GetAsync.
public sealed record ReviewOfficialTurnPageRequest(string WorkspacePath, string RunId,
    string ReviewerSessionSource, int Offset, int Limit);
public sealed record ReviewOfficialTurnPage(int Offset, int Limit, int TotalCount, int ReturnedSessionCount,
    IReadOnlyList<ReviewOfficialPersistedTurn> Turns, StoredArtifact ReadbackProof);
public sealed record ReviewOfficialPersistedTurn(string WorkspacePath, string ReviewerSessionSource,
    string SessionId, string RequestId, string Status, string RunId, string Head,
    string ParentSessionId, string ParentRequestId, string ProviderNativeSessionId,
    string ProviderCallId, string FullVisibleVerdict, IReadOnlyList<StoredArtifact> Evidence);

public interface IReviewOfficialPurposeInvoker
{
    Task<ReviewOfficialInstallationObservation> ObserveInstallationAsync(
        ReviewOfficialInstallationRequest request, CancellationToken cancellationToken = default);
    Task<ReviewOfficialTurnPage> ReadReviewerTurnsPageAsync(
        ReviewOfficialTurnPageRequest request, CancellationToken cancellationToken = default);
}

// Selected by trusted parser bindings for a pinned CLI schema, never populated from argv or prose.
public sealed record ReviewObservedProviderRuntime(string ProviderCallId, string ProviderNativeSessionId,
    string ActualModel, string ActualEffort, string FullVisibleReport, StoredArtifact VisibleStreamProof);
public sealed record ReviewObservedReviewerTurnLink(string ProviderCallId, string ReviewerSessionSource,
    string SessionId, string RequestId, StoredArtifact ToolResultProof);
public sealed record ReviewProviderRuntimeObservation(IReadOnlyList<ReviewObservedProviderRuntime> Runtime,
    IReadOnlyList<ReviewObservedReviewerTurnLink> ReviewerTurns);
public interface IReviewProviderRuntimeEvidenceSource
{
    Task<ReviewProviderRuntimeObservation> ObserveAsync(ReviewSnapshot snapshot,
        IReadOnlyList<StoredArtifact> providerReceipts, CancellationToken cancellationToken = default);
}
