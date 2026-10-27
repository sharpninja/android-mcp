namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Represents a control bounds rectangle in device-independent pixels.
/// </summary>
/// <param name="X">The rectangle X coordinate.</param>
/// <param name="Y">The rectangle Y coordinate.</param>
/// <param name="Width">The rectangle width.</param>
/// <param name="Height">The rectangle height.</param>
public sealed record RemoteControlRect(double X, double Y, double Width, double Height);
