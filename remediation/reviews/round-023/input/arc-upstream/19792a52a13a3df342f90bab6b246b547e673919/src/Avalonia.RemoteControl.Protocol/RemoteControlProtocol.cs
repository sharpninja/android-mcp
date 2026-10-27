namespace Avalonia.RemoteControl.Protocol;

/// <summary>
/// Defines protocol-level constants shared by server and client packages.
/// </summary>
public static class RemoteControlProtocol
{
    /// <summary>
    /// Current major protocol version.
    /// </summary>
    public const int MajorVersion = 1;

    /// <summary>
    /// Current minor protocol version.
    /// </summary>
    public const int MinorVersion = 0;

    /// <summary>
    /// Default debuggee endpoint port used by local and ADB-forwarded sessions.
    /// </summary>
    public const int DefaultPort = 47100;

    /// <summary>
    /// Marker protocol value for the desktop-facing gRPC transport.
    /// </summary>
    public const string GrpcTransportProtocol = "grpc";

    /// <summary>
    /// Marker protocol value for the Android-compatible protobuf bridge transport.
    /// </summary>
    public const string AndroidBridgeTransportProtocol = "arc-protobuf-v1";

    /// <summary>
    /// Logger category used for transport keepalive entries on otherwise idle log streams.
    /// </summary>
    public const string LogStreamKeepAliveCategory = "Avalonia.RemoteControl.LogStreamKeepAlive";

    /// <summary>
    /// Current protocol version rendered as a display string.
    /// </summary>
    public static string DisplayVersion => $"{MajorVersion}.{MinorVersion}";
}
