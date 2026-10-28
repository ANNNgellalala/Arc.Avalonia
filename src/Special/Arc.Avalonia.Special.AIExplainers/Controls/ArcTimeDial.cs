using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// A twenty four hour rotary dial for picking a time across time zones. Every city rides the dial at its own
/// local time, a shaded band marks the shared working hours, and the home city reads out in the centre with
/// a knob on the rim. Dragging turns the dial with quarter hour detents; the arrow keys step a quarter hour,
/// Page Up steps an hour, and Home and End jump to the ends of the home city's day.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public time-dial documentation. Approximations: offsets are fixed
/// minutes rather than a time zone database (pass daylight saving aware offsets yourself in a real app), and
/// the dial is dragged with a mouse rather than flicked with touch momentum. The value is UTC minutes from
/// midnight, exactly as the documented API describes.
/// </remarks>
public class ArcTimeDial : ArcExplainer
{
    private sealed record City(string Name, string Code, int Offset);

    private static readonly City[] Cities =
    {
        new("New York", "NYC", -240),
        new("London", "LON", 60),
        new("Tokyo", "TYO", 540)
    };

    private static readonly Point Centre = new(252, 204);
    private const double RingRadius = 104;
    private const double RingWidth = 16;
    private const int WorkingStart = 540;
    private const int WorkingEnd = 1080;

    private readonly Rect[] _cityRows = new Rect[3];
    private int _value = 14 * 60 + 30;
    private int _home;
    private bool _dragging;
    private double _dragAngle;
    private int _dragValue;
    private double _hoverAngle = -999;

