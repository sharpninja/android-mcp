using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Resolves stable remote-control node IDs back to live Avalonia controls.
/// </summary>
public interface IRemoteControlNodeResolver
{
    /// <summary>
    /// Attempts to resolve a node ID to a live control.
    /// </summary>
    /// <param name="nodeId">The stable node ID.</param>
    /// <param name="control">The resolved control when found.</param>
    /// <returns><see langword="true" /> when the control is still available.</returns>
    bool TryResolve(string nodeId, out Control control);
}
