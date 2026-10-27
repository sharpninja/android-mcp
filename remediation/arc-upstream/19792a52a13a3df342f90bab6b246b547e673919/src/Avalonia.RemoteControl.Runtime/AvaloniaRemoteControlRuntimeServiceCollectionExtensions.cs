using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Registers host-independent Avalonia.RemoteControl runtime services.
/// </summary>
public static class AvaloniaRemoteControlRuntimeServiceCollectionExtensions
{
    private static readonly string RemoteControlLoggerProviderName =
        typeof(Logging.RemoteControlLoggerProvider).FullName!;

    /// <summary>
    /// Adds the remote-control runtime services without registering a transport host.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddAvaloniaRemoteControlRuntime(
        this IServiceCollection services,
        Action<AvaloniaRemoteControlOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is null)
        {
            services.AddOptions<AvaloniaRemoteControlOptions>();
        }
        else
        {
            services.AddOptions<AvaloniaRemoteControlOptions>().Configure(configure);
        }

        services.AddLogging();
        services.Configure<LoggerFilterOptions>(options =>
        {
            if (!options.Rules.Any(static rule => string.Equals(
                rule.ProviderName,
                RemoteControlLoggerProviderName,
                StringComparison.Ordinal)))
            {
                options.Rules.Add(new LoggerFilterRule(
                    RemoteControlLoggerProviderName,
                    categoryName: null,
                    LogLevel.Debug,
                    filter: null));
            }
        });
        services.AddSingleton<AvaloniaRemoteControlService>();
        services.AddSingleton<IRemoteControlRootProvider, EmptyRemoteControlRootProvider>();
        services.AddSingleton<Security.RemoteControlStartupValidator>();
        services.AddSingleton<Security.RemoteControlBearerTokenAuthenticator>();
        services.AddSingleton<Logging.RemoteControlLogBuffer>();
        services.AddSingleton<Logging.RemoteControlLogStreamService>();
        services.AddSingleton<Logging.RemoteControlLoggerProvider>();
        services.AddSingleton<ILoggerProvider>(provider =>
            provider.GetRequiredService<Logging.RemoteControlLoggerProvider>());
        services.AddSingleton<Commands.RemoteControlActionInvoker>();
        services.AddSingleton<Commands.RemoteControlPropertyMutationService>();
        services.AddSingleton<Input.RemoteControlInputDispatcher>();
        services.AddSingleton<Rendering.IRemoteControlFrameProvider, Rendering.AvaloniaRenderTargetFrameProvider>();
        services.AddSingleton<Rendering.RemoteControlFrameStreamService>();
        services.AddSingleton<Runtime.IRemoteControlRuntime, Runtime.RemoteControlRuntime>();
        services.AddSingleton<Bridge.RemoteControlBridgeRequestHandler>();
        services.AddSingleton<Bridge.RemoteControlBridgeTcpListener>();
        services.AddSingleton<Threading.IRemoteControlDispatcher, Threading.AvaloniaUiThreadRemoteControlDispatcher>();
        services.AddSingleton<Snapshots.RemoteControlTreeStreamService>();
        services.AddSingleton<Snapshots.AvaloniaControlTreeSnapshotProvider>();
        services.AddSingleton<Snapshots.IControlTreeSnapshotProvider>(provider =>
            provider.GetRequiredService<Snapshots.AvaloniaControlTreeSnapshotProvider>());
        services.AddSingleton<Snapshots.IRemoteControlNodeResolver>(provider =>
            provider.GetRequiredService<Snapshots.AvaloniaControlTreeSnapshotProvider>());

        return services;
    }
}
