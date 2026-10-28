using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>
/// A round button that bursts into an arc of actions. Press, drag onto an action and release to fire it, or
/// tap to keep the arc open and tap again to close it. Targeting goes by angle like a pie menu, with a dead
/// zone around the trigger; releasing off the arc folds it away without firing. Reduced motion drops the
/// swirl and the magnet pull, keeping short fades.
/// </summary>
public class ArcOrbitMenu : ArcMotionCanvas
{
    private static readonly (string Id, string Label, string Icon, string Done, bool Danger)[] Actions =
    {
        ("pin", "Pin", "pin", "Pinned", false),
        ("share", "Share", "share", "Shared", false),
        ("copy", "Copy", "link-2", "Copied", false),
        ("delete", "Delete", "trash-2", "Deleted", true)
    };

    private const int IdTrigger = 1;
    private const int IdAction = 10;
    private const double Radius = 100;
    private const double Spread = 150;
    private const double Direction = -90;

    private bool _open = true;
    private int _target = -1;
    private int _focus;
    private double _reveal = 1;
    private double _press;
    private string? _done;
    private Point _pressPoint;
    private bool _dragged;
    private bool _triggerPress;
    private bool _openedOnPress;
    private DateTime _pressAt;

    public ArcOrbitMenu()
    {
        Sm.Name(this, "Note actions", "Hold or click the button: arrow keys move around the arc, Enter fires, Escape closes.");
    }

    protected override double DefaultWidth => 960;

    protected override double DefaultHeight => 380;

    protected override bool NeedsFrame => base.NeedsFrame;

    private Point Trigger(Size size) => new(size.Width / 2, size.Height - 66);

    private Point ActionCenter(Size size, int index)
    {
        var trigger = Trigger(size);
        var angle = (Direction + (index - (Actions.Length - 1) / 2.0) * (Spread / Math.Max(1, Actions.Length - 1))) * Math.PI / 180.0;
        var distance = Radius * (0.75 + 0.25 * _reveal);
        return new Point(trigger.X + Math.Cos(angle) * distance, trigger.Y + Math.Sin(angle) * distance);
    }

    private static double AngleOf(Point from, Point to)
    {
        var degrees = Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI;
        return degrees;
    }

    private int TargetAt(Size size, Point point)
    {
        var trigger = Trigger(size);
        var distance = Math.Sqrt((point.X - trigger.X) * (point.X - trigger.X) + (point.Y - trigger.Y) * (point.Y - trigger.Y));
        if (distance < 34)
        {
            return -1;
        }

        var angle = AngleOf(trigger, point);
        var best = -1;
        var bestDelta = double.MaxValue;
        for (var index = 0; index < Actions.Length; index++)
        {
            var actionAngle = Direction + (index - (Actions.Length - 1) / 2.0) * (Spread / Math.Max(1, Actions.Length - 1));
            var delta = Math.Abs(((angle - actionAngle + 540) % 360) - 180);
            if (delta < 26 && delta < bestDelta)
            {
                bestDelta = delta;
                best = index;
            }
        }

        return best;
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 320 || size.Height < 240)
        {
            return;
        }

        DrawNote(context, size);
        var trigger = Trigger(size);

        if (_open)
        {
            var opacity = Sm.Clamp01(_reveal);
            using (context.PushOpacity(opacity))
            {
                for (var index = 0; index < Actions.Length; index++)
                {
                    var stagger = Sm.Clamp01((_reveal - index * 0.08) / 0.6);
                    if (stagger <= 0.02)
                    {
                        continue;
                    }

                    var center = ActionCenter(size, index);
                    var targeted = _target == index || (IsFocused && _focus == index);
                    var dot = new Point(
                        Sm.Lerp(trigger.X, center.X, stagger),
                        Sm.Lerp(trigger.Y, center.Y, stagger));
                    var scale = 0.7 + 0.3 * stagger + (targeted ? 0.06 : 0);
                    var radius = 22 * scale;

                    var action = Actions[index];
                    var fill = targeted
                        ? Sm.Brush(action.Danger ? ArcTokens.Danger : ArcTokens.Accent)
                        : Sm.Brush(ArcTokens.SurfaceRaised);
                    var ink = targeted
                        ? Sm.Brush(action.Danger ? ArcTokens.DangerForeground : ArcTokens.AccentForeground)
                        : Sm.Brush(action.Danger ? ArcTokens.Danger : ArcTokens.TextSecondary);

                    Sm.Lift(context, new Rect(dot.X - radius, dot.Y - radius, radius * 2, radius * 2), radius, 0.55);
                    context.DrawEllipse(fill, new Pen(Sm.Brush(ArcTokens.Border), 1), dot, radius, radius);
                    Sm.Icon(context, action.Icon, dot, 18 * scale, ink);

                    if (IsFocused && _open && _focus == index && ShowFocusRing)
                    {
                        Sm.FocusRing(context, new Rect(dot.X - 20, dot.Y - 20, 40, 40), 20);
                    }

                    HitCircle(dot, 24, IdAction + index);
                }
            }
        }

