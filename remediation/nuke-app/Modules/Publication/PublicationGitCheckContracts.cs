using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

public sealed record PublicationGitCheckRequest(
    string CheckoutRoot, ToolIdentity Git, string ReceiptDirectory,
    string PolicyRelativePath="deliverables.json");

// Read-only checkout observations. Resolve/admit paths through ArtifactPaths.
// This interface has no write, stage, commit, push or acquisition operation.
public interface IPublicationWorkspaceReader
{
    Task<ReadOnlyMemory<byte>> ReadPolicyAsync(string checkoutRoot,string relativePath,
        CancellationToken cancellationToken=default);
    Task<IReadOnlyList<PublicationFile>> ObserveAsync(string checkoutRoot,
        IReadOnlyList<string> trackedPaths,CancellationToken cancellationToken=default);
}

public sealed record PublicationGitCheckResult(
    PublicationCheckResult Check, IReadOnlyList<PublicationFile> Files,
    string PolicySha256, string TrackedPathsSha256, ProcessResult GitProcess);

public interface IPublicationGitCheck
{
    Task<PublicationGitCheckResult> CheckAsync(PublicationGitCheckRequest request,
        CancellationToken cancellationToken=default);
}

// Root owns CLI status/exit rendering. Policy violations remain the existing
// checker's issues; admission/IO/process/capture failures are explicit errors.
// Never include raw stderr or credential values in this exception.
public sealed class PublicationGitCheckException(
    string code,string message,ProcessResult? gitProcess=null,PublicationCheckResult? policyCheck=null)
    :Exception(message)
{
    public string Code { get; }=code;
    public ProcessResult? GitProcess { get; }=gitProcess;
    public PublicationCheckResult? PolicyCheck { get; }=policyCheck;
}
