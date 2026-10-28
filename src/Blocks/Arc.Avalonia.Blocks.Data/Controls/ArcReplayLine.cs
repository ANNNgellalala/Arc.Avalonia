using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Controls;

/// <summary>One annotated moment on the replay timeline.</summary>
internal sealed class ReplayEvent
{
    public ReplayEvent(int index, string label)
    {
        Index = index;
        Label = label;
    }

    public int Index { get; }

    public string Label { get; }
}

/// <summary>
/// A time replay line chart: a day of traffic drawn as a line with a moving playhead, the value at the tip,
/// key moments annotated as the playhead passes them, and a click or drag that scrubs the head. Arrow keys
/// step one interval, Home and End jump to the ends. The path is drawn fully from the start so a screenshot
/// is deterministic; the head is what moves.
/// </summary>
internal sealed class ArcReplayLine : DataStage
{
    private readonly double[] _values;
    private readonly IReadOnlyList<string> _labels;
    private readonly IReadOnlyList<ReplayEvent> _events;
    private readonly string _unit;
    private int _head;
    private bool _dragging;

    public ArcReplayLine(IReadOnlyList<double> values, IReadOnlyList<string> labels, IReadOnlyList<ReplayEvent> events,
        string unit, int head = 0)
    {
        _values = values.ToArray();
        _labels = labels;
        _events = events;
        _unit = unit;
        _head = Math.Clamp(head, 0, _values.Length - 1);
        Height = 280;
    }

    public int Head => _head;

    public event EventHandler<int>? HeadChanged;

    protected override double DefaultHeight => 280;

    private Rect Plot => new(46, 16, Math.Max(10, Bounds.Width - 66), Math.Max(10, Bounds.Height - 62));

    private double Max => DataMath.NiceMax(_values.Max());

    private double PointX(int index)
    {
        var plot = Plot;
        return plot.Left + index / (double)Math.Max(1, _values.Length - 1) * plot.Width;
    }

    private double PointY(double value)
    {
        var plot = Plot;
        return DataMath.Map(value, 0, Max, plot.Bottom, plot.Top);
    }

    public void SetHead(int index, bool raise = true)
    {
        var next = Math.Clamp(index, 0, _values.Length - 1);
        if (next == _head)
        {
            return;
        }

        _head = next;
        InvalidateVisual();
        if (raise)
        {
            HeadChanged?.Invoke(this, next);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        if (!Plot.Contains(point))
        {
            return;
        }

        _dragging = true;
        Focus();
        e.Pointer.Capture(this);
        SetHead(IndexAt(point.X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragging)
        {
            SetHead(IndexAt(point.X));
            return;
        }

        Cursor = Plot.Contains(point) ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                SetHead(_head - 1);
                break;
            case Key.Right:
                SetHead(_head + 1);
                break;
            case Key.Home:
                SetHead(0);
                break;
            case Key.End:
                SetHead(_values.Length - 1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private int IndexAt(double x)
    {
        var plot = Plot;
        return Math.Clamp((int)Math.Round((x - plot.Left) / plot.Width * (_values.Length - 1)), 0, _values.Length - 1);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var plot = Plot;
        var max = Max;
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = Crisp(DataMath.Map(max * tick / 4, 0, max, plot.Bottom, plot.Top));
            context.DrawLine(Ink.Pen(Ink.BorderSubtle, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            Text(context, DataKit.Compact(max * tick / 4), plot.Left - 6, y, 10, Ink.Muted,
                alignment: TextAlignment.Right);
        }

        var points = new List<Point>();
        for (var index = 0; index < _values.Length; index++)
        {
            points.Add(new Point(PointX(index), PointY(_values[index])));
        }

        var completed = Math.Min(_head + 1, points.Count);
        context.DrawGeometry(Ink.Alpha(Ink.Series(0), 0.10), null, DataStage.AreaGeometry(points, plot.Bottom));
        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(Ink.Series(0), 0.55), 1.2), DataStage.LineGeometry(points));
        if (completed > 1)
        {
            var headPoints = points.Take(completed).ToList();
            context.DrawGeometry(null, Ink.Pen(Ink.Series(0), 2), DataStage.LineGeometry(headPoints));
        }

        foreach (var replayEvent in _events)
        {
            if (replayEvent.Index < 0 || replayEvent.Index >= _values.Length)
            {
                continue;
            }

            var x = PointX(replayEvent.Index);
            var passed = replayEvent.Index <= _head;
            context.DrawLine(Ink.Pen(Ink.Alpha(Ink.BorderStrong, 0.6), 1, true),
                new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawEllipse(passed ? Ink.Series(1) : Ink.Muted, null,
                new Point(x, PlotY(_values[replayEvent.Index])), 3, 3);
            Text(context, replayEvent.Label, x, plot.Bottom + 12, 9, passed ? Ink.Secondary : Ink.Muted,
                alignment: TextAlignment.Center, maxWidth: 92);
        }

        var headX = PointX(_head);
        var headY = PointY(_values[_head]);
        context.DrawLine(Ink.Pen(Ink.Foreground, 1.4), new Point(headX, plot.Top), new Point(headX, plot.Bottom));
        context.DrawEllipse(Ink.Series(0), Ink.Pen(Ink.Surface, 1.5), new Point(headX, headY), 4.4, 4.4);

        var label = $"{_labels[_head]} · {DataKit.Number(_values[_head])} {_unit}";
        var width = Math.Min(190, plot.Width);
        var box = new Rect(
            Math.Clamp(headX - width / 2, plot.Left, plot.Right - width),
            Math.Max(plot.Top + 2, headY - 28),
            width,
            20);
        context.DrawRectangle(Ink.SurfaceRaised, Ink.Pen(Ink.Border, 1), box, 8, 8);
        Text(context, label, box.Center.X, box.Center.Y, 10, Ink.Foreground, FontWeight.Medium,
            TextAlignment.Center);

        for (var hour = 0; hour <= 24; hour += 4)
        {
            var index = (int)Math.Round(hour / 24.0 * (_values.Length - 1));
            var x = PointX(index);
            Text(context, hour.ToString("00") + ":00", x, Bounds.Height - 10, 9, Ink.Muted,
                alignment: TextAlignment.Center);
        }
    }

    private double PlotY(double value) => PointY(value);
}