        // Trigger: ellipsis, close, or a check after firing.
        var triggerRadius = 28 - _press * 2;
        var triggerFill = _done is not null ? Sm.Brush(ArcTokens.Success) : Sm.Brush(ArcTokens.Accent);
        Sm.Lift(context, new Rect(trigger.X - 28, trigger.Y - 28, 56, 56), 28, 0.9);
        context.DrawEllipse(triggerFill, null, trigger, triggerRadius, triggerRadius);

        if (_done is not null)
        {
            DrawCheck(context, trigger);
        }
        else if (_open)
        {
            var pen = new Pen(Sm.Brush(ArcTokens.AccentForeground), 2.2, null, PenLineCap.Round);
            var half = 6.5;
            context.DrawLine(pen, new Point(trigger.X - half, trigger.Y - half), new Point(trigger.X + half, trigger.Y + half));
            context.DrawLine(pen, new Point(trigger.X + half, trigger.Y - half), new Point(trigger.X - half, trigger.Y + half));
        }
        else
        {
            var ink = Sm.Brush(ArcTokens.AccentForeground);
            for (var index = -1; index <= 1; index++)
            {
                context.DrawEllipse(ink, null, new Point(trigger.X + index * 6.5, trigger.Y), 2, 2);
            }
        }

        if (ShowFocusRing)
        {
            Sm.FocusRing(context, new Rect(trigger.X - 28, trigger.Y - 28, 56, 56), 28);
        }

        HitCircle(trigger, 30, IdTrigger);

