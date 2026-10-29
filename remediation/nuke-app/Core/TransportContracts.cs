namespace AndroidMcp.BuildTool.Core;

public sealed record ByteRange(long Offset, int Length);
public sealed record TransportRetryPolicy(int Attempts, TimeSpan MinimumDelay)
{
    public static TransportRetryPolicy CanonicalRestore { get; } = new(3, TimeSpan.FromSeconds(1));
    public static TransportRetryPolicy SingleAttempt { get; } = new(1, TimeSpan.Zero);
}
public enum ToolTransferKind { Artifact, PublishedChecksum }
public sealed record ToolTransferPin(string ArtifactSha256,long ExactArtifactBytes,long MaxDownloadBytes,
    ToolTransferKind Kind=ToolTransferKind.Artifact,IReadOnlyList<Uri>? AllowedRedirects=null);
public sealed record PinnedRequest(Uri Url, string Revision, string? ExpectedSha256 = null,
    long? ExpectedLength = null, long MaxBytes = 64 * 1024 * 1024,
    ByteRange? Range = null, string? IfMatch = null, string? ExpectedETag = null,
    string Method = "GET", string ContentEncoding = "identity", TransportRetryPolicy? Retry = null,
    long? ExpectedTotalLength = null,string? PartialMemberSha256 = null,ToolTransferPin? ToolTransfer=null);
public sealed record TransportResponse(Uri Url, int StatusCode, ReadOnlyMemory<byte> Bytes,
    string? ETag = null, string? ContentRange = null, string ContentEncoding = "identity",
    long? DeclaredLength = null,IReadOnlyList<Uri>? RequestChain=null);
public interface IByteTransport
{
    Task<TransportResponse> SendAsync(PinnedRequest request, CancellationToken cancellationToken = default);
}
public interface ITransportDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
public sealed class TransportException(string code, string message) : IOException(message)
{
    public string Code { get; } = code;
}
