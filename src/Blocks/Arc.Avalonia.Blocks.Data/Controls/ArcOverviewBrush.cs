using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>
/// The overview strip under a range chart: a miniature of the whole history with a draggable window.
/// Drag the window to pan, its edges to resize, or the empty track to draw a new window. Left and right
/// arrows pan one step, Shift with the arrows resizes, and Home and End select everything.
/// </summary>
internal sealed class ArcOverviewBrush : DataStage
{
    private readonly double[] _values;
    private DragMode _drag;
    private double _start;
    private double _end = 1;
    private double _dragOrigin;

    private enum DragMode
    {
        None,
        Start,
        End,
        Pan,
        New
    }

    public ArcOverviewBrush(IReadOnlyList<double> values, double start = 0, double end = 1)
    {
        _values = values.ToArray();
        _start = Math.Clamp(start, 0, 0.95);
        _end = Math.Clamp(end, _start + 0.05, 1);
        Height = 44;
    }

    public double Start => _start;

    public double End => _end;

    /// <summary>Moves the window programmatically, for a reset action.</summary>
    public void SetWindow(double start, double end)
    {
        _start = Math.Clamp(start, 0, 0.94);
        _end = Math.Clamp(end, _start + 0.06, 1);
        Raise();
    }

    /// <summary>Raised while the window moves, with the new start and end as 0..1 fractions.</summary>
    public event EventHandler<(double Start, double End)>? WindowChanged;

    protected override double DefaultHeight => 44;

    private Rect Track => new(2, 4, Math.Max(10, Bounds.Width - 4), Math.Max(8, Bounds.Height - 12));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var track = Track;
        if (!track.Contains(point))
        {
            return;
        }

        Focus();
        e.Pointer.Capture(this);
        var startX = track.Left + _start * track.Width;
        var endX = track.Left + _end * track.Width;
        _dragOrigin = point.X;
        if (Math.Abs(point.X - startX) < 8)
        {
            _drag = DragMode.Start;
        }
        else if (Math.Abs(point.X - endX) < 8)
        {
            _drag = DragMode.End;
        }
        else if (point.X > startX && point.X < endX)
        {
            _drag = DragMode.Pan;
        }
        else
        {
            _drag = DragMode.New;
            var fraction = DataMath.Clamp01((point.X - track.Left) / track.Width);
            _start = Math.Max(0, fraction - 0.1);
            _end = Math.Min(1, fraction + 0.1);
            Raise();
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag == DragMode.None)
        {
            var point = e.GetPosition(this);
            var track = Track;
            var startX = track.Left + _start * track.Width;
            var endX = track.Left + _end * track.Width;
            Cursor = Math.Abs(point.X - startX) < 8 || Math.Abs(point.X - endX) < 8
                ? new Cursor(StandardCursorType.SizeWestEast)
                : track.Contains(point) ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            return;
        }

        var trackRect = Track;
        var delta = (e.GetPosition(this).X - _dragOrigin) / Math.Max(1, trackRect.Width);
        var span = _end - _start;
        switch (_drag)
        {
            case DragMode.Pan:
                var shift = Math.Clamp(delta, -_start, 1 - _end);
                _start += shift;
                _end += shift;
                _dragOrigin = e.GetPosition(this).X;
                break;
            case DragMode.Start:
                _start = Math.Clamp(_start + delta, 0, _end - 0.06);
                _dragOrigin = e.GetPosition(this).X;
                break;
            case DragMode.End:
                _end = Math.Clamp(_end + delta, _start + 0.06, 1);
                _dragOrigin = e.GetPosition(this).X;
                break;
            case DragMode.New:
                _end = Math.Clamp(_start + 0.06 + span, 0, 1);
                _drag = DragMode.End;
                break;
        }

        Raise();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag = DragMode.None;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = 1.0 / Math.Max(1, _values.Length - 1) * 3;
        var resize = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left:
                if (resize)
                {
                    _start = Math.Clamp(_start - step, 0, _end - 0.06);
                }
                else
                {
                    var shift = Math.Clamp(-step, -_start, 1 - _end);
                    _start += shift;
                    _end += shift;
                }

                break;
            case Key.Right:
                if (resize)
                {
                    _end = Math.Clamp(_end + step, _start + 0.06, 1);
                }
                else
                {
                    var shift = Math.Clamp(step, -_start, 1 - _end);
                    _start += shift;
                    _end += shift;
                }

                break;
            case Key.Home:
                _start = 0;
                _end = 1;
                break;
            case Key.End:
                _start = 0.7;
                _end = 1;
                break;
            default:
                return;
        }

        Raise();
        e.Handled = true;
    }

    private void Raise()
    {
        WindowChanged?.Invoke(this, (_start, _end));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_values.Length < 2)
        {
            return;
        }

        var track = Track;
        context.DrawRectangle(Ink.Alpha(Ink.SurfaceRaised, 0.7), null, track, 8, 8);
        var min = _values.Min();
        var max = _values.Max();
        var points = new List<Point>(_values.Length);
        for (var index = 0; index < _values.Length; index++)
        {
            points.Add(new Point(
                track.Left + index / (double)(_values.Length - 1) * track.Width,
                DataMath.Map(_values[index], min, max, track.Bottom - 2, track.Top + 2)));
        }

        context.DrawGeometry(Ink.Alpha(Ink.Series(0), 0.18), null, DataStage.AreaGeometry(points, track.Bottom));
        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(Ink.Series(0), 0.75), 1.2), DataStage.LineGeometry(points));

        var window = new Rect(
            track.Left + _start * track.Width,
            track.Top - 2,
            Math.Max(4, (_end - _start) * track.Width),
            track.Height + 4);
        context.DrawRectangle(Ink.Alpha(Ink.Series(0), 0.07), Ink.Pen(Ink.Series(0), 1.2), window, 6, 6);
        context.DrawRectangle(Ink.SurfaceRaised, Ink.Pen(Ink.Series(0), 1), new Rect(window.Left - 1, window.Top, 3, window.Height), 2, 2);
        context.DrawRectangle(Ink.SurfaceRaised, Ink.Pen(Ink.Series(0), 1), new Rect(window.Right - 2, window.Top, 3, window.Height), 2, 2);
    }
}
