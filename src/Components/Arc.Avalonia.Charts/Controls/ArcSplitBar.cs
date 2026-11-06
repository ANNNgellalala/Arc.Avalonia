using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A single hundred-percent bar split into shares. The readout above names the total at rest or
/// the hovered share, and the legend below carries the exact percentages.
/// </summary>
public class ArcSplitBar : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartValue>?> SegmentsProperty =
        AvaloniaProperty.Register<ArcSplitBar, IReadOnlyList<ChartValue>?>(nameof(Segments));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcSplitBar, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<string> TotalLabelProperty =
        AvaloniaProperty.Register<ArcSplitBar, string>(nameof(TotalLabel), "Total");

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcSplitBar, string?>(nameof(Unit));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcSplitBar, Func<double, string>?>(nameof(ValueFormat));

    private readonly List<(Rect Bounds, int Index)> _legendHits = new();
    private Rect _bar;
    private int _activeIndex = -1;

    static ArcSplitBar()
    {
        AffectsRender<ArcSplitBar>(
            SegmentsProperty,
            ShowLegendProperty,
            TotalLabelProperty,
            UnitProperty,
            ValueFormatProperty);
        SegmentsProperty.Changed.AddClassHandler<ArcSplitBar>((chart, _) => chart._activeIndex = -1);
    }

    public ArcSplitBar()
    {
        Focusable = true;
    }

    public IReadOnlyList<ChartValue>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public string TotalLabel
    {
        get => GetValue(TotalLabelProperty);
        set => SetValue(TotalLabelProperty, value);
    }

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

    protected override double DefaultHeight => 196;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                SetActive(hit.Index);
                return;
            }
        }

        if (_bar.Contains(point))
        {
            var segments = Visible();
            var total = segments.Sum(item => item.Value);
            var running = _bar.Left;
            for (var i = 0; i < segments.Count; i++)
            {
                var width = segments[i].Value / total * _bar.Width;
                if (point.X >= running && point.X <= running + width)
                {
                    SetActive(i);
                    return;
                }

                running += width;
            }
        }

        SetActive(-1);
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

        var point = e.GetPosition(this);
        foreach (var hit in _legendHits)
        {
            if (!hit.Bounds.Contains(point))
            {
                continue;
            }

            var items = Visible();
            if (hit.Index >= 0 && hit.Index < items.Count)
            {
                RaiseLegendClick(hit.Index, items[hit.Index].Name, true);
            }

            break;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Visible().Count;
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? 0 : _activeIndex;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                next = (next - 1 + count) % count;
                break;
            case Key.Right:
            case Key.Down:
                next = (next + 1) % count;
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
        var segments = Visible();
        var total = segments.Sum(item => item.Value);
        if (segments.Count == 0 || total <= 0)
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

        var active = _activeIndex >= 0 && _activeIndex < segments.Count ? segments[_activeIndex] : null;
        var label = active?.Name ?? TotalLabel;
        var value = active?.Value ?? total;
        var share = active is null ? 1 : active.Value / total;
        ChartText.Draw(context, label, 4, 15, ChartMetrics.LabelFontSize, ChartPalette.Muted);
        ChartText.Draw(
            context,
            FormatValue(value),
            4,
            38,
            24,
            ChartPalette.Foreground,
            FontWeight.Medium,
            maxWidth: Bounds.Width - 86);
        ChartText.Draw(
            context,
            ChartFormat.Percent(share, 1),
            Bounds.Width - 4,
            34,
            18,
            ChartPalette.Secondary,
            FontWeight.Medium,
            TextAlignment.Right);

        var barTop = 62.0;
        var barHeight = Math.Clamp(Bounds.Height * 0.16, 26, 38);
        _bar = new Rect(4, barTop, Math.Max(20, Bounds.Width - 8), barHeight);
        var radius = barHeight / 2;
        context.DrawRectangle(ChartPalette.BorderSubtle, null, _bar, radius, radius);
        using (context.PushClip(new RoundedRect(_bar, new CornerRadius(radius))))
        {
            var x = _bar.Left;
            for (var i = 0; i < segments.Count; i++)
            {
                var width = segments[i].Value / total * _bar.Width;
                var brush = segments[i].Brush ?? ChartPalette.Series(i);
                if (_activeIndex >= 0 && i != _activeIndex)
                {
                    brush = ChartPalette.Alpha(brush, 0.42);
                }

                context.DrawRectangle(brush, null, new Rect(x, _bar.Top, width, _bar.Height));
                x += width;
                if (i < segments.Count - 1)
                {
                    context.DrawLine(
                        new Pen(ChartPalette.Surface, 2),
                        new Point(x, _bar.Top),
                        new Point(x, _bar.Bottom));
                }
            }
        }

        if (ShowLegend)
        {
            DrawLegend(context, segments, total);
        }
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartValue> segments, double total)
    {
        var x = 4.0;
        var y = _bar.Bottom + 28;
        var rowHeight = 24.0;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var name = ChartText.Create(segment.Name, ChartMetrics.LabelFontSize, i == _activeIndex ? ChartPalette.Foreground : ChartPalette.Secondary, i == _activeIndex ? FontWeight.Medium : FontWeight.Normal, 96);
            var percent = ChartText.Create(ChartFormat.Percent(segment.Value / total, 0), ChartMetrics.LabelFontSize, ChartPalette.Muted);
            var width = 10 + 5 + name.Width + 6 + percent.Width + 12;
            if (x + width > Bounds.Width && x > 4)
            {
                x = 4;
                y += rowHeight;
            }

            if (y + 8 > Bounds.Height)
            {
                break;
            }

            var brush = segment.Brush ?? ChartPalette.Series(i);
            context.DrawRectangle(brush, null, new Rect(x, y - 4, 8, 8), 2, 2);
            context.DrawText(name, new Point(x + 13, y - 8));
            context.DrawText(percent, new Point(x + 13 + name.Width + 6, y - 8));
            _legendHits.Add((new Rect(x - 2, y - 11, width, 20), i));
            x += width;
        }
    }

    private List<ChartValue> Visible()
        => Segments?.Where(item => item.Value > 0).ToList() ?? new List<ChartValue>();

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 0);
        return string.IsNullOrEmpty(Unit) ? formatted : formatted + " " + Unit;
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
