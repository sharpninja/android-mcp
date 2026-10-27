using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.RemoteControl.Client;
using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server;
using Avalonia.RemoteControl.Server.Bridge;
using Avalonia.RemoteControl.Server.Commands;
using Avalonia.RemoteControl.Server.Logging;
using Avalonia.RemoteControl.Server.Rendering;
using Avalonia.RemoteControl.Server.Runtime;
using Avalonia.RemoteControl.Server.Snapshots;
using Avalonia.RemoteControl.Server.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.RemoteControl.Tests;

public sealed class RemoteControlBridgeTcpListenerTests
{
    [Fact]
    public async Task BridgeTcpListenerBindsLoopbackAndAcceptsAuthenticatedCapabilitiesRequest()
    {
        await using var provider = CreateProvider(new TextBlock { Name = "ListenerRoot" });
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            Assert.True(listener.IsRunning);
            Assert.NotNull(listener.BoundEndpoint);
            Assert.Equal(IPAddress.Loopback, listener.BoundEndpoint!.Address);

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);

            var capabilities = await session.GetCapabilitiesAsync();

            Assert.Equal(RemoteControlProtocol.DisplayVersion, capabilities.ProtocolVersion);
            Assert.True(capabilities.SupportsTreeSnapshots);
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [Fact]
    public async Task BridgeTcpListenerCapturesSnapshotThroughRuntime()
    {
        await using var provider = CreateProvider(new StubRemoteControlRuntime());
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);

            var snapshot = await session.GetSnapshotAsync();

            Assert.Contains(snapshot.Nodes, node => node.Name == "ListenerSnapshotRoot");
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [Fact]
    public async Task BridgeClientReportsClosedBridgeSocketAsDiagnostic()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var request = await BridgeFrameCodec.ReadAsync(stream, BridgeRequest.Parser);

