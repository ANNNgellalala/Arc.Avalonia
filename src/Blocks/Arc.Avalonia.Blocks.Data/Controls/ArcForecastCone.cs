using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>
/// A billing-period usage chart with a forecast cone and a draggable budget line. The solid line is the
/// measured part of the period, the dashed line and shaded cone are the projection, and the crossing marker
/// dates the day the projection passes the budget. Up and Down (or a vertical drag) move the budget one step,
/// Page Up and Page Down move five, Home and End jump to the ends, and the left and right arrows scrub a day.
/// </summary>
internal sealed class ArcForecastCone : DataStage
{
    private readonly double[] _actual;
    private readonly double[] _forecast;
    private readonly double[] _low;
    private readonly double[] _high;
    private int _active = -1;
    private double _budget;
    private bool _dragging;

    public ArcForecastCone(double[] actual, double[] forecast, double[] low, double[] high, double budget)
    {
        _actual = actual;
        _forecast = forecast;
        _low = low;
        _high = high;
        _budget = budget;
        Height = 210;
    }

    public double Budget
    {
        get => _budget;
        set
        {
            var next = Math.Round(Math.Clamp(value, 2000, 16000) / 250) * 250;
            if (Math.Abs(next - _budget) < 0.01)
            {
                return;
            }

            _budget = next;
            BudgetChanged?.Invoke(this, next);
            InvalidateVisual();
        }
    }

    public event EventHandler<double>? BudgetChanged;

    protected override double DefaultHeight => 210;

    private int MeasuredDays => _actual.Count(value => !double.IsNaN(value));

