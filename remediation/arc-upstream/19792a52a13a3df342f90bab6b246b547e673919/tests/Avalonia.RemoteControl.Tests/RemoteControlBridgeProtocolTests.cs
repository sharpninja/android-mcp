using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Google.Protobuf;

namespace Avalonia.RemoteControl.Tests;

public sealed class RemoteControlBridgeProtocolTests
{
    [Fact]
    public void BridgeProtocolDefinesAndroidTransportMetadata()
    {
        Assert.Equal("grpc", RemoteControlProtocol.GrpcTransportProtocol);
        Assert.Equal("arc-protobuf-v1", RemoteControlProtocol.AndroidBridgeTransportProtocol);
        Assert.Equal("1.0", RemoteControlProtocol.DisplayVersion);
    }

    [Fact]
    public void BridgeFrameCodecRoundTripsLengthPrefixedRequest()
    {
        var request = new BridgeRequest
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = "req-bridge-001",
            Method = BridgeMethod.GetCapabilities,
            Authorization = RemoteControlBridgeProtocol.CreateBearerAuthorization("debug-token"),
            Payload = new GetCapabilitiesRequest().ToByteString(),
        };

        var frame = BridgeFrameCodec.Encode(request);
        var decoded = BridgeFrameCodec.Decode(frame, BridgeRequest.Parser);

        Assert.Equal(request.CalculateSize(), ReadLengthPrefix(frame));
        Assert.Equal(request.RequestId, decoded.RequestId);
        Assert.Equal(request.Method, decoded.Method);
        Assert.Equal(request.Authorization, decoded.Authorization);
        Assert.Equal(request.Payload, decoded.Payload);
    }

    [Fact]
    public async Task BridgeFrameCodecRejectsOversizedFrame()
    {
        await using var stream = new MemoryStream([0x00, 0x00, 0x00, 0x08]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => BridgeFrameCodec.ReadAsync(stream, BridgeRequest.Parser, maxFrameLength: 4).AsTask());

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgeResponseCarriesSanitizedFailureShape()
    {
        var response = new BridgeResponse
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = "req-bridge-002",
            Status = BridgeStatus.Unauthorized,
            ErrorMessage = "Action is not authorized.",
            EndOfStream = true,
        };

        var frame = BridgeFrameCodec.Encode(response);
        var decoded = BridgeFrameCodec.Decode(frame, BridgeResponse.Parser);

        Assert.Equal(BridgeStatus.Unauthorized, decoded.Status);
        Assert.Equal("Action is not authorized.", decoded.ErrorMessage);
        Assert.True(decoded.EndOfStream);
        Assert.Empty(decoded.Payload);
    }

    private static int ReadLengthPrefix(byte[] frame)
    {
        return (frame[0] << 24)
            | (frame[1] << 16)
            | (frame[2] << 8)
            | frame[3];
    }
}
