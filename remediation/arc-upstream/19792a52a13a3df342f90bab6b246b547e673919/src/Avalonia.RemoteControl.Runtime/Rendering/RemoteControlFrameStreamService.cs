using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Rendering;

/// <summary>
/// Produces live remote UI frames for connected clients.
/// </summary>
public sealed class RemoteControlFrameStreamService
{
    private readonly IRemoteControlRootProvider rootProvider;
    private readonly IRemoteControlFrameProvider frameProvider;
    private readonly AvaloniaRemoteControlOptions options;
    private readonly ILogger<RemoteControlFrameStreamService> logger;
    private ulong nextSequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlFrameStreamService"/> class.
    /// </summary>
    /// <param name="rootProvider">Remote-control root provider.</param>
    /// <param name="frameProvider">Frame provider.</param>
    /// <param name="options">Remote-control options.</param>
    /// <param name="logger">Audit logger.</param>
    public RemoteControlFrameStreamService(
        IRemoteControlRootProvider rootProvider,
        IRemoteControlFrameProvider frameProvider,
        IOptions<AvaloniaRemoteControlOptions> options,
        ILogger<RemoteControlFrameStreamService> logger)
    {
        this.rootProvider = rootProvider;
        this.frameProvider = frameProvider;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Watches the current remote-control root and emits frames until canceled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Frame stream.</returns>
    public async IAsyncEnumerable<RemoteControlFrame> WatchFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!options.AllowRemoteFrames)
        {
            logger.LogWarning("Remote frame stream rejected because live frames are disabled.");
            throw new Runtime.RemoteControlRuntimeException(
                Runtime.RemoteControlRuntimeErrorCode.FailedPrecondition,
                "Live frame streaming is disabled by policy.");
        }

        var root = rootProvider.GetRootControl()
            ?? throw new Runtime.RemoteControlRuntimeException(
                Runtime.RemoteControlRuntimeErrorCode.FailedPrecondition,
                "No Avalonia remote-control root control is registered.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var sequence = Interlocked.Increment(ref nextSequence);
            yield return await frameProvider.CaptureFrameAsync(root, sequence, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await Task.Delay(options.FrameStreamInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
        }
    }
}
