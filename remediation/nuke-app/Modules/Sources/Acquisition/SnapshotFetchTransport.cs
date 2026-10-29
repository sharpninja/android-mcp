using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Modules.Sources.Acquisition;

// Policy adapter only. HTTP status, exact same-URL429 backoff, body bounds,
// cancellation and integrity remain the existing shared transport's behavior.
public sealed class SnapshotFetchTransport(IByteTransport transport):IByteTransport
{
    public static TransportRetryPolicy Retry{get;}=new(3,TimeSpan.Zero);
    public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        =>transport.SendAsync(request with{Retry=Retry},cancellationToken);
}
