using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Logging;

/// <summary>
/// Captures application ILogger entries for remote-control streaming.
/// </summary>
public sealed class RemoteControlLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly RemoteControlLogBuffer buffer;
    private readonly RemoteControlLogRedactor redactor;
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlLoggerProvider"/> class.
    /// </summary>
    /// <param name="buffer">The remote-control log buffer.</param>
    /// <param name="options">Remote-control options.</param>
    public RemoteControlLoggerProvider(
        RemoteControlLogBuffer buffer,
        IOptions<AvaloniaRemoteControlOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.buffer = buffer;
        redactor = new RemoteControlLogRedactor(options.Value.SensitiveNameFragments);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new RemoteControlLogger(
            categoryName,
            buffer,
            redactor,
            () => scopeProvider);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
