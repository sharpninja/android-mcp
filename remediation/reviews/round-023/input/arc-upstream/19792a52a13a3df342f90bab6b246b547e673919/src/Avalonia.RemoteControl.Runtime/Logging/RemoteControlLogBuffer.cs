using System.Runtime.CompilerServices;
using Avalonia.RemoteControl.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Logging;

/// <summary>
/// Stores sanitized log entries in a bounded replay buffer for remote subscribers.
/// </summary>
public sealed class RemoteControlLogBuffer
{
    private readonly object syncRoot = new();
    private readonly List<RemoteControlLogEntry> entries = [];
    private readonly SemaphoreSlim signal = new(0);
    private readonly int capacity;
    private readonly TimeSpan keepAliveInterval;
    private ulong nextSequence;
    private ulong droppedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlLogBuffer"/> class.
    /// </summary>
    /// <param name="options">Remote-control options.</param>
    public RemoteControlLogBuffer(IOptions<AvaloniaRemoteControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        capacity = Math.Max(1, options.Value.LogBufferCapacity);
        keepAliveInterval = options.Value.LogStreamKeepAliveInterval;
    }

    /// <summary>
    /// Publishes a sanitized log entry to the buffer.
    /// </summary>
    /// <param name="entry">The log entry to publish.</param>
    public void Publish(RemoteControlLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (syncRoot)
        {
            if (entries.Count >= capacity)
            {
                entries.RemoveAt(0);
                droppedCount++;
            }

            entries.Add(entry with
            {
                Sequence = ++nextSequence,
                DroppedCount = droppedCount,
            });
        }

        signal.Release();
    }

    /// <summary>
    /// Reads retained and future entries that match the requested filter.
    /// </summary>
    /// <param name="minimumLevel">Minimum log level to return.</param>
    /// <param name="categoryPrefix">Optional logger category prefix.</param>
    /// <param name="cancellationToken">Token used to end the stream.</param>
    /// <returns>An asynchronous stream of sanitized log entries.</returns>
    public async IAsyncEnumerable<RemoteControlLogEntry> ReadAllAsync(
        LogLevel minimumLevel,
        string? categoryPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var nextToRead = GetInitialSequence();

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = GetBatch(minimumLevel, categoryPrefix, ref nextToRead);

            foreach (var entry in batch)
            {
                yield return entry;
            }

            if (batch.Count != 0)
            {
                continue;
            }

            if (signal.Wait(0))
            {
                continue;
            }

            var waitResult = await WaitForSignalAsync(cancellationToken).ConfigureAwait(false);
            if (waitResult == LogBufferWaitResult.Cancelled)
            {
                yield break;
            }

            if (waitResult == LogBufferWaitResult.TimedOut)
            {
                yield return CreateKeepAlive(minimumLevel);
            }
        }
    }

    private async Task<LogBufferWaitResult> WaitForSignalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (keepAliveInterval <= TimeSpan.Zero)
            {
                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                return LogBufferWaitResult.Signaled;
            }

            return await signal.WaitAsync(keepAliveInterval, cancellationToken).ConfigureAwait(false)
                ? LogBufferWaitResult.Signaled
                : LogBufferWaitResult.TimedOut;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LogBufferWaitResult.Cancelled;
        }
    }

    private ulong GetInitialSequence()
    {
        lock (syncRoot)
        {
            return entries.Count == 0 ? nextSequence + 1 : entries[0].Sequence;
        }
    }

    private List<RemoteControlLogEntry> GetBatch(
        LogLevel minimumLevel,
        string? categoryPrefix,
        ref ulong nextToRead)
    {
        lock (syncRoot)
        {
            if (entries.Count == 0)
            {
                return [];
            }

            if (nextToRead < entries[0].Sequence)
            {
                nextToRead = entries[0].Sequence;
            }

            var batch = new List<RemoteControlLogEntry>();

            foreach (var entry in entries)
            {
                if (entry.Sequence >= nextToRead && Matches(entry, minimumLevel, categoryPrefix))
                {
                    batch.Add(entry);
                }
            }

            nextToRead = entries[^1].Sequence + 1;
            return batch;
        }
    }

    private static bool Matches(
        RemoteControlLogEntry entry,
        LogLevel minimumLevel,
        string? categoryPrefix)
    {
        if (entry.Level < minimumLevel)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(categoryPrefix)
            || entry.Category.StartsWith(categoryPrefix, StringComparison.Ordinal);
    }

    private static RemoteControlLogEntry CreateKeepAlive(LogLevel minimumLevel)
    {
        return new RemoteControlLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = minimumLevel == LogLevel.None ? LogLevel.Trace : minimumLevel,
            Category = RemoteControlProtocol.LogStreamKeepAliveCategory,
            Message = "Log stream keepalive.",
        };
    }

    private enum LogBufferWaitResult
    {
        Signaled,
        TimedOut,
        Cancelled,
    }
}
