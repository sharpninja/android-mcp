namespace Avalonia.RemoteControl.Server.Threading;

/// <summary>
/// Executes remote-control work on the appropriate Avalonia UI dispatcher.
/// </summary>
public interface IRemoteControlDispatcher
{
    /// <summary>
    /// Invokes an operation on the dispatcher.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The operation result.</returns>
    ValueTask<T> InvokeAsync<T>(Func<T> operation);
}
