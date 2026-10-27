using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Security;

/// <summary>
/// Validates bearer credentials for transport-independent remote-control entry points.
/// </summary>
public sealed class RemoteControlBearerTokenAuthenticator
{
    private const string BearerPrefix = "Bearer ";
    private readonly AvaloniaRemoteControlOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlBearerTokenAuthenticator"/> class.
    /// </summary>
    /// <param name="options">Remote-control options.</param>
    public RemoteControlBearerTokenAuthenticator(IOptions<AvaloniaRemoteControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
    }

    /// <summary>
    /// Validates an Authorization header-compatible bearer value.
    /// </summary>
    /// <param name="authorization">Authorization header value, or equivalent bridge field.</param>
    /// <returns>The sanitized authentication result.</returns>
    public RemoteControlAuthenticationResult AuthenticateAuthorization(string? authorization)
    {
        if (!options.RequireAuthentication)
        {
            return RemoteControlAuthenticationResult.Success(GetConfiguredIdentity());
        }

        var expectedToken = options.AuthenticationToken;
        var presentedToken = GetBearerToken(authorization);

        if (string.IsNullOrWhiteSpace(expectedToken)
            || string.IsNullOrWhiteSpace(presentedToken)
            || !FixedTimeEquals(expectedToken, presentedToken))
        {
            return RemoteControlAuthenticationResult.Failure("Authentication is required.");
        }

        return RemoteControlAuthenticationResult.Success(GetConfiguredIdentity());
    }

    private string GetConfiguredIdentity()
    {
        return string.IsNullOrWhiteSpace(options.AuthenticatedClientIdentity)
            ? RemoteControlClientIdentity.Unknown
            : options.AuthenticatedClientIdentity;
    }

    private static string? GetBearerToken(string? authorization)
    {
        if (authorization is null || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization[BearerPrefix.Length..].Trim();
    }

    private static bool FixedTimeEquals(string expectedToken, string presentedToken)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var presentedBytes = Encoding.UTF8.GetBytes(presentedToken);

        return expectedBytes.Length == presentedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }
}
