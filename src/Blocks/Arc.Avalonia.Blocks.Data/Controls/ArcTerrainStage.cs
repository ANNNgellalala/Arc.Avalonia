using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>
/// A stylised 3D terrain of a 7 by 24 activity grid. Every column is drawn as an isometric block, the
/// camera can orbit and zoom, and flattening springs to a top-down heatmap. It is a 2D projection, not
/// WebGL: there is no perspective divide, the columns are sorted back to front and nothing is depth tested.
/// Arrow keys move the highlighted hour, Shift with the arrows orbits, + and - zoom, Enter highlights the
/// peak of the current metric and Escape returns to the whole week.
/// </summary>
internal sealed class ArcTerrainStage : DataStage
{
    private readonly double[,] _values;
    private readonly double _max;
    private readonly string _unit;
    private int _day = 3;
    private int _hour = 14;
    private double _orbit = 0.5;
    private double _zoom = 1;
    private bool _hovering;

    public ArcTerrainStage(double[,] values, string unit)
    {
        _values = values;
        _unit = unit;
        _max = 1;
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                _max = Math.Max(_max, values[day, hour]);
            }
        }

        Height = 300;
    }

    public bool Flattened { get; set; }

    public int Day => _day;

    public int Hour => _hour;

    /// <summary>Highlights a cell from outside the stage, for a peak hour button.</summary>
    public void Highlight(int day, int hour)
    {
        Flattened = false;
        SetSelection(day, hour, raise: true);
        InvalidateVisual();
    }

    public event EventHandler<(int Day, int Hour)>? SelectionChanged;

    protected override double DefaultHeight => 300;

    private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private double _cellW = 16;
    private double _cellH = 22;
    private double _heightScale = 110;
    private double _tilt = 1;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit is { } cell)
        {
            _hovering = true;
            SetSelection(cell.Day, cell.Hour, raise: false);
        }
        else
        {
            _hovering = false;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovering = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit is { } cell)
        {
            SetSelection(cell.Day, cell.Hour, raise: true);
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
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    _orbit = Math.Clamp(_orbit - 0.04, 0, 1);
                }
                else
                {
                    SetSelection(_day, (_hour + 23) % 24, raise: true);
                    return;
                }

                break;
            case Key.Right:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    _orbit = Math.Clamp(_orbit + 0.04, 0, 1);
                }
                else
                {
                    SetSelection(_day, (_hour + 1) % 24, raise: true);
                    return;
                }

                break;
            case Key.Up:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    _tilt = Math.Min(1.6, _tilt + 0.06);
                }
                else
                {
                    SetSelection((_day + 6) % 7, _hour, raise: true);
                    return;
                }

                break;
            case Key.Down:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    _tilt = Math.Max(0.5, _tilt - 0.06);
                }
                else
                {
                    SetSelection((_day + 1) % 7, _hour, raise: true);
                    return;
                }

                break;
            case Key.Add:
            case Key.OemPlus:
                _zoom = Math.Min(2, _zoom + 0.1);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                _zoom = Math.Max(0.7, _zoom - 0.1);
                break;
            case Key.Home:
                SetSelection(_day, 0, raise: true);
                return;
            case Key.End:
                SetSelection(_day, 23, raise: true);
                return;
            case Key.Enter:
            case Key.Space:
                Flattened = !Flattened;
                break;
            case Key.Escape:
                ResetCamera();
                break;
            default:
                return;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Returns the camera and the flattening to the opening state.</summary>
    public void ResetCamera()
    {
        _zoom = 1;
        _orbit = 0.5;
        _tilt = 1;
        Flattened = false;
        InvalidateVisual();
    }

    private void SetSelection(int day, int hour, bool raise)
    {
        day = Math.Clamp(day, 0, 6);
        hour = Math.Clamp(hour, 0, 23);
        if (day == _day && hour == _hour)
        {
            InvalidateVisual();
            return;
        }

        _day = day;
        _hour = hour;
        InvalidateVisual();
        if (raise)
        {
            SelectionChanged?.Invoke(this, (day, hour));
        }
    }

    private (int Day, int Hour)? HitTest(Point point)
    {
        if (Flattened)
        {
            var grid = FlatGrid;
            if (!grid.Contains(point))
            {
                return null;
            }

            var hour = Math.Clamp((int)((point.X - grid.Left) / grid.Width * 24), 0, 23);
            var day = Math.Clamp((int)((point.Y - grid.Top) / grid.Height * 7), 0, 6);
            return (day, hour);
        }

        var cells = Lattice();
        var best = ((int Day, int Hour)?)null;
        var bestDistance = 26.0;
        foreach (var cell in cells)
        {
            var distance = Math.Sqrt(Math.Pow(cell.Point.X - point.X, 2) + Math.Pow(cell.Point.Y - point.Y, 2));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = (cell.Day, cell.Hour);
            }
        }

        return best;
    }

    private Rect FlatGrid => new(46, 18, Math.Max(10, Bounds.Width - 64), Math.Max(10, Bounds.Height - 56));

    private List<(int Day, int Hour, Point Point, double Base, double Top, double Height)> Lattice()
    {
        var cells = new List<(int, int, Point, double, double, double)>();
        _cellW = Math.Clamp((Bounds.Width - 150) / 24.0, 10, 34);
        _cellH = _cellW * 1.16;
        _heightScale = Math.Min(140, Bounds.Height * 0.32);
        var centerX = Bounds.Width / 2 + 10;
        var centerY = Bounds.Height * 0.60;
        var angle = (_orbit - 0.5) * 1.1;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                var dx = (hour - 11.5) * _cellW * _zoom;
                var dy = (day - 3) * _cellH * _zoom;
                var rx = dx * cos - dy * sin;
                var ry = (dx * sin * 0.45 + dy * cos) * _tilt;
                var height = _values[day, hour] / _max * _heightScale * _zoom;
                var point = new Point(centerX + rx, centerY + ry - height);
                cells.Add((day, hour, point, centerY + ry, point.Y, height));
            }
        }

        // Painter's algorithm: the far row and far hours first.
        cells.Sort((left, right) => left.Item4.CompareTo(right.Item4));
        return cells;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Flattened)
        {
            DrawHeatmap(context);
            return;
        }

        var cells = Lattice();
        var halfW = _cellW * 0.52 * _zoom;
        var halfH = _cellH * 0.30 * _zoom;
        foreach (var cell in cells)
        {
            var value = _values[cell.Day, cell.Hour];
            var t = value / _max;
            var fill = Mix(Ink.Alpha(Ink.Series(0), 0.35), Ink.Series(0), t);
            var top = new Point[]
            {
                new(cell.Point.X, cell.Point.Y - halfH),
                new(cell.Point.X + halfW, cell.Point.Y),
                new(cell.Point.X, cell.Point.Y + halfH),
                new(cell.Point.X - halfW, cell.Point.Y)
            };
            var side = new Point[]
            {
                new(cell.Point.X - halfW, cell.Point.Y),
                new(cell.Point.X, cell.Point.Y + halfH),
                new(cell.Point.X, cell.Base + halfH),
                new(cell.Point.X - halfW, cell.Base)
            };
            var sideTwo = new Point[]
            {
                new(cell.Point.X, cell.Point.Y + halfH),
                new(cell.Point.X + halfW, cell.Point.Y),
                new(cell.Point.X + halfW, cell.Base),
                new(cell.Point.X, cell.Base + halfH)
            };
            context.DrawGeometry(Ink.Alpha(fill, 0.55), null, Polygon(side));
            context.DrawGeometry(Ink.Alpha(fill, 0.45), null, Polygon(sideTwo));
            context.DrawGeometry(fill, cell.Day == _day && cell.Hour == _hour ? Ink.Pen(Ink.Foreground, 1.4) : null, Polygon(top));
        }

        // Day labels sit just left of each row, whatever the orbit angle.
        var angle = (_orbit - 0.5) * 1.1;
        for (var day = 0; day < 7; day++)
        {
            var dx = (0 - 11.5) * _cellW * _zoom;
            var dy = (day - 3) * _cellH * _zoom;
            var x = Bounds.Width / 2 + 10 + (dx * Math.Cos(angle) - dy * Math.Sin(angle));
            var y = Bounds.Height * 0.60 + (dx * Math.Sin(angle) * 0.45 + dy * Math.Cos(angle)) * _tilt;
            Text(context, Days[day], Math.Max(4, x - 18), y, 10, Ink.Muted, alignment: TextAlignment.Right);
        }

        for (var hour = 0; hour < 24; hour += 3)
        {
            var dx = (hour - 11.5) * _cellW * _zoom;
            var dy = (6 - 3) * _cellH * _zoom + 12;
            var x = Bounds.Width / 2 + 10 + (dx * Math.Cos(angle) - dy * Math.Sin(angle));
            var y = Bounds.Height * 0.60 + (dx * Math.Sin(angle) * 0.45 + dy * Math.Cos(angle)) * _tilt;
            Text(context, hour.ToString("00"), x, Math.Min(Bounds.Height - 12, y), 10, Ink.Muted,
                alignment: TextAlignment.Center);
        }

        var valueAtCursor = _values[_day, _hour];
        if (_hovering || IsFocused)
        {
            Tooltip(context, Bounds.Size, new Point(Bounds.Width * 0.5, Bounds.Height * 0.24),
                $"{Days[_day]} {_hour:00}:00", new[]
                {
                    ("Activity", DataKit.Number(valueAtCursor) + " " + _unit, (IBrush?)null),
                    ("Share of peak", DataKit.Percent(valueAtCursor / _max), (IBrush?)null)
                });
        }
    }

    private void DrawHeatmap(DrawingContext context)
    {
        var grid = FlatGrid;
        var cellW = grid.Width / 24;
        var cellH = grid.Height / 7;
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                var t = _values[day, hour] / _max;
                var rect = new Rect(grid.Left + hour * cellW + 1, grid.Top + day * cellH + 1,
                    Math.Max(1, cellW - 2), Math.Max(1, cellH - 2));
                var fill = Mix(Ink.Alpha(Ink.Series(0), 0.12), Ink.Series(0), t);
                context.DrawRectangle(fill, day == _day && hour == _hour ? Ink.Pen(Ink.Foreground, 1.4) : null,
                    rect, 3, 3);
            }
        }

        for (var day = 0; day < 7; day++)
        {
            Text(context, Days[day], grid.Left - 8, grid.Top + day * cellH + cellH / 2, 10, Ink.Muted,
                alignment: TextAlignment.Right);
        }

        for (var hour = 0; hour < 24; hour += 3)
        {
            Text(context, hour.ToString("00"), grid.Left + hour * cellW + cellW / 2, grid.Bottom + 10, 10, Ink.Muted,
                alignment: TextAlignment.Center);
        }
    }

    private static StreamGeometry Polygon(Point[] points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
        {
            context.LineTo(points[i]);
        }

        context.EndFigure(true);
        return geometry;
    }

    private static IBrush Mix(IBrush from, IBrush to, double amount)
    {
        if (from is not ISolidColorBrush a || to is not ISolidColorBrush b)
        {
            return to;
        }

        var t = DataMath.Clamp01(amount);
        return new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(a.Color.A * (1 - t) + b.Color.A * t),
            (byte)Math.Round(a.Color.R * (1 - t) + b.Color.R * t),
            (byte)Math.Round(a.Color.G * (1 - t) + b.Color.G * t),
            (byte)Math.Round(a.Color.B * (1 - t) + b.Color.B * t)));
    }
}
