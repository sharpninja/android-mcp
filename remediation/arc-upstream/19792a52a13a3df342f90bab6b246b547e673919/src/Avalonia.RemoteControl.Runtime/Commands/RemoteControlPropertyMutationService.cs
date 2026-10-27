using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.RemoteControl.Server.Snapshots;
using Avalonia.RemoteControl.Server.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Commands;

/// <summary>
/// Applies safe, policy-approved property mutations to live controls.
/// </summary>
public sealed class RemoteControlPropertyMutationService
{
    private readonly IRemoteControlNodeResolver nodeResolver;
    private readonly AvaloniaRemoteControlOptions options;
    private readonly IRemoteControlDispatcher dispatcher;
    private readonly ILogger<RemoteControlPropertyMutationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlPropertyMutationService"/> class.
    /// </summary>
    /// <param name="nodeResolver">Resolver for stable node IDs.</param>
    /// <param name="options">Remote-control options.</param>
    /// <param name="dispatcher">Dispatcher used to access controls safely.</param>
    /// <param name="logger">Audit logger.</param>
    public RemoteControlPropertyMutationService(
        IRemoteControlNodeResolver nodeResolver,
        IOptions<AvaloniaRemoteControlOptions> options,
        IRemoteControlDispatcher dispatcher,
        ILogger<RemoteControlPropertyMutationService> logger)
    {
        this.nodeResolver = nodeResolver;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>
    /// Sets a policy-approved public property on a live control.
    /// </summary>
    /// <param name="nodeId">The stable node ID.</param>
    /// <param name="propertyName">The public property name.</param>
    /// <param name="value">The requested string value.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <returns>The sanitized command result.</returns>
    public ValueTask<RemoteControlCommandResult> SetPropertyAsync(
        string nodeId,
        string propertyName,
        string value,
        string clientIdentity = "unknown")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return dispatcher.InvokeAsync(() => SetProperty(nodeId, propertyName, value, clientIdentity));
    }

    private RemoteControlCommandResult SetProperty(
        string nodeId,
        string propertyName,
        string value,
        string clientIdentity)
    {
        if (!nodeResolver.TryResolve(nodeId, out var control))
        {
            logger.LogWarning(
                "Remote property mutation rejected for stale node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Node is no longer available.");
        }

        if (IsSensitive(propertyName))
        {
            logger.LogWarning(
                "Remote property mutation blocked for sensitive property {PropertyName} on node {NodeId} from {ClientIdentity}",
                propertyName,
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Property is blocked by redaction policy.");
        }

        if (!IsPropertyAllowed(control.GetType(), propertyName))
        {
            logger.LogWarning(
                "Remote property mutation denied by policy for {ControlType}.{PropertyName} on node {NodeId} from {ClientIdentity}",
                control.GetType().Name,
                propertyName,
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Property mutation is not allowed by policy.");
        }

        var property = control.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        if (property is null || property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length != 0)
        {
            return new RemoteControlCommandResult(false, "Property is not publicly writable.");
        }

        if (!TryConvert(value, property.PropertyType, out var convertedValue))
        {
            return new RemoteControlCommandResult(false, "Value cannot be converted to the target property type.");
        }

        try
        {
            property.SetValue(control, convertedValue);
            logger.LogInformation(
                "Remote property mutation succeeded for {ControlType}.{PropertyName} on node {NodeId} from {ClientIdentity}",
                control.GetType().Name,
                propertyName,
                nodeId,
                clientIdentity);

            return new RemoteControlCommandResult(true, "Property updated.");
        }
        catch (Exception ex) when (ex is ArgumentException or TargetInvocationException or MethodAccessException)
        {
            logger.LogWarning(
                "Remote property mutation failed for {ControlType}.{PropertyName} on node {NodeId} from {ClientIdentity}",
                control.GetType().Name,
                propertyName,
                nodeId,
                clientIdentity);

            return new RemoteControlCommandResult(false, "Property update failed.");
        }
    }

    private bool IsPropertyAllowed(Type controlType, string propertyName)
    {
        if (!options.DenyPropertyMutationByDefault)
        {
            return true;
        }

        return options.AllowedMutableProperties.Contains(propertyName)
            || options.AllowedMutableProperties.Contains($"{controlType.Name}.{propertyName}")
            || options.AllowedMutableProperties.Contains($"{controlType.FullName}.{propertyName}");
    }

    private bool IsSensitive(string propertyName)
    {
        return options.SensitiveNameFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryConvert(string value, Type targetType, out object? convertedValue)
    {
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (string.IsNullOrEmpty(value) && Nullable.GetUnderlyingType(targetType) is not null)
        {
            convertedValue = null;
            return true;
        }

        try
        {
            if (actualType == typeof(string))
            {
                convertedValue = value;
                return true;
            }

            if (actualType == typeof(bool))
            {
                convertedValue = bool.Parse(value);
                return true;
            }

            if (actualType.IsEnum)
            {
                convertedValue = Enum.Parse(actualType, value, ignoreCase: true);
                return true;
            }

            if (actualType == typeof(Guid))
            {
                convertedValue = Guid.Parse(value);
                return true;
            }

            if (actualType == typeof(Thickness))
            {
                convertedValue = ParseThickness(value);
                return true;
            }

            if (actualType == typeof(CornerRadius))
            {
                convertedValue = ParseCornerRadius(value);
                return true;
            }

            if (actualType == typeof(Point))
            {
                var values = ParseDoubles(value, 2);
                convertedValue = new Point(values[0], values[1]);
                return true;
            }

            if (actualType == typeof(Size))
            {
                var values = ParseDoubles(value, 2);
                convertedValue = new Size(values[0], values[1]);
                return true;
            }

            if (actualType == typeof(Rect))
            {
                var values = ParseDoubles(value, 4);
                convertedValue = new Rect(values[0], values[1], values[2], values[3]);
                return true;
            }

            if (actualType == typeof(Color))
            {
                convertedValue = Color.Parse(value);
                return true;
            }

            if (actualType.IsAssignableFrom(typeof(SolidColorBrush)))
            {
                convertedValue = new SolidColorBrush(Color.Parse(value));
                return true;
            }

            convertedValue = Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            convertedValue = null;
            return false;
        }
    }

    private static Thickness ParseThickness(string value)
    {
        var values = ParseDoubles(value, 1, 4);

        return values.Length == 1
            ? new Thickness(values[0])
            : new Thickness(values[0], values[1], values[2], values[3]);
    }

    private static CornerRadius ParseCornerRadius(string value)
    {
        var values = ParseDoubles(value, 1, 4);

        return values.Length == 1
            ? new CornerRadius(values[0])
            : new CornerRadius(values[0], values[1], values[2], values[3]);
    }

    private static double[] ParseDoubles(string value, params int[] acceptedCounts)
    {
        var values = value
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        if (!acceptedCounts.Contains(values.Length))
        {
            throw new FormatException(
                $"Expected {string.Join(" or ", acceptedCounts)} numeric values.");
        }

        return values;
    }
}