    public ArcTimeDial()
    {
        Describe(
            "Time dial: a twenty four hour rotary control that shows New York, London and Tokyo at their local time, with the shared working hours shaded",
            "Drag the dial or use the arrow keys to step a quarter hour, Page Up and Page Down for an hour, Home and End for the ends of the day. Click a city to make it the home city.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double PosterPhase => 0.5;

    private static int Wrap(int minutes)
    {
        var value = minutes % 1440;
        return value < 0 ? value + 1440 : value;
    }

    private static string Clock(int minutes)
    {
        var value = Wrap(minutes);
        return $"{value / 60:00}:{value % 60:00}";
    }

    private string HomeTime => Clock(_value + Cities[_home].Offset);

    private bool AtWork(int city) =>
        Wrap(_value + Cities[city].Offset) is >= WorkingStart and < WorkingEnd;

    private void SetValue(int minutes)
    {
        var next = Wrap(minutes);
        if (next == _value)
        {
            return;
        }

        _value = next;
        InvalidateVisual();
    }

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        for (var i = 0; i < 3; i++)
        {
            if (_cityRows[i].Contains(point))
            {
                _home = i;
                return true;
            }
        }

        var distance = Math.Sqrt((point.X - Centre.X) * (point.X - Centre.X) + (point.Y - Centre.Y) * (point.Y - Centre.Y));
        if (distance > RingRadius - 40 && distance < RingRadius + 34)
        {
            _dragging = true;
            _dragAngle = AngleOf(point);
            _dragValue = _value;
            e.Pointer.Capture(this);
            return true;
        }

        return false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var angle = AngleOf(e.GetPosition(this));
        var delta = angle - _dragAngle;
        while (delta > 180)
        {
            delta -= 360;
        }

        while (delta < -180)
        {
            delta += 360;
        }

        var minutes = (int)Math.Round(delta / 15) * 15;
        SetValue(_dragValue + minutes);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override bool OnHover(Point? point)
    {
        var angle = -999.0;
        if (point is { } p)
        {
            var distance = Math.Sqrt((p.X - Centre.X) * (p.X - Centre.X) + (p.Y - Centre.Y) * (p.Y - Centre.Y));
            if (distance > RingRadius - 40 && distance < RingRadius + 34)
            {
                angle = AngleOf(p);
            }
        }

        if (Math.Abs(angle - _hoverAngle) < 0.5)
        {
            return false;
        }

        _hoverAngle = angle;
        return true;
    }

    private static double AngleOf(Point point)
    {
        var angle = Math.Atan2(point.Y - Centre.Y, point.X - Centre.X) * 180 / Math.PI + 90;
        return angle < 0 ? angle + 360 : angle;
    }

    protected override bool OnKey(Key key)
    {
        switch (key)
        {
            case Key.Right:
            case Key.Up:
                SetValue(_value + 15);
                return true;
            case Key.Left:
            case Key.Down:
                SetValue(_value - 15);
                return true;
            case Key.PageUp:
                SetValue(_value + 60);
                return true;
            case Key.PageDown:
                SetValue(_value - 60);
                return true;
            case Key.Home:
                SetValue(WorkingStart - Cities[_home].Offset);
                return true;
            case Key.End:
                SetValue(WorkingEnd - 15 - Cities[_home].Offset);
                return true;
            default:
                return false;
        }
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        _ = phase;
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var muted = Ink.B(ArcTokens.TextMuted);
        var home = Cities[_home];

        // Dial plate, with a full ring track so the rim always reads as a dial.
        context.DrawEllipse(Ink.B(ArcTokens.SurfaceRaised), new Pen(Ink.B(ArcTokens.Border), 1), Centre, RingRadius + RingWidth, RingRadius + RingWidth);
        context.DrawEllipse(null, new Pen(Ink.B(ArcTokens.SurfaceMuted), RingWidth), Centre, RingRadius, RingRadius);
        context.DrawEllipse(null, new Pen(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.30), 1), Centre, RingRadius + RingWidth / 2, RingRadius + RingWidth / 2);
        context.DrawEllipse(null, new Pen(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.22), 1), Centre, RingRadius - RingWidth / 2, RingRadius - RingWidth / 2);

        // Working hours band first, so the ticks read on top of a shaded stretch of the rim.
        var startLocal = WorkingStart - home.Offset;
        var endLocal = WorkingEnd - home.Offset;
        var startAngle = startLocal / 1440.0 * 360 - 90;
        var sweep = (endLocal - startLocal) / 1440.0 * 360;
        if (sweep < 0)
        {
            sweep += 360;
        }

        context.DrawGeometry(null, new Pen(Ink.A(accent, 0.20), RingWidth, null, PenLineCap.Round), Ink.Arc(Centre, RingRadius, startAngle, sweep));
        context.DrawGeometry(null, new Pen(Ink.A(accent, 0.8), 2), Ink.Arc(Centre, RingRadius + RingWidth / 2 - 1, startAngle, sweep));

        // Hour ticks on the rim and labels inside for the quarter hours of the day.
        for (var hour = 0; hour < 24; hour++)
        {
            var angle = hour / 24.0 * 360 - 90;
            var major = hour % 6 == 0;
            var inner = Ink.OnCircle(Centre, RingRadius - (major ? 10 : 7), angle);
            var outer = Ink.OnCircle(Centre, RingRadius + (major ? 10 : 7), angle);
            context.DrawLine(Ink.Stroke(major ? Ink.A(Ink.B(ArcTokens.TextSecondary), 0.55) : Ink.A(Ink.B(ArcTokens.TextMuted), 0.3), major ? 1.2 : 1), inner, outer);
            if (major)
            {
                var at = Ink.OnCircle(Centre, RingRadius - 30, angle);
                Ink.Label(context, $"{hour:00}", at.X, at.Y, 9.5, muted, FontWeight.Medium, Align.Center);
            }
        }

        for (var i = 0; i < Cities.Length; i++)
        {
            var city = Cities[i];
            var angle = Wrap(_value + city.Offset) / 1440.0 * 360 - 90;
            var point = Ink.OnCircle(Centre, RingRadius, angle);
            var isHome = i == _home;
            var tone = i == 0 ? Series(0) : i == 1 ? Series(2) : Series(3);
            var atWork = AtWork(i);
            if (isHome)
            {
                context.DrawEllipse(accent, new Pen(Ink.B(ArcTokens.Surface), 2), point, 8, 8);
                context.DrawEllipse(Ink.B(ArcTokens.Surface), null, point, 3, 3);
            }
            else
            {
                context.DrawEllipse(atWork ? tone : Ink.B(ArcTokens.SurfaceRaised), new Pen(tone, 1.6), point, 6, 6);
            }

            var labelAt = Ink.OnCircle(Centre, RingRadius + 30, angle);
            var label = city.Code;
            var labelWidth = Ink.ChipWidth(label, 9.5);
            Ink.Chip(
                context,
                labelAt.X - labelWidth / 2,
                labelAt.Y - 9,
                label,
                isHome ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.SurfaceRaised),
                isHome ? accent : Ink.B(ArcTokens.TextSecondary),
                18,
                9.5,
                isHome ? Ink.A(accent, 0.5) : Ink.B(ArcTokens.Border));
        }

