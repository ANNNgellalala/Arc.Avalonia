using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A single-measure column chart: rounded-top bars, a quiet value axis, an optional average
/// line, and a value readout while a bar is hovered or focused.
/// </summary>
public class ArcBarChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ArcBarChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ArcBarChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<ArcBarChart, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> ShowAverageProperty =
        AvaloniaProperty.Register<ArcBarChart, bool>(nameof(ShowAverage), true);

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<ArcBarChart, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcBarChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcBarChart, Func<double, string>?>(nameof(ValueFormat));

    private Rect _plot;
    private int _hoverIndex = -1;

    static ArcBarChart()
    {
        AffectsRender<ArcBarChart>(
            ValuesProperty,
            LabelsProperty,
            ShowGridProperty,
            ShowAverageProperty,
            BarBrushProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        ValuesProperty.Changed.AddClassHandler<ArcBarChart>((chart, _) => chart._hoverIndex = -1);
    }

    public ArcBarChart()
    {
        Focusable = true;
    }

    /// <summary>Bar values in display order.</summary>
    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Category labels under the bars; one per value.</summary>
    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>Draws quiet horizontal gridlines and value labels.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Draws the mean as a dashed reference line.</summary>
    public bool ShowAverage
    {
        get => GetValue(ShowAverageProperty);
        set => SetValue(ShowAverageProperty, value);
    }

    /// <summary>Bar colour; defaults to the first series token.</summary>
    public IBrush? BarBrush
    {
        get => GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    /// <summary>Unit appended to values, such as " min".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Optional custom number format for value labels and tooltips.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 176;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var values = Values;
        if (values is null || values.Count == 0 || !_plot.Contains(point))
        {
            SetHover(-1);
            return;
        }

        var slot = _plot.Width / values.Count;
        SetHover(Math.Clamp((int)((point.X - _plot.Left) / slot), 0, values.Count - 1));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (_hoverIndex >= 0 && Values is { } barValues && _hoverIndex < barValues.Count)
        {
            RaisePointClick(string.Empty, 0, _hoverIndex, barValues[_hoverIndex], e.GetPosition(this));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Values?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var next = _hoverIndex < 0 ? count - 1 : _hoverIndex;
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
                SetHover(-1);
                e.Handled = true;
                return;
            default:
                return;
        }

        SetHover(next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _plot = default;
        var values = Values;
        if (values is null || values.Count == 0)
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

        var max = NiceMaximum(values);
        _plot = new Rect(36, 14, Math.Max(1, Bounds.Width - 44), Math.Max(1, Bounds.Height - 36));
        if (_plot.Width < 20 || _plot.Height < 20)
        {
            return;
        }

        if (ShowGrid)
        {
            for (var tick = 0; tick <= 3; tick++)
            {
                var value = max * tick / 3;
                var y = Crisp(ChartFormat.Map(value, 0, max, _plot.Bottom, _plot.Top));
                context.DrawLine(
                    new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
                    new Point(_plot.Left, y),
                    new Point(_plot.Right, y));
                ChartText.Draw(
                    context,
                    ChartFormat.Compact(value),
                    _plot.Left - 6,
                    y,
                    ChartMetrics.LabelFontSize,
                    ChartPalette.Muted,
                    alignment: TextAlignment.Right);
            }
        }

        var average = values.Average();
        if (ShowAverage)
        {
            var averageY = Crisp(ChartFormat.Map(average, 0, max, _plot.Bottom, _plot.Top));
            context.DrawLine(
                new Pen(ChartPalette.BorderStrong, 1, new DashStyle(new[] { 4.0, 3.0 }, 0)),
                new Point(_plot.Left, averageY),
                new Point(_plot.Right, averageY));
            ChartText.Draw(
                context,
                "avg " + FormatValue(average),
                _plot.Right,
                averageY - 9,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Right);
        }

        var slot = _plot.Width / values.Count;
        var barWidth = Math.Min(28, Math.Max(5, slot * 0.58));
        var brush = BarBrush ?? ChartPalette.Series(0);
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var centerX = _plot.Left + slot * (i + 0.5);
            var height = Math.Max(0, value / max * _plot.Height);
            var bar = new Rect(centerX - barWidth / 2, _plot.Bottom - height, barWidth, height);
            if (bar.Height < 0.5)
            {
                bar = new Rect(bar.X, _plot.Bottom - 1, bar.Width, 1);
            }

            if (i == _hoverIndex)
            {
                context.DrawRectangle(
                    ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.62),
                    null,
                    new Rect(centerX - slot / 2 + 1, _plot.Top, slot - 2, _plot.Height),
                    5,
                    5);
            }

            var fill = i == _hoverIndex || _hoverIndex < 0
                ? brush
                : ChartPalette.Alpha(brush, 0.42);
            context.DrawGeometry(fill, null, RoundedTopBar(bar, Math.Min(6, barWidth / 2)));
        }

        context.DrawLine(
            new Pen(ChartPalette.Border, ChartMetrics.Hairline),
            new Point(_plot.Left, _plot.Bottom + 0.5),
            new Point(_plot.Right, _plot.Bottom + 0.5));

        DrawLabels(context, values.Count, slot);
        if (_hoverIndex >= 0 && _hoverIndex < values.Count)
        {
            var centerX = _plot.Left + slot * (_hoverIndex + 0.5);
            var label = Labels is not null && _hoverIndex < Labels.Count ? Labels[_hoverIndex] : $"Point {_hoverIndex + 1}";
            var anchorY = Math.Max(_plot.Top + 20, _plot.Bottom - values[_hoverIndex] / max * _plot.Height);
            ChartTooltip.Draw(
                context,
                Bounds,
                new Point(centerX, anchorY),
                label,
                new[] { ("Value", FormatValue(values[_hoverIndex]), (IBrush?)brush) });
        }
    }

    private void DrawLabels(DrawingContext context, int count, double slot)
    {
        var labels = Labels;
        if (labels is null || labels.Count == 0)
        {
            return;
        }

        var maxLabels = Math.Max(2, (int)(_plot.Width / 38));
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

            ChartText.Draw(
                context,
                labels[index],
                _plot.Left + slot * (index + 0.5),
                _plot.Bottom + 11,
                ChartMetrics.LabelFontSize,
                index == _hoverIndex ? ChartPalette.Secondary : ChartPalette.Muted,
                alignment: TextAlignment.Center,
                maxWidth: Math.Max(30, slot - 2));
        }
    }

    private static StreamGeometry RoundedTopBar(Rect rect, double radius)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        var r = Math.Min(radius, Math.Min(rect.Width / 2, rect.Height));
        context.BeginFigure(new Point(rect.Left, rect.Bottom), true);
        context.LineTo(new Point(rect.Left, rect.Top + r));
        context.QuadraticBezierTo(new Point(rect.Left, rect.Top), new Point(rect.Left + r, rect.Top));
        context.LineTo(new Point(rect.Right - r, rect.Top));
        context.QuadraticBezierTo(new Point(rect.Right, rect.Top), new Point(rect.Right, rect.Top + r));
        context.LineTo(new Point(rect.Right, rect.Bottom));
        context.EndFigure(true);
        return geometry;
    }

    private static double NiceMaximum(IReadOnlyList<double> values)
    {
        var max = values.Count == 0 ? 0 : values.Max();
        if (max <= 0)
        {
            return 1;
        }

        max *= 1.08;
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

    private void SetHover(int index)
    {
        if (_hoverIndex == index)
        {
            return;
        }

        _hoverIndex = index;
        InvalidateVisual();
    }
}
