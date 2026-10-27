using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Google.Protobuf;

namespace Avalonia.RemoteControl.Client.Bridge;

internal sealed class RemoteControlBridgeClient : IDisposable
{
    private readonly Uri endpoint;
    private readonly string authorization;

    public RemoteControlBridgeClient(Uri endpoint, string token)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (endpoint.Port < 1)
        {
            throw new ArgumentException("Bridge endpoint must include a TCP port.", nameof(endpoint));
        }

        this.endpoint = endpoint;
        authorization = RemoteControlBridgeProtocol.CreateBearerAuthorization(token);
    }

    public Task<GetCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return SendUnaryAsync(
            BridgeMethod.GetCapabilities,
            new GetCapabilitiesRequest(),
            GetCapabilitiesResponse.Parser,
            cancellationToken);
    }

    public Task<TreeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return SendUnaryAsync(
            BridgeMethod.GetSnapshot,
            new GetSnapshotRequest(),
            TreeSnapshot.Parser,
            cancellationToken);
    }

    public IAsyncEnumerable<TreeUpdate> WatchTreeAsync(CancellationToken cancellationToken = default)
    {
        return SendStreamAsync(
            BridgeMethod.WatchTree,
            new WatchTreeRequest(),
            TreeUpdate.Parser,
            cancellationToken);
    }

    public IAsyncEnumerable<FrameUpdate> WatchFramesAsync(CancellationToken cancellationToken = default)
    {
        return SendStreamAsync(
            BridgeMethod.WatchFrames,
            new WatchFramesRequest(),
            FrameUpdate.Parser,
            cancellationToken);
    }

    public Task<CommandResult> InvokeClickAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return SendUnaryAsync(
            BridgeMethod.InvokeClick,
            new InvokeClickRequest { NodeId = nodeId },
            CommandResult.Parser,
            cancellationToken);
    }

    public Task<CommandResult> InvokeFocusAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return SendUnaryAsync(
            BridgeMethod.InvokeFocus,
            new InvokeFocusRequest { NodeId = nodeId },
            CommandResult.Parser,
            cancellationToken);
    }

    public Task<CommandResult> SetPropertyAsync(
        string nodeId,
        string propertyName,
        string value,
        CancellationToken cancellationToken = default)
    {
        return SendUnaryAsync(
            BridgeMethod.SetProperty,
            new SetPropertyRequest
            {
                NodeId = nodeId,
                PropertyName = propertyName,
                Value = value,
            },
            CommandResult.Parser,
            cancellationToken);
    }

    public Task<CommandResult> SendInputAsync(
        IReadOnlyList<RemoteInputEvent> events,
        CancellationToken cancellationToken = default)
    {
        var request = new SendInputRequest();
        request.Events.AddRange(events);

        return SendUnaryAsync(
            BridgeMethod.SendInput,
            request,
            CommandResult.Parser,
            cancellationToken);
    }

    public IAsyncEnumerable<LogEntry> WatchLogsAsync(
        string minimumLevel,
        string? categoryPrefix,
        CancellationToken cancellationToken = default)
    {
        return SendStreamAsync(
            BridgeMethod.WatchLogs,
            new WatchLogsRequest
            {
                MinimumLevel = minimumLevel,
                CategoryPrefix = categoryPrefix ?? string.Empty,
            },
            LogEntry.Parser,
            cancellationToken);
    }

    public void Dispose()
    {
    }

    private async Task<TResponse> SendUnaryAsync<TRequest, TResponse>(
        BridgeMethod method,
        TRequest payload,
        MessageParser<TResponse> responseParser,
        CancellationToken cancellationToken)
        where TRequest : IMessage
        where TResponse : IMessage<TResponse>
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var request = new BridgeRequest
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = requestId,
            Method = method,
            Authorization = authorization,
            Payload = payload.ToByteString(),
        };

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();

        await BridgeFrameCodec.WriteAsync(stream, request, cancellationToken).ConfigureAwait(false);
        var response = await ReadBridgeResponseAsync(
            method,
            stream,
            cancellationToken).ConfigureAwait(false);

        if (!string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bridge response request ID did not match the request.");
        }

        if (!string.Equals(
            response.ProtocolVersion,
            RemoteControlProtocol.DisplayVersion,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bridge response protocol version is not supported.");
        }

        if (response.Status != BridgeStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Bridge request failed with status {response.Status}: {response.ErrorMessage}");
        }

        return responseParser.ParseFrom(response.Payload);
    }

    private async IAsyncEnumerable<TResponse> SendStreamAsync<TRequest, TResponse>(
        BridgeMethod method,
        TRequest payload,
        MessageParser<TResponse> responseParser,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TRequest : IMessage
        where TResponse : IMessage<TResponse>
    {
        var requestId = $"req-{Guid.NewGuid():N}";
        var request = new BridgeRequest
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = requestId,
            Method = method,
            Authorization = authorization,
            Payload = payload.ToByteString(),
        };

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();

        await BridgeFrameCodec.WriteAsync(stream, request, cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await ReadBridgeResponseAsync(
                method,
                stream,
                cancellationToken).ConfigureAwait(false);

            ValidateResponse(requestId, response);

            if (response.EndOfStream)
            {
                yield break;
            }

            yield return responseParser.ParseFrom(response.Payload);
        }
    }

    private static void ValidateResponse(string requestId, BridgeResponse response)
    {
        if (!string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bridge response request ID did not match the request.");
        }

        if (!string.Equals(
            response.ProtocolVersion,
            RemoteControlProtocol.DisplayVersion,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bridge response protocol version is not supported.");
        }

        if (response.Status != BridgeStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Bridge request failed with status {response.Status}: {response.ErrorMessage}");
        }
    }

    private static async ValueTask<BridgeResponse> ReadBridgeResponseAsync(
        BridgeMethod method,
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            return await BridgeFrameCodec.ReadAsync(
                stream,
                BridgeResponse.Parser,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsClosedBridgeConnection(exception))
        {
            throw CreateClosedBridgeConnectionException(method, exception);
        }
    }

    private static bool IsClosedBridgeConnection(Exception exception)
    {
        return exception is EndOfStreamException or IOException or SocketException;
    }

    private static InvalidOperationException CreateClosedBridgeConnectionException(
        BridgeMethod method,
        Exception innerException)
    {
        return new InvalidOperationException(
            $"Bridge connection for {method} closed before a complete response was received. " +
            "If this is an ADB connection, launch the Android app, wait for the Avalonia.RemoteControl bridge to start, " +
            "and remove stale forwards if needed.",
            innerException);
    }
}
