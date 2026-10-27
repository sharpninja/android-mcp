namespace Avalonia.RemoteControl.Protocol;

/// <summary>
/// Defines helpers for the Android-compatible remote-control bridge protocol.
/// </summary>
public static class RemoteControlBridgeProtocol
{
    /// <summary>
    /// Creates the authorization value carried by bridge request envelopes.
    /// </summary>
    /// <param name="token">Bearer token value.</param>
    /// <returns>Authorization header-compatible value.</returns>
    public static string CreateBearerAuthorization(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return $"Bearer {token}";
    }
}
