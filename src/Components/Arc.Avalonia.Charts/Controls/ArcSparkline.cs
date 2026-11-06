using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A compact line or area trend with a last-point dot, optional headline value, and optional
/// semantic tone. With no headline it is a tiny inline sparkline; inside a card it can carry
/// the current value above the trend.
/// </summary>
public class ArcSparkline : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ArcSparkline, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ArcSparkline, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<ArcSparkline, bool>(nameof(ShowArea), true);

    public static readonly StyledProperty<bool> ShowLastPointProperty =
        AvaloniaProperty.Register<ArcSparkline, bool>(nameof(ShowLastPoint), true);

    public static readonly StyledProperty<ChartTone> ToneProperty =
        AvaloniaProperty.Register<ArcSparkline, ChartTone>(nameof(Tone));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ArcSparkline, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcSparkline, string?>(nameof(Value));

    public static readonly StyledProperty<string?> ChangeProperty =
        AvaloniaProperty.Register<ArcSparkline, string?>(nameof(Change));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcSparkline, Func<double, string>?>(nameof(ValueFormat));

    private Point _plotTopLeft;
    private Size _plotSize;
    private int _activeIndex = -1;

    static ArcSparkline()
    {
        AffectsRender<ArcSparkline>(
            ValuesProperty,
            LabelsProperty,
            ShowAreaProperty,
            ShowLastPointProperty,
            ToneProperty,
            StrokeProperty,
            ValueProperty,
            ChangeProperty,
            ValueFormatProperty);
        ValuesProperty.Changed.AddClassHandler<ArcSparkline>((chart, _) => chart._activeIndex = -1);
    }

    public ArcSparkline()
    {
        Focusable = true;
    }

    /// <summary>Points in order, oldest first.</summary>
    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Optional labels shown while scrubbing.</summary>
    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>Fills quietly under the line.</summary>
    public bool ShowArea
    {
        get => GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    /// <summary>Draws a dot at the latest point.</summary>
    public bool ShowLastPoint
    {
        get => GetValue(ShowLastPointProperty);
        set => SetValue(ShowLastPointProperty, value);
    }

    /// <summary>Accent by default; success, warning, and danger carry a trend meaning.</summary>
    public ChartTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Optional explicit line colour; overrides <see cref="Tone"/>.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Optional headline value shown above the trend, such as "34".</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Optional aside under the headline, such as "+26%".</summary>
    public string? Change
    {
        get => GetValue(ChangeProperty);
        set => SetValue(ChangeProperty, value);
    }

    /// <summary>Formats a point while scrubbing.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 64;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var count = Values?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var plot = new Rect(_plotTopLeft, _plotSize);
        if (!plot.Contains(point))
        {
            SetActive(-1);
            return;
        }

        var index = (int)Math.Round((point.X - plot.Left) / Math.Max(1, plot.Width) * (count - 1));
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
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Values?.Count ?? 0;
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

        var brush = Stroke ?? ChartPalette.Tone(Tone);
        var headline = Value;
        if (_activeIndex >= 0 && _activeIndex < values.Count)
        {
            headline = ValueFormat?.Invoke(values[_activeIndex]) ?? ChartFormat.Number(values[_activeIndex], 0);
        }

        var top = 4.0;
        if (!string.IsNullOrWhiteSpace(Value))
        {
            ChartText.Draw(
                context,
                headline ?? string.Empty,
                2,
                19,
                28,
                ChartPalette.Foreground,
                FontWeight.Medium,
                maxWidth: Math.Max(40, Bounds.Width - 110));
            if (!string.IsNullOrWhiteSpace(Change))
            {
                ChartText.Draw(
                    context,
                    Change,
                    Bounds.Width - 2,
                    20,
                    ChartMetrics.LabelFontSize,
                    ChartPalette.Tone(Tone),
                    FontWeight.Medium,
                    TextAlignment.Right);
            }

            top = 45;
        }

        var plotBottom = Bounds.Height - 7;
        var plotHeight = Math.Max(12, plotBottom - top);
        _plotTopLeft = new Point(2, top);
        _plotSize = new Size(Math.Max(1, Bounds.Width - 4), plotHeight);

        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= 1;
            max += 1;
        }
        else
        {
            var pad = (max - min) * 0.12;
            min -= pad;
            max += pad;
        }

        var points = new List<Point>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1
                ? _plotSize.Width / 2
                : i / (double)(values.Count - 1) * _plotSize.Width;
            var y = ChartFormat.Map(values[i], min, max, _plotSize.Height, 0);
            points.Add(new Point(_plotTopLeft.X + x, _plotTopLeft.Y + y));
        }

        if (ShowArea && points.Count > 1)
        {
            context.DrawGeometry(
                ChartPalette.Alpha(brush, 0.10),
                null,
                ChartGeometry.Area(points, _plotTopLeft.Y + _plotSize.Height, true));
        }

        if (points.Count > 1)
        {
            context.DrawGeometry(null, ChartPalette.Pen(brush, 1.75), ChartGeometry.Line(points, true));
        }

        if (ShowLastPoint)
        {
            context.DrawEllipse(ChartPalette.Surface, null, points[^1], 4.2, 4.2);
            context.DrawEllipse(brush, null, points[^1], 3, 3);
        }

        if (_activeIndex >= 0 && _activeIndex < points.Count)
        {
            var point = points[_activeIndex];
            context.DrawLine(
                new Pen(ChartPalette.BorderStrong, 1, new DashStyle(new[] { 3.0, 3.0 }, 0)),
                new Point(point.X, _plotTopLeft.Y),
                new Point(point.X, _plotTopLeft.Y + _plotSize.Height));
            context.DrawEllipse(ChartPalette.Surface, null, point, 4.4, 4.4);
            context.DrawEllipse(brush, null, point, 3.2, 3.2);
            var label = Labels is not null && _activeIndex < Labels.Count
                ? Labels[_activeIndex]
                : $"Point {_activeIndex + 1}";
            ChartTooltip.Draw(
                context,
                Bounds,
                point,
                label,
                new[]
                {
                    ("Value", ValueFormat?.Invoke(values[_activeIndex]) ?? ChartFormat.Number(values[_activeIndex], 0), (IBrush?)brush)
                });
        }
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
