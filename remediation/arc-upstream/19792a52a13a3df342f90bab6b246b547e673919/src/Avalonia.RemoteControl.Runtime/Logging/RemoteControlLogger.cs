using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Avalonia.RemoteControl.Server.Logging;

internal sealed class RemoteControlLogger : ILogger
{
    private readonly string categoryName;
    private readonly RemoteControlLogBuffer buffer;
    private readonly RemoteControlLogRedactor redactor;
    private readonly Func<IExternalScopeProvider> scopeProviderAccessor;

    public RemoteControlLogger(
        string categoryName,
        RemoteControlLogBuffer buffer,
        RemoteControlLogRedactor redactor,
        Func<IExternalScopeProvider> scopeProviderAccessor)
    {
        this.categoryName = categoryName;
        this.buffer = buffer;
        this.redactor = redactor;
        this.scopeProviderAccessor = scopeProviderAccessor;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return scopeProviderAccessor().Push(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        buffer.Publish(new RemoteControlLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = logLevel,
            Category = categoryName,
            EventId = eventId.Id,
            Message = redactor.RedactText(formatter(state, exception)),
            StructuredState = CreateStructuredState(state),
            ScopeSummary = CreateScopeSummary(),
            ExceptionSummary = CreateExceptionSummary(exception),
        });
    }

    private string CreateStructuredState<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return string.Empty;
        }

        return string.Join(
            ", ",
            values
                .Where(value => value.Key != "{OriginalFormat}")
                .Select(value => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{value.Key}={redactor.RedactStructuredValue(value.Key, value.Value)}")));
    }

    private string CreateScopeSummary()
    {
        var scopes = new List<string>();

        scopeProviderAccessor().ForEachScope(
            (scope, state) =>
            {
                var sanitized = redactor.RedactText(scope?.ToString());

                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    state.Add(sanitized);
                }
            },
            scopes);

        return string.Join(" => ", scopes);
    }

    private string CreateExceptionSummary(Exception? exception)
    {
        return exception is null
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{exception.GetType().Name}: {redactor.RedactText(exception.Message)}");
    }
}
