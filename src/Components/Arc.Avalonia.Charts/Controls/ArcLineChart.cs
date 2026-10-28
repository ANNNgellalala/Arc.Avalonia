using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts;

/// <summary>
/// A multi-series line chart with quiet axes, optional area fills, a crosshair tooltip,
/// and legend items that toggle series. Smooth paths use monotone cubic segments.
/// </summary>
public class ArcLineChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SeriesProperty =
        AvaloniaProperty.Register<ArcLineChart, IReadOnlyList<ChartSeries>?>(nameof(Series));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ArcLineChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<ArcLineChart, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcLineChart, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<ArcLineChart, bool>(nameof(ShowArea), true);

    public static readonly StyledProperty<bool> SmoothProperty =
        AvaloniaProperty.Register<ArcLineChart, bool>(nameof(Smooth), true);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcLineChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcLineChart, Func<double, string>?>(nameof(ValueFormat));

    private readonly HashSet<string> _hidden = new(StringComparer.Ordinal);
    private readonly List<(Rect Bounds, ChartSeries Series)> _legendHits = new();
    private Rect _plot;
    private int _activeIndex = -1;

    static ArcLineChart()
    {
        AffectsRender<ArcLineChart>(
            SeriesProperty,
            LabelsProperty,
            ShowGridProperty,
            ShowLegendProperty,
            ShowAreaProperty,
            SmoothProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        SeriesProperty.Changed.AddClassHandler<ArcLineChart>((chart, _) =>
        {
            chart._hidden.Clear();
            chart._activeIndex = -1;
        });
    }

    public ArcLineChart()
    {
        Focusable = true;
    }

    /// <summary>Lines in draw order. A series with a dashed brush is useful for a target or previous period.</summary>
    public IReadOnlyList<ChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>Category labels under the plot; one per point.</summary>
    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>Draws four quiet horizontal gridlines and the value axis.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Shows series toggles along the top when there is more than one series.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>Fills quietly under the first visible series.</summary>
    public bool ShowArea
    {
        get => GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    /// <summary>Monotone curves by default; false draws straight segments.</summary>
    public bool Smooth
    {
        get => GetValue(SmoothProperty);
        set => SetValue(SmoothProperty, value);
    }

    /// <summary>Unit appended in tooltips, such as "ms" or " visits".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Optional custom number format for tooltips.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 220;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var count = PointCount();
        if (count == 0 || _plot.Width <= 0 || !_plot.Contains(point))
        {
            SetActive(-1);
            return;
        }

        var index = (int)Math.Round((point.X - _plot.Left) / _plot.Width * (count - 1));
        SetActive(Math.Clamp(index, 0, count - 1));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
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

            _activeIndex = -1;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = PointCount();
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? count - 1 : _activeIndex;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                next = Math.Max(0, next - 1);
                break;
            case Key.Right:
            case Key.Up:
                next = Math.Min(count - 1, next + 1);
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = count - 1;
                break;
            case Key.Escape:
                SetActive(-1);
                e.Handled = true;
                return;
            default:
                return;
        }

        SetActive(next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _legendHits.Clear();
        _plot = default;

        var series = Series;
        var count = PointCount();
        if (series is null || series.Count == 0 || count == 0)
        {
            DrawEmpty(context);
            return;
        }

        var visible = series.Where(item => !_hidden.Contains(item.Name)).ToList();
        var showLegend = ShowLegend && series.Count > 1;
        var left = 38.0;
        var right = Bounds.Width - 8;
        var top = showLegend ? 26.0 : 8.0;
        var bottom = Bounds.Height - 22;
        if (right - left < 24 || bottom - top < 24)
        {
            return;
        }

        _plot = new Rect(left, top, right - left, bottom - top);
        var max = NiceMaximum(series);
        var gridPen = new Pen(ChartPalette.BorderSubtle, 1);
        var axisPen = new Pen(ChartPalette.Border, 1);

        if (ShowGrid)
        {
            for (var tick = 0; tick <= 4; tick++)
            {
                var value = max * tick / 4;
                var y = Crisp(ChartFormat.Map(value, 0, max, _plot.Bottom, _plot.Top));
                context.DrawLine(gridPen, new Point(_plot.Left, y), new Point(_plot.Right, y));
                ChartText.Draw(
                    context,
                    ChartFormat.Compact(value),
                    _plot.Left - 6,
                    y,
                    12,
                    ChartPalette.Muted,
                    alignment: TextAlignment.Right);
            }
        }

        context.DrawLine(axisPen, new Point(_plot.Left, _plot.Bottom + 0.5), new Point(_plot.Right, _plot.Bottom + 0.5));

        if (showLegend)
        {
            DrawLegend(context, series);
        }

        if (visible.Count > 0)
        {
            var highlighted = visible[0];
            if (ShowArea)
            {
                var areaPoints = Points(highlighted, count, max);
                if (areaPoints.Count > 1)
                {
                    var area = ChartGeometry.Area(areaPoints, _plot.Bottom, Smooth);
                    context.DrawGeometry(ChartPalette.Alpha(highlighted.Brush ?? ChartPalette.Series(0), 0.10), null, area);
                }
            }

            for (var index = 0; index < visible.Count; index++)
            {
                var item = visible[index];
                var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item));
                var points = Points(item, count, max);
                if (points.Count == 0)
                {
                    continue;
                }

                var geometry = ChartGeometry.Line(points, Smooth);
                var dashed = item.Dashed;
                context.DrawGeometry(null, ChartPalette.Pen(brush, 1.75, dashed), geometry);
                if (points.Count > 0)
                {
                    context.DrawEllipse(ChartPalette.Surface, null, points[^1], 2.4, 2.4);
                    context.DrawEllipse(brush, null, points[^1], 1.6, 1.6);
                }
            }
        }

        DrawCategoryLabels(context, count);
        if (_activeIndex >= 0 && _activeIndex < count)
        {
            DrawCrosshair(context, visible, count, max, _activeIndex);
        }
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartSeries> series)
    {
        var x = 2.0;
        var centerY = 11.0;
        foreach (var item in series)
        {
            var name = ChartText.Create(item.Name, 12, _hidden.Contains(item.Name) ? ChartPalette.Muted : ChartPalette.Secondary);
            var itemWidth = 18 + 5 + name.Width + 12;
            if (x + itemWidth > Bounds.Width)
            {
                break;
            }

            var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item));
            var swatchY = centerY;
            if (_hidden.Contains(item.Name))
            {
                context.DrawLine(
                    ChartPalette.Pen(ChartPalette.Muted, 1.5),
                    new Point(x, swatchY),
                    new Point(x + 12, swatchY));
            }
            else
            {
                context.DrawLine(
                    ChartPalette.Pen(brush, 2, item.Dashed),
                    new Point(x, swatchY),
                    new Point(x + 12, swatchY));
            }

            context.DrawText(name, new Point(x + 18, centerY - name.Height / 2));
            _legendHits.Add((new Rect(x - 3, centerY - 10, itemWidth, 20), item));
            x += itemWidth;
        }
    }

    private void DrawCategoryLabels(DrawingContext context, int count)
    {
        var labels = Labels;
        if (labels is null || labels.Count == 0)
        {
            return;
        }

        var maxLabels = Math.Max(2, (int)(_plot.Width / 76));
        var step = Math.Max(1, (int)Math.Ceiling(count / (double)maxLabels));
        for (var i = 0; i < count; i += step)
        {
            DrawLabel(i);
        }

        if ((count - 1) % step != 0)
        {
            DrawLabel(count - 1);
        }

        void DrawLabel(int index)
        {
            if (index < 0 || index >= labels.Count)
            {
                return;
            }

            var text = ChartText.Create(labels[index], 12, ChartPalette.Muted, maxWidth: 64);
            var x = Math.Clamp(
                PointX(index, count),
                text.Width / 2 + 2,
                Math.Max(text.Width / 2 + 2, Bounds.Width - text.Width / 2 - 2));
            ChartText.Draw(context, text, x, _plot.Bottom + 11, TextAlignment.Center);
        }
    }

    private void DrawCrosshair(
        DrawingContext context,
        IReadOnlyList<ChartSeries> visible,
        int count,
        double max,
        int index)
    {
        var x = PointX(index, count);
        context.DrawLine(
            new Pen(ChartPalette.BorderStrong, 1, new DashStyle(new[] { 3.0, 3.0 }, 0)),
            new Point(x, _plot.Top),
            new Point(x, _plot.Bottom));

        foreach (var item in visible)
        {
            var brush = item.Brush ?? ChartPalette.Series(SeriesIndex(item));
            var value = ValueAt(item, index);
            var y = ChartFormat.Map(value, 0, max, _plot.Bottom, _plot.Top);
            context.DrawEllipse(brush, new Pen(ChartPalette.Surface, 1.5), new Point(x, y), 4.2, 4.2);
        }

        var labels = Labels;
        var title = labels is not null && index < labels.Count ? labels[index] : $"Point {index + 1}";
        var rows = visible.Select(item => (
            item.Name,
            FormatValue(ValueAt(item, index)),
            (IBrush?)(item.Brush ?? ChartPalette.Series(SeriesIndex(item))))).ToList();
        ChartTooltip.Draw(context, Bounds, new Point(x, _plot.Top + _plot.Height / 2), title, rows);
    }

    private void DrawEmpty(DrawingContext context)
    {
        ChartText.Draw(
            context,
            "No data",
            Bounds.Width / 2,
            Bounds.Height / 2,
            13,
            ChartPalette.Muted,
            alignment: TextAlignment.Center);
    }

    private List<Point> Points(ChartSeries series, int count, double max)
    {
        var points = new List<Point>(count);
        for (var i = 0; i < count; i++)
        {
            var value = ValueAt(series, i);
            points.Add(new Point(PointX(i, count), ChartFormat.Map(value, 0, max, _plot.Bottom, _plot.Top)));
        }

        return points;
    }

    private double PointX(int index, int count)
        => count <= 1 ? _plot.Center.X : _plot.Left + index / (double)(count - 1) * _plot.Width;

    private int PointCount()
    {
        var series = Series;
        var count = series is null || series.Count == 0 ? 0 : series.Max(item => item.Values.Count);
        return Math.Max(count, Labels?.Count ?? 0);
    }

    private static double ValueAt(ChartSeries series, int index)
        => index >= 0 && index < series.Values.Count ? series.Values[index] : 0;

    private int SeriesIndex(ChartSeries item)
    {
        var source = Series;
        if (source is not null)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (ReferenceEquals(source[i], item) || string.Equals(source[i].Name, item.Name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    private double NiceMaximum(IReadOnlyList<ChartSeries> series)
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
            return 1;
        }

        max *= 1.06;
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

    private void SetActive(int index)
    {
        if (_activeIndex == index)
        {
            return;
        }

        _activeIndex = index;
        InvalidateVisual();
    }
}
