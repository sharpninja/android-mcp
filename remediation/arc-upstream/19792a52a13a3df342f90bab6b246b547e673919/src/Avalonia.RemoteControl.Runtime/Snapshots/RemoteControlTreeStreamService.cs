using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Produces live pushed tree snapshots for connected clients.
/// </summary>
public sealed class RemoteControlTreeStreamService
{
    private readonly IControlTreeSnapshotProvider snapshotProvider;
    private readonly IRemoteControlRootProvider rootProvider;
    private readonly AvaloniaRemoteControlOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlTreeStreamService"/> class.
    /// </summary>
    /// <param name="snapshotProvider">The snapshot provider.</param>
    /// <param name="rootProvider">The root control provider.</param>
    /// <param name="options">Remote-control options.</param>
    public RemoteControlTreeStreamService(
        IControlTreeSnapshotProvider snapshotProvider,
        IRemoteControlRootProvider rootProvider,
        IOptions<AvaloniaRemoteControlOptions> options)
    {
        this.snapshotProvider = snapshotProvider;
        this.rootProvider = rootProvider;
        this.options = options.Value;
    }

    /// <summary>
    /// Watches the current root control and emits periodic snapshots until canceled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the stream.</param>
    /// <returns>A stream of tree snapshots.</returns>
    public async IAsyncEnumerable<RemoteControlTreeSnapshot> WatchSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = rootProvider.GetRootControl()
            ?? throw new InvalidOperationException("No Avalonia remote-control root control is registered.");

        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await snapshotProvider.CaptureSnapshotAsync(root);

            try
            {
                await Task.Delay(options.TreeStreamInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
        }
    }
}
