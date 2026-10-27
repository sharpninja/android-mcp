using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Avalonia.RemoteControl.Server.Security;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Coordinates the remote-control server lifecycle.
/// </summary>
public sealed class AvaloniaRemoteControlService
{
    private readonly AvaloniaRemoteControlOptions? options;
    private readonly ILogger<AvaloniaRemoteControlService>? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaRemoteControlService"/> class.
    /// </summary>
    public AvaloniaRemoteControlService()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaRemoteControlService"/> class.
    /// </summary>
    /// <param name="options">Configured remote-control options.</param>
    /// <param name="logger">Logger used for startup diagnostics.</param>
    public AvaloniaRemoteControlService(
        IOptions<AvaloniaRemoteControlOptions> options,
        ILogger<AvaloniaRemoteControlService> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the capabilities currently supported by this server package.
    /// </summary>
    /// <returns>The supported remote-control capabilities.</returns>
    public RemoteControlCapabilities GetCapabilities()
    {
        var configuredOptions = options ?? new AvaloniaRemoteControlOptions();

        return new RemoteControlCapabilities
        {
            AuthenticatedClientIdentity = string.IsNullOrWhiteSpace(configuredOptions.AuthenticatedClientIdentity)
                ? RemoteControlClientIdentity.Unknown
                : configuredOptions.AuthenticatedClientIdentity,
            SupportsFrameStreaming = configuredOptions.AllowRemoteFrames,
            SupportsRemoteInput = configuredOptions.AllowRemoteActions && configuredOptions.AllowRemoteInput,
        };
    }

    /// <summary>
    /// Gets the current startup posture without exposing secrets.
    /// </summary>
    /// <returns>The current startup posture.</returns>
    public AvaloniaRemoteControlStartupState GetStartupState()
    {
        var state = (options ?? new AvaloniaRemoteControlOptions()).ToStartupState();

        logger?.LogDebug(
            "Avalonia remote control startup state: enabled={IsEnabled}, host={Host}, port={Port}",
            state.IsEnabled,
            state.Host,
            state.Port);

        return state;
    }
}
