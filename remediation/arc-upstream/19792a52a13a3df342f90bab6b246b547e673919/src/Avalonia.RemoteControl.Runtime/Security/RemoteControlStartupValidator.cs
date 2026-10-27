using System.Net;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Security;

/// <summary>
/// Validates startup options before the remote-control transport is opened.
/// </summary>
public sealed class RemoteControlStartupValidator
{
    private readonly AvaloniaRemoteControlOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlStartupValidator"/> class.
    /// </summary>
    /// <param name="options">Remote-control options.</param>
    public RemoteControlStartupValidator(IOptions<AvaloniaRemoteControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
    }

    /// <summary>
    /// Validates the current options.
    /// </summary>
    /// <returns>A sanitized validation result.</returns>
    public RemoteControlStartupValidationResult Validate()
    {
        if (!options.IsEnabled)
        {
            return new RemoteControlStartupValidationResult();
        }

        var errors = new List<string>();
        var isLoopback = IPAddress.IsLoopback(options.Host);

        if (options.RequireAuthentication && string.IsNullOrWhiteSpace(options.AuthenticationToken))
        {
            errors.Add("Authentication token is required when remote control is enabled.");
        }

        if (!isLoopback && !options.IsAdbTunnel)
        {
            if (!options.RequireTlsForNonLoopback)
            {
                errors.Add("Non-loopback cleartext startup is not allowed.");
            }
            else if (string.IsNullOrWhiteSpace(options.TlsCertificatePath))
            {
                errors.Add("A TLS certificate path is required for non-loopback startup.");
            }
        }

        if (!options.AllowCleartextForLoopbackOrAdb && string.IsNullOrWhiteSpace(options.TlsCertificatePath))
        {
            errors.Add("A TLS certificate path is required when cleartext loopback or ADB sessions are disabled.");
        }

        return new RemoteControlStartupValidationResult
        {
            Errors = errors,
        };
    }
}
