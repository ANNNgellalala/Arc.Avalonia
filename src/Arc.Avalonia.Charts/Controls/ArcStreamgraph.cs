using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts;

/// <summary>
/// Stacked stream layers on a softly varying baseline. Hovering a layer quiets the others and
/// reads that layer's value at the nearest point in time.
/// </summary>
public class ArcStreamgraph : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SeriesProperty =
        AvaloniaProperty.Register<ArcStreamgraph, IReadOnlyList<ChartSeries>?>(nameof(Series));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ArcStreamgraph, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcStreamgraph, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcStreamgraph, string?>(nameof(ValueSuffix));

    private readonly List<(Rect Bounds, int Index)> _legendHits = new();
    private Rect _plot;
    private double[] _baseline = Array.Empty<double>();
    private double[] _totals = Array.Empty<double>();
    private double[,] _tops = new double[0, 0];
    private double[,] _bottoms = new double[0, 0];
    private double _scale;
    private int _activeLayer = -1;
    private int _activeColumn = -1;

    static ArcStreamgraph()
    {
        AffectsRender<ArcStreamgraph>(
            SeriesProperty,
            LabelsProperty,
            ShowLegendProperty,
            ValueSuffixProperty);
        SeriesProperty.Changed.AddClassHandler<ArcStreamgraph>((chart, _) =>
        {
            chart._activeLayer = -1;
            chart._activeColumn = -1;
        });
    }

    public ArcStreamgraph()
    {
        Focusable = true;
    }

    public IReadOnlyList<ChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    protected override double DefaultHeight => 240;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                var legendColumn = _activeColumn >= 0 ? _activeColumn : Math.Max(0, ColumnCount() - 1);
                SetActive(hit.Index, legendColumn);
                return;
            }
        }

        var count = ColumnCount();
        if (count == 0 || _plot.Width <= 0 || !_plot.Contains(point))
        {
            SetActive(-1, -1);
            return;
        }

        var column = (int)Math.Round((point.X - _plot.Left) / _plot.Width * (count - 1));
        column = Math.Clamp(column, 0, count - 1);
        for (var layer = 0; layer < (Series?.Count ?? 0); layer++)
        {
            if (point.Y >= _tops[layer, column] && point.Y <= _bottoms[layer, column])
            {
                SetActive(layer, column);
                return;
            }
        }

        SetActive(-1, column);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1, -1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = ColumnCount();
        var layers = Series?.Count ?? 0;
        if (count == 0 || layers == 0)
        {
            return;
        }

        var column = _activeColumn < 0 ? count - 1 : _activeColumn;
        var layer = _activeLayer < 0 ? 0 : _activeLayer;
        switch (e.Key)
        {
            case Key.Left:
                column = Math.Max(0, column - 1);
                break;
            case Key.Right:
                column = Math.Min(count - 1, column + 1);
                break;
            case Key.Up:
                layer = Math.Max(0, layer - 1);
                break;
            case Key.Down:
                layer = Math.Min(layers - 1, layer + 1);
                break;
            case Key.Escape:
                SetActive(-1, -1);
                e.Handled = true;
                return;
            default:
                return;
        }

        SetActive(layer, column);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _legendHits.Clear();
        var series = Series;
        var count = ColumnCount();
        if (series is null || series.Count == 0 || count == 0)
        {
            ChartText.Draw(
                context,
                "No data",
                Bounds.Width / 2,
                Bounds.Height / 2,
                13,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        var showLegend = ShowLegend && series.Count > 1;
        var top = showLegend ? 28.0 : 8.0;
        _plot = new Rect(8, top, Math.Max(1, Bounds.Width - 16), Math.Max(1, Bounds.Height - top - 22));
        if (_plot.Width < 30 || _plot.Height < 30)
        {
            return;
        }

        _totals = new double[count];
        for (var column = 0; column < count; column++)
        {
            _totals[column] = series.Sum(item => Math.Max(0, ValueAt(item, column)));
        }

        var maxTotal = Math.Max(1, _totals.Max());
        _scale = _plot.Height * 0.78 / maxTotal;
        var smoothed = Smooth(_totals, 2);
        var average = smoothed.Average();
        _baseline = new double[count];
        for (var column = 0; column < count; column++)
        {
            var wiggle = (smoothed[column] - average) * 0.22 * _scale;
            _baseline[column] = _plot.Center.Y + _totals[column] * _scale / 2 - wiggle;
        }

        _tops = new double[series.Count, count];
        _bottoms = new double[series.Count, count];
        for (var layer = 0; layer < series.Count; layer++)
        {
            for (var column = 0; column < count; column++)
            {
                var previous = 0.0;
                for (var prior = 0; prior < layer; prior++)
                {
                    previous += Math.Max(0, ValueAt(series[prior], column));
                }

                var current = previous + Math.Max(0, ValueAt(series[layer], column));
                _bottoms[layer, column] = _baseline[column] - previous * _scale;
                _tops[layer, column] = _baseline[column] - current * _scale;
            }
        }

        if (showLegend)
        {
            DrawLegend(context, series);
        }

        for (var layer = 0; layer < series.Count; layer++)
        {
            var topPoints = new List<Point>(count);
            var bottomPoints = new List<Point>(count);
            for (var column = 0; column < count; column++)
            {
                var x = PointX(column, count);
                topPoints.Add(new Point(x, _tops[layer, column]));
                bottomPoints.Add(new Point(x, _bottoms[layer, column]));
            }

            var brush = series[layer].Brush ?? ChartPalette.Series(layer);
            if (_activeLayer >= 0 && layer != _activeLayer)
            {
                brush = ChartPalette.Alpha(brush, 0.28);
            }

            context.DrawGeometry(
                ChartPalette.Alpha(brush, _activeLayer < 0 ? 0.80 : 0.92),
                new Pen(ChartPalette.Surface, 1.3),
                Ribbon(topPoints, bottomPoints));
        }

        DrawLabels(context, count);
        if (_activeLayer >= 0 && _activeColumn >= 0)
        {
            DrawTooltip(context, series, count);
        }
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartSeries> series)
    {
        var x = 2.0;
        var centerY = 12.0;
        for (var index = 0; index < series.Count; index++)
        {
            var item = series[index];
            var text = ChartText.Create(item.Name, 12, ChartPalette.Secondary);
            var width = 14 + 5 + text.Width + 12;
            if (x + width > Bounds.Width)
            {
                break;
            }

            var brush = item.Brush ?? ChartPalette.Series(index);
            context.DrawRectangle(brush, null, new Rect(x, centerY - 4, 8, 8), 2, 2);
            context.DrawText(text, new Point(x + 14, centerY - text.Height / 2));
            _legendHits.Add((new Rect(x - 2, centerY - 10, width, 20), index));
            x += width;
        }
    }

    private void DrawLabels(DrawingContext context, int count)
    {
        var labels = Labels;
        if (labels is null || labels.Count == 0)
        {
            return;
        }

        var maxLabels = Math.Max(2, (int)(_plot.Width / 72));
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

    private void DrawTooltip(DrawingContext context, IReadOnlyList<ChartSeries> series, int count)
    {
        var layer = series[_activeLayer];
        var value = Math.Max(0, ValueAt(layer, _activeColumn));
        var total = _totals[_activeColumn];
        var title = Labels is not null && _activeColumn < Labels.Count
            ? Labels[_activeColumn]
            : $"Point {_activeColumn + 1}";
        ChartTooltip.Draw(
            context,
            Bounds,
            new Point(PointX(_activeColumn, count), (_tops[_activeLayer, _activeColumn] + _bottoms[_activeLayer, _activeColumn]) / 2),
            title,
            new[]
            {
                (layer.Name, Format(value), (IBrush?)(layer.Brush ?? ChartPalette.Series(_activeLayer))),
                ("Share", total > 0 ? ChartFormat.Percent(value / total, 1) : "0%", null)
            });
    }

    private static StreamGeometry Ribbon(IReadOnlyList<Point> top, IReadOnlyList<Point> bottom)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(bottom[0], true);
        for (var i = 1; i < bottom.Count; i++)
        {
            context.LineTo(bottom[i]);
        }

        for (var i = top.Count - 1; i >= 0; i--)
        {
            context.LineTo(top[i]);
        }

        context.EndFigure(true);
        return geometry;
    }

    private static double[] Smooth(double[] values, int radius)
    {
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var from = Math.Max(0, i - radius);
            var to = Math.Min(values.Length - 1, i + radius);
            var sum = 0.0;
            for (var j = from; j <= to; j++)
            {
                sum += values[j];
            }

            result[i] = sum / (to - from + 1);
        }

        return result;
    }

    private double PointX(int index, int count)
        => count <= 1 ? _plot.Center.X : _plot.Left + index / (double)(count - 1) * _plot.Width;

    private int ColumnCount()
    {
        var series = Series;
        var count = series is null || series.Count == 0 ? 0 : series.Max(item => item.Values.Count);
        return Math.Max(count, Labels?.Count ?? 0);
    }

    private static double ValueAt(ChartSeries series, int index)
        => index >= 0 && index < series.Values.Count ? series.Values[index] : 0;

    private string Format(double value)
    {
        var formatted = ChartFormat.Compact(value);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }

    private void SetActive(int layer, int column)
    {
        if (_activeLayer == layer && _activeColumn == column)
        {
            return;
        }

        _activeLayer = layer;
        _activeColumn = column;
        InvalidateVisual();
    }
}

