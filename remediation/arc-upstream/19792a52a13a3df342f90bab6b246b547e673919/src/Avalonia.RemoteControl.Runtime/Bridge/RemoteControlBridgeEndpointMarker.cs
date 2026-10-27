using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.RemoteControl.Protocol;

namespace Avalonia.RemoteControl.Server.Bridge;

/// <summary>
/// Describes the package-private Android bridge endpoint metadata read by the ADB client workflow.
/// </summary>
public sealed class RemoteControlBridgeEndpointMarker
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private RemoteControlBridgeEndpointMarker(
        int devicePort,
        string token,
        string bridgeProtocol)
    {
        DevicePort = devicePort;
        Token = token;
        BridgeProtocol = bridgeProtocol;
    }

    /// <summary>
    /// Gets the Android marker file name.
    /// </summary>
    public const string FileName = "avalonia-remote-control.json";

    /// <summary>
    /// Gets the current marker schema version.
    /// </summary>
    public const string CurrentSchemaVersion = "1";

    /// <summary>
    /// Gets the marker schema version.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets the TCP listener port inside the Android app process.
    /// </summary>
    [JsonPropertyName("devicePort")]
    public int DevicePort { get; }

    /// <summary>
    /// Gets the bearer token required by the bridge listener.
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; }

    /// <summary>
    /// Gets the transport protocol identifier.
    /// </summary>
    [JsonPropertyName("bridgeProtocol")]
    public string BridgeProtocol { get; }

    /// <summary>
    /// Creates marker metadata for an Android bridge listener.
    /// </summary>
    /// <param name="devicePort">The TCP listener port inside the Android app process.</param>
    /// <param name="token">The bearer token required by the listener.</param>
    /// <returns>Marker metadata.</returns>
    public static RemoteControlBridgeEndpointMarker Create(int devicePort, string token)
    {
        if (devicePort is < IPEndPointMinPort or > IPEndPointMaxPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(devicePort),
                "Device port must be a valid TCP port.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new RemoteControlBridgeEndpointMarker(
            devicePort,
            token,
            RemoteControlProtocol.AndroidBridgeTransportProtocol);
    }

    /// <summary>
    /// Serializes the marker metadata as JSON.
    /// </summary>
    /// <returns>JSON marker content.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    /// <summary>
    /// Writes the marker JSON to the supplied package-private directory.
    /// </summary>
    /// <param name="directoryPath">Package-private directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The written marker file path.</returns>
    public async ValueTask<string> WriteAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        var markerPath = Path.Combine(directoryPath, FileName);
        await File.WriteAllTextAsync(markerPath, ToJson(), cancellationToken).ConfigureAwait(false);
        return markerPath;
    }

    private const int IPEndPointMinPort = 1;
    private const int IPEndPointMaxPort = 65535;
}
