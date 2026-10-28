namespace AndroidMcp.BuildTool.Core;

// Shared seams. Implementations must preserve argv boundaries and complete receipts.
public sealed record ToolIdentity(string ExecutablePath, string Sha256, string Version);
public sealed record ProcessRequest(ToolIdentity Tool, IReadOnlyList<string> Arguments,
    string WorkingDirectory, IReadOnlyDictionary<string,string> PublicEnvironment,
    string ReceiptDirectory, string? StandardInputPath = null);
public sealed record ProcessResult(int ExitCode, string StandardOutputPath,
    string StandardErrorPath, DateTimeOffset StartedAt, DateTimeOffset EndedAt,
    bool Cancelled);
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
public interface IProcessSessionFactory
{
    Task<IProcessSession> StartAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
public interface IProcessSession : IAsyncDisposable
{
    int ProcessId { get; }
    string StandardOutputPath { get; }
    string StandardErrorPath { get; }
    Task<ProcessResult> Completion { get; }
    // Explicit owner cancellation/cleanup, never an elapsed-time watchdog.
    Task StopAsync(CancellationToken cancellationToken = default);
}
public interface IProcessCaptureReader
{
    Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result, bool standardError,
        CancellationToken cancellationToken = default);
}
public sealed record StoredArtifact(string RelativePath, string Sha256, long Length, string MediaType);
public interface IArtifactStore
{
    // Immutable write-new-or-identical. Conflicting existing bytes are retained.
    // A partial failed write is uncommitted and cannot become a success receipt.
    Task<StoredArtifact> WriteAsync(Artifact artifact, CancellationToken cancellationToken = default);
    Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
}
public interface IArtifactLeaseProvider
{
    ValueTask<IAsyncDisposable> AcquireAsync(string scope, CancellationToken cancellationToken = default);
}
public interface IClock { DateTimeOffset Now { get; } }
public sealed record OperationReceipt(string RunId, OperationKey Operation,
    DateTimeOffset StartedAt, DateTimeOffset EndedAt, string Status,
    IReadOnlyList<StoredArtifact> Inputs, IReadOnlyList<StoredArtifact> Outputs,
    IReadOnlyList<string> Issues);
