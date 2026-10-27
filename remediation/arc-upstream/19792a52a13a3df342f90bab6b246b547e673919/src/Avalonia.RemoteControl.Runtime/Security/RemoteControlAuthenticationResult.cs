namespace Avalonia.RemoteControl.Server.Security;

/// <summary>
/// Represents the sanitized result of remote-control bearer authentication.
/// </summary>
/// <param name="IsAuthenticated">Whether the presented credentials are accepted.</param>
/// <param name="ClientIdentity">Sanitized identity assigned to the authenticated client.</param>
/// <param name="FailureMessage">Sanitized failure message safe for clients.</param>
public sealed record RemoteControlAuthenticationResult(
    bool IsAuthenticated,
    string ClientIdentity,
    string FailureMessage)
{
    /// <summary>
    /// Creates an accepted authentication result.
    /// </summary>
    /// <param name="clientIdentity">Sanitized client identity.</param>
    /// <returns>An accepted authentication result.</returns>
    public static RemoteControlAuthenticationResult Success(string clientIdentity)
    {
        return new RemoteControlAuthenticationResult(
            true,
            clientIdentity,
            string.Empty);
    }

    /// <summary>
    /// Creates a rejected authentication result.
    /// </summary>
    /// <param name="failureMessage">Sanitized failure message.</param>
    /// <returns>A rejected authentication result.</returns>
    public static RemoteControlAuthenticationResult Failure(string failureMessage)
    {
        return new RemoteControlAuthenticationResult(
            false,
            RemoteControlClientIdentity.Unknown,
            failureMessage);
    }
}
