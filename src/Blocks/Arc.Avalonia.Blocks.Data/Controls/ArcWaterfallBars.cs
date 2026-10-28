using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>One floating bar of the MRR bridge: a start, a movement, or ending total.</summary>
internal sealed class WaterfallBar
{
    public WaterfallBar(string label, double delta, string detail, bool total = false)
    {
        Label = label;
        Delta = delta;
        Detail = detail;
        Total = total;
    }

    public string Label { get; }

    public double Delta { get; }

    public string Detail { get; }

    /// <summary>A total bar starts at the baseline; a movement bar floats from the previous level.</summary>
    public bool Total { get; }
}

/// <summary>
/// The MRR bridge body: floating bars whose length is the movement and whose base is the running total,
/// with hairline connectors, a selected bar that the accounts panel follows, and a hover readout. Arrow keys
/// move the selection, Home and End jump to the ends. An independent Avalonia drawing of Arc's public
/// mrr-waterfall block; no Pro source was used.
/// </summary>
internal sealed class ArcWaterfallBars : DataStage
{
    private readonly List<Rect> _hits = new();
    private int _selected;
    private int _hover = -1;

    public ArcWaterfallBars(IReadOnlyList<WaterfallBar> bars)
    {
        Bars = bars;
        Height = 250;
    }

    public IReadOnlyList<WaterfallBar> Bars { get; }

    public int Selected
    {
        get => _selected;
        set
        {
            var next = Bars.Count == 0 ? 0 : Math.Clamp(value, 0, Bars.Count - 1);
            if (next == _selected)
            {
                return;
            }

            _selected = next;
            SelectionChanged?.Invoke(this, next);
            InvalidateVisual();
        }
    }

    public event EventHandler<int>? SelectionChanged;

    protected override double DefaultHeight => 250;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var hover = HitTest(point);
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var hit = HitTest(e.GetPosition(this));
        if (hit >= 0)
        {
            Selected = hit;
            Focus();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Bars.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                Selected = Selected == 0 ? Bars.Count - 1 : Selected - 1;
                break;
            case Key.Right:
            case Key.Down:
                Selected = Selected == Bars.Count - 1 ? 0 : Selected + 1;
                break;
            case Key.Home:
                Selected = 0;
                break;
            case Key.End:
                Selected = Bars.Count - 1;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _hits.Clear();
        if (Bars.Count == 0)
        {
            return;
        }

        var levels = new List<(double Start, double End)>(Bars.Count);
        var running = 0.0;
        foreach (var bar in Bars)
        {
            if (bar.Total)
            {
                running = bar.Delta;
                levels.Add((0, running));
            }
            else
            {
                levels.Add((running, running + bar.Delta));
                running += bar.Delta;
            }
        }

        // The axis band covers the movements and the two totals. Total bars are drawn from the plot floor,
        // which is why the gridline labels matter: the band is truncated, and says so.
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var index = 0; index < Bars.Count; index++)
        {
            if (Bars[index].Total)
            {
                min = Math.Min(min, levels[index].End);
                max = Math.Max(max, levels[index].End);
            }
            else
            {
                min = Math.Min(min, Math.Min(levels[index].Start, levels[index].End));
                max = Math.Max(max, Math.Max(levels[index].Start, levels[index].End));
            }
        }

        var padding = Math.Max(1200, (max - min) * 0.24);
        min = Math.Max(0, min - padding);
        max += padding;
        if (Math.Abs(max - min) < 1)
        {
            max = min + 1;
        }

        var plot = new Rect(52, 18, Math.Max(10, Bounds.Width - 64), Math.Max(10, Bounds.Height - 44));
        var gridPen = Ink.Pen(Ink.BorderSubtle, 1);
        for (var tick = 0; tick <= 4; tick++)
        {
            var value = min + (max - min) * tick / 4;
            var y = Crisp(DataMath.Map(value, min, max, plot.Bottom, plot.Top));
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            Text(context, DataKit.Compact(value), plot.Left - 6, y, 11, Ink.Muted, alignment: TextAlignment.Right);
        }

        var slot = plot.Width / Bars.Count;
        var barWidth = Math.Min(58, slot * 0.54);
        for (var index = 0; index < Bars.Count; index++)
        {
            var bar = Bars[index];
            var level = levels[index];
            var x = plot.Left + slot * index + (slot - barWidth) / 2;
            var top = DataMath.Map(Math.Max(level.Start, level.End), min, max, plot.Bottom, plot.Top);
            var bottom = bar.Total
                ? plot.Bottom
                : DataMath.Map(Math.Min(level.Start, level.End), min, max, plot.Bottom, plot.Top);
            var rect = new Rect(x, top, barWidth, Math.Max(3, bottom - top));
            _hits.Add(rect);

            var positive = bar.Delta >= 0;
            var fill = bar.Total
                ? Ink.Foreground
                : positive ? Ink.Success : Ink.Danger;
            if (bar.Total)
            {
                fill = Ink.Alpha(Ink.Foreground, 0.82);
            }

            var selected = index == _selected;
            var hovered = index == _hover;
            var alpha = selected || hovered ? 1.0 : 0.82;
            context.DrawRectangle(
                Ink.Alpha(fill, alpha),
                selected ? Ink.Pen(Ink.Foreground, 1.5) : null,
                rect,
                5,
                5);

            if (index < Bars.Count - 1)
            {
                var connectorY = Crisp(DataMath.Map(index >= 0 ? levels[index].End : 0, min, max, plot.Bottom, plot.Top));
                var nextX = plot.Left + slot * (index + 1);
                DashedLine(context, Ink.Border, new Point(rect.Right, connectorY), new Point(nextX, connectorY));
            }

            var labelSize = slot < 60 ? 10 : 11;
            Text(context, bar.Label, x + barWidth / 2, plot.Bottom + 12, labelSize,
                selected ? Ink.Foreground : Ink.Muted, FontWeight.Normal, TextAlignment.Center, maxWidth: slot - 2);
        }

        // The value of the selected bar sits above it, never inside a short bar.
        var selectedLevel = levels[_selected];
        var selectedX = plot.Left + slot * _selected + slot / 2;
        var selectedTop = DataMath.Map(Math.Max(selectedLevel.Start, selectedLevel.End), min, max, plot.Bottom, plot.Top);
        var selectedValue = Bars[_selected].Delta;
        var valueText = Bars[_selected].Total ? DataKit.MoneyCompact(selectedValue) : DataKit.Signed(selectedValue);
        Text(context, valueText, selectedX, Math.Max(10, selectedTop - 10), 12, Ink.Foreground, FontWeight.Medium,
            TextAlignment.Center);

        if (_hover >= 0 && _hover < Bars.Count)
        {
            var bar = Bars[_hover];
            var rect = _hits[_hover];
            Tooltip(context, Bounds.Size, new Point(rect.Center.X, rect.Top), bar.Label, new[]
            {
                ("Movement", bar.Total ? DataKit.MoneyCompact(bar.Delta) : DataKit.Signed(bar.Delta), (IBrush?)null),
                ("Detail", bar.Detail, (IBrush?)null)
            });
        }
    }

    private int HitTest(Point point)
    {
        for (var index = 0; index < _hits.Count; index++)
        {
            if (_hits[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }
}
