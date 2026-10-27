namespace Avalonia.RemoteControl.Server.Runtime;

/// <summary>
/// Identifies sanitized runtime failures before transport-specific mapping.
/// </summary>
public enum RemoteControlRuntimeErrorCode
{
    /// <summary>
    /// The requested operation cannot run in the current runtime state.
    /// </summary>
    FailedPrecondition,

    /// <summary>
    /// The request contains invalid caller input.
    /// </summary>
    InvalidArgument,

    /// <summary>
    /// The requested operation is not supported by this runtime.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The requested operation was cancelled.
    /// </summary>
    Cancelled,
}
