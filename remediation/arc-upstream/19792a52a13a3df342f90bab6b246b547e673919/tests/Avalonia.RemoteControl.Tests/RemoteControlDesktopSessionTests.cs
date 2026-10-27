using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Avalonia.RemoteControl.Client;
using Avalonia.RemoteControl.Client.Logging;
using Avalonia.RemoteControl.Client.Security;
using Avalonia.RemoteControl.Protocol;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server;
using Avalonia.RemoteControl.Server.Bridge;
using Avalonia.RemoteControl.Server.Hosting;
using Avalonia.RemoteControl.Server.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Tests;

public sealed class RemoteControlDesktopSessionTests
{
    [Fact]
    public async Task DesktopSessionReadsCapabilitiesFromHostedServer()
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
            options.AuthenticatedClientIdentity = "desktop-client";
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();

        try
        {
            await host.StartAsync();

            using var session = RemoteControlDesktopSession.Create(host.BoundAddress!, "dev-token");
            var capabilities = await session.GetCapabilitiesAsync();

            Assert.Equal(RemoteControlProtocol.DisplayVersion, capabilities.ProtocolVersion);
            Assert.Equal("desktop-client", capabilities.AuthenticatedClientIdentity);
            Assert.True(capabilities.SupportsTreeSnapshots);
            Assert.True(capabilities.SupportsLogStreaming);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task DesktopSessionStreamsHostedLogs()
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();
        var logBuffer = provider.GetRequiredService<RemoteControlLogBuffer>();

        try
        {
            await host.StartAsync();
            logBuffer.Publish(new RemoteControlLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = LogLevel.Information,
                Category = "Hosted.Test",
                EventId = 42,
                Message = "streamed",
            });

            using var session = RemoteControlDesktopSession.Create(host.BoundAddress!, "dev-token");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = session.WatchLogsAsync("Information", "Hosted", cts.Token)
                .GetAsyncEnumerator(cts.Token);

            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal("streamed", enumerator.Current.Message);
            Assert.Equal("Hosted.Test", enumerator.Current.Category);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task DesktopSessionFeedsDefaultWarningLogsIntoClientLogView()
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();
        var logBuffer = provider.GetRequiredService<RemoteControlLogBuffer>();
        var logView = new RemoteLogViewModel();

        try
        {
            await host.StartAsync();
            logBuffer.Publish(new RemoteControlLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = LogLevel.Information,
                Category = "Hosted.Test",
                EventId = 1,
                Message = "not visible at warning",
            });
            logBuffer.Publish(new RemoteControlLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = LogLevel.Warning,
                Category = "Hosted.Test",
                EventId = 2,
                Message = "warning visible",
            });

            using var session = RemoteControlDesktopSession.Create(host.BoundAddress!, "dev-token");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var enumerator = session.WatchLogsAsync(
                    logView.SelectedVerbosity.MinimumLevelName,
                    "Hosted",
                    cts.Token)
                .GetAsyncEnumerator(cts.Token);

            Assert.Equal(LogLevel.Warning, logView.SelectedVerbosity.MinimumLevel);
            Assert.True(await enumerator.MoveNextAsync());
            logView.Rows.Add(RemoteLogDisplayFormatter.Format(enumerator.Current));

