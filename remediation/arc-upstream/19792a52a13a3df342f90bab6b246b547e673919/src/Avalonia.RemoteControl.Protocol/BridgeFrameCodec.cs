using System.Buffers.Binary;
using Google.Protobuf;

namespace Avalonia.RemoteControl.Protocol;

/// <summary>
/// Encodes and decodes length-prefixed protobuf bridge frames.
/// </summary>
public static class BridgeFrameCodec
{
    /// <summary>
    /// Number of bytes in the big-endian frame length prefix.
    /// </summary>
    public const int LengthPrefixByteCount = 4;

    /// <summary>
    /// Default maximum bridge frame payload size.
    /// </summary>
    public const int DefaultMaxFrameLength = 8 * 1024 * 1024;

    /// <summary>
    /// Encodes a protobuf message as a length-prefixed bridge frame.
    /// </summary>
    /// <param name="message">Message to encode.</param>
    /// <returns>Length-prefixed frame bytes.</returns>
    public static byte[] Encode(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = message.ToByteArray();
        var frame = new byte[LengthPrefixByteCount + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, LengthPrefixByteCount), payload.Length);
        payload.CopyTo(frame.AsSpan(LengthPrefixByteCount));
        return frame;
    }

    /// <summary>
    /// Decodes a complete length-prefixed bridge frame.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="frame">Complete frame bytes.</param>
    /// <param name="parser">Message parser.</param>
    /// <param name="maxFrameLength">Maximum accepted payload size.</param>
    /// <returns>Decoded protobuf message.</returns>
    public static T Decode<T>(
        ReadOnlySpan<byte> frame,
        MessageParser<T> parser,
        int maxFrameLength = DefaultMaxFrameLength)
        where T : IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(parser);

        if (frame.Length < LengthPrefixByteCount)
        {
            throw new InvalidDataException("Bridge frame did not contain a complete length prefix.");
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(frame[..LengthPrefixByteCount]);
        ValidateLength(length, maxFrameLength);

        if (frame.Length - LengthPrefixByteCount != length)
        {
            throw new InvalidDataException("Bridge frame length did not match the payload length.");
        }

        return parser.ParseFrom(frame[LengthPrefixByteCount..]);
    }

    /// <summary>
    /// Writes a protobuf message as a length-prefixed bridge frame.
    /// </summary>
    /// <param name="stream">Target stream.</param>
    /// <param name="message">Message to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the write.</returns>
    public static async ValueTask WriteAsync(
        Stream stream,
        IMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var frame = Encode(message);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one length-prefixed protobuf bridge frame from a stream.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="stream">Source stream.</param>
    /// <param name="parser">Message parser.</param>
    /// <param name="maxFrameLength">Maximum accepted payload size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded protobuf message.</returns>
    public static async ValueTask<T> ReadAsync<T>(
        Stream stream,
        MessageParser<T> parser,
        int maxFrameLength = DefaultMaxFrameLength,
        CancellationToken cancellationToken = default)
        where T : IMessage<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(parser);

        var lengthBuffer = new byte[LengthPrefixByteCount];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        ValidateLength(length, maxFrameLength);

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return parser.ParseFrom(payload);
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Bridge stream ended before a complete frame was read.");
            }

            offset += read;
        }
    }

    private static void ValidateLength(int length, int maxFrameLength)
    {
        if (maxFrameLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "Maximum frame length must be positive.");
        }

        if (length < 0)
        {
            throw new InvalidDataException("Bridge frame length was negative.");
        }

        if (length > maxFrameLength)
        {
            throw new InvalidDataException(
                $"Bridge frame length {length} exceeds the maximum allowed length {maxFrameLength}.");
        }
    }
}
