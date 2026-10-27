using Avalonia.RemoteControl.Protocol.V1;
using Google.Protobuf;

namespace Avalonia.RemoteControl.Server.Rendering;

/// <summary>
/// Represents one captured live remote UI frame.
/// </summary>
/// <param name="Sequence">Frame sequence number.</param>
/// <param name="Png">PNG encoded frame bytes.</param>
/// <param name="PixelWidth">Frame width in pixels.</param>
/// <param name="PixelHeight">Frame height in pixels.</param>
/// <param name="RootWidth">Remote root width in device-independent pixels.</param>
/// <param name="RootHeight">Remote root height in device-independent pixels.</param>
/// <param name="RenderScale">Scale between root DIPs and encoded pixels.</param>
/// <param name="TimestampUtc">Capture timestamp.</param>
public sealed record RemoteControlFrame(
    ulong Sequence,
    byte[] Png,
    int PixelWidth,
    int PixelHeight,
    double RootWidth,
    double RootHeight,
    double RenderScale,
    DateTimeOffset TimestampUtc)
{
    /// <summary>
    /// Converts the frame to a protocol frame update.
    /// </summary>
    /// <returns>The protocol frame update.</returns>
    public FrameUpdate ToProtocol()
    {
        return new FrameUpdate
        {
            Sequence = Sequence,
            Png = ByteString.CopyFrom(Png),
            PixelWidth = PixelWidth,
            PixelHeight = PixelHeight,
            RootWidth = RootWidth,
            RootHeight = RootHeight,
            RenderScale = RenderScale,
            TimestampUtc = TimestampUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
