namespace Avalonia.RemoteControl.Server.Threading;

/// <summary>
/// Executes remote-control work inline. Intended for tests and non-running design-time probes.
/// </summary>
public sealed class InlineRemoteControlDispatcher : IRemoteControlDispatcher
{
    /// <inheritdoc />
    public ValueTask<T> InvokeAsync<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ValueTask.FromResult(operation());
    }
}
