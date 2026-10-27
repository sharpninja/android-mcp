using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.RemoteControl.Server.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Snapshots;

/// <summary>
/// Captures read-only control tree snapshots from Avalonia controls.
/// </summary>
public sealed class AvaloniaControlTreeSnapshotProvider : IControlTreeSnapshotProvider, IRemoteControlNodeResolver
{
    private readonly ConditionalWeakTable<Control, NodeIdentity> identities = new();
    private readonly ConcurrentDictionary<string, WeakReference<Control>> controlsById = new(StringComparer.Ordinal);
    private readonly AvaloniaRemoteControlOptions options;
    private readonly IRemoteControlDispatcher dispatcher;
    private ulong nextNodeId;
    private ulong nextSequence;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaControlTreeSnapshotProvider"/> class.
    /// </summary>
    public AvaloniaControlTreeSnapshotProvider()
        : this(
            Options.Create(new AvaloniaRemoteControlOptions()),
            new AvaloniaUiThreadRemoteControlDispatcher())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaControlTreeSnapshotProvider"/> class.
    /// </summary>
    /// <param name="options">Remote-control options that affect snapshot redaction.</param>
    /// <param name="dispatcher">Dispatcher used to access Avalonia controls safely.</param>
    public AvaloniaControlTreeSnapshotProvider(
        IOptions<AvaloniaRemoteControlOptions> options,
        IRemoteControlDispatcher dispatcher)
    {
        this.options = options.Value;
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public ValueTask<RemoteControlTreeSnapshot> CaptureSnapshotAsync(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return dispatcher.InvokeAsync(() =>
        {
            root = RemoteControlRootNormalizer.Normalize(root);
            var nodes = new List<RemoteControlNodeSnapshot>();
            CaptureNode(root, parentId: null, parentOffset: default, nodes);

            var sequence = Interlocked.Increment(ref nextSequence);
            return new RemoteControlTreeSnapshot(sequence, nodes);
        });
    }

    /// <inheritdoc />
    public bool TryResolve(string nodeId, out Control control)
    {
        if (controlsById.TryGetValue(nodeId, out var reference) && reference.TryGetTarget(out control!))
        {
            return true;
        }

        control = null!;
        return false;
    }

    private void CaptureNode(
        Control control,
        string? parentId,
        Point parentOffset,
        List<RemoteControlNodeSnapshot> nodes)
    {
        var identity = identities.GetValue(control, _ => new NodeIdentity($"node-{Interlocked.Increment(ref nextNodeId)}"));
        controlsById[identity.Id] = new WeakReference<Control>(control);
        var bounds = control.Bounds;
        var absoluteOffset = new Point(parentOffset.X + bounds.X, parentOffset.Y + bounds.Y);

        nodes.Add(new RemoteControlNodeSnapshot
        {
            Id = identity.Id,
            ParentId = parentId,
            TypeName = control.GetType().Name,
            Name = control.Name,
            AutomationName = AutomationProperties.GetName(control),
            AutomationId = AutomationProperties.GetAutomationId(control),
            Classes = control.Classes.ToArray(),
            Bounds = new RemoteControlRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            AbsoluteBounds = new RemoteControlRect(
                absoluteOffset.X,
                absoluteOffset.Y,
                bounds.Width,
                bounds.Height),
            IsVisible = control.IsVisible,
            IsEnabled = control.IsEnabled,
            IsFocused = control.IsFocused,
            Properties = CaptureProperties(control),
        });

        foreach (var child in control.GetVisualChildren().OfType<Control>())
        {
            CaptureNode(child, identity.Id, absoluteOffset, nodes);
        }
    }

    private IReadOnlyList<RemoteControlPropertySnapshot> CaptureProperties(Control control)
    {
        var properties = new List<RemoteControlPropertySnapshot>();

        foreach (var property in GetInspectableProperties(control.GetType()))
        {
            var isRedacted = IsSensitive(property.Name);
            var valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var isEnum = valueType.IsEnum;
            var value = isRedacted ? "[redacted]" : RenderValue(property.GetValue(control), valueType);

            properties.Add(new RemoteControlPropertySnapshot(
                property.Name,
                property.DeclaringType?.Name ?? control.GetType().Name,
                value,
                GetFriendlyTypeName(valueType),
                property.SetMethod is { IsPublic: true },
                isRedacted,
                isEnum,
                isEnum ? Enum.GetNames(valueType) : []));
        }

        return properties
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<PropertyInfo> GetInspectableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is { IsPublic: true })
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Where(static property => IsInspectableType(property.PropertyType));
    }

    private static bool IsInspectableType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        return actualType.IsEnum
            || actualType == typeof(string)
            || actualType == typeof(bool)
            || actualType == typeof(byte)
            || actualType == typeof(sbyte)
            || actualType == typeof(short)
            || actualType == typeof(ushort)
            || actualType == typeof(int)
            || actualType == typeof(uint)
            || actualType == typeof(long)
            || actualType == typeof(ulong)
            || actualType == typeof(float)
            || actualType == typeof(double)
            || actualType == typeof(decimal)
            || actualType == typeof(Guid)
            || actualType == typeof(TimeSpan)
            || actualType == typeof(DateTime)
            || actualType == typeof(DateTimeOffset)
            || actualType == typeof(Thickness)
            || actualType == typeof(CornerRadius)
            || actualType == typeof(Point)
            || actualType == typeof(Size)
            || actualType == typeof(Rect);
    }

    private bool IsSensitive(string propertyName)
    {
        return options.SensitiveNameFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderValue(object? value, Type valueType)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        return type.Name;
    }

    private sealed record NodeIdentity(string Id);
}
