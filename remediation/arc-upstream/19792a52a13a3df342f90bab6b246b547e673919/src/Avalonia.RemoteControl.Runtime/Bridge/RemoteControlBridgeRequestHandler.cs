using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server.Protocol;
using Avalonia.RemoteControl.Server.Runtime;
using Avalonia.RemoteControl.Server.Security;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Bridge;

/// <summary>
/// Dispatches Android bridge request envelopes to the host-independent remote-control runtime.
/// </summary>
public sealed class RemoteControlBridgeRequestHandler
{
    private readonly IRemoteControlRuntime runtime;
    private readonly RemoteControlBearerTokenAuthenticator authenticator;
    private readonly ILogger<RemoteControlBridgeRequestHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlBridgeRequestHandler"/> class.
    /// </summary>
    /// <param name="runtime">Transport-independent runtime service.</param>
    /// <param name="authenticator">Bearer token authenticator.</param>
    /// <param name="logger">Bridge logger.</param>
    public RemoteControlBridgeRequestHandler(
        IRemoteControlRuntime runtime,
        RemoteControlBearerTokenAuthenticator authenticator,
        ILogger<RemoteControlBridgeRequestHandler> logger)
    {
        this.runtime = runtime;
        this.authenticator = authenticator;
        this.logger = logger;
    }

    /// <summary>
    /// Handles a single bridge request envelope.
    /// </summary>
    /// <param name="request">Bridge request envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A bridge response envelope.</returns>
    public async ValueTask<BridgeResponse> HandleAsync(
        BridgeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(
            request.ProtocolVersion,
            RemoteControlProtocol.DisplayVersion,
            StringComparison.Ordinal))
        {
            return Failure(
                request,
                BridgeStatus.Unsupported,
                "Unsupported bridge protocol version.");
        }

        var auth = authenticator.AuthenticateAuthorization(request.Authorization);
        if (!auth.IsAuthenticated)
        {
            return Failure(request, BridgeStatus.Unauthenticated, auth.FailureMessage);
        }

