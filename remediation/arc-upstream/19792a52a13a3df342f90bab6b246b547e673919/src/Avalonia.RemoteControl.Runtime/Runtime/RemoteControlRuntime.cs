using System.Runtime.CompilerServices;
using Avalonia.RemoteControl.Server.Commands;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server.Input;
using Avalonia.RemoteControl.Server.Logging;
using Avalonia.RemoteControl.Server.Rendering;
using Avalonia.RemoteControl.Server.Snapshots;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Runtime;

/// <summary>
/// Implements transport-independent remote-control operations.
/// </summary>
public sealed class RemoteControlRuntime : IRemoteControlRuntime
{
    private readonly AvaloniaRemoteControlService remoteControlService;
    private readonly IControlTreeSnapshotProvider snapshotProvider;
    private readonly IRemoteControlRootProvider rootProvider;
    private readonly RemoteControlTreeStreamService treeStreamService;
    private readonly RemoteControlLogStreamService logStreamService;
    private readonly RemoteControlFrameStreamService frameStreamService;
    private readonly RemoteControlActionInvoker actionInvoker;
    private readonly RemoteControlPropertyMutationService propertyMutationService;
    private readonly RemoteControlInputDispatcher inputDispatcher;
    private readonly ILogger<RemoteControlRuntime>? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlRuntime"/> class.
    /// </summary>
    /// <param name="remoteControlService">The remote-control application service.</param>
    /// <param name="snapshotProvider">The read-only tree snapshot provider.</param>
    /// <param name="rootProvider">The host app root provider.</param>
    /// <param name="treeStreamService">The live tree stream service.</param>
    /// <param name="logStreamService">The live log stream service.</param>
    /// <param name="frameStreamService">The live frame stream service.</param>
    /// <param name="actionInvoker">The remote action invoker.</param>
    /// <param name="propertyMutationService">The remote property mutation service.</param>
    /// <param name="inputDispatcher">The live remote input dispatcher.</param>
    /// <param name="logger">Optional protocol event logger.</param>
    public RemoteControlRuntime(
        AvaloniaRemoteControlService remoteControlService,
        IControlTreeSnapshotProvider snapshotProvider,
        IRemoteControlRootProvider rootProvider,
        RemoteControlTreeStreamService treeStreamService,
        RemoteControlLogStreamService logStreamService,
        RemoteControlFrameStreamService frameStreamService,
        RemoteControlActionInvoker actionInvoker,
        RemoteControlPropertyMutationService propertyMutationService,
        RemoteControlInputDispatcher inputDispatcher,
        ILogger<RemoteControlRuntime>? logger = null)
    {
        this.remoteControlService = remoteControlService;
        this.snapshotProvider = snapshotProvider;
        this.rootProvider = rootProvider;
        this.treeStreamService = treeStreamService;
        this.logStreamService = logStreamService;
        this.frameStreamService = frameStreamService;
        this.actionInvoker = actionInvoker;
        this.propertyMutationService = propertyMutationService;
        this.inputDispatcher = inputDispatcher;
        this.logger = logger;
    }

