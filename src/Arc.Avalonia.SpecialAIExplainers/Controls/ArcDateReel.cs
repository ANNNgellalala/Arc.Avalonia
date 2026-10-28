using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIExplainers.Controls;

/// <summary>
/// A three dimensional reel date and time picker: each column is a curved drum drawn on a cylinder, so
/// entries rise on a sine curve, compress toward the edges and fade toward the glass lens. A flick keeps its
/// momentum, glides and snaps; a drag past the first or last entry stretches like rubber. A summary reads
/// the moment back in words, quick picks spin every reel, and Enter confirms.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public date-reel documentation. Approximations: the drum is a drawn
/// sine projection rather than a CSS 3D cylinder, and free text entry is limited to arrows, Page Up and
/// Down, Home and End. The value is a local DateTime exactly as the documented API describes.
/// </remarks>
public class ArcDateReel : ArcExplainer
{
    private sealed class Reel
    {
        public string[] Items = Array.Empty<string>();
        public double Offset;
        public int Index;
        public Rect Bounds;
        public string Name = string.Empty;
        public double Velocity;
        public bool Drag;
        public double DragStartOffset;
        public double DragStartY;
    }

    private static readonly DateTime Today = new(2026, 9, 24);
    private const double RowPixels = 50.5;
    private const double Theta = 0.34;
    private const double CylinderRadius = 152;
    private const double LensY = 194;
    private const double ColumnX = 40;
    private const double ColumnWidth = 112;
    private const double ColumnGap = 16;

    private readonly Reel[] _reels = new Reel[4];
    private readonly Rect[] _presets = new Rect[3];
    private Rect _confirm;
    private int _focusReel;
    private bool _snapping;
    private Reel? _snapReel;
    private double _snapFrom;
    private double _snapTarget;
    private double _snapTime;
    private bool _confirmed;

