using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Logging;

/// <summary>
/// Provides filtered access to the remote-control log stream.
/// </summary>
public sealed class RemoteControlLogStreamService
{
    private readonly RemoteControlLogBuffer buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlLogStreamService"/> class.
    /// </summary>
    /// <param name="buffer">The remote-control log buffer.</param>
    public RemoteControlLogStreamService(RemoteControlLogBuffer buffer)
    {
        this.buffer = buffer;
    }

    /// <summary>
    /// Watches retained and future log entries that match the requested filters.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level to return.</param>
    /// <param name="categoryPrefix">Optional logger category prefix.</param>
    /// <param name="cancellationToken">Token used to end the stream.</param>
    /// <returns>An asynchronous stream of sanitized log entries.</returns>
    public IAsyncEnumerable<RemoteControlLogEntry> WatchEntriesAsync(
        LogLevel minimumLevel,
        string? categoryPrefix,
        CancellationToken cancellationToken = default)
    {
        return buffer.ReadAllAsync(minimumLevel, categoryPrefix, cancellationToken);
    }
}
