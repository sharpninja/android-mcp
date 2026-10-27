using System.Net;
using System.Net.Sockets;
using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Bridge;

/// <summary>
/// Hosts the Android-compatible bridge protocol over a loopback TCP listener.
/// </summary>
public sealed class RemoteControlBridgeTcpListener : IAsyncDisposable
{
    private readonly RemoteControlBridgeRequestHandler requestHandler;
    private readonly AvaloniaRemoteControlOptions options;
    private readonly ILogger<RemoteControlBridgeTcpListener> logger;
    private readonly object sync = new();
    private readonly HashSet<Task> clientTasks = [];

    private TcpListener? tcpListener;
    private CancellationTokenSource? stopSource;
    private Task? acceptLoopTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlBridgeTcpListener"/> class.
    /// </summary>
    /// <param name="requestHandler">Bridge request handler.</param>
    /// <param name="options">Remote-control options.</param>
    /// <param name="logger">Listener logger.</param>
    public RemoteControlBridgeTcpListener(
        RemoteControlBridgeRequestHandler requestHandler,
        IOptions<AvaloniaRemoteControlOptions> options,
        ILogger<RemoteControlBridgeTcpListener> logger)
    {
        ArgumentNullException.ThrowIfNull(requestHandler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.requestHandler = requestHandler;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the bound loopback endpoint after the listener starts.
    /// </summary>
    public IPEndPoint? BoundEndpoint { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the listener is running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (sync)
            {
                return tcpListener is not null;
            }
        }
    }

    /// <summary>
    /// Starts the loopback TCP listener.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the listener has been started.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStartupOptions();

        lock (sync)
        {
            if (tcpListener is not null)
            {
                return Task.CompletedTask;
            }

            stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tcpListener = new TcpListener(IPAddress.Loopback, options.Port);
            tcpListener.Start();
            BoundEndpoint = (IPEndPoint)tcpListener.LocalEndpoint;
            acceptLoopTask = AcceptLoopAsync(tcpListener, stopSource.Token);
        }

        logger.LogInformation(
            "Avalonia remote-control bridge listener started on loopback port {Port}.",
            BoundEndpoint!.Port);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the loopback TCP listener and waits for in-flight bridge requests to finish.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        TcpListener? listener;
        CancellationTokenSource? source;
        Task? acceptLoop;

        lock (sync)
        {
            listener = tcpListener;
            source = stopSource;
            acceptLoop = acceptLoopTask;

            tcpListener = null;
            stopSource = null;
            acceptLoopTask = null;
            BoundEndpoint = null;
        }

        if (listener is null)
        {
            return;
        }

        source?.Cancel();
        listener.Stop();

        if (acceptLoop is not null)
        {
            await WaitWithoutCancellationAsync(acceptLoop).ConfigureAwait(false);
        }

        Task[] pendingClients;
        lock (sync)
        {
            pendingClients = [.. clientTasks];
        }

        if (pendingClients.Length > 0)
        {
            await Task.WhenAll(pendingClients).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        source?.Dispose();
        logger.LogInformation("Avalonia remote-control bridge listener stopped.");
    }

    /// <summary>
    /// Creates marker metadata for the currently bound endpoint.
    /// </summary>
    /// <returns>Endpoint marker metadata.</returns>
    public RemoteControlBridgeEndpointMarker CreateEndpointMarker()
    {
        var endpoint = BoundEndpoint
            ?? throw new InvalidOperationException("Bridge listener must be started before creating a marker.");

        var token = options.AuthenticationToken
            ?? throw new InvalidOperationException("Bridge authentication token is not configured.");

        return RemoteControlBridgeEndpointMarker.Create(endpoint.Port, token);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                logger.LogDebug(
                    "Bridge TCP client accepted from {RemoteEndPoint}.",
                    client.Client.RemoteEndPoint?.ToString() ?? "unknown");
                var clientTask = HandleClientAsync(client, cancellationToken);
                TrackClientTask(clientTask);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Avalonia remote-control bridge listener failed.");
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                var request = await BridgeFrameCodec.ReadAsync(
                    stream,
                    BridgeRequest.Parser,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                logger.LogDebug(
                    "Bridge TCP request frame received from client: {Method}; request {RequestId}; payload bytes {PayloadByteCount}.",
                    request.Method,
                    request.RequestId,
                    request.Payload.Length);

                var responseFrameCount = 0;
                var payloadFrameCount = 0;
                var isStreamingRequest = IsStreamingMethod(request.Method);

                await foreach (var response in requestHandler.HandleResponsesAsync(request, cancellationToken)
                    .ConfigureAwait(false))
                {
                    await BridgeFrameCodec.WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
                    responseFrameCount++;

                    if (!response.EndOfStream)
                    {
                        payloadFrameCount++;
                    }

                    if (request.Method != BridgeMethod.WatchLogs || response.EndOfStream)
                    {
                        logger.LogDebug(
                            "Bridge TCP response frame sent to client: {Method}; request {RequestId}; status {Status}; end of stream {EndOfStream}; payload bytes {PayloadByteCount}.",
                            request.Method,
                            response.RequestId,
                            response.Status,
                            response.EndOfStream,
                            response.Payload.Length);
                    }

                    if (response.EndOfStream)
                    {
                        if (isStreamingRequest)
                        {
                            logger.LogDebug(
                                "Bridge TCP stream completed for client: {Method}; request {RequestId}; response frames {ResponseFrameCount}; payload frames {PayloadFrameCount}.",
                                request.Method,
                                response.RequestId,
                                responseFrameCount,
                                payloadFrameCount);
                        }

                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Bridge TCP client request failed.");
            }
        }
    }

    private static bool IsStreamingMethod(BridgeMethod method)
    {
        return method is BridgeMethod.WatchTree or BridgeMethod.WatchFrames or BridgeMethod.WatchLogs;
    }

    private void TrackClientTask(Task clientTask)
    {
        lock (sync)
        {
            clientTasks.Add(clientTask);
        }

        _ = clientTask.ContinueWith(
            task =>
            {
                lock (sync)
                {
                    clientTasks.Remove(task);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ValidateStartupOptions()
    {
        if (options.Port is < 0 or > IPEndPoint.MaxPort)
        {
            throw new InvalidOperationException("Bridge listener port must be a valid TCP port.");
        }

        if (options.RequireAuthentication && string.IsNullOrWhiteSpace(options.AuthenticationToken))
        {
            throw new InvalidOperationException(
                "Bridge listener requires an authentication token before startup.");
        }
    }

    private static async Task WaitWithoutCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
