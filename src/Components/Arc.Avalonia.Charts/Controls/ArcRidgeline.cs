using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A ridgeline (joy) plot: one kernel density per series, overlapping like mountain ridges.
/// All ridges share the accent family; the hovered ridge is the single full-strength colour.
/// </summary>
public class ArcRidgeline : ArcChartBase
{
    private const int Samples = 96;

    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SeriesProperty =
        AvaloniaProperty.Register<ArcRidgeline, IReadOnlyList<ChartSeries>?>(nameof(Series));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcRidgeline, string?>(nameof(Unit));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcRidgeline, Func<double, string>?>(nameof(ValueFormat));

    public static readonly StyledProperty<int> HighlightIndexProperty =
        AvaloniaProperty.Register<ArcRidgeline, int>(nameof(HighlightIndex));

    private Rect _plot;
    private double _spacing;
    private double _ridgeHeight;
    private readonly List<double> _baselines = new();
    private int _activeIndex = -1;

    static ArcRidgeline()
    {
        AffectsRender<ArcRidgeline>(SeriesProperty, UnitProperty, ValueFormatProperty, HighlightIndexProperty);
        SeriesProperty.Changed.AddClassHandler<ArcRidgeline>((chart, _) => chart._activeIndex = -1);
    }

    public ArcRidgeline()
    {
        Focusable = true;
    }

    public IReadOnlyList<ChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>Unit after values in the hover readout, such as " ms".</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    /// <summary>Ridge index that uses the full accent; all other ridges are quiet.</summary>
    public int HighlightIndex
    {
        get => GetValue(HighlightIndexProperty);
        set => SetValue(HighlightIndexProperty, value);
    }