    public ArcDateReel()
    {
        var day = new List<string> { "Today", "Tomorrow" };
        for (var i = 2; i < 15; i++)
        {
            day.Add(Today.AddDays(i).ToString("ddd, MMM d", CultureInfo.InvariantCulture));
        }

        var hours = new string[12];
        for (var i = 0; i < 12; i++)
        {
            hours[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        var minutes = new string[12];
        for (var i = 0; i < 12; i++)
        {
            minutes[i] = (i * 5).ToString("00", CultureInfo.InvariantCulture);
        }

        _reels[0] = new Reel { Items = day.ToArray(), Name = "Day", Index = 1, Offset = 1 };
        _reels[1] = new Reel { Items = hours, Name = "Hour", Index = 8, Offset = 8 };
        _reels[2] = new Reel { Items = minutes, Name = "Minute", Index = 0, Offset = 0 };
        _reels[3] = new Reel { Items = new[] { "AM", "PM" }, Name = "Period", Index = 0, Offset = 0 };

        Describe(
            "Date reel: four curved wheel columns for day, hour, minute and AM or PM, with a summary, three quick picks and a confirm button",
            "Drag or flick a reel, use the up and down arrows to turn it and the left and right arrows to change reel. Enter confirms the schedule.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double PosterPhase => 0.4;

    private DateTime Value
    {
        get
        {
            var day = _reels[0].Index;
            var hours = (_reels[1].Index + 1) % 12;
            if (_reels[3].Index == 1)
            {
                hours += 12;
            }

            return Today.AddDays(day).AddHours(hours).AddMinutes(_reels[2].Index * 5);
        }
    }

    private void Snap(Reel reel)
    {
        var target = Math.Clamp(Math.Round(reel.Offset), 0, reel.Items.Length - 1);
        reel.Velocity = 0;
        _snapReel = reel;
        _snapFrom = reel.Offset;
        _snapTarget = target;
        _snapTime = 0;
        _snapping = true;
    }

    private void BeginStep(Reel reel, double target)
    {
        _snapReel = reel;
        _snapFrom = reel.Offset;
        _snapTarget = Math.Clamp(target, 0, reel.Items.Length - 1);
        _snapTime = 0;
        _snapping = true;
        reel.Velocity = 0;
        InvalidateVisual();
    }

    private void Commit(Reel reel)
    {
        reel.Offset = Math.Clamp(Math.Round(reel.Offset), 0, reel.Items.Length - 1);
        reel.Index = (int)reel.Offset;
        reel.Velocity = 0;
        _snapping = false;
        InvalidateVisual();
    }

    protected override bool OnTick(double seconds)
    {
        var moved = false;
        if (_snapping && _snapReel is not null)
        {
            _snapTime += seconds * 6;
            var t = Ink.Smooth(Math.Min(1, _snapTime));
            _snapReel.Offset = Ink.Lerp(_snapFrom, _snapTarget, t);
            moved = true;
            if (t >= 1)
            {
                Commit(_snapReel);
            }
        }

        foreach (var reel in _reels)
        {
            if (Math.Abs(reel.Velocity) <= 0.01)
            {
                continue;
            }

            reel.Offset = Math.Clamp(reel.Offset + reel.Velocity, -0.4, reel.Items.Length - 0.6);
            reel.Velocity *= 0.90;
            moved = true;
            if (Math.Abs(reel.Velocity) < 0.05)
            {
                Snap(reel);
            }
        }

        return moved;
    }

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        if (_confirm.Contains(point))
        {
            _confirmed = true;
            return true;
        }

        for (var i = 0; i < 3; i++)
        {
            if (_presets[i].Contains(point))
            {
                ApplyPreset(i);
                return true;
            }
        }

        for (var i = 0; i < 4; i++)
        {
            if (!_reels[i].Bounds.Contains(point))
            {
                continue;
            }

            _focusReel = i;
            var reel = _reels[i];
            reel.Drag = true;
            reel.DragStartOffset = reel.Offset;
            reel.DragStartY = point.Y;
            reel.Velocity = 0;
            _snapping = false;
            e.Pointer.Capture(this);
            return true;
        }

        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        foreach (var reel in _reels)
        {
            if (!reel.Drag)
            {
                continue;
            }

            var delta = (reel.DragStartY - point.Y) / RowPixels;
            var offset = reel.DragStartOffset + delta;
            var max = reel.Items.Length - 1;
            if (offset < 0)
            {
                offset *= 0.35;
            }
            else if (offset > max)
            {
                offset = max + (offset - max) * 0.35;
            }

            reel.Velocity = (offset - reel.Offset) * 0.55;
            reel.Offset = offset;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        foreach (var reel in _reels)
        {
            if (!reel.Drag)
            {
                continue;
            }

            reel.Drag = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }

            if (Math.Abs(reel.Velocity) > 0.12)
            {
                reel.Velocity = Math.Clamp(reel.Velocity, -0.7, 0.7);
            }
            else
            {
                Snap(reel);
            }
        }
    }

    protected override bool OnWheel(Point point, double delta, PointerWheelEventArgs e)
    {
        for (var i = 0; i < 4; i++)
        {
            if (!_reels[i].Bounds.Contains(point))
            {
                continue;
            }

            var reel = _reels[i];
            _focusReel = i;
            Snap(reel);
            BeginStep(reel, Math.Round(reel.Offset) - delta * 0.8);
            return true;
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        switch (key)
        {
            case Key.Up:
                Step(1);
                return true;
            case Key.Down:
                Step(-1);
                return true;
            case Key.PageUp:
                Step(_focusReel == 0 ? 7 : 4);
                return true;
            case Key.PageDown:
                Step(_focusReel == 0 ? -7 : -4);
                return true;
            case Key.Left:
                _focusReel = (_focusReel + 3) % 4;
                return true;
            case Key.Right:
                _focusReel = (_focusReel + 1) % 4;
                return true;
            case Key.Home:
                BeginStep(_reels[_focusReel], 0);
                return true;
            case Key.End:
                BeginStep(_reels[_focusReel], _reels[_focusReel].Items.Length - 1);
                return true;
            case Key.Enter:
                _confirmed = true;
                return true;
            default:
                return false;
        }
    }

    private void Step(int delta)
    {
        var reel = _reels[_focusReel];
        BeginStep(reel, Math.Round(reel.Offset) + delta);
    }

    private void ApplyPreset(int preset)
    {
        var target = preset switch
        {
            0 => Today.AddHours(18),
            1 => Today.AddDays(1).AddHours(9),
            _ => Today.AddDays(DaysUntilMonday()).AddHours(9)
        };

        SetReel(0, (int)(target.Date - Today).TotalDays);
        SetReel(1, (target.Hour % 12 == 0 ? 12 : target.Hour % 12) - 1);
        SetReel(2, target.Minute / 5);
        SetReel(3, target.Hour >= 12 ? 1 : 0);
        _confirmed = false;
    }

    private static int DaysUntilMonday()
    {
        var day = (int)Today.DayOfWeek;
        var until = ((int)DayOfWeek.Monday - day + 7) % 7;
        return until == 0 ? 7 : until;
    }

    private void SetReel(int index, int value)
    {
        var reel = _reels[index];
        reel.Offset = Math.Clamp(value, 0, reel.Items.Length - 1);
        reel.Index = (int)reel.Offset;
        reel.Velocity = 0;
    }

    private static string FormatTime(DateTime value)
    {
        var hour = value.Hour % 12;
        if (hour == 0)
        {
            hour = 12;
        }

        return $"{hour}:{value.Minute:00} {(value.Hour >= 12 ? "PM" : "AM")}";
    }

    private static string FormatDay(DateTime value)
    {
        var days = (int)(value.Date - Today).TotalDays;
        return days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            _ => value.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
        };
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        _ = phase;
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var muted = Ink.B(ArcTokens.TextMuted);
        var value = Value;

        // Title and summary.
        Ink.Label(context, "Send later", 40, 26, 14, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, $"{FormatDay(value)} at {FormatTime(value)}", 40, 46, 11, Ink.B(ArcTokens.TextSecondary));

        // Column headers and hit bounds.
        for (var i = 0; i < 4; i++)
        {
            var x = ColumnX + i * (ColumnWidth + ColumnGap);
            Ink.Label(context, _reels[i].Name, x + ColumnWidth / 2, 74, 10, muted, FontWeight.Medium, Align.Center);
            _reels[i].Bounds = new Rect(x, LensY - 110, ColumnWidth, 220);
        }

        // Glass lens.
        var lens = new Rect(ColumnX - 10, LensY - 26, 4 * ColumnWidth + 3 * ColumnGap + 20, 52);
        context.DrawRectangle(Ink.B(ArcTokens.Surface), new Pen(Ink.B(ArcTokens.Border), 1), lens, 16, 16);
        context.DrawLine(Ink.Stroke(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.22), 1), new Point(lens.X + 8, LensY), new Point(lens.Right - 8, LensY));

        // Reels.
        for (var column = 0; column < 4; column++)
        {
            var reel = _reels[column];
            var x = ColumnX + column * (ColumnWidth + ColumnGap);
            var focused = _focusReel == column && KeyboardFocus && IsFocused;
            using (context.PushClip(new Rect(x - 4, LensY - 104, ColumnWidth + 8, 208)))
            {
                var floor = Math.Floor(reel.Offset);
                var phaseOffset = reel.Offset - floor;
                for (var row = -5; row <= 5; row++)
                {
                    var index = (int)floor + row;
                    if (index < 0 || index >= reel.Items.Length)
                    {
                        continue;
                    }

                    var angle = (row - phaseOffset) * Theta;
                    var y = LensY + CylinderRadius * Math.Sin(angle);
                    var scale = Math.Max(0.24, Math.Cos(angle));
                    var distance = Math.Abs(row - phaseOffset);
                    if (distance < 0.45)
                    {
                        continue;
                    }

                    var alpha = Math.Clamp(1 - distance / 4.3, 0.06, 1);
                    using (context.PushTransform(Matrix.CreateScale(1, scale) * Matrix.CreateTranslation(0, y)))
                    {
                        Ink.Label(
                            context,
                            reel.Items[index],
                            x + ColumnWidth / 2,
                            0,
                            13.5,
                            Ink.A(Ink.B(ArcTokens.TextSecondary), alpha),
                            FontWeight.Medium,
                            Align.Center,
                            ColumnWidth - 8);
                    }
                }

                // Crisp selected entry on top of the lens.
                Ink.Label(
                    context,
                    reel.Items[reel.Index],
                    x + ColumnWidth / 2,
                    LensY,
                    14.5,
                    focused ? accent : Ink.B(ArcTokens.Foreground),
                    FontWeight.Medium,
                    Align.Center,
                    ColumnWidth - 8);

                if (focused)
                {
                    context.DrawRectangle(null, new Pen(Ink.B(ArcTokens.FocusOutline), 2), new Rect(x - 2, LensY - 24, ColumnWidth + 4, 48), 12, 12);
                }
            }
        }

        // Summary panel.
        var panel = new Rect(600, 66, width - 624, 240);
        Ink.Card(context, panel, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Selected", panel.X + 18, panel.Y + 20, 10.5, muted, FontWeight.Medium);
        Ink.Label(context, FormatTime(value), panel.X + 18, panel.Y + 52, 28, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, $"{FormatDay(value)}, {value.ToString("MMM d", CultureInfo.InvariantCulture)}", panel.X + 18, panel.Y + 78, 12, Ink.B(ArcTokens.TextSecondary));

        var dayCount = (int)(value.Date - Today).TotalDays;
        var relative = dayCount switch
        {
            0 => "Today",
            1 => "Tomorrow",
            var days => $"In {days} days"
        };
        Ink.Chip(context, panel.X + 18, panel.Y + 92, relative, Ink.Wash(accent, 0.14), accent, 20, 10, Ink.A(accent, 0.4));

        // Quick picks.
        var presets = new[] { "This evening", "Tomorrow", "Monday" };
        var presetWidth = (panel.Width - 52) / 3;
        for (var i = 0; i < 3; i++)
        {
            var rect = new Rect(panel.X + 18 + i * (presetWidth + 8), panel.Y + 128, presetWidth, 26);
            _presets[i] = rect;
            context.DrawRectangle(Ink.B(ArcTokens.SurfaceMuted), new Pen(Ink.B(ArcTokens.Border), 1), rect, 9, 9);
            Ink.Label(context, presets[i], rect.Center.X, rect.Center.Y, 10, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center, presetWidth - 8);
        }

        // Confirm.
        _confirm = new Rect(panel.X + 18, panel.Y + 170, 168, 34);
        context.DrawRectangle(
            _confirmed ? Ink.Wash(success, 0.16) : accent,
            new Pen(_confirmed ? Ink.A(success, 0.6) : accent, 1),
            _confirm,
            11,
            11);
        if (_confirmed)
        {
            Ink.Icon(context, "check", _confirm.X + 30, _confirm.Center.Y, 13, success);
        }

        Ink.Label(
            context,
            _confirmed ? "Scheduled" : "Schedule send",
            _confirm.X + (_confirmed ? 40 : 20),
            _confirm.Center.Y,
            11,
            _confirmed ? success : Ink.B(ArcTokens.AccentForeground),
            FontWeight.Medium);
        Ink.Label(context, "Enter confirms.", panel.X + 200, panel.Y + 187, 9.5, muted);

        Ink.Label(context, "Drag or flick a reel; Page Up jumps a week on the day reel.", 40, 356, 10, muted);
        Ink.Label(context, "A flick keeps momentum and snaps to the nearest entry.", width - 24, 356, 10, muted, Align.Right);
        _ = height;
    }
}
