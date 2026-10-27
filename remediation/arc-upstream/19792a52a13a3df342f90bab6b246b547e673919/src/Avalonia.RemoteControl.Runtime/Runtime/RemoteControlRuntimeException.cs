namespace Avalonia.RemoteControl.Server.Runtime;

/// <summary>
/// Represents a sanitized runtime failure that transports can map to their own status model.
/// </summary>
public sealed class RemoteControlRuntimeException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlRuntimeException"/> class.
    /// </summary>
    /// <param name="errorCode">Sanitized runtime error code.</param>
    /// <param name="message">Sanitized error message.</param>
    public RemoteControlRuntimeException(RemoteControlRuntimeErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the sanitized runtime error code.
    /// </summary>
    public RemoteControlRuntimeErrorCode ErrorCode { get; }
}
