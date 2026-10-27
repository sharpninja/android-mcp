using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.RemoteControl.Protocol.V1;
using Avalonia.RemoteControl.Server.Commands;
using Avalonia.RemoteControl.Server.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Avalonia.RemoteControl.Server.Input;

/// <summary>
/// Dispatches policy-approved live remote input to the Avalonia root.
/// </summary>
public sealed class RemoteControlInputDispatcher
{
    private readonly IRemoteControlRootProvider rootProvider;
    private readonly AvaloniaRemoteControlOptions options;
    private readonly IRemoteControlDispatcher dispatcher;
    private readonly ILogger<RemoteControlInputDispatcher> logger;
    private readonly Pointer pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
    private bool isLeftButtonPressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteControlInputDispatcher"/> class.
    /// </summary>
    /// <param name="rootProvider">Remote-control root provider.</param>
    /// <param name="options">Remote-control options.</param>
    /// <param name="dispatcher">Avalonia dispatcher.</param>
    /// <param name="logger">Audit logger.</param>
    public RemoteControlInputDispatcher(
        IRemoteControlRootProvider rootProvider,
        IOptions<AvaloniaRemoteControlOptions> options,
        IRemoteControlDispatcher dispatcher,
        ILogger<RemoteControlInputDispatcher> logger)
    {
        this.rootProvider = rootProvider;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>
    /// Sends a batch of live input events to the remote root.
    /// </summary>
    /// <param name="events">Input events in root-relative DIP coordinates.</param>
    /// <param name="clientIdentity">Sanitized authenticated client identity.</param>
    /// <returns>The sanitized command result.</returns>
    public ValueTask<RemoteControlCommandResult> SendInputAsync(
        IReadOnlyList<RemoteInputEvent> events,
        string clientIdentity = "unknown")
    {
        ArgumentNullException.ThrowIfNull(events);
        return dispatcher.InvokeAsync(() => SendInput(events, clientIdentity));
    }

    private RemoteControlCommandResult SendInput(
        IReadOnlyList<RemoteInputEvent> events,
        string clientIdentity)
    {
        if (!options.AllowRemoteActions || !options.AllowRemoteInput)
        {
            logger.LogWarning(
                "Remote input rejected because remote input is disabled for {EventCount} events from {ClientIdentity}",
                events.Count,
                clientIdentity);
            return new RemoteControlCommandResult(false, "Remote input is disabled by policy.");
        }

        var root = rootProvider.GetRootControl();

        if (root is null)
        {
            return new RemoteControlCommandResult(false, "No Avalonia remote-control root control is registered.");
        }

        var inputRoot = RemoteControlRootNormalizer.Normalize(root);

        foreach (var inputEvent in events)
        {
            Dispatch(inputRoot, inputEvent);
        }

        logger.LogInformation(
            "Remote input dispatched {EventCount} events from {ClientIdentity}",
            events.Count,
            clientIdentity);
        return new RemoteControlCommandResult(true, "Remote input dispatched.");
    }

    private void Dispatch(Control root, RemoteInputEvent inputEvent)
    {
        var point = new Point(inputEvent.X, inputEvent.Y);
        var target = ResolvePointerTarget(root, point);

        switch (inputEvent.Kind)
        {
            case RemoteInputKind.PointerMove:
                target.RaiseEvent(CreatePointerEvent(InputElement.PointerMovedEvent, target, root, point, PointerUpdateKind.Other));
                break;
            case RemoteInputKind.PointerPress:
                isLeftButtonPressed = inputEvent.Button == RemoteMouseButton.Left || inputEvent.Button == RemoteMouseButton.Unspecified;
                target.RaiseEvent(new PointerPressedEventArgs(
                    target,
                    pointer,
                    root,
                    point,
                    GetTimestamp(inputEvent),
                    new PointerPointProperties(
                        isLeftButtonPressed ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None,
                        ToPressedKind(inputEvent.Button)),
                    KeyModifiers.None));
                break;
            case RemoteInputKind.PointerRelease:
                target.RaiseEvent(new PointerReleasedEventArgs(
                    target,
                    pointer,
                    root,
                    point,
                    GetTimestamp(inputEvent),
                    new PointerPointProperties(RawInputModifiers.None, ToReleasedKind(inputEvent.Button)),
                    KeyModifiers.None,
                    ToMouseButton(inputEvent.Button)));
                isLeftButtonPressed = false;
                break;
            case RemoteInputKind.Wheel:
                target.RaiseEvent(new PointerWheelEventArgs(
                    target,
                    pointer,
                    root,
                    point,
                    GetTimestamp(inputEvent),
                    new PointerPointProperties(
                        isLeftButtonPressed ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None,
                        PointerUpdateKind.Other),
                    KeyModifiers.None,
                    new Vector(inputEvent.DeltaX, inputEvent.DeltaY)));
                break;
            case RemoteInputKind.KeyDown:
                ResolveKeyboardTarget(root).RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Source = ResolveKeyboardTarget(root),
                    Key = ParseKey(inputEvent.Key),
                    KeyDeviceType = KeyDeviceType.Keyboard,
                });
                break;
            case RemoteInputKind.KeyUp:
                ResolveKeyboardTarget(root).RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyUpEvent,
                    Source = ResolveKeyboardTarget(root),
                    Key = ParseKey(inputEvent.Key),
                    KeyDeviceType = KeyDeviceType.Keyboard,
                });
                break;
            case RemoteInputKind.Text:
                ResolveKeyboardTarget(root).RaiseEvent(new TextInputEventArgs
                {
                    RoutedEvent = InputElement.TextInputEvent,
                    Source = ResolveKeyboardTarget(root),
                    Text = inputEvent.Text,
                });
                break;
        }
    }

    private PointerEventArgs CreatePointerEvent(
        RoutedEvent routedEvent,
        Control target,
        Control root,
        Point point,
        PointerUpdateKind updateKind)
    {
        return new PointerEventArgs(
            routedEvent,
            target,
            pointer,
            root,
            point,
            (ulong)Environment.TickCount64,
            new PointerPointProperties(
                isLeftButtonPressed ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None,
                updateKind),
            KeyModifiers.None);
    }

    private static Control ResolvePointerTarget(Control root, Point point)
    {
        return InputExtensions.InputHitTest(root, point) as Control ?? root;
    }

    private static Control ResolveKeyboardTarget(Control root)
    {
        return root.GetSelfAndVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(static control => control.IsFocused)
            ?? root;
    }

    private static ulong GetTimestamp(RemoteInputEvent inputEvent)
    {
        return inputEvent.Timestamp == 0
            ? (ulong)Environment.TickCount64
            : inputEvent.Timestamp;
    }

    private static Key ParseKey(string key)
    {
        return Enum.TryParse<Key>(key, ignoreCase: true, out var parsed)
            ? parsed
            : Key.None;
    }

    private static PointerUpdateKind ToPressedKind(RemoteMouseButton button)
    {
        return button switch
        {
            RemoteMouseButton.Right => PointerUpdateKind.RightButtonPressed,
            RemoteMouseButton.Middle => PointerUpdateKind.MiddleButtonPressed,
            _ => PointerUpdateKind.LeftButtonPressed,
        };
    }

    private static PointerUpdateKind ToReleasedKind(RemoteMouseButton button)
    {
        return button switch
        {
            RemoteMouseButton.Right => PointerUpdateKind.RightButtonReleased,
            RemoteMouseButton.Middle => PointerUpdateKind.MiddleButtonReleased,
            _ => PointerUpdateKind.LeftButtonReleased,
        };
    }

    private static MouseButton ToMouseButton(RemoteMouseButton button)
    {
        return button switch
        {
            RemoteMouseButton.Right => MouseButton.Right,
            RemoteMouseButton.Middle => MouseButton.Middle,
            _ => MouseButton.Left,
        };
    }
}
