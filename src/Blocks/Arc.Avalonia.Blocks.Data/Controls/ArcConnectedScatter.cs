using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data.Controls;

/// <summary>One month on the connected scatter path.</summary>
internal sealed class ScatterPoint
{
    public ScatterPoint(string label, double x, double y)
    {
        Label = label;
        X = x;
        Y = y;
    }

    public string Label { get; }

    public double X { get; }

    public double Y { get; }
}

internal sealed class ScatterSeries
{
    public ScatterSeries(string name, IReadOnlyList<ScatterPoint> points, IBrush brush)
    {
        Name = name;
        Points = points;
        Brush = brush;
    }

    public string Name { get; }

    public IReadOnlyList<ScatterPoint> Points { get; }

    public IBrush Brush { get; }
}

/// <summary>
/// A connected scatterplot: CAC on x, LTV on y, months joined in time order with a head that scrubs along
/// the real curve, arrows for direction, a dashed 3x target with the healthy side shaded, and labels for the
/// first and last month. Hover or the arrow keys move the head; the target ratio is fixed by the page.
/// </summary>
internal sealed class ArcConnectedScatter : DataStage
{
    private readonly IReadOnlyList<ScatterSeries> _series;
    private readonly double _xMin;
    private readonly double _xMax;
    private readonly double _yMin;
    private readonly double _yMax;
    private readonly double _target;
    private int _head;
    private int _hover = -1;

    public ArcConnectedScatter(IReadOnlyList<ScatterSeries> series, double xMin, double xMax, double yMin,
        double yMax, double targetRatio = 3, int head = 17)
    {
        _series = series;
        _xMin = xMin;
        _xMax = xMax;
        _yMin = yMin;
        _yMax = yMax;
        _target = targetRatio;
        _head = head;
        Height = 320;
    }

    public string SeriesKey { get; set; } = string.Empty;

    public int Head
    {
        get => _head;
        set
        {
            var next = Math.Clamp(value, 0, Active.Points.Count - 1);
            if (next == _head)
            {
                return;
            }

            _head = next;
            HeadChanged?.Invoke(this, next);
            InvalidateVisual();
        }
    }

    public event EventHandler<int>? HeadChanged;

    protected override double DefaultHeight => 320;

    private ScatterSeries Active => _series.First(item => item.Name == SeriesKey);

    private Rect Plot => new(52, 18, Math.Max(10, Bounds.Width - 76), Math.Max(10, Bounds.Height - 62));

