using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server;

/// <summary>
/// Provides the root control used by the remote-control server for tree snapshots.
/// </summary>
public interface IRemoteControlRootProvider
{
    /// <summary>
    /// Gets the current root control, or <see langword="null" /> when the app is not ready.
    /// </summary>
    /// <returns>The root control to inspect, or <see langword="null" />.</returns>
    Control? GetRootControl();
}