            var row = Assert.Single(logView.Rows);
            Assert.Contains("warning visible", row, StringComparison.Ordinal);
            Assert.DoesNotContain("not visible", row, StringComparison.Ordinal);
            Assert.Equal(1, logView.RemoteEntryCount);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task DesktopSessionTrustsConfiguredTlsCertificate()
    {
        var certificatePath = Path.Combine(Path.GetTempPath(), $"arc-{Guid.NewGuid():N}.pfx");
        var trustPath = Path.Combine(Path.GetTempPath(), $"arc-{Guid.NewGuid():N}.cer");
        const string certificatePassword = "test-password";

        var certificateBytes = CreateCertificateBytes(certificatePassword);
        await File.WriteAllBytesAsync(
            certificatePath,
            certificateBytes.Pfx);
        await File.WriteAllBytesAsync(
            trustPath,
            certificateBytes.Cert);

        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
            options.AllowCleartextForLoopbackOrAdb = false;
            options.TlsCertificatePath = certificatePath;
            options.TlsCertificatePassword = certificatePassword;
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();

        try
        {
            await host.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                host.BoundAddress!,
                "dev-token",
                trustPath);

            var capabilities = await session.GetCapabilitiesAsync();

            Assert.Equal(RemoteControlProtocol.DisplayVersion, capabilities.ProtocolVersion);
        }
        finally
        {
            await host.StopAsync();
            File.Delete(certificatePath);
            File.Delete(trustPath);
        }
    }

    [Fact]
    public async Task DesktopSessionTrustsAcceptedTlsCertificateFingerprint()
    {
        var certificatePath = Path.Combine(Path.GetTempPath(), $"arc-{Guid.NewGuid():N}.pfx");
        const string certificatePassword = "test-password";

        var certificateBytes = CreateCertificateBytes(certificatePassword);
        await File.WriteAllBytesAsync(
            certificatePath,
            certificateBytes.Pfx);

        using var trustedCertificate =
            X509CertificateLoader.LoadCertificate(certificateBytes.Cert);
        var acceptedFingerprint =
            RemoteControlServerCertificateTrust.GetSha256Fingerprint(trustedCertificate);

        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
            options.AllowCleartextForLoopbackOrAdb = false;
            options.TlsCertificatePath = certificatePath;
            options.TlsCertificatePassword = certificatePassword;
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();

        try
        {
            await host.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                host.BoundAddress!,
                "dev-token",
                acceptedServerCertificateSha256Fingerprint: acceptedFingerprint);

            var capabilities = await session.GetCapabilitiesAsync();

            Assert.Equal(RemoteControlProtocol.DisplayVersion, capabilities.ProtocolVersion);
        }
        finally
        {
            await host.StopAsync();
            File.Delete(certificatePath);
        }
    }

    [Fact]
    public async Task DesktopSessionRejectsMismatchedAcceptedTlsCertificateFingerprint()
    {
        var certificatePath = Path.Combine(Path.GetTempPath(), $"arc-{Guid.NewGuid():N}.pfx");
        const string certificatePassword = "test-password";

        var certificateBytes = CreateCertificateBytes(certificatePassword);
        await File.WriteAllBytesAsync(
            certificatePath,
            certificateBytes.Pfx);

        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
            options.AllowCleartextForLoopbackOrAdb = false;
            options.TlsCertificatePath = certificatePath;
            options.TlsCertificatePassword = certificatePassword;
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();

        try
        {
            await host.StartAsync();

            using var session = RemoteControlDesktopSession.Create(
                host.BoundAddress!,
                "dev-token",
                acceptedServerCertificateSha256Fingerprint:
                    "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

            await Assert.ThrowsAnyAsync<Exception>(() => session.GetCapabilitiesAsync());
        }
        finally
        {
            await host.StopAsync();
            File.Delete(certificatePath);
        }
    }

    [Fact]
    public async Task CertificateInspectorReadsServerCertificateFingerprint()
    {
        var certificatePath = Path.Combine(Path.GetTempPath(), $"arc-{Guid.NewGuid():N}.pfx");
        const string certificatePassword = "test-password";

        var certificateBytes = CreateCertificateBytes(certificatePassword);
        await File.WriteAllBytesAsync(
            certificatePath,
            certificateBytes.Pfx);

        using var trustedCertificate =
            X509CertificateLoader.LoadCertificate(certificateBytes.Cert);
        var expectedFingerprint =
            RemoteControlServerCertificateTrust.GetSha256Fingerprint(trustedCertificate);

        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControl(options =>
        {
            options.IsEnabled = true;
            options.Host = IPAddress.Loopback;
            options.Port = 0;
            options.AuthenticationToken = "dev-token";
            options.AllowCleartextForLoopbackOrAdb = false;
            options.TlsCertificatePath = certificatePath;
            options.TlsCertificatePassword = certificatePassword;
        });

        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<AvaloniaRemoteControlServerHost>();

        try
        {
            await host.StartAsync();

            var certificateInfo =
                await RemoteControlServerCertificateInspector.InspectAsync(host.BoundAddress!);

            Assert.Equal(expectedFingerprint, certificateInfo.Sha256Fingerprint);
            Assert.Contains("CN=localhost", certificateInfo.Subject, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
            File.Delete(certificatePath);
        }
    }

    [Fact]
    public async Task DesktopSessionReadsCapabilitiesFromBridgeTransport()
    {
        var services = new ServiceCollection();
        services.AddAvaloniaRemoteControlRuntime(options =>
        {
            options.AuthenticationToken = "dev-token";
            options.AuthenticatedClientIdentity = "bridge-client";
        });

        await using var provider = services.BuildServiceProvider();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var endpoint = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}");
        var serverTask = HandleSingleBridgeRequestAsync(
            listener,
            provider.GetRequiredService<RemoteControlBridgeRequestHandler>());

        using var session = RemoteControlDesktopSession.Create(
            endpoint,
            "dev-token",
            transportProtocol: RemoteControlProtocol.AndroidBridgeTransportProtocol);

        var capabilities = await session.GetCapabilitiesAsync();

        await serverTask;

        Assert.Equal(RemoteControlProtocol.DisplayVersion, capabilities.ProtocolVersion);
        Assert.Equal("bridge-client", capabilities.AuthenticatedClientIdentity);
        Assert.True(capabilities.SupportsTreeSnapshots);
    }

    private static (byte[] Pfx, byte[] Cert) CreateCertificateBytes(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));

        return (
            certificate.Export(X509ContentType.Pfx, password),
            certificate.Export(X509ContentType.Cert));
    }

    private static async Task HandleSingleBridgeRequestAsync(
        TcpListener listener,
        RemoteControlBridgeRequestHandler handler)
    {
        using var tcpClient = await listener.AcceptTcpClientAsync();
        await using var stream = tcpClient.GetStream();
        var request = await BridgeFrameCodec.ReadAsync(stream, BridgeRequest.Parser);
        var response = await handler.HandleAsync(request);
        await BridgeFrameCodec.WriteAsync(stream, response);
        listener.Stop();
    }
}
