using System.Net;
using Avalonia.RemoteControl.Protocol;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Configures the embeddable Avalonia remote-control server.
/// </summary>
public sealed class AvaloniaRemoteControlOptions
{
    private readonly List<string> sensitiveNameFragments =
    [
        "password",
        "token",
        "secret",
        "key",
        "credential",
        "auth",
        "cookie",
        "connection string"
    ];

    private readonly HashSet<string> allowedMutableProperties = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether the remote-control server is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the listener host. The safe default is loopback.
    /// </summary>
    public IPAddress Host { get; set; } = IPAddress.Loopback;

    /// <summary>
    /// Gets or sets the listener port.
    /// </summary>
    public int Port { get; set; } = RemoteControlProtocol.DefaultPort;

    /// <summary>
    /// Gets or sets whether bearer authentication is required for every request.
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>
    /// Gets or sets the bearer token expected from clients. This value is never exposed in startup state.
    /// </summary>
    public string? AuthenticationToken { get; set; }

    /// <summary>
    /// Gets or sets the sanitized identity assigned to successfully authenticated clients.
    /// </summary>
    public string AuthenticatedClientIdentity { get; set; } = "remote-client";

    /// <summary>
    /// Gets or sets whether TLS is required for non-loopback listeners.
    /// </summary>
    public bool RequireTlsForNonLoopback { get; set; } = true;

    /// <summary>
    /// Gets or sets the TLS certificate path used for non-loopback listeners.
    /// </summary>
    public string? TlsCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the TLS certificate password used for non-loopback listeners.
    /// </summary>
    public string? TlsCertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets whether cleartext HTTP/2 is allowed for loopback and explicit ADB tunnel sessions.
    /// </summary>
    public bool AllowCleartextForLoopbackOrAdb { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the listener is reached only through an explicit ADB localhost tunnel.
    /// </summary>
    public bool IsAdbTunnel { get; set; }

    /// <summary>
    /// Gets or sets whether property mutation starts from a deny-by-default policy.
    /// </summary>
    public bool DenyPropertyMutationByDefault { get; set; } = true;

    /// <summary>
    /// Gets or sets whether remote actions such as click invocation are enabled.
    /// </summary>
    public bool AllowRemoteActions { get; set; }

    /// <summary>
    /// Gets or sets whether live remote UI frame streaming is enabled.
    /// </summary>
    public bool AllowRemoteFrames { get; set; }

    /// <summary>
    /// Gets or sets whether live remote pointer and keyboard input is enabled.
    /// </summary>
    public bool AllowRemoteInput { get; set; }

    /// <summary>
    /// Gets or sets the periodic snapshot interval for live tree streams.
    /// </summary>
    public TimeSpan TreeStreamInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets the periodic frame interval for live UI frame streams.
    /// </summary>
    public TimeSpan FrameStreamInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum pixel count allowed for a captured live frame.
    /// </summary>
    public int MaxFramePixelCount { get; set; } = 4_000_000;

    /// <summary>
    /// Gets or sets the maximum number of log entries retained for new log stream subscribers.
    /// </summary>
    public int LogBufferCapacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the interval for transport keepalive entries on idle log streams.
    /// </summary>
    public TimeSpan LogStreamKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets sensitive name fragments that are redacted by default.
    /// </summary>
    public IList<string> SensitiveNameFragments => sensitiveNameFragments;

    /// <summary>
    /// Gets property names or type-qualified property names allowed for mutation.
    /// </summary>
    public ISet<string> AllowedMutableProperties => allowedMutableProperties;

    /// <summary>
    /// Creates a startup-state snapshot for diagnostics and tests.
    /// </summary>
    /// <returns>The current startup-state snapshot.</returns>
    public AvaloniaRemoteControlStartupState ToStartupState()
    {
        return new AvaloniaRemoteControlStartupState(
            IsEnabled,
            Host.ToString(),
            Port,
            RequireAuthentication,
            RequireTlsForNonLoopback,
            DenyPropertyMutationByDefault);
    }
}
