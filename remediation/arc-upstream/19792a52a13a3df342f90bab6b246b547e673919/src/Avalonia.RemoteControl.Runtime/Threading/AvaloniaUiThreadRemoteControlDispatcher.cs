using Avalonia.Threading;

namespace Avalonia.RemoteControl.Server.Threading;

/// <summary>
/// Executes remote-control work through <see cref="Dispatcher.UIThread"/>.
/// </summary>
public sealed class AvaloniaUiThreadRemoteControlDispatcher : IRemoteControlDispatcher
{
    /// <inheritdoc />
    public async ValueTask<T> InvokeAsync<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Dispatcher.UIThread.CheckAccess())
        {
            return operation();
        }

        return await Dispatcher.UIThread.InvokeAsync(operation);
    }
}
