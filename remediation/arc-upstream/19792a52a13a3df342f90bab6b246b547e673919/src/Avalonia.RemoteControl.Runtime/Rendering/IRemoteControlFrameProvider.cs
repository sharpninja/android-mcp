using Avalonia.Controls;

namespace Avalonia.RemoteControl.Server.Rendering;

/// <summary>
/// Captures a live frame from a remote-control root control.
/// </summary>
public interface IRemoteControlFrameProvider
{
    /// <summary>
    /// Captures one frame from the root control.
    /// </summary>
    /// <param name="root">Root control to capture.</param>
    /// <param name="sequence">Frame sequence number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The captured frame.</returns>
    ValueTask<RemoteControlFrame> CaptureFrameAsync(
        Control root,
        ulong sequence,
        CancellationToken cancellationToken);
}
