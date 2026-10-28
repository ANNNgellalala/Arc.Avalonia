using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data.Controls;

/// <summary>One panel of the small-multiples trellis.</summary>
internal sealed class MultipleSeries
{
    public MultipleSeries(string name, double[] values)
    {
        Name = name;
        Values = values;
    }

    public string Name { get; }

    public double[] Values { get; }
}

/// <summary>
/// A trellis of small line charts that reads as one chart: a synced crosshair across every panel, a ghost of
/// the overall total behind each line, a shared or independent y scale, and dimming for flat trends. Hover or
/// arrow keys move the shared crosshair; panels are ordered by name, peak or latest value.
/// </summary>
internal sealed class ArcSmallMultiples : DataStage
{
    private readonly IReadOnlyList<MultipleSeries> _series;
    private readonly IReadOnlyList<string> _labels;
    private readonly double[] _total;
    private int _hoverIndex = -1;
    private int _hoverPanel = -1;

    public ArcSmallMultiples(IReadOnlyList<MultipleSeries> series, IReadOnlyList<string> labels)
    {
        _series = series;
        _labels = labels;
        _total = Enumerable.Range(0, labels.Count)
            .Select(index => series.Sum(item => index < item.Values.Length ? item.Values[index] : 0))
            .ToArray();
        Height = 300;
    }

    public bool SharedScale { get; set; } = true;

    public string Sort { get; set; } = "peak";

    public event EventHandler<int>? DayChanged;

    protected override double DefaultHeight => 300;

    private List<(MultipleSeries Series, Rect Rect)> Panels()
    {
        var ordered = Sort switch
        {
            "name" => _series.OrderBy(item => item.Name, StringComparer.Ordinal).ToList(),
            "latest" => _series.OrderByDescending(item => item.Values[^1]).ToList(),
            _ => _series.OrderByDescending(item => item.Values.Max() - item.Values.Min()).ToList()
        };
        var columns = 4;
        var rows = (int)Math.Ceiling(ordered.Count / (double)columns);
        var gap = 8.0;
        var width = (Bounds.Width - gap * (columns - 1)) / columns;
        var height = (Bounds.Height - gap * (rows - 1)) / rows;
        var result = new List<(MultipleSeries, Rect)>();
        for (var index = 0; index < ordered.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            result.Add((ordered[index], new Rect(
                column * (width + gap),
                row * (height + gap),
                width,
                height)));
        }

        return result;
    }

    private static Rect Plot(Rect panel) => new(panel.Left + 8, panel.Top + 22, panel.Width - 16, panel.Height - 32);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var panels = Panels();
        var panelIndex = -1;
        for (var index = 0; index < panels.Count; index++)
        {
            if (panels[index].Rect.Contains(point))
            {
                panelIndex = index;
                break;
            }
        }

        var hover = -1;
        if (panelIndex >= 0)
        {
            var plot = Plot(panels[panelIndex].Rect);
            hover = Math.Clamp((int)Math.Round((point.X - plot.Left) / plot.Width * (_labels.Count - 1)), 0, _labels.Count - 1);
        }

        if (hover != _hoverIndex || panelIndex != _hoverPanel)
        {
            _hoverIndex = hover;
            _hoverPanel = panelIndex;
            InvalidateVisual();
            if (hover >= 0)
            {
                DayChanged?.Invoke(this, hover);
            }
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverIndex = -1;
        _hoverPanel = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var next = _hoverIndex < 0 ? _labels.Count - 1 : _hoverIndex;
        switch (e.Key)
        {
            case Key.Left:
                next = Math.Max(0, next - 1);
                break;
            case Key.Right:
                next = Math.Min(_labels.Count - 1, next + 1);
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = _labels.Count - 1;
                break;
            default:
                return;
        }

        _hoverIndex = next;
        InvalidateVisual();
        DayChanged?.Invoke(this, next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var panels = Panels();
        var sharedMax = _series.Max(item => item.Values.Max()) * 1.08;
        var ghostMax = _total.Max() * 1.08;
        foreach (var (series, panel) in panels)
        {
            var plot = Plot(panel);
            context.DrawRectangle(Ink.SurfaceMuted, null, panel, 10, 10);
            var flat = series.Values.Max() - series.Values.Min() < series.Values.Max() * 0.03;
            Text(context, series.Name, panel.Left + 8, panel.Top + 11, 10,
                flat ? Ink.Muted : Ink.Secondary, FontWeight.Medium, maxWidth: panel.Width - 60);
            Text(context, DataKit.Compact(series.Values[^1]), panel.Right - 8, panel.Top + 11, 10, Ink.Foreground,
                FontWeight.Medium, TextAlignment.Right);

            // The ghost of the overall total sits behind every panel on its own scale.
            var ghost = new List<Point>();
            for (var index = 0; index < _total.Length; index++)
            {
                ghost.Add(new Point(
                    plot.Left + index / (double)Math.Max(1, _total.Length - 1) * plot.Width,
                    DataMath.Map(_total[index], 0, ghostMax, plot.Bottom, plot.Top)));
            }

            context.DrawGeometry(null, Ink.Pen(Ink.Alpha(Ink.Muted, 0.30), 1, true), DataStage.LineGeometry(ghost));

            var max = SharedScale ? sharedMax : series.Values.Max() * 1.08;
            var min = Math.Min(series.Values.Min() * 0.9, max * 0.2);
            var points = new List<Point>();
            for (var index = 0; index < series.Values.Length; index++)
            {
                points.Add(new Point(
                    plot.Left + index / (double)Math.Max(1, series.Values.Length - 1) * plot.Width,
                    DataMath.Map(series.Values[index], min, max, plot.Bottom, plot.Top)));
            }

            var brush = flat ? Ink.Muted : Ink.Series(0);
            if (!flat)
            {
                context.DrawGeometry(Ink.Alpha(brush, 0.10), null, DataStage.AreaGeometry(points, plot.Bottom));
            }

            context.DrawGeometry(null, Ink.Pen(brush, flat ? 1.2 : 1.6), DataStage.LineGeometry(points));
            if (_hoverIndex >= 0)
            {
                var x = plot.Left + _hoverIndex / (double)Math.Max(1, series.Values.Length - 1) * plot.Width;
                context.DrawLine(Ink.Pen(Ink.Alpha(Ink.BorderStrong, 0.8), 1, true),
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
                var value = series.Values[Math.Min(_hoverIndex, series.Values.Length - 1)];
                context.DrawEllipse(brush, Ink.Pen(Ink.Surface, 1.2),
                    new Point(x, DataMath.Map(value, min, max, plot.Bottom, plot.Top)), 2.6, 2.6);
            }
        }

        var index2 = _hoverIndex;
        if (index2 >= 0 && index2 < _labels.Count)
        {
            var label = _labels[index2];
            var selected = _hoverPanel >= 0 && _hoverPanel < panels.Count ? panels[_hoverPanel] : panels[0];
            Tooltip(context, Bounds.Size, new Point(selected.Rect.Center.X, selected.Rect.Bottom),
                label, _series.Take(3).Select(item => (
                    item.Name,
                    DataKit.Number(item.Values[Math.Min(index2, item.Values.Length - 1)]),
                    (IBrush?)null)).ToList());
        }
    }
}
