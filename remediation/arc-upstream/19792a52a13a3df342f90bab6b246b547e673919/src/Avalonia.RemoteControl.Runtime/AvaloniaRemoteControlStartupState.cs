namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Describes the server startup posture without exposing secrets.
/// </summary>
/// <param name="IsEnabled">Whether the remote-control server is enabled.</param>
/// <param name="Host">The configured listener host.</param>
/// <param name="Port">The configured listener port.</param>
/// <param name="RequiresAuthentication">Whether authentication is required.</param>
/// <param name="RequiresTlsForNonLoopback">Whether non-loopback listeners require TLS.</param>
/// <param name="DenyPropertyMutationByDefault">Whether mutation starts from a deny-by-default posture.</param>
public sealed record AvaloniaRemoteControlStartupState(
    bool IsEnabled,
    string Host,
    int Port,
    bool RequiresAuthentication,
    bool RequiresTlsForNonLoopback,
    bool DenyPropertyMutationByDefault);
