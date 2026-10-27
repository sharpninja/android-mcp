namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Represents one control in a remote-control tree snapshot.
/// </summary>
public sealed record RemoteControlNodeSnapshot
{
    /// <summary>
    /// Gets the stable node ID for this server instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the parent stable node ID when this node has a parent in the snapshot.
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// Gets the concrete control type name.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets the Avalonia control name when present.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the automation name when present.
    /// </summary>
    public string? AutomationName { get; init; }

    /// <summary>
    /// Gets the automation ID when present.
    /// </summary>
    public string? AutomationId { get; init; }

    /// <summary>
    /// Gets the CSS-style class names assigned to the control.
    /// </summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>
    /// Gets the current control bounds.
    /// </summary>
    public required RemoteControlRect Bounds { get; init; }

    /// <summary>
    /// Gets the current control bounds relative to the remote-control root.
    /// </summary>
    public required RemoteControlRect AbsoluteBounds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the control is visible.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Gets a value indicating whether the control is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the control currently has focus.
    /// </summary>
    public bool IsFocused { get; init; }

    /// <summary>
    /// Gets the safe read-only property snapshots for the control.
    /// </summary>
    public IReadOnlyList<RemoteControlPropertySnapshot> Properties { get; init; } = [];
}