    protected override double DefaultHeight => 220;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_plot.Width <= 0 || point.X < _plot.Left - 60 || point.X > _plot.Right)
        {
            SetActive(-1);
            return;
        }

        var best = -1;
        var bestDistance = _spacing * 0.8;
        for (var i = 0; i < _baselines.Count; i++)
        {
            var distance = Math.Abs(point.Y - _baselines[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        SetActive(best);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Series?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? 0 : _activeIndex;
        switch (e.Key)
        {
            case Key.Up:
                next = Math.Max(0, next - 1);
                break;
            case Key.Down:
                next = Math.Min(count - 1, next + 1);
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
        _baselines.Clear();
        var series = Series;
        if (series is null || series.Count == 0 || series.All(item => item.Values.Count == 0))
        {
            ChartText.Draw(
                context,
                "No distributions",
                Bounds.Width / 2,
                Bounds.Height / 2,
                ChartMetrics.TitleFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        _plot = new Rect(64, 8, Math.Max(1, Bounds.Width - 72), Math.Max(1, Bounds.Height - 32));
        if (_plot.Width < 30 || _plot.Height < 30)
        {
            return;
        }

        var count = series.Count;
        var baseSpacing = _plot.Height / (count + 1.35);
        _ridgeHeight = Math.Min(baseSpacing * 2.05, _plot.Height * 0.42);
        var firstBaseline = _plot.Top + _ridgeHeight;
        _spacing = count <= 1 ? _plot.Height * 0.5 : (_plot.Bottom - firstBaseline) / (count - 1);
        for (var i = 0; i < count; i++)
        {
            _baselines.Add(count <= 1 ? _plot.Bottom - _ridgeHeight / 2 : firstBaseline + i * _spacing);
        }

        var domain = Domain(series);
        var kernels = series.Select(item => Density(item.Values, domain, out _)).ToList();
        var draws = Enumerable.Range(0, count).Where(index => index != _activeIndex).ToList();
        if (_activeIndex >= 0 && _activeIndex < count)
        {
            draws.Add(_activeIndex);
        }

        foreach (var index in draws)
        {
            DrawRidge(context, series[index], kernels[index], index, index == _activeIndex);
        }

        if (_activeIndex >= 0 && _activeIndex < count)
        {
            var values = series[_activeIndex].Values;
            if (values.Count > 0)
            {
                var sorted = values.OrderBy(value => value).ToArray();
                ChartTooltip.Draw(
                    context,
                    Bounds,
                    new Point(_plot.Left + _plot.Width * 0.65, _baselines[_activeIndex] - 10),
                    series[_activeIndex].Name,
                    new[]
                    {
                        ("Median", Format(Quantile(sorted, 0.5)), (IBrush?)null),
                        ("Middle half", Format(Quantile(sorted, 0.25)) + " to " + Format(Quantile(sorted, 0.75)), null),
                        ("Range", Format(sorted[0]) + " to " + Format(sorted[^1]), null),
                        ("Count", sorted.Length.ToString(), null)
                    });
            }
        }

        ChartText.Draw(
            context,
            ChartFormat.Compact(domain.Min),
            _plot.Left,
            _plot.Bottom + 11,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted);
        ChartText.Draw(
            context,
            ChartFormat.Compact(domain.Max),
            _plot.Right,
            _plot.Bottom + 11,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Right);
    }

    private void DrawRidge(
        DrawingContext context,
        ChartSeries series,
        IReadOnlyList<double> density,
        int index,
        bool active)
    {
        if (density.Count == 0)
        {
            return;
        }

        var max = density.Max();
        if (max <= 0)
        {
            max = 1;
        }

        var baseline = _baselines[index] - (active ? 4 : 0);
        context.DrawLine(
            new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
            new Point(_plot.Left, Crisp(baseline)),
            new Point(_plot.Right, Crisp(baseline)));

        var points = new List<Point>(density.Count);
        for (var i = 0; i < density.Count; i++)
        {
            var x = _plot.Left + i / (double)(density.Count - 1) * _plot.Width;
            var y = baseline - density[i] / max * _ridgeHeight;
            points.Add(new Point(x, y));
        }

        var highlighted = active || index == HighlightIndex;
        var accent = ChartPalette.Series(0);
        var stroke = highlighted
            ? ChartPalette.Pen(accent, 1.75)
            : ChartPalette.Pen(ChartPalette.BorderStrong, 1.1);
        var fill = highlighted
            ? ChartPalette.Alpha(accent, active ? 0.28 : 0.20)
            : ChartPalette.Alpha(ChartPalette.BorderStrong, 0.10);
        context.DrawGeometry(fill, stroke, ChartGeometry.Area(points, baseline, true));

        if (active || index == HighlightIndex)
        {
            var median = Quantile(series.Values.OrderBy(value => value).ToArray(), 0.5);
            var x = MapValue(median, Domain(Series!));
            context.DrawLine(
                new Pen(accent, 1.2, new DashStyle(new[] { 2.0, 2.0 }, 0)),
                new Point(x, baseline - _ridgeHeight * 0.82),
                new Point(x, baseline));
        }

        ChartText.Draw(
            context,
            series.Name,
            56,
            baseline - 3,
            ChartMetrics.LabelFontSize,
            active || index == HighlightIndex ? ChartPalette.Foreground : ChartPalette.Secondary,
            active || index == HighlightIndex ? FontWeight.Medium : FontWeight.Normal,
            TextAlignment.Right,
            maxWidth: 58);
    }

    private static (double Min, double Max) Domain(IReadOnlyList<ChartSeries> series)
    {
        var values = series.SelectMany(item => item.Values).Where(value => double.IsFinite(value)).ToArray();
        if (values.Length == 0)
        {
            return (0, 1);
        }

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= 1;
            max += 1;
        }

        var pad = (max - min) * 0.08;
        return (min - pad, max + pad);
    }

    private static List<double> Density(IReadOnlyList<double> values, (double Min, double Max) domain, out double bandwidth)
    {
        var result = new List<double>(Samples);
        if (values.Count == 0)
        {
            bandwidth = 1;
            return result;
        }

        var mean = values.Average();
        var variance = values.Count > 1
            ? values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1)
            : 0;
        var std = Math.Sqrt(Math.Max(0, variance));
        bandwidth = 1.06 * Math.Max(std, (domain.Max - domain.Min) / 20) * Math.Pow(values.Count, -0.2);
        bandwidth = Math.Max(1e-6, bandwidth);
        var factor = 1 / (values.Count * bandwidth * Math.Sqrt(2 * Math.PI));
        for (var i = 0; i < Samples; i++)
        {
            var x = domain.Min + (domain.Max - domain.Min) * i / (Samples - 1);
            var sum = 0.0;
            foreach (var value in values)
            {
                var z = (x - value) / bandwidth;
                sum += Math.Exp(-0.5 * z * z);
            }

            result.Add(sum * factor);
        }

        return result;
    }

    private double MapValue(double value, (double Min, double Max) domain)
        => _plot.Left + (value - domain.Min) / Math.Max(1e-9, domain.Max - domain.Min) * _plot.Width;

    private static double Quantile(double[] sorted, double q)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var position = (sorted.Length - 1) * q;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? sorted[lower]
            : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private string Format(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Compact(value);
        return string.IsNullOrEmpty(Unit) ? formatted : formatted + Unit;
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
