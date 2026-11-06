using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A profile comparison on one web: four concentric guides, one spoke per measure, and one polygon per
/// series with a light fill and a two pixel stroke. Moving across the web picks the nearest axis and
/// compares every profile there; the legend brings a shape forward on hover and hides it on click.
/// </summary>
/// <remarks>
/// Approximation: Arc Pro morphs a shape when the series at one position is replaced. Avalonia data here
/// is assigned rather than swapped by position, so the web redraws; the hover, legend, and keyboard
/// states follow the public behaviour.
/// </remarks>
public class ArcRadarChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<string>?> AxesProperty =
        AvaloniaProperty.Register<ArcRadarChart, IReadOnlyList<string>?>(nameof(Axes));

    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SeriesProperty =
        AvaloniaProperty.Register<ArcRadarChart, IReadOnlyList<ChartSeries>?>(nameof(Series));

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<ArcRadarChart, double>(nameof(MaxValue));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcRadarChart, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcRadarChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcRadarChart, Func<double, string>?>(nameof(ValueFormat));

    private const int Guides = 4;

    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);
    private readonly List<(Rect Bounds, ChartSeries Series)> _legendHits = new();
    private Point _center;
    private double _radius;
    private int _activeAxis = -1;
    private string? _hoverSeries;
    private bool _keyboardFocus;

    static ArcRadarChart()
    {
        AffectsRender<ArcRadarChart>(
            AxesProperty,
            SeriesProperty,
            MaxValueProperty,
            ShowLegendProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        SeriesProperty.Changed.AddClassHandler<ArcRadarChart>((chart, _) =>
        {
            chart._hidden.Clear();
            chart._hoverSeries = null;
            chart._activeAxis = -1;
        });
        AxesProperty.Changed.AddClassHandler<ArcRadarChart>((chart, _) => chart._activeAxis = -1);
    }

    public ArcRadarChart()
    {
        Focusable = true;
    }

    /// <summary>Measure names around the web, clockwise from the top. At least three are needed.</summary>
    public IReadOnlyList<string>? Axes
    {
        get => GetValue(AxesProperty);
        set => SetValue(AxesProperty, value);
    }

    /// <summary>Profiles to compare. Each series supplies one value per axis, in axis order.</summary>
    public IReadOnlyList<ChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>Top of every axis. Zero follows the data.</summary>
    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>Shows the profile keys above the web; a click hides a profile.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>Unit after values in the tooltip, such as " pts".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Formats values in the tooltip.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 244;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                if (!string.Equals(_hoverSeries, hit.Series.Name, StringComparison.Ordinal))
                {
                    _hoverSeries = hit.Series.Name;
                    InvalidateVisual();
                }

                _activeAxis = -1;
                return;
            }
        }

        if (!string.Equals(_hoverSeries, null, StringComparison.Ordinal))
        {
            _hoverSeries = null;
            InvalidateVisual();
        }

        SetAxis(AxisAt(point));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        var changed = _hoverSeries is not null || _activeAxis >= 0;
        _hoverSeries = null;
        _activeAxis = -1;
        if (changed)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_keyboardFocus)
        {
            _keyboardFocus = false;
            InvalidateVisual();
        }

        var point = e.GetPosition(this);
        foreach (var hit in _legendHits)
        {
            if (!hit.Bounds.Contains(point))
            {
                continue;
            }

            if (!_hidden.Remove(hit.Series.Name))
            {
                _hidden.Add(hit.Series.Name);
            }

            var seriesIndex = -1;
            if (Series is { } seriesList)
            {
                for (var s = 0; s < seriesList.Count; s++)
                {
                    if (ReferenceEquals(seriesList[s], hit.Series))
                    {
                        seriesIndex = s;
                        break;
                    }
                }
            }

            RaiseLegendClick(seriesIndex, hit.Series.Name, !_hidden.Contains(hit.Series.Name));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        SetAxis(AxisAt(point));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Axes?.Count ?? 0;
        if (count < 3)
        {
            return;
        }

        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        var next = _activeAxis < 0 ? 0 : _activeAxis;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                next = (next + 1) % count;
                break;
            case Key.Left:
            case Key.Up:
                next = (next - 1 + count) % count;
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = count - 1;
                break;
            case Key.Escape:
                SetAxis(-1);
                e.Handled = true;
                return;
            default:
                return;
        }

        _hoverSeries = null;
        SetAxis(next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _legendHits.Clear();

        var axes = Axes;
        var series = Series?.ToList() ?? new List<ChartSeries>();
        var visible = series.Where(item => !_hidden.Contains(item.Name)).ToList();
        if (axes is null || axes.Count < 3 || series.Count == 0)
        {
            ChartText.Draw(
                context,
                "No data",
                Bounds.Width / 2,
                Bounds.Height / 2,
                ChartMetrics.TitleFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        var showLegend = ShowLegend && series.Count > 1;
        var top = showLegend ? 46.0 : 18.0;
        var labelRoom = Math.Min(132, Bounds.Width * 0.28);
        _radius = Math.Max(36, Math.Min(
            (Bounds.Width - labelRoom * 2) / 2,
            (Bounds.Height - top - 30) / 2));
        _center = new Point(Bounds.Width / 2, top + _radius);

        if (showLegend)
        {
            DrawLegend(context, series);
        }

        var maximum = MaxValue > 0 ? MaxValue : NiceMaximum(series);

        // Shapes, back to front so a hovered profile sits on top.
        var ordered = visible
            .OrderBy(item => string.Equals(item.Name, _hoverSeries, StringComparison.Ordinal) ? 1 : 0)
            .ToList();
        foreach (var item in ordered)
        {
            var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item, series));
            var dim = _hoverSeries is not null && !string.Equals(item.Name, _hoverSeries, StringComparison.Ordinal);
            var points = new List<Point>(axes.Count);
            for (var index = 0; index < axes.Count; index++)
            {
                var value = ValueAt(item, index);
                points.Add(PointAt(index, axes.Count, _radius * ChartFormat.Clamp01(value / Math.Max(1e-6, maximum))));
            }

            context.DrawGeometry(
                ChartPalette.Alpha(brush, dim ? 0.05 : 0.12),
                ChartPalette.Pen(dim ? ChartPalette.Alpha(brush, 0.35) : brush, 2),
                Polygon(points, true));
        }

        // Guides sit over the shapes so the web stays readable through three profiles.
        var guidePen = new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline);
        for (var guide = 1; guide <= Guides; guide++)
        {
            var ring = new List<Point>();
            for (var index = 0; index < axes.Count; index++)
            {
                ring.Add(PointAt(index, axes.Count, _radius * guide / Guides));
            }

            context.DrawGeometry(null, guidePen, Polygon(ring, true));
        }

        for (var index = 0; index < axes.Count; index++)
        {
            var at = PointAt(index, axes.Count, _radius);
            context.DrawLine(
                new Pen(index == _activeAxis ? ChartPalette.BorderStrong : ChartPalette.BorderSubtle, ChartMetrics.Hairline),
                _center,
                at);
        }

        // Axis labels.
        for (var index = 0; index < axes.Count; index++)
        {
            var at = PointAt(index, axes.Count, _radius + 13);
            var angle = AngleOf(index, axes.Count);
            var alignment = Math.Cos(angle) switch
            {
                > 0.28 => TextAlignment.Left,
                < -0.28 => TextAlignment.Right,
                _ => TextAlignment.Center
            };
            var y = at.Y + (Math.Sin(angle) switch { > 0.3 => 4, < -0.3 => -4, _ => 0 });
            var label = ChartText.Create(
                axes[index],
                ChartMetrics.CaptionFontSize,
                index == _activeAxis ? ChartPalette.Foreground : ChartPalette.Muted,
                maxWidth: labelRoom);
            ChartText.Draw(context, label, at.X, y, alignment);
        }

        // Vertex markers on the axis in focus.
        if (_activeAxis >= 0 && _activeAxis < axes.Count)
        {
            foreach (var item in visible)
            {
                var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item, series));
                var value = ValueAt(item, _activeAxis);
                var at = PointAt(_activeAxis, axes.Count, _radius * ChartFormat.Clamp01(value / Math.Max(1e-6, maximum)));
                context.DrawEllipse(ChartPalette.Surface, null, at, 4.2, 4.2);
                context.DrawEllipse(brush, null, at, 2.8, 2.8);
            }

            var vertex = PointAt(_activeAxis, axes.Count, _radius + 2);
            var rows = visible
                .Select(item => (
                    item.Name,
                    FormatValue(ValueAt(item, _activeAxis)),
                    (IBrush?)(item.Brush ?? ChartPalette.Series(SeriesIndex(item, series)))))
                .ToList();
            ChartTooltip.Draw(context, Bounds, vertex, axes[_activeAxis], rows);
        }

        if (_keyboardFocus && IsFocused)
        {
            context.DrawRectangle(
                null,
                new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2),
                new RoundedRect(
                    new Rect(1.5, 1.5, Math.Max(0, Bounds.Width - 3), Math.Max(0, Bounds.Height - 3)),
                    12));
        }
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartSeries> series)
    {
        var x = 2.0;
        var centerY = 11.0;
        foreach (var item in series)
        {
            var hidden = _hidden.Contains(item.Name);
            var name = ChartText.Create(
                item.Name,
                ChartMetrics.CaptionFontSize,
                hidden ? ChartPalette.Muted : ChartPalette.Secondary,
                string.Equals(item.Name, _hoverSeries, StringComparison.Ordinal) ? FontWeight.Medium : FontWeight.Normal);
            var itemWidth = 18 + 5 + name.Width + 12;
            if (x + itemWidth > Bounds.Width)
            {
                break;
            }

            var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item, series));
            var swatchY = centerY;
            if (hidden)
            {
                context.DrawLine(ChartPalette.Pen(ChartPalette.Muted, 1.5), new Point(x, swatchY), new Point(x + 12, swatchY));
            }
            else
            {
                context.DrawLine(ChartPalette.Pen(brush, 2), new Point(x, swatchY), new Point(x + 12, swatchY));
                context.DrawEllipse(brush, null, new Point(x + 6, swatchY), 2.6, 2.6);
            }

            context.DrawText(name, new Point(x + 18, centerY - name.Height / 2));
            _legendHits.Add((new Rect(x - 3, centerY - 10, itemWidth, 20), item));
            x += itemWidth;
        }
    }

    private Point PointAt(int index, int count, double radius)
    {
        var angle = AngleOf(index, count);
        return new Point(_center.X + Math.Cos(angle) * radius, _center.Y + Math.Sin(angle) * radius);
    }

    private static double AngleOf(int index, int count)
        => -Math.PI / 2 + index * 2 * Math.PI / count;

    private int AxisAt(Point point)
    {
        var count = Axes?.Count ?? 0;
        if (count < 3 || _radius <= 0)
        {
            return -1;
        }

        var dx = point.X - _center.X;
        var dy = point.Y - _center.Y;
        if (Math.Sqrt(dx * dx + dy * dy) > _radius + 24)
        {
            return -1;
        }

        var angle = Math.Atan2(dy, dx) + Math.PI / 2;
        if (angle < 0)
        {
            angle += 2 * Math.PI;
        }

        var step = 2 * Math.PI / count;
        var index = (int)Math.Round(angle / step) % count;
        return index < 0 ? index + count : index;
    }

    private void SetAxis(int index)
    {
        if (_activeAxis == index)
        {
            return;
        }

        _activeAxis = index;
        InvalidateVisual();
    }

    private static StreamGeometry Polygon(IReadOnlyList<Point> points, bool close)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(points[0], close);
        for (var index = 1; index < points.Count; index++)
        {
            context.LineTo(points[index]);
        }

        context.EndFigure(close);
        return geometry;
    }

    private static double ValueAt(ChartSeries series, int index)
        => index >= 0 && index < series.Values.Count ? Math.Max(0, series.Values[index]) : 0;

    private static int SeriesIndex(ChartSeries item, IReadOnlyList<ChartSeries> source)
    {
        for (var index = 0; index < source.Count; index++)
        {
            if (ReferenceEquals(source[index], item) ||
                string.Equals(source[index].Name, item.Name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static double NiceMaximum(IReadOnlyList<ChartSeries> series)
    {
        var max = 0.0;
        foreach (var item in series)
        {
            foreach (var value in item.Values)
            {
                max = Math.Max(max, value);
            }
        }

        if (max <= 0)
        {
            return 100;
        }

        max *= 1.05;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(max)));
        var normalized = max / magnitude;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 0);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }
}
