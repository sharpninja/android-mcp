using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.RemoteControl.Server.Snapshots;
using Avalonia.RemoteControl.Server.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Commands;

/// <summary>
/// Invokes policy-approved remote control actions.
/// </summary>
public sealed class RemoteControlActionInvoker
{
    private readonly IRemoteControlNodeResolver nodeResolver;
    private readonly AvaloniaRemoteControlOptions options;
    private readonly IRemoteControlDispatcher dispatcher;
    private readonly ILogger<RemoteControlActionInvoker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlActionInvoker"/> class.
    /// </summary>
    /// <param name="nodeResolver">Resolver for stable node IDs.</param>
    /// <param name="options">Remote-control options.</param>
    /// <param name="dispatcher">Dispatcher used to access controls safely.</param>
    /// <param name="logger">Audit logger.</param>
    public RemoteControlActionInvoker(
        IRemoteControlNodeResolver nodeResolver,
        IOptions<AvaloniaRemoteControlOptions> options,
        IRemoteControlDispatcher dispatcher,
        ILogger<RemoteControlActionInvoker> logger)
    {
        this.nodeResolver = nodeResolver;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>
    /// Invokes a basic click action for a stable node ID.
    /// </summary>
    /// <param name="nodeId">The stable node ID.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <returns>The sanitized command result.</returns>
    public ValueTask<RemoteControlCommandResult> InvokeClickAsync(
        string nodeId,
        string clientIdentity = "unknown")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return dispatcher.InvokeAsync(() => InvokeClick(nodeId, clientIdentity));
    }

    /// <summary>
    /// Requests focus for a stable node ID.
    /// </summary>
    /// <param name="nodeId">The stable node ID.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <returns>The sanitized command result.</returns>
    public ValueTask<RemoteControlCommandResult> InvokeFocusAsync(
        string nodeId,
        string clientIdentity = "unknown")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return dispatcher.InvokeAsync(() => InvokeFocus(nodeId, clientIdentity));
    }

    private RemoteControlCommandResult InvokeClick(string nodeId, string clientIdentity)
    {
        if (!options.AllowRemoteActions)
        {
            logger.LogWarning(
                "Remote click rejected because remote actions are disabled for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Remote actions are disabled by policy.");
        }

        if (!nodeResolver.TryResolve(nodeId, out var control))
        {
            logger.LogWarning(
                "Remote click rejected for stale node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Node is no longer available.");
        }

        if (control is Avalonia.Controls.ComboBoxItem item)
        {
            if (!item.IsEnabled || !item.IsVisible)
            {
                return new RemoteControlCommandResult(false, "Combo box item is unavailable.");
            }

            var combo = item.GetLogicalAncestors().OfType<ComboBox>().FirstOrDefault();
            if (combo is null)
            {
                return new RemoteControlCommandResult(false, "Owning combo box is unavailable.");
            }

            item.IsSelected = true;
            combo.IsDropDownOpen = false;
            logger.LogInformation(
                "Remote combo selection succeeded for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(true, "Click invoked.");
        }

        if (control is ListBoxItem listItem)
        {
            if (!listItem.IsEnabled || !listItem.IsVisible)
            {
                return new RemoteControlCommandResult(false, "List box item is unavailable.");
            }

            var list = listItem.GetLogicalAncestors().OfType<ListBox>().FirstOrDefault();
            if (list is null || !list.IsEnabled || !list.IsVisible)
            {
                return new RemoteControlCommandResult(false, "Owning list box is unavailable.");
            }

            listItem.IsSelected = true;
            logger.LogInformation(
                "Remote list selection succeeded for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(true, "Click invoked.");
        }

        if (control is Avalonia.Controls.Primitives.ToggleButton toggle)
        {
            if (toggle.Command is not null && !toggle.Command.CanExecute(toggle.CommandParameter))
            {
                return new RemoteControlCommandResult(false, "Toggle command cannot execute.");
            }

            toggle.IsChecked = toggle.IsChecked != true;
            if (toggle.Command is not null)
            {
                toggle.Command.Execute(toggle.CommandParameter);
            }
            else
            {
                toggle.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            }

            logger.LogInformation(
                "Remote toggle click succeeded for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(true, "Click invoked.");
        }

        if (control is Avalonia.Controls.Button button)
        {
            if (button.Command is not null)
            {
                if (!button.Command.CanExecute(button.CommandParameter))
                {
                    return new RemoteControlCommandResult(false, "Button command cannot execute.");
                }

                button.Command.Execute(button.CommandParameter);
                button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            }
            else
            {
                button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            }

            logger.LogInformation(
                "Remote click succeeded for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(true, "Click invoked.");
        }

        var releasedArgs = RaisePointerClickSequence(control);
        control.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, releasedArgs));
        logger.LogInformation(
            "Remote tap succeeded for node {NodeId} from {ClientIdentity}",
            nodeId,
            clientIdentity);
        return new RemoteControlCommandResult(true, "Tap invoked.");
    }

    private static PointerReleasedEventArgs RaisePointerClickSequence(Control control)
    {
        var point = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var timestamp = (ulong)Environment.TickCount64;
        var pressedArgs = new PointerPressedEventArgs(
            control,
            pointer,
            control,
            point,
            timestamp,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None);
        control.RaiseEvent(pressedArgs);

        var releasedArgs = new PointerReleasedEventArgs(
            control,
            pointer,
            control,
            point,
            timestamp + 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left);
        control.RaiseEvent(releasedArgs);
        return releasedArgs;
    }

    private RemoteControlCommandResult InvokeFocus(string nodeId, string clientIdentity)
    {
        if (!options.AllowRemoteActions)
        {
            logger.LogWarning(
                "Remote focus rejected because remote actions are disabled for node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Remote actions are disabled by policy.");
        }

        if (!nodeResolver.TryResolve(nodeId, out var control))
        {
            logger.LogWarning(
                "Remote focus rejected for stale node {NodeId} from {ClientIdentity}",
                nodeId,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Node is no longer available.");
        }

        control.Focus();
        logger.LogInformation(
            "Remote focus requested for node {NodeId} from {ClientIdentity}",
            nodeId,
            clientIdentity);
        return new RemoteControlCommandResult(true, "Focus requested.");
    }
}
