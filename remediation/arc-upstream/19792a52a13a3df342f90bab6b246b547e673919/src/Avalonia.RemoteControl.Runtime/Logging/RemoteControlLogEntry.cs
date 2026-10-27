using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Logging;

/// <summary>
/// Represents a sanitized log entry exposed through the remote-control stream.
/// </summary>
public sealed record RemoteControlLogEntry
{
    /// <summary>
    /// Gets the monotonic sequence assigned by the server log buffer.
    /// </summary>
    public ulong Sequence { get; init; }

    /// <summary>
    /// Gets the UTC timestamp captured when the entry was written.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the Microsoft.Extensions.Logging level.
    /// </summary>
    public LogLevel Level { get; init; }

    /// <summary>
    /// Gets the logger category name.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the numeric event ID.
    /// </summary>
    public int EventId { get; init; }

    /// <summary>
    /// Gets the sanitized formatted message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets sanitized structured logging state as key-value text.
    /// </summary>
    public string StructuredState { get; init; } = string.Empty;

    /// <summary>
    /// Gets sanitized active logging scopes as display text.
    /// </summary>
    public string ScopeSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sanitized exception type and message summary.
    /// </summary>
    public string ExceptionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the cumulative number of entries dropped from the bounded buffer before this entry.
    /// </summary>
    public ulong DroppedCount { get; init; }
}
