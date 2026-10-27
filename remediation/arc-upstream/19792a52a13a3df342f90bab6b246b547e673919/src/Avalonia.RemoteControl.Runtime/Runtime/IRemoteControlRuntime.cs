using Avalonia.RemoteControl.Server.Commands;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server.Logging;
using Avalonia.RemoteControl.Server.Rendering;
using Avalonia.RemoteControl.Server.Snapshots;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Runtime;

/// <summary>
/// Defines the host-independent remote-control operations shared by transports.
/// </summary>
public interface IRemoteControlRuntime
{
    /// <summary>
    /// Gets the capabilities currently supported by this runtime.
    /// </summary>
    /// <returns>The supported remote-control capabilities.</returns>
    RemoteControlCapabilities GetCapabilities();

    /// <summary>
    /// Captures the current control tree snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The captured tree snapshot.</returns>
    ValueTask<RemoteControlTreeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches current and future tree snapshots.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of tree snapshots.</returns>
    IAsyncEnumerable<RemoteControlTreeSnapshot> WatchSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches current and future live remote UI frames.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of live UI frames.</returns>
    IAsyncEnumerable<RemoteControlFrame> WatchFramesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a click on the selected node.
    /// </summary>
    /// <param name="nodeId">Target node ID.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    ValueTask<RemoteControlCommandResult> InvokeClickAsync(
        string nodeId,
        string clientIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests focus for the selected node.
    /// </summary>
    /// <param name="nodeId">Target node ID.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    ValueTask<RemoteControlCommandResult> InvokeFocusAsync(
        string nodeId,
        string clientIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an approved property on the selected node.
    /// </summary>
    /// <param name="nodeId">Target node ID.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="value">String value to convert and assign.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    ValueTask<RemoteControlCommandResult> SetPropertyAsync(
        string nodeId,
        string propertyName,
        string value,
        string clientIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends live remote input events to the remote root.
    /// </summary>
    /// <param name="events">Input events.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command result.</returns>
    ValueTask<RemoteControlCommandResult> SendInputAsync(
        IReadOnlyList<RemoteInputEvent> events,
        string clientIdentity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches retained and future log entries that match the requested filters.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level.</param>
    /// <param name="categoryPrefix">Optional category prefix.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of sanitized log entries.</returns>
    IAsyncEnumerable<RemoteControlLogEntry> WatchLogEntriesAsync(
        LogLevel minimumLevel,
        string? categoryPrefix,
        CancellationToken cancellationToken = default);
}