    private Point Map(ScatterPoint point)
    {
        var plot = Plot;
        return new Point(
            DataMath.Map(point.X, _xMin, _xMax, plot.Left, plot.Right),
            DataMath.Map(point.Y, _yMin, _yMax, plot.Bottom, plot.Top));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var nearest = -1;
        var best = 16.0;
        var active = Active;
        for (var index = 0; index < active.Points.Count; index++)
        {
            var mapped = Map(active.Points[index]);
            var distance = Math.Sqrt(Math.Pow(mapped.X - point.X, 2) + Math.Pow(mapped.Y - point.Y, 2));
            if (distance < best)
            {
                best = distance;
                nearest = index;
            }
        }

        if (nearest != _hover)
        {
            _hover = nearest;
            Cursor = nearest >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_hover >= 0)
        {
            Head = _hover;
            Focus();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                Head -= 1;
                break;
            case Key.Right:
                Head += 1;
                break;
            case Key.Home:
                Head = 0;
                break;
            case Key.End:
                Head = Active.Points.Count - 1;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var plot = Plot;
        var active = Active;

        // Gridlines, axis labels and the healthy zone above y = 3x.
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = Crisp(DataMath.Map(_yMin + (_yMax - _yMin) * tick / 4, _yMin, _yMax, plot.Bottom, plot.Top));
            context.DrawLine(Ink.Pen(Ink.BorderSubtle, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            Text(context, DataKit.MoneyCompact(_yMin + (_yMax - _yMin) * tick / 4), plot.Left - 6, y, 10, Ink.Muted,
                alignment: TextAlignment.Right);
            var x = Crisp(DataMath.Map(_xMin + (_xMax - _xMin) * tick / 4, _xMin, _xMax, plot.Left, plot.Right));
            Text(context, DataKit.MoneyCompact(_xMin + (_xMax - _xMin) * tick / 4), x, plot.Bottom + 12, 10, Ink.Muted,
                alignment: TextAlignment.Center);
        }

        var healthy = new StreamGeometry();
        using (var zone = healthy.Open())
        {
            zone.BeginFigure(new Point(plot.Left, plot.Bottom), true);
            zone.LineTo(new Point(plot.Left, DataMath.Map(_target * _xMin, _yMin, _yMax, plot.Bottom, plot.Top)));
            zone.LineTo(new Point(plot.Right, DataMath.Map(_target * _xMax, _yMin, _yMax, plot.Bottom, plot.Top)));
            zone.LineTo(new Point(plot.Right, plot.Bottom));
            zone.EndFigure(true);
        }

        using (context.PushClip(new RoundedRect(plot, 4, 4)))
        {
            context.DrawGeometry(Ink.Alpha(Ink.Series(0), 0.08), null, healthy);
            var targetStart = new Point(plot.Left, DataMath.Map(_target * _xMin, _yMin, _yMax, plot.Bottom, plot.Top));
            var targetEnd = new Point(plot.Right, DataMath.Map(_target * _xMax, _yMin, _yMax, plot.Bottom, plot.Top));
            context.DrawLine(Ink.Pen(Ink.BorderStrong, 1.2, true), targetStart, targetEnd);
        }

        Text(context, "LTV = 3× CAC", plot.Right - 4, DataMath.Map(_target * _xMax, _yMin, _yMax, plot.Bottom, plot.Top) - 12,
            10, Ink.Secondary, FontWeight.Medium, TextAlignment.Right);

        // The path, drawn through every month in order.
        var points = active.Points.Select(Map).ToList();
        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(active.Brush, 0.55), 1.6), DataStage.LineGeometry(points));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var isHead = index == _head;
            var isHover = index == _hover;
            context.DrawEllipse(isHead ? active.Brush : Ink.Surface,
                Ink.Pen(isHead ? Ink.Surface : active.Brush, isHead ? 2 : 1.2),
                point, isHead ? 5 : isHover ? 4 : 2.6, isHead ? 5 : isHover ? 4 : 2.6);
            if (index % 4 == 0 && index != _head && index != points.Count - 1)
            {
                Text(context, active.Points[index].Label, point.X, point.Y - 9, 9, Ink.Muted, alignment: TextAlignment.Center);
            }
        }

        Text(context, active.Points[0].Label, points[0].X + 8, points[0].Y, 9, Ink.Muted);
        Text(context, active.Points[^1].Label, points[^1].X - 8, points[^1].Y + 12, 9, Ink.Muted, alignment: TextAlignment.Right);

        var head = active.Points[_head];
        var headPoint = points[_head];
        var ratio = head.Y / Math.Max(1, head.X);
        Text(context, active.Points[_head].Label, headPoint.X, Math.Max(plot.Top + 8, headPoint.Y - 18), 11, Ink.Foreground,
            FontWeight.Medium, TextAlignment.Center);

        var rows = new List<(string, string, IBrush?)>
        {
            ("CAC", DataKit.Money(head.X), (IBrush?)null),
            ("LTV", DataKit.Money(head.Y), (IBrush?)null),
            ("LTV / CAC", ratio.ToString("0.00") + "×", (IBrush?)null),
            ("Target", ratio >= _target ? "above 3×" : "below 3×", (IBrush?)null)
        };
        if (_hover >= 0 && _hover != _head)
        {
            var hover = active.Points[_hover];
            rows.Add((hover.Label, DataKit.Money(hover.X) + " / " + DataKit.Money(hover.Y), (IBrush?)null));
        }

        Tooltip(context, Bounds.Size, new Point(headPoint.X, plot.Top + 42), head.Label, rows);
        Text(context, "CAC", plot.Center.X, Bounds.Height - 10, 10, Ink.Muted, alignment: TextAlignment.Center);
        Text(context, "LTV", 12, plot.Center.Y, 10, Ink.Muted);
    }
}