        try
        {
            return request.Method switch
            {
                BridgeMethod.GetCapabilities => HandleGetCapabilities(request, auth.ClientIdentity),
                BridgeMethod.GetSnapshot => await HandleGetSnapshotAsync(request, cancellationToken).ConfigureAwait(false),
                BridgeMethod.InvokeClick => await HandleInvokeClickAsync(request, auth.ClientIdentity, cancellationToken).ConfigureAwait(false),
                BridgeMethod.InvokeFocus => await HandleInvokeFocusAsync(request, auth.ClientIdentity, cancellationToken).ConfigureAwait(false),
                BridgeMethod.SetProperty => await HandleSetPropertyAsync(request, auth.ClientIdentity, cancellationToken).ConfigureAwait(false),
                BridgeMethod.SendInput => await HandleSendInputAsync(request, auth.ClientIdentity, cancellationToken).ConfigureAwait(false),
                BridgeMethod.WatchTree or BridgeMethod.WatchFrames or BridgeMethod.WatchLogs => Failure(
                    request,
                    BridgeStatus.Unsupported,
                    "Bridge streaming must be read through a streaming bridge connection."),
                _ => Failure(request, BridgeStatus.Unsupported, "Unsupported bridge method."),
            };
        }
        catch (InvalidProtocolBufferException)
        {
            return Failure(request, BridgeStatus.Error, "Invalid bridge payload.");
        }
        catch (RemoteControlRuntimeException exception)
        {
            return Failure(request, ToBridgeStatus(exception.ErrorCode), exception.Message);
        }
        catch (OperationCanceledException)
        {
            return Failure(request, BridgeStatus.Cancelled, "Bridge request was cancelled.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Bridge request {RequestId} failed for method {Method}.",
                request.RequestId,
                request.Method);

            return Failure(request, BridgeStatus.Error, "Bridge request failed.");
        }
    }

    /// <summary>
    /// Handles a bridge request as a potentially long-lived response stream.
    /// </summary>
    /// <param name="request">Bridge request envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response envelopes.</returns>
    public async IAsyncEnumerable<BridgeResponse> HandleResponsesAsync(
        BridgeRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request.Method is not (BridgeMethod.WatchTree or BridgeMethod.WatchFrames or BridgeMethod.WatchLogs))
        {
            yield return await HandleAsync(request, cancellationToken).ConfigureAwait(false);
            yield break;
        }

        if (!string.Equals(
            request.ProtocolVersion,
            RemoteControlProtocol.DisplayVersion,
            StringComparison.Ordinal))
        {
            yield return Failure(request, BridgeStatus.Unsupported, "Unsupported bridge protocol version.");
            yield break;
        }

        var auth = authenticator.AuthenticateAuthorization(request.Authorization);
        if (!auth.IsAuthenticated)
        {
            yield return Failure(request, BridgeStatus.Unauthenticated, auth.FailureMessage);
            yield break;
        }

        var stream = CreateStreamPayloadsAsync(request, cancellationToken);
        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            BridgeResponse? failure = null;
            var hasNext = false;

            try
            {
                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (InvalidProtocolBufferException)
            {
                failure = Failure(request, BridgeStatus.Error, "Invalid bridge payload.");
            }
            catch (RemoteControlRuntimeException exception)
            {
                failure = Failure(request, ToBridgeStatus(exception.ErrorCode), exception.Message);
            }
            catch (OperationCanceledException)
            {
                failure = Failure(request, BridgeStatus.Cancelled, "Bridge request was cancelled.");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Bridge stream request {RequestId} failed for method {Method}.",
                    request.RequestId,
                    request.Method);

                failure = Failure(request, BridgeStatus.Error, "Bridge request failed.");
            }

            if (failure is not null)
            {
                yield return failure;
                yield break;
            }

            if (!hasNext)
            {
                break;
            }

            yield return StreamItem(request, enumerator.Current);
        }

        yield return Success(request, ByteString.Empty);
    }

    private async IAsyncEnumerable<ByteString> CreateStreamPayloadsAsync(
        BridgeRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        switch (request.Method)
        {
            case BridgeMethod.WatchTree:
                WatchTreeRequest.Parser.ParseFrom(request.Payload);
                await foreach (var snapshot in runtime.WatchSnapshotsAsync(cancellationToken).ConfigureAwait(false))
                {
                    var update = new TreeUpdate
                    {
                        Sequence = snapshot.Sequence,
                        Snapshot = snapshot.ToProtocol(),
                        Reason = "periodic",
                    };
                    yield return update.ToByteString();
                }

                break;
            case BridgeMethod.WatchFrames:
                WatchFramesRequest.Parser.ParseFrom(request.Payload);
                await foreach (var frame in runtime.WatchFramesAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return frame.ToProtocol().ToByteString();
                }

                break;
            case BridgeMethod.WatchLogs:
                var logRequest = WatchLogsRequest.Parser.ParseFrom(request.Payload);
                var minimumLevel = Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(
                    logRequest.MinimumLevel,
                    ignoreCase: true,
                    out var parsed)
                    ? parsed
                    : Microsoft.Extensions.Logging.LogLevel.Trace;
                var categoryPrefix = string.IsNullOrWhiteSpace(logRequest.CategoryPrefix)
                    ? null
                    : logRequest.CategoryPrefix;

                await foreach (var logEntry in runtime.WatchLogEntriesAsync(
                    minimumLevel,
                    categoryPrefix,
                    cancellationToken).ConfigureAwait(false))
                {
                    yield return logEntry.ToProtocol().ToByteString();
                }

                break;
        }
    }

    private BridgeResponse HandleGetCapabilities(BridgeRequest request, string authenticatedClientIdentity)
    {
        GetCapabilitiesRequest.Parser.ParseFrom(request.Payload);
        var capabilities = runtime.GetCapabilities().ToProtocol();
        capabilities.AuthenticatedClientIdentity = authenticatedClientIdentity;
        return Success(request, capabilities.ToByteString());
    }

    private async ValueTask<BridgeResponse> HandleGetSnapshotAsync(
        BridgeRequest request,
        CancellationToken cancellationToken)
    {
        GetSnapshotRequest.Parser.ParseFrom(request.Payload);
        var snapshot = await runtime.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return Success(request, snapshot.ToProtocol().ToByteString());
    }

    private async ValueTask<BridgeResponse> HandleInvokeClickAsync(
        BridgeRequest request,
        string clientIdentity,
        CancellationToken cancellationToken)
    {
        var invokeRequest = InvokeClickRequest.Parser.ParseFrom(request.Payload);
        var result = await runtime.InvokeClickAsync(
            invokeRequest.NodeId,
            clientIdentity,
            cancellationToken).ConfigureAwait(false);

        return Success(request, result.ToProtocol().ToByteString());
    }

    private async ValueTask<BridgeResponse> HandleInvokeFocusAsync(
        BridgeRequest request,
        string clientIdentity,
        CancellationToken cancellationToken)
    {
        var invokeRequest = InvokeFocusRequest.Parser.ParseFrom(request.Payload);
        var result = await runtime.InvokeFocusAsync(
            invokeRequest.NodeId,
            clientIdentity,
            cancellationToken).ConfigureAwait(false);

        return Success(request, result.ToProtocol().ToByteString());
    }

    private async ValueTask<BridgeResponse> HandleSetPropertyAsync(
        BridgeRequest request,
        string clientIdentity,
        CancellationToken cancellationToken)
    {
        var setRequest = SetPropertyRequest.Parser.ParseFrom(request.Payload);
        var result = await runtime.SetPropertyAsync(
            setRequest.NodeId,
            setRequest.PropertyName,
            setRequest.Value,
            clientIdentity,
            cancellationToken).ConfigureAwait(false);

        return Success(request, result.ToProtocol().ToByteString());
    }

    private async ValueTask<BridgeResponse> HandleSendInputAsync(
        BridgeRequest request,
        string clientIdentity,
        CancellationToken cancellationToken)
    {
        var inputRequest = SendInputRequest.Parser.ParseFrom(request.Payload);
        var result = await runtime.SendInputAsync(
            inputRequest.Events,
            clientIdentity,
            cancellationToken).ConfigureAwait(false);

        return Success(request, result.ToProtocol().ToByteString());
    }

    private static BridgeResponse Success(BridgeRequest request, ByteString payload)
    {
        return new BridgeResponse
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = request.RequestId,
            Status = BridgeStatus.Ok,
            Payload = payload,
            EndOfStream = true,
        };
    }

    private static BridgeResponse StreamItem(BridgeRequest request, ByteString payload)
    {
        return new BridgeResponse
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = request.RequestId,
            Status = BridgeStatus.Ok,
            Payload = payload,
            EndOfStream = false,
        };
    }

    private static BridgeResponse Failure(
        BridgeRequest request,
        BridgeStatus status,
        string errorMessage)
    {
        return new BridgeResponse
        {
            ProtocolVersion = RemoteControlProtocol.DisplayVersion,
            RequestId = request.RequestId,
            Status = status,
            ErrorMessage = errorMessage,
            EndOfStream = true,
        };
    }

    private static BridgeStatus ToBridgeStatus(RemoteControlRuntimeErrorCode errorCode)
    {
        return errorCode switch
        {
            RemoteControlRuntimeErrorCode.Unsupported => BridgeStatus.Unsupported,
            RemoteControlRuntimeErrorCode.Cancelled => BridgeStatus.Cancelled,
            _ => BridgeStatus.Error,
        };
    }
}