            Assert.Equal(BridgeMethod.GetCapabilities, request.Method);
        });

        try
        {
            using var session = RemoteControlDesktopSession.Create(
                new Uri($"http://127.0.0.1:{endpoint.Port}"),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.GetCapabilitiesAsync());

            Assert.Contains("closed before a complete response", exception.Message, StringComparison.Ordinal);
            Assert.IsType<EndOfStreamException>(exception.InnerException);
            await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task BridgeTcpListenerStreamsFramesThroughRuntime()
    {
        await using var provider = CreateProvider(new StubRemoteControlRuntime());
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);
            await using var enumerator = session.WatchFramesAsync().GetAsyncEnumerator();

            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal(1UL, enumerator.Current.Sequence);
            Assert.Equal(1, enumerator.Current.PixelWidth);
            Assert.False(await enumerator.MoveNextAsync());
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [Fact]
    public async Task BridgeTcpListenerStreamsTreeThroughRuntime()
    {
        await using var provider = CreateProvider(new StubRemoteControlRuntime());
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);
            await using var enumerator = session.WatchTreeAsync().GetAsyncEnumerator();

            Assert.True(await enumerator.MoveNextAsync());
            Assert.Contains(enumerator.Current.Snapshot.Nodes, node => node.Name == "ListenerSnapshotRoot");
            Assert.False(await enumerator.MoveNextAsync());
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [Fact]
    public async Task BridgeTcpListenerLogsDebugFrameLifecycle()
    {
        var loggerProvider = new CapturingLoggerProvider();
        await using var provider = CreateProvider(new StubRemoteControlRuntime(), loggerProvider);
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);

            await session.GetCapabilitiesAsync();
        }
        finally
        {
            await listener.StopAsync();
        }

        var entries = loggerProvider.Entries.ToArray();
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP client accepted", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP request frame received from client: GetCapabilities", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP response frame sent to client: GetCapabilities", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BridgeTcpListenerDoesNotLogEachWatchLogsResponseFrame()
    {
        var loggerProvider = new CapturingLoggerProvider();
        await using var provider = CreateProvider(
            new StubRemoteControlRuntime(
                [
                    new RemoteControlLogEntry
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Level = LogLevel.Information,
                        Category = "FunWasHad",
                        Message = "first",
                    },
                    new RemoteControlLogEntry
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Level = LogLevel.Warning,
                        Category = "FunWasHad",
                        Message = "second",
                    },
                ]),
            loggerProvider);
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                CreateBridgeUri(listener),
                "dev-token",
                transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);
            await using var enumerator = session.WatchLogsAsync("Debug", null).GetAsyncEnumerator();

            Assert.True(await enumerator.MoveNextAsync());
            Assert.True(await enumerator.MoveNextAsync());
            Assert.False(await enumerator.MoveNextAsync());
        }
        finally
        {
            await listener.StopAsync();
        }

        var entries = loggerProvider.Entries.ToArray();
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP request frame received from client: WatchLogs", StringComparison.Ordinal));
        Assert.Contains(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP stream completed for client: WatchLogs", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry =>
            entry.Level == LogLevel.Debug &&
            entry.Message.Contains("Bridge TCP response frame sent to client: WatchLogs", StringComparison.Ordinal) &&
            entry.Message.Contains("end of stream False", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BridgeEndpointMarkerWritesPackagePrivateJson()
    {
        var marker = RemoteControlBridgeEndpointMarker.Create(47123, "marker-token");
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var markerPath = await marker.WriteAsync(directory);
            var json = await File.ReadAllTextAsync(markerPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(
                Path.Combine(directory, RemoteControlBridgeEndpointMarker.FileName),
                markerPath);
            Assert.Equal("1", root.GetProperty("schemaVersion").GetString());
            Assert.Equal(47123, root.GetProperty("devicePort").GetInt32());
            Assert.Equal("marker-token", root.GetProperty("token").GetString());
            Assert.Equal(
                RemoteControlProtocol.AndroidBridgeTransportProtocol,
                root.GetProperty("bridgeProtocol").GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BridgeTcpListenerCreatesMarkerFromBoundEndpoint()
    {
        await using var provider = CreateProvider(new TextBlock());
        var listener = provider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await listener.StartAsync();

            var marker = listener.CreateEndpointMarker();

            Assert.Equal(listener.BoundEndpoint!.Port, marker.DevicePort);
            Assert.Equal("dev-token", marker.Token);
            Assert.Equal(RemoteControlProtocol.AndroidBridgeTransportProtocol, marker.BridgeProtocol);
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [Fact]
    public async Task TwoAndroidBridgeListenersSelectDistinctPorts()
    {
        await using var firstProvider = CreateProvider(new TextBlock());
        await using var secondProvider = CreateProvider(new TextBlock());
        var first = firstProvider.GetRequiredService<RemoteControlBridgeTcpListener>();
        var second = secondProvider.GetRequiredService<RemoteControlBridgeTcpListener>();

        try
        {
            await first.StartAsync();
            await second.StartAsync();

            Assert.NotEqual(first.BoundEndpoint!.Port, second.BoundEndpoint!.Port);
            Assert.Equal(first.BoundEndpoint.Port, first.CreateEndpointMarker().DevicePort);
            Assert.Equal(second.BoundEndpoint.Port, second.CreateEndpointMarker().DevicePort);
        }
        finally
        {
            await second.StopAsync();
            await first.StopAsync();
        }
    }

    private static Uri CreateBridgeUri(RemoteControlBridgeTcpListener listener)
    {
        return new Uri($"http://127.0.0.1:{listener.BoundEndpoint!.Port}");
    }

    private static ServiceProvider CreateProvider(Control root)
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControlRuntime(options =>
        {
            options.AuthenticationToken = "dev-token";
            options.Port = 0;
            options.IsAdbTunnel = true;
        });
        services.AddSingleton<IRemoteControlDispatcher, InlineRemoteControlDispatcher>();
        services.AddSingleton<IRemoteControlRootProvider>(new StaticRemoteControlRootProvider(root));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateProvider(
        IRemoteControlRuntime runtime,
        ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControlRuntime(options =>
        {
            options.AuthenticationToken = "dev-token";
            options.Port = 0;
            options.IsAdbTunnel = true;
        });
        services.AddSingleton(runtime);
        if (loggerProvider is not null)
        {
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(loggerProvider);
            });
        }

        return services.BuildServiceProvider();
    }

    private sealed class StubRemoteControlRuntime : IRemoteControlRuntime
    {
        private readonly IReadOnlyList<RemoteControlLogEntry> logEntries;

        public StubRemoteControlRuntime(IReadOnlyList<RemoteControlLogEntry>? logEntries = null)
        {
            this.logEntries = logEntries ?? [];
        }

        public RemoteControlCapabilities GetCapabilities()
        {
            return new RemoteControlCapabilities();
        }

        public ValueTask<RemoteControlTreeSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new RemoteControlTreeSnapshot(
                1,
                [
                    new RemoteControlNodeSnapshot
                    {
                        Id = "node-1",
                        TypeName = nameof(TextBlock),
                        Name = "ListenerSnapshotRoot",
                        Bounds = new RemoteControlRect(0, 0, 100, 32),
                        AbsoluteBounds = new RemoteControlRect(0, 0, 100, 32),
                        IsVisible = true,
                        IsEnabled = true,
                    },
                ]));
        }

        public async IAsyncEnumerable<RemoteControlTreeSnapshot> WatchSnapshotsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<RemoteControlCommandResult> InvokeClickAsync(
            string nodeId,
            string clientIdentity,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new RemoteControlCommandResult(false, "Not implemented."));
        }

        public ValueTask<RemoteControlCommandResult> InvokeFocusAsync(
            string nodeId,
            string clientIdentity,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new RemoteControlCommandResult(false, "Not implemented."));
        }

        public ValueTask<RemoteControlCommandResult> SetPropertyAsync(
            string nodeId,
            string propertyName,
            string value,
            string clientIdentity,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new RemoteControlCommandResult(false, "Not implemented."));
        }

        public async IAsyncEnumerable<RemoteControlFrame> WatchFramesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new RemoteControlFrame(
                1,
                [1, 2, 3],
                1,
                1,
                1,
                1,
                1,
                DateTimeOffset.UtcNow);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public ValueTask<RemoteControlCommandResult> SendInputAsync(
            IReadOnlyList<RemoteInputEvent> events,
            string clientIdentity,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new RemoteControlCommandResult(true, "Input accepted."));
        }

        public async IAsyncEnumerable<RemoteControlLogEntry> WatchLogEntriesAsync(
            LogLevel minimumLevel,
            string? categoryPrefix,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var entry in logEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return entry;
            }
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLogEntry> entries = new();

        public IEnumerable<CapturedLogEntry> Entries => entries;

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(categoryName, entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        string categoryName,
        ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new CapturedLogEntry(categoryName, logLevel, formatter(state, exception)));
        }
    }

    private sealed record CapturedLogEntry(string Category, LogLevel Level, string Message);
}
