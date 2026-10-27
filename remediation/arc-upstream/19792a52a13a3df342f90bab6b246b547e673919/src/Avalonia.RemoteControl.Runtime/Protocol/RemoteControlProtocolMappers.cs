using System.Globalization;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server.Commands;
using Avalonia.RemoteControl.Server.Logging;
using Avalonia.RemoteControl.Server.Snapshots;
using ProtocolLogEntry = Avalonia.RemoteControl.Protocol.V1.LogEntry;
using ProtocolRect = Avalonia.RemoteControl.Protocol.V1.Rect;

namespace Avalonia.RemoteControl.Server.Protocol;

/// <summary>
/// Maps transport-independent runtime models to protobuf protocol messages.
/// </summary>
public static class RemoteControlProtocolMappers
{
    /// <summary>
    /// Maps runtime capabilities to the protobuf capabilities response.
    /// </summary>
    /// <param name="capabilities">Runtime capabilities.</param>
    /// <returns>The protobuf capabilities response.</returns>
    public static GetCapabilitiesResponse ToProtocol(this RemoteControlCapabilities capabilities)
    {
        return new GetCapabilitiesResponse
        {
            ProtocolVersion = capabilities.ProtocolVersion,
            AuthenticatedClientIdentity = capabilities.AuthenticatedClientIdentity,
            SupportsTreeSnapshots = capabilities.SupportsTreeSnapshots,
            SupportsTreeStreaming = capabilities.SupportsTreeStreaming,
            SupportsClickInvocation = capabilities.SupportsClickInvocation,
            SupportsPropertyMutation = capabilities.SupportsPropertyMutation,
            SupportsLogStreaming = capabilities.SupportsLogStreaming,
            SupportsFrameStreaming = capabilities.SupportsFrameStreaming,
            SupportsRemoteInput = capabilities.SupportsRemoteInput,
        };
    }

    /// <summary>
    /// Maps a runtime tree snapshot to the protobuf tree snapshot.
    /// </summary>
    /// <param name="snapshot">Runtime tree snapshot.</param>
    /// <returns>The protobuf tree snapshot.</returns>
    public static TreeSnapshot ToProtocol(this RemoteControlTreeSnapshot snapshot)
    {
        var response = new TreeSnapshot
        {
            Sequence = snapshot.Sequence,
        };

        response.Nodes.AddRange(snapshot.Nodes.Select(ToProtocol));

        return response;
    }

    /// <summary>
    /// Maps a runtime command result to the protobuf command result.
    /// </summary>
    /// <param name="result">Runtime command result.</param>
    /// <returns>The protobuf command result.</returns>
    public static CommandResult ToProtocol(this RemoteControlCommandResult result)
    {
        return new CommandResult
        {
            Succeeded = result.Succeeded,
            Message = result.Message,
        };
    }

    /// <summary>
    /// Maps a runtime log entry to the protobuf log entry.
    /// </summary>
    /// <param name="entry">Runtime log entry.</param>
    /// <returns>The protobuf log entry.</returns>
    public static ProtocolLogEntry ToProtocol(this RemoteControlLogEntry entry)
    {
        return new ProtocolLogEntry
        {
            Sequence = entry.Sequence,
            TimestampUtc = entry.TimestampUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Level = entry.Level.ToString(),
            Category = entry.Category,
            EventId = entry.EventId,
            Message = entry.Message,
            ExceptionSummary = entry.ExceptionSummary,
            DroppedCount = entry.DroppedCount,
            StructuredState = entry.StructuredState,
            ScopeSummary = entry.ScopeSummary,
        };
    }

    private static TreeNode ToProtocol(RemoteControlNodeSnapshot node)
    {
        var response = new TreeNode
        {
            Id = node.Id,
            ParentId = node.ParentId ?? string.Empty,
            TypeName = node.TypeName,
            Name = node.Name ?? string.Empty,
            AutomationName = node.AutomationName ?? string.Empty,
            AutomationId = node.AutomationId ?? string.Empty,
            Bounds = new ProtocolRect
            {
                X = node.Bounds.X,
                Y = node.Bounds.Y,
                Width = node.Bounds.Width,
                Height = node.Bounds.Height,
            },
            AbsoluteBounds = new ProtocolRect
            {
                X = node.AbsoluteBounds.X,
                Y = node.AbsoluteBounds.Y,
                Width = node.AbsoluteBounds.Width,
                Height = node.AbsoluteBounds.Height,
            },
            IsVisible = node.IsVisible,
            IsEnabled = node.IsEnabled,
            IsFocused = node.IsFocused,
        };

        response.Classes.AddRange(node.Classes);
        response.Properties.AddRange(node.Properties.Select(ToProtocol));

        return response;
    }

    private static PropertyValue ToProtocol(RemoteControlPropertySnapshot property)
    {
        var response = new PropertyValue
        {
            Name = property.Name,
            DeclaringType = property.DeclaringType,
            Value = property.Value,
            ValueType = property.ValueType,
            CanWrite = property.CanWrite,
            IsRedacted = property.IsRedacted,
            IsEnum = property.IsEnum,
        };

        response.EnumValues.AddRange(property.EnumValues ?? []);
        return response;
    }
}