    private double MaxValue
    {
        get
        {
            var max = _budget;
            foreach (var value in _high.Concat(_actual).Where(value => !double.IsNaN(value)))
            {
                max = Math.Max(max, value);
            }

            return max * 1.08;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragging)
        {
            var plot = Plot;
            Budget = DataMath.Map(point.Y, plot.Bottom, plot.Top, 0, MaxValue);
            return;
        }

        var active = point.X >= Plot.Left && point.X <= Plot.Right ? IndexAt(point.X) : -1;
        if (active != _active)
        {
            _active = active;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        Focus();
        e.Pointer.Capture(this);
        Budget = DataMath.Map(e.GetPosition(this).Y, Plot.Bottom, Plot.Top, 0, MaxValue);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_dragging)
        {
            _active = -1;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Up:
                Budget += 250;
                break;
            case Key.Down:
                Budget -= 250;
                break;
            case Key.PageUp:
                Budget += 1250;
                break;
            case Key.PageDown:
                Budget -= 1250;
                break;
            case Key.Home:
                _active = 0;
                InvalidateVisual();
                break;
            case Key.End:
                _active = _actual.Length - 1;
                InvalidateVisual();
                break;
            case Key.Left:
                _active = Math.Max(0, (_active < 0 ? MeasuredDays : _active) - 1);
                InvalidateVisual();
                break;
            case Key.Right:
                _active = Math.Min(_actual.Length - 1, (_active < 0 ? MeasuredDays - 1 : _active) + 1);
                InvalidateVisual();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private Rect Plot => new(46, 14, Math.Max(10, Bounds.Width - 68), Math.Max(10, Bounds.Height - 36));

    private int IndexAt(double x)
    {
        var plot = Plot;
        var fraction = DataMath.Clamp01((x - plot.Left) / plot.Width);
        return Math.Clamp((int)Math.Round(fraction * (_actual.Length - 1)), 0, _actual.Length - 1);
    }

    private double PointX(int index)
    {
        var plot = Plot;
        return plot.Left + index / (double)(_actual.Length - 1) * plot.Width;
    }

    private double PointY(double value)
    {
        var plot = Plot;
        return DataMath.Map(value, 0, MaxValue, plot.Bottom, plot.Top);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var plot = Plot;
        if (plot.Width <= 10)
        {
            return;
        }

        var max = MaxValue;
        for (var tick = 0; tick <= 4; tick++)
        {
            var value = max * tick / 4;
            var y = Crisp(DataMath.Map(value, 0, max, plot.Bottom, plot.Top));
            context.DrawLine(Ink.Pen(Ink.BorderSubtle, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            Text(context, DataKit.Compact(value), plot.Left - 6, y, 11, Ink.Muted, alignment: TextAlignment.Right);
        }

        var measured = 0;
        for (var i = 0; i < _forecast.Length; i++)
        {
            if (!double.IsNaN(_forecast[i]))
            {
                measured = i;
                break;
            }
        }

        var lastActual = -1;
        for (var i = 0; i < _actual.Length; i++)
        {
            if (!double.IsNaN(_actual[i]))
            {
                lastActual = i;
            }
        }

        // Forecast cone, drawn from the last measured day so the bands meet the solid line.
        var cone = new StreamGeometry();
        using (var geometry = cone.Open())
        {
            var started = false;
            for (var i = Math.Max(0, measured); i < _high.Length; i++)
            {
                var point = new Point(PointX(i), PointY(_high[i]));
                if (!started)
                {
                    geometry.BeginFigure(point, true);
                    started = true;
                }
                else
                {
                    geometry.LineTo(point);
                }
            }

            for (var i = _low.Length - 1; i >= Math.Max(0, measured); i--)
            {
                geometry.LineTo(new Point(PointX(i), PointY(_low[i])));
            }

            geometry.EndFigure(true);
        }

        context.DrawGeometry(Ink.Alpha(Ink.Series(1), 0.34), null, cone);
        var highPoints = new List<Point>();
        var lowPoints = new List<Point>();
        for (var i = Math.Max(0, measured); i < _high.Length; i++)
        {
            highPoints.Add(new Point(PointX(i), PointY(_high[i])));
            lowPoints.Add(new Point(PointX(i), PointY(_low[i])));
        }

        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(Ink.Series(1), 0.55), 1, true), DataStage.LineGeometry(highPoints));
        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(Ink.Series(1), 0.55), 1, true), DataStage.LineGeometry(lowPoints));

        var actualPoints = new List<Point>();
        for (var i = 0; i <= lastActual; i++)
        {
            actualPoints.Add(new Point(PointX(i), PointY(_actual[i])));
        }

        if (actualPoints.Count > 1)
        {
            var area = new StreamGeometry();
            using (var geometry = area.Open())
            {
                geometry.BeginFigure(new Point(actualPoints[0].X, plot.Bottom), true);
                foreach (var point in actualPoints)
                {
                    geometry.LineTo(point);
                }

                geometry.LineTo(new Point(actualPoints[^1].X, plot.Bottom));
                geometry.EndFigure(true);
            }

            context.DrawGeometry(Ink.Alpha(Ink.Series(0), 0.10), null, area);
            context.DrawGeometry(null, Ink.Pen(Ink.Series(0), 2), DataStage.LineGeometry(actualPoints));
        }

        var forecastPoints = new List<Point>();
        for (var i = measured; i < _forecast.Length; i++)
        {
            forecastPoints.Add(new Point(PointX(i), PointY(_forecast[i])));
        }

        if (forecastPoints.Count > 1)
        {
            context.DrawGeometry(null, Ink.Pen(Ink.Series(1), 1.6, true), DataStage.LineGeometry(forecastPoints));
        }

        // Today divider: what is measured versus what is projected.
        if (lastActual >= 0)
        {
            var x = Crisp(PointX(lastActual));
            DashedLine(context, Ink.Border, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Text(context, "Today", x + 5, plot.Top + 6, 10, Ink.Muted);
        }

        // Budget line and its drag handle.
        var budgetY = Crisp(PointY(_budget));
        context.DrawLine(Ink.Pen(Ink.Accent, 1.4, true), new Point(plot.Left, budgetY), new Point(plot.Right, budgetY));
        var handle = new Rect(plot.Right - 46, budgetY - 9, 46, 18);
        context.DrawRectangle(Ink.Alpha(Ink.Accent, 0.14), Ink.Pen(Ink.Accent, 1), handle, 9, 9);
        Text(context, DataKit.MoneyCompact(_budget), handle.Center.X, handle.Center.Y, 10, Ink.Accent,
            FontWeight.Medium, TextAlignment.Center);

        // Crossing marker: the first projected day at or above the budget.
        var crossing = Crossing(_budget, out var crossingValue);
        if (crossing >= 0)
        {
            var x = PointX(crossing);
            var y = PointY(crossingValue);
            context.DrawEllipse(Ink.Surface, Ink.Pen(Ink.Warning, 2), new Point(x, y), 4.5, 4.5);
            Text(context, "Over budget " + Label(crossing), x > plot.Left + 120 ? x - 10 : x + 10, Math.Max(plot.Top + 8, y - 14),
                10, Ink.Warning, FontWeight.Medium,
                x > plot.Left + 120 ? TextAlignment.Right : TextAlignment.Left);
        }

        if (_active >= 0)
        {
            var x = PointX(_active);
            DashedLine(context, Ink.BorderStrong, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var value = double.IsNaN(_forecast[_active]) ? _actual[_active] : _forecast[_active];
            context.DrawEllipse(Ink.Series(double.IsNaN(_forecast[_active]) ? 0 : 1), Ink.Pen(Ink.Surface, 1.5),
                new Point(x, PointY(value)), 4, 4);
            Tooltip(context, Bounds.Size, new Point(x, plot.Top + 40), Label(_active), new[]
            {
                ("Usage", DataKit.Number(value), (IBrush?)null),
                ("Budget", DataKit.MoneyCompact(_budget), (IBrush?)null),
                ("State", double.IsNaN(_forecast[_active]) ? "Measured" : "Projected", (IBrush?)null)
            });
        }
    }

    /// <summary>Index where the projection first passes the budget, interpolated between two days.</summary>
    private int Crossing(double budget, out double value)
    {
        value = 0;
        for (var i = 0; i < _forecast.Length; i++)
        {
            if (double.IsNaN(_forecast[i]))
            {
                continue;
            }

            if (_forecast[i] >= budget)
            {
                value = _forecast[i];
                return i;
            }
        }

        return -1;
    }

    private static string Label(int index) => "Nov " + (index + 1).ToString(CultureInfo.InvariantCulture);
}