        if (_hoverAngle > -900)
        {
            var tick = Ink.OnCircle(Centre, RingRadius, _hoverAngle - 90);
            context.DrawEllipse(null, new Pen(Ink.A(accent, 0.4), 2), tick, 5, 5);
        }

        // Centre readout, stacked to clear the hour labels inside the rim.
        Ink.Icon(context, "clock", Centre.X, Centre.Y - 40, 13, muted);
        Ink.Label(context, HomeTime, Centre.X, Centre.Y - 12, 31, Ink.B(ArcTokens.Foreground), FontWeight.Medium, Align.Center);
        Ink.Label(context, Cities[_home].Name, Centre.X, Centre.Y + 13, 11.5, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        var workText = AtWork(_home) ? "at work" : "off hours";
        var workInk = AtWork(_home) ? success : muted;
        Ink.Chip(
            context,
            Centre.X - Ink.ChipWidth(workText, 9.5, dot: true) / 2,
            Centre.Y + 25,
            workText,
            Ink.Wash(workInk, 0.12),
            workInk,
            17,
            9.5,
            dot: true);
        Ink.Label(context, $"UTC {Clock(_value)}", Centre.X, Centre.Y + 54, 9.5, muted, Align.Center);

        // City rows.
        var panel = new Rect(430, 62, width - 454, 244);
        Ink.Card(context, panel, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Team cities", panel.X + 16, panel.Y + 20, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, "working hours 09:00 to 18:00", panel.Right - 16, panel.Y + 20, 9.5, muted, Align.Right);

        for (var i = 0; i < 3; i++)
        {
            var city = Cities[i];
            var row = new Rect(panel.X + 10, panel.Y + 34 + i * 66, panel.Width - 20, 58);
            _cityRows[i] = row;
            var isHome = i == _home;
            context.DrawRectangle(
                isHome ? Ink.Wash(accent, 0.10) : Ink.B(ArcTokens.SurfaceMuted),
                new Pen(isHome ? Ink.A(accent, 0.5) : Ink.B(ArcTokens.Border), 1),
                row,
                12,
                12);

            var tone = i == 0 ? Series(0) : i == 1 ? Series(2) : Series(3);
            context.DrawEllipse(Ink.A(tone, 0.9), null, new Point(row.X + 22, row.Center.Y), 4, 4);
            Ink.Label(context, city.Name, row.X + 36, row.Y + 18, 12, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
            Ink.Label(context, $"UTC{(city.Offset >= 0 ? "+" : "-")}{Math.Abs(city.Offset) / 60:00}:{Math.Abs(city.Offset) % 60:00}", row.X + 36, row.Y + 36, 9.5, muted);

            Ink.Label(context, Clock(_value + city.Offset), row.Right - 96, row.Center.Y - 8, 16, Ink.B(ArcTokens.Foreground), FontWeight.Medium, Align.Right);
            var status = AtWork(i) ? "at work" : "off hours";
            var statusInk = AtWork(i) ? success : muted;
            Ink.Chip(
                context,
                row.Right - 92,
                row.Center.Y - 9,
                status,
                Ink.Wash(statusInk, 0.12),
                statusInk,
                18,
                9.5,
                dot: true);

            if (isHome)
            {
                Ink.Label(context, "Home", row.Right - 14, row.Y + 12, 9, accent, FontWeight.Medium, Align.Right);
            }
        }

        Ink.Label(context, "Drag the dial, or step a quarter hour with the arrow keys.", 24, 356, 10, muted);
        Ink.Label(context, "Click a city to read the dial in its time.", width - 24, 356, 10, muted, Align.Right);
        _ = height;
    }
}