    /// <inheritdoc />
    public RemoteControlCapabilities GetCapabilities()
    {
        LogReceived("GetCapabilities");
        var capabilities = remoteControlService.GetCapabilities();
        logger?.LogDebug(
            "Remote-control event sent to client: {EventName}; protocol {ProtocolVersion}; tree snapshots {SupportsTreeSnapshots}; tree streaming {SupportsTreeStreaming}; logs {SupportsLogStreaming}; frames {SupportsFrameStreaming}; input {SupportsRemoteInput}.",
            "GetCapabilitiesResponse",
            capabilities.ProtocolVersion,
            capabilities.SupportsTreeSnapshots,
            capabilities.SupportsTreeStreaming,
            capabilities.SupportsLogStreaming,
            capabilities.SupportsFrameStreaming,
            capabilities.SupportsRemoteInput);
        return capabilities;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteControlTreeSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        LogReceived("GetSnapshot");
        cancellationToken.ThrowIfCancellationRequested();

        var root = rootProvider.GetRootControl();

        if (root is null)
        {
            throw new RemoteControlRuntimeException(
                RemoteControlRuntimeErrorCode.FailedPrecondition,
                "No Avalonia remote-control root control is registered.");
        }

        var snapshot = await snapshotProvider.CaptureSnapshotAsync(root).ConfigureAwait(false);
        LogTreeSnapshotSent("TreeSnapshot", snapshot);
        return snapshot;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteControlTreeSnapshot> WatchSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        LogReceived("WatchTree");
        return WatchSnapshotsWithDebugLoggingAsync(cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteControlFrame> WatchFramesAsync(
        CancellationToken cancellationToken = default)
    {
        LogReceived("WatchFrames");
        return WatchFramesWithDebugLoggingAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<RemoteControlCommandResult> InvokeClickAsync(
        string nodeId,
        string clientIdentity,
        CancellationToken cancellationToken = default)
    {
        LogReceived("InvokeClick");
        cancellationToken.ThrowIfCancellationRequested();
        var result = await actionInvoker.InvokeClickAsync(nodeId, clientIdentity).ConfigureAwait(false);
        LogCommandResultSent("InvokeClickResult", result);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteControlCommandResult> InvokeFocusAsync(
        string nodeId,
        string clientIdentity,
        CancellationToken cancellationToken = default)
    {
        LogReceived("InvokeFocus");
        cancellationToken.ThrowIfCancellationRequested();
        var result = await actionInvoker.InvokeFocusAsync(nodeId, clientIdentity).ConfigureAwait(false);
        LogCommandResultSent("InvokeFocusResult", result);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteControlCommandResult> SetPropertyAsync(
        string nodeId,
        string propertyName,
        string value,
        string clientIdentity,
        CancellationToken cancellationToken = default)
    {
        LogReceived("SetProperty");
        cancellationToken.ThrowIfCancellationRequested();
        var result = await propertyMutationService.SetPropertyAsync(nodeId, propertyName, value, clientIdentity)
            .ConfigureAwait(false);
        LogCommandResultSent("SetPropertyResult", result);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<RemoteControlCommandResult> SendInputAsync(
        IReadOnlyList<RemoteInputEvent> events,
        string clientIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        logger?.LogDebug(
            "Remote-control event received from client: {EventName}; event count {EventCount}.",
            "SendInput",
            events.Count);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await inputDispatcher.SendInputAsync(events, clientIdentity).ConfigureAwait(false);
        LogCommandResultSent("SendInputResult", result);
        return result;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteControlLogEntry> WatchLogEntriesAsync(
        LogLevel minimumLevel,
        string? categoryPrefix,
        CancellationToken cancellationToken = default)
    {
        logger?.LogDebug(
            "Remote-control event received from client: {EventName}; minimum level {MinimumLevel}; category prefix configured {HasCategoryPrefix}.",
            "WatchLogs",
            minimumLevel,
            !string.IsNullOrWhiteSpace(categoryPrefix));
        logger?.LogDebug(
            "Remote-control log stream opened for client. Individual log entries are not re-logged to avoid recursive log generation.");
        return WatchLogEntriesWithDebugLoggingAsync(minimumLevel, categoryPrefix, cancellationToken);
    }

    private async IAsyncEnumerable<RemoteControlTreeSnapshot> WatchSnapshotsWithDebugLoggingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ulong count = 0;

        try
        {
            await foreach (var snapshot in treeStreamService.WatchSnapshotsAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                LogTreeSnapshotSent("WatchTreeUpdate", snapshot);
                yield return snapshot;
            }
        }
        finally
        {
            LogStreamCompleted("WatchTree", count);
        }
    }

    private async IAsyncEnumerable<RemoteControlFrame> WatchFramesWithDebugLoggingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ulong count = 0;

        try
        {
            await foreach (var frame in frameStreamService.WatchFramesAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                logger?.LogDebug(
                    "Remote-control event sent to client: {EventName}; sequence {Sequence}; pixels {PixelWidth}x{PixelHeight}; bytes {ByteCount}.",
                    "WatchFramesUpdate",
                    frame.Sequence,
                    frame.PixelWidth,
                    frame.PixelHeight,
                    frame.Png.Length);
                yield return frame;
            }
        }
        finally
        {
            LogStreamCompleted("WatchFrames", count);
        }
    }

    private async IAsyncEnumerable<RemoteControlLogEntry> WatchLogEntriesWithDebugLoggingAsync(
        LogLevel minimumLevel,
        string? categoryPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ulong count = 0;

        try
        {
            await foreach (var entry in logStreamService
                .WatchEntriesAsync(minimumLevel, categoryPrefix, cancellationToken)
                .ConfigureAwait(false))
            {
                count++;
                yield return entry;
            }
        }
        finally
        {
            logger?.LogDebug(
                "Remote-control log stream completed for client after {EventCount} entries. Individual log entries were not re-logged.",
                count);
        }
    }

    private void LogReceived(string eventName)
    {
        logger?.LogDebug(
            "Remote-control event received from client: {EventName}.",
            eventName);
    }

    private void LogTreeSnapshotSent(string eventName, RemoteControlTreeSnapshot snapshot)
    {
        logger?.LogDebug(
            "Remote-control event sent to client: {EventName}; sequence {Sequence}; nodes {NodeCount}.",
            eventName,
            snapshot.Sequence,
            snapshot.Nodes.Count);
    }

    private void LogCommandResultSent(string eventName, RemoteControlCommandResult result)
    {
        logger?.LogDebug(
            "Remote-control event sent to client: {EventName}; succeeded {Succeeded}.",
            eventName,
            result.Succeeded);
    }

    private void LogStreamCompleted(string eventName, ulong count)
    {
        logger?.LogDebug(
            "Remote-control event sent to client: {EventName} stream completed after {EventCount} events.",
            eventName,
            count);
    }
}