        // Label pill: the menu name while open and nothing is targeted, the action name while targeting,
        // or the confirmation after firing.
        var pillText = _done ?? (_target >= 0 || (IsFocused && _open) ? Actions[Math.Clamp(_target >= 0 ? _target : _focus, 0, Actions.Length - 1)].Label : _open ? "Actions" : string.Empty);
        if (!string.IsNullOrEmpty(pillText))
        {
            var text = Sm.Text(pillText, 12, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
            var width = text.Width + 24;
            var pill = new Rect(trigger.X - width / 2, trigger.Y + 38, width, 28);
            Sm.Lift(context, pill, 14, 0.5);
            context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), pill, 14, 14);
            Sm.TextCentered(context, text, pill.Center.X, pill.Center.Y);
        }

        if (_open && _target < 0 && _dragged && Hovering)
        {
            var hint = Sm.Text("Release to cancel", 11, Sm.Brush(ArcTokens.TextMuted));
            var x = trigger.X - hint.Width / 2;
            context.DrawText(hint, new Point(x, trigger.Y + 72));
        }
    }

    private static void DrawCheck(DrawingContext context, Point center)
    {
        var pen = new Pen(Sm.Brush(ArcTokens.Background), 2.4, null, PenLineCap.Round, PenLineJoin.Round);
        context.DrawLine(pen, new Point(center.X - 9, center.Y), new Point(center.X - 3, center.Y + 6));
        context.DrawLine(pen, new Point(center.X - 3, center.Y + 6), new Point(center.X + 10, center.Y - 7));
    }

    private void DrawNote(DrawingContext context, Size size)
    {
        var card = new Rect(size.Width / 2 - 230, 26, 460, 148);
        Sm.Lift(context, card, 24, 0.7);
        context.DrawRectangle(Sm.Brush(ArcTokens.Surface), new Pen(Sm.Brush(ArcTokens.Border), 1), card, 24, 24);

        var label = Sm.Text("Design notes", 12, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium);
        context.DrawText(label, new Point(card.X + 24, card.Y + 16));
        var title = Sm.Text("Keep the motion quiet", 21, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(card.X + 24, card.Y + 38));

        for (var row = 0; row < 3; row++)
        {
            var y = card.Y + 80 + row * 20;
            var width = row == 2 ? card.Width - 170 : card.Width - 48;
            context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, 0.07), null, new Rect(card.X + 24, y, width, 8), 4, 4);
        }
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        _pressPoint = point;
        _dragged = false;
        if (id == IdTrigger)
        {
            _triggerPress = true;
            _openedOnPress = !_open;
            _pressAt = DateTime.UtcNow;
            if (!_open)
            {
                Open();
            }

            _press = 1;
            Animate(120, ArcMotion.EaseStandard, progress =>
            {
                _press = 1 - progress;
                InvalidateVisual();
            });
            return;
        }

        if (id >= IdAction && id < IdAction + Actions.Length)
        {
            _target = id - IdAction;
            InvalidateVisual();
        }

        _pressPoint = point;
        _dragged = false;
    }

    private void Open()
    {
        _open = true;
        _reveal = 0;
        _target = -1;
        Animate(260, ArcMotion.SpringMorph, progress =>
        {
            _reveal = progress;
            InvalidateVisual();
        });
    }

    protected override void OnDrag(int id, Point point)
    {
        if (!_open)
        {
            return;
        }

        if (Math.Abs(point.X - _pressPoint.X) > 6 || Math.Abs(point.Y - _pressPoint.Y) > 6)
        {
            _dragged = true;
        }

        var target = TargetAt(Bounds.Size, point);
        if (target != _target)
        {
            _target = target;
            _focus = Math.Max(0, target);
        }

        InvalidateVisual();
    }

    protected override void OnRelease(int id, Point point)
    {
        if (!_open)
        {
            _triggerPress = false;
            return;
        }

        var target = TargetAt(Bounds.Size, point);
        var quickTap = (DateTime.UtcNow - _pressAt).TotalMilliseconds < 240;
        if (target >= 0)
        {
            Fire(target);
        }
        else if (_triggerPress && quickTap && !_dragged && _openedOnPress)
        {
            // A tap that opened the arc keeps it open until the next tap or Escape.
        }
        else
        {
            _open = false;
            _reveal = 0;
            Animate(160, ArcMotion.EaseStandard, progress =>
            {
                _reveal = 1 - progress;
                InvalidateVisual();
            });
        }
        _triggerPress = false;
        _target = -1;
        _dragged = false;
        InvalidateVisual();
    }

    private void Fire(int index)
    {
        _done = Actions[index].Done;
        _target = -1;
        _open = false;
        _reveal = 0;
        Animate(150, ArcMotion.EaseStandard, progress =>
        {
            _reveal = 1 - progress;
            InvalidateVisual();
        });
        Animate(1400, ArcMotion.EaseStandard, _ => { }, () =>
        {
            _done = null;
            InvalidateVisual();
        });
        AutomationProperties.SetName(this, Actions[index].Done);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                if (!_open)
                {
                    Open();
                }

                _focus = (_focus + 1) % Actions.Length;
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Left:
                if (!_open)
                {
                    Open();
                }

                _focus = (_focus - 1 + Actions.Length) % Actions.Length;
                e.Handled = true;
                break;
            case Key.Home:
                _focus = 0;
                e.Handled = true;
                break;
            case Key.End:
                _focus = Actions.Length - 1;
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (!_open)
                {
                    Open();
                }
                else
                {
                    Fire(_focus);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                if (_open)
                {
                    _open = false;
                    _reveal = 0;
                    Animate(160, ArcMotion.EaseStandard, progress =>
                    {
                        _reveal = 1 - progress;
                        InvalidateVisual();
                    });
                }

                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    protected override void OnHoverChanged(int id)
    {
        if (_open && id >= IdAction && id < IdAction + Actions.Length)
        {
            _target = id - IdAction;
        }
        else if (_open && !Hovering && PressId < 0)
        {
            _target = -1;
        }

        InvalidateVisual();
    }
}
