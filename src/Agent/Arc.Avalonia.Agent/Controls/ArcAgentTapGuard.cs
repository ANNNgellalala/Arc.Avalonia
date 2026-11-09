using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Suppresses a click/tap that follows a scroll drag: a pointer that moves more than <see cref="Slop"/>
/// device-independent pixels between press and release cancels the pending click. Used by the agent cards
/// rendered inside a scrolling transcript, so swiping the timeline never approves a tool call or picks an
/// option by accident.
/// </summary>
internal sealed class ArcAgentTapGuard
{
    /// <summary>Movement (px) above which the gesture counts as a scroll drag rather than a tap.</summary>
    public const double Slop = 8;

    private Point _origin;
    private bool _pressed;
    private bool _cancelled;

    /// <summary>True when the current/last gesture moved far enough to be a drag.</summary>
    public bool IsCancelled => _cancelled;

    /// <summary>Starts observing pointer gestures on <paramref name="target"/>.</summary>
    public void Attach(Control target)
    {
        target.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        target.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        target.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        target.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Attaches a guard and returns a click filter that reports whether the click may run.</summary>
    public static Func<bool> Watch(Control target)
    {
        var guard = new ArcAgentTapGuard();
        guard.Attach(target);
        return () => !guard.IsCancelled;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = true;
        _cancelled = false;
        if (sender is Visual visual)
        {
            _origin = e.GetPosition(visual);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed || sender is not Visual visual)
        {
            return;
        }

        var position = e.GetPosition(visual);
        if (Math.Abs(position.X - _origin.X) > Slop || Math.Abs(position.Y - _origin.Y) > Slop)
        {
            _cancelled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _pressed = false;

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pressed = false;
        _cancelled = true;
    }
}
