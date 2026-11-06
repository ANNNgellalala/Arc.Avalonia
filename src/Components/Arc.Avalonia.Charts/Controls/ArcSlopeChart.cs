using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A two-period slope chart. Names sit on the left, values and end ranks sit on the right, and
/// the highlighted item keeps the accent while the rest stay quiet. Hovering a line lifts it.
/// </summary>
public class ArcSlopeChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<SlopeItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcSlopeChart, IReadOnlyList<SlopeItem>?>(nameof(Items));

    public static readonly StyledProperty<string> StartLabelProperty =
        AvaloniaProperty.Register<ArcSlopeChart, string>(nameof(StartLabel), "Before");

    public static readonly StyledProperty<string> EndLabelProperty =
        AvaloniaProperty.Register<ArcSlopeChart, string>(nameof(EndLabel), "After");

    public static readonly StyledProperty<string?> HighlightKeyProperty =
        AvaloniaProperty.Register<ArcSlopeChart, string?>(nameof(HighlightKey));

    public static readonly StyledProperty<bool> ShowRanksProperty =
        AvaloniaProperty.Register<ArcSlopeChart, bool>(nameof(ShowRanks), true);

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcSlopeChart, Func<double, string>?>(nameof(ValueFormat));

    private double _startX;
    private double _endX;
    private double _top;
    private double _bottom;
    private int _activeIndex = -1;

    static ArcSlopeChart()
    {
        AffectsRender<ArcSlopeChart>(
            ItemsProperty,
            StartLabelProperty,
            EndLabelProperty,
            HighlightKeyProperty,
            ShowRanksProperty,
            ValueFormatProperty);
        ItemsProperty.Changed.AddClassHandler<ArcSlopeChart>((chart, _) => chart._activeIndex = -1);
    }

    public ArcSlopeChart()
    {
        Focusable = true;
    }

    public IReadOnlyList<SlopeItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Heading over the first column.</summary>
    public string StartLabel
    {
        get => GetValue(StartLabelProperty);
        set => SetValue(StartLabelProperty, value);
    }

    /// <summary>Heading over the second column.</summary>
    public string EndLabel
    {
        get => GetValue(EndLabelProperty);
        set => SetValue(EndLabelProperty, value);
    }

    /// <summary>Name of the item that keeps the accent. Others render in a quiet line colour.</summary>
    public string? HighlightKey
    {
        get => GetValue(HighlightKeyProperty);
        set => SetValue(HighlightKeyProperty, value);
    }

    /// <summary>Shows each item's end rank beside its value.</summary>
    public bool ShowRanks
    {
        get => GetValue(ShowRanksProperty);
        set => SetValue(ShowRanksProperty, value);
    }

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
        var items = Items;
        if (items is null || items.Count == 0 || point.X < _startX - 8 || point.X > _endX + 8)
        {
            SetActive(-1);
            return;
        }

        var t = Math.Clamp((point.X - _startX) / Math.Max(1, _endX - _startX), 0, 1);
        var best = -1;
        var bestDistance = 12.0;
        var range = ValueRange(items);
        for (var i = 0; i < items.Count; i++)
        {
            var value = items[i].Start + (items[i].End - items[i].Start) * t;
            var y = MapY(value, range);
            var distance = Math.Abs(point.Y - y);
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
        var count = Items?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? 0 : _activeIndex;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                next = Math.Max(0, next - 1);
                break;
            case Key.Right:
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
        var items = Items;
        if (items is null || items.Count == 0)
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

        _startX = 102;
        _endX = Math.Max(_startX + 52, Bounds.Width - 92);
        _top = 34;
        _bottom = Bounds.Height - 12;
        if (_bottom - _top < 24)
        {
            return;
        }

        context.DrawLine(
            new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
            new Point(Crisp(_startX), _top - 2),
            new Point(Crisp(_startX), _bottom + 2));
        context.DrawLine(
            new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
            new Point(Crisp(_endX), _top - 2),
            new Point(Crisp(_endX), _bottom + 2));

        ChartText.Draw(
            context,
            StartLabel,
            _startX,
            14,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: 72);
        ChartText.Draw(
            context,
            EndLabel,
            _endX,
            14,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: 72);

        var range = ValueRange(items);
        var startYs = ResolveLabelPositions(items.Select(item => MapY(item.Start, range)).ToArray());
        var endYs = ResolveLabelPositions(items.Select(item => MapY(item.End, range)).ToArray());
        var ranks = EndRanks(items);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isHighlight = string.Equals(item.Name, HighlightKey, StringComparison.Ordinal);
            var isActive = i == _activeIndex;
            var brush = item.Brush ??
                (isHighlight || isActive ? ChartPalette.Series(0) : ChartPalette.BorderStrong);
            if (_activeIndex >= 0 && !isActive && !isHighlight)
            {
                brush = ChartPalette.Alpha(brush, 0.4);
            }

            var startPoint = new Point(_startX, MapY(item.Start, range));
            var endPoint = new Point(_endX, MapY(item.End, range));
            context.DrawLine(
                ChartPalette.Pen(brush, isActive || isHighlight ? 2 : 1.5),
                startPoint,
                endPoint);
            context.DrawEllipse(ChartPalette.Surface, null, startPoint, 4, 4);
            context.DrawEllipse(brush, null, startPoint, 2.7, 2.7);
            context.DrawEllipse(ChartPalette.Surface, null, endPoint, 4.4, 4.4);
            context.DrawEllipse(brush, null, endPoint, 3.1, 3.1);

            if (Math.Abs(startYs[i] - startPoint.Y) > 1.5)
            {
                context.DrawLine(
                    new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
                    new Point(88, startYs[i]),
                    new Point(_startX - 4, startPoint.Y));
            }

            ChartText.Draw(
                context,
                item.Name,
                82,
                startYs[i],
                ChartMetrics.LabelFontSize,
                isHighlight || isActive ? ChartPalette.Foreground : ChartPalette.Secondary,
                isHighlight || isActive ? FontWeight.Medium : FontWeight.Normal,
                TextAlignment.Right,
                maxWidth: 78);

            if (Math.Abs(endYs[i] - endPoint.Y) > 1.5)
            {
                context.DrawLine(
                    new Pen(ChartPalette.BorderSubtle, ChartMetrics.Hairline),
                    new Point(_endX + 4, endPoint.Y),
                    new Point(_endX + 9, endYs[i]));
            }

            ChartText.Draw(
                context,
                FormatValue(item.End),
                _endX + 9,
                endYs[i],
                ChartMetrics.LabelFontSize,
                isHighlight || isActive ? ChartPalette.Foreground : ChartPalette.Secondary,
                isHighlight || isActive ? FontWeight.Medium : FontWeight.Normal,
                maxWidth: 48);
            if (ShowRanks)
            {
                ChartText.Draw(
                    context,
                    "#" + ranks[i],
                    Bounds.Width - 2,
                    endYs[i],
                    ChartMetrics.LabelFontSize,
                    ChartPalette.Muted,
                    alignment: TextAlignment.Right);
            }
        }

        if (_activeIndex >= 0 && _activeIndex < items.Count)
        {
            var item = items[_activeIndex];
            var startRank = StartRanks(items)[_activeIndex];
            var endRank = ranks[_activeIndex];
            var change = startRank - endRank;
            var changeText = change == 0 ? "no rank change" : change > 0 ? $"up {change}" : $"down {Math.Abs(change)}";
            ChartTooltip.Draw(
                context,
                Bounds,
                new Point((_startX + _endX) / 2, MapY((item.Start + item.End) / 2, range)),
                item.Name,
                new[]
                {
                    (StartLabel, FormatValue(item.Start), (IBrush?)ChartPalette.Muted),
                    (EndLabel, FormatValue(item.End), (IBrush?)ChartPalette.Foreground),
                    ("Rank", changeText, (IBrush?)ChartPalette.Muted)
                });
        }
    }

    private double[] ResolveLabelPositions(double[] values)
    {
        const double gap = 15;
        var result = (double[])values.Clone();
        var order = Enumerable.Range(0, values.Length).OrderBy(index => values[index]).ToArray();
        var minY = _top + 8;
        var maxY = _bottom - 6;
        var previous = minY - gap;
        foreach (var index in order)
        {
            result[index] = Math.Max(values[index], previous + gap);
            previous = result[index];
        }

        var next = maxY + gap;
        for (var position = order.Length - 1; position >= 0; position--)
        {
            var index = order[position];
            result[index] = Math.Min(result[index], next - gap);
            next = result[index];
        }

        return result;
    }

    private static (double Min, double Max) ValueRange(IReadOnlyList<SlopeItem> items)
    {
        var min = items.Min(item => Math.Min(item.Start, item.End));
        var max = items.Max(item => Math.Max(item.Start, item.End));
        var pad = Math.Max(0.5, (max - min) * 0.16);
        return (min - pad, max + pad);
    }

    private double MapY(double value, (double Min, double Max) range)
        => _bottom - (value - range.Min) / Math.Max(0.001, range.Max - range.Min) * (_bottom - _top);

    private static int[] EndRanks(IReadOnlyList<SlopeItem> items)
        => items.Select(item => items.Count(other => other.End > item.End) + 1).ToArray();

    private static int[] StartRanks(IReadOnlyList<SlopeItem> items)
        => items.Select(item => items.Count(other => other.Start > item.Start) + 1).ToArray();

    private string FormatValue(double value)
        => ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 1);

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
