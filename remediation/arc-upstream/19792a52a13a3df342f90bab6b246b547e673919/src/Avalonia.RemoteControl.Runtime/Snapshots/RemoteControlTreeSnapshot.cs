namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Represents a point-in-time read-only remote-control tree snapshot.
/// </summary>
/// <param name="Sequence">The snapshot sequence number.</param>
/// <param name="Nodes">The flattened tree nodes in traversal order.</param>
public sealed record RemoteControlTreeSnapshot(ulong Sequence, IReadOnlyList<RemoteControlNodeSnapshot> Nodes);
