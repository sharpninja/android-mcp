using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Provides a fixed root control for simple host integrations and tests.
/// </summary>
public sealed class StaticRemoteControlRootProvider : IRemoteControlRootProvider
{
    private readonly Control root;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticRemoteControlRootProvider"/> class.
    /// </summary>
    /// <param name="root">The root control to expose.</param>
    public StaticRemoteControlRootProvider(Control root)
    {
        this.root = root;
    }

    /// <inheritdoc />
    public Control? GetRootControl()
    {
        return root;
    }
}
