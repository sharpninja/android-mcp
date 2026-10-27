using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Default root provider used when the host app has not registered a root control.
/// </summary>
public sealed class EmptyRemoteControlRootProvider : IRemoteControlRootProvider
{
    /// <inheritdoc />
    public Control? GetRootControl()
    {
        return null;
    }
}
