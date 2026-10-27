using Avalonia.RemoteControl.Protocol;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Describes the remote-control features supported by the current server instance.
/// </summary>
public sealed record RemoteControlCapabilities
{
    /// <summary>
    /// Gets the protocol version implemented by the server.
    /// </summary>
    public string ProtocolVersion { get; init; } = RemoteControlProtocol.DisplayVersion;

    /// <summary>
    /// Gets the sanitized authenticated client identity used in remote audit logs.
    /// </summary>
    public string AuthenticatedClientIdentity { get; init; } = "unknown";

    /// <summary>
    /// Gets a value indicating whether read-only tree snapshots are supported.
    /// </summary>
    public bool SupportsTreeSnapshots { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether tree streaming is supported.
    /// </summary>
    public bool SupportsTreeStreaming { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether remote click invocation is supported.
    /// </summary>
    public bool SupportsClickInvocation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether remote property mutation is supported.
    /// </summary>
    public bool SupportsPropertyMutation { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether log streaming is supported.
    /// </summary>
    public bool SupportsLogStreaming { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether live frame streaming is enabled.
    /// </summary>
    public bool SupportsFrameStreaming { get; init; }

    /// <summary>
    /// Gets a value indicating whether live remote input is enabled.
    /// </summary>
    public bool SupportsRemoteInput { get; init; }
}
