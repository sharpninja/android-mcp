using Grpc.Core;

namespace Avalonia.RemoteControl.Server.Security;

/// <summary>
/// Stores sanitized authenticated client identity information for a gRPC call.
/// </summary>
public static class RemoteControlClientIdentity
{
    /// <summary>
    /// Gets the server call context key used for the authenticated client identity.
    /// </summary>
    public const string UserStateKey = "Avalonia.RemoteControl.ClientIdentity";

    /// <summary>
    /// Gets the fallback identity for non-gRPC or unauthenticated internal calls.
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// Reads the authenticated client identity from a gRPC call context.
    /// </summary>
    /// <param name="context">The gRPC call context.</param>
    /// <returns>The sanitized client identity.</returns>
    public static string From(ServerCallContext? context)
    {
        return context?.UserState.TryGetValue(UserStateKey, out var value) == true && value is string identity
            ? identity
            : Unknown;
    }
}
