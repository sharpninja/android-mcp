using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Captures read-only Avalonia control tree snapshots.
/// </summary>
public interface IControlTreeSnapshotProvider
{
    /// <summary>
    /// Captures a snapshot rooted at the specified control.
    /// </summary>
    /// <param name="root">The root control to inspect.</param>
    /// <returns>A read-only snapshot of the control tree.</returns>
    ValueTask<RemoteControlTreeSnapshot> CaptureSnapshotAsync(Control root);
}
