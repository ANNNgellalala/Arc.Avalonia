using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>One row of the horizon chart: a signed distance from its own baseline.</summary>
internal sealed class HorizonSeries
{
    public HorizonSeries(string name, double[] values)
    {
        Name = name;
        Values = values;
    }

    public string Name { get; }

    public double[] Values { get; }
}

/// <summary>
/// A horizon chart: many rows folded into bands above and below a baseline, so fourteen series fit in the
/// height of two line charts. A synced crosshair reads every row on one day, rows sort by name, peak swing or
/// latest value, and the band count folds every row together. The above side uses the first series hue and
/// the below side the third.
/// </summary>
internal sealed class ArcHorizonChart : DataStage
{
    private readonly IReadOnlyList<HorizonSeries> _series;
    private readonly IReadOnlyList<string> _labels;
    private int _crosshair = -1;
    private int _hoverRow = -1;

    public ArcHorizonChart(IReadOnlyList<HorizonSeries> series, IReadOnlyList<string> labels)
    {
        _series = series;
        _labels = labels;
        Height = 300;
    }

    public int Bands { get; set; } = 3;

    public string Sort { get; set; } = "peak";

    public event EventHandler<int>? DayChanged;

    protected override double DefaultHeight => 300;

    private List<HorizonSeries> Ordered() => Sort switch
    {
        "name" => _series.OrderBy(item => item.Name, StringComparer.Ordinal).ToList(),
        "latest" => _series.OrderByDescending(item => item.Values[^1]).ToList(),
        _ => _series.OrderByDescending(item => item.Values.Max(Math.Abs)).ToList()
    };

    private double Extent => Math.Max(12, Math.Ceiling(_series.Max(item => item.Values.Max(Math.Abs)) / 12) * 12);

    private const double LabelWidth = 116;
    private const double ValueWidth = 54;
    private const double TopPad = 16;

    private Rect Row(int index, int count) =>
        new(LabelWidth, TopPad + index * ((Bounds.Height - TopPad - 16) / Math.Max(1, count)),
            Math.Max(10, Bounds.Width - LabelWidth - ValueWidth), (Bounds.Height - TopPad - 16) / Math.Max(1, count));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var ordered = Ordered();
        var row = -1;
        for (var index = 0; index < ordered.Count; index++)
        {
            if (point.Y >= TopPad + index * ((Bounds.Height - TopPad - 16) / Math.Max(1, ordered.Count)) &&
                point.Y < TopPad + (index + 1) * ((Bounds.Height - TopPad - 16) / Math.Max(1, ordered.Count)))
            {
                row = index;
                break;
            }
        }

        var cross = -1;
        if (row >= 0)
        {
            var plot = Row(row, ordered.Count);
            if (point.X >= plot.Left && point.X <= plot.Right)
            {
                cross = Math.Clamp((int)Math.Round((point.X - plot.Left) / plot.Width * (_labels.Count - 1)), 0,
                    _labels.Count - 1);
            }
        }

        if (row != _hoverRow || cross != _crosshair)
        {
            _hoverRow = row;
            _crosshair = cross;
            InvalidateVisual();
            if (cross >= 0)
            {
                DayChanged?.Invoke(this, cross);
            }
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverRow = -1;
        _crosshair = -1;
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
        var next = _crosshair < 0 ? _labels.Count - 1 : _crosshair;
        switch (e.Key)
        {
            case Key.Left:
                next = Math.Max(0, next - (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 7 : 1));
                break;
            case Key.Right:
                next = Math.Min(_labels.Count - 1, next + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 7 : 1));
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = _labels.Count - 1;
                break;
            case Key.Escape:
                _crosshair = -1;
                InvalidateVisual();
                e.Handled = true;
                return;
            default:
                return;
        }

        _crosshair = next;
        InvalidateVisual();
        DayChanged?.Invoke(this, next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var ordered = Ordered();
        var extent = Extent;
        var step = extent / Math.Max(1, Bands);
        for (var rowIndex = 0; rowIndex < ordered.Count; rowIndex++)
        {
            var series = ordered[rowIndex];
            var plot = Row(rowIndex, ordered.Count);
            var mid = plot.Top + plot.Height / 2;
            var scale = plot.Height / 2 / step;
            Text(context, series.Name, 4, mid, 10, Ink.Secondary, maxWidth: LabelWidth - 8);
            Text(context, DataKit.Signed(series.Values[^1], "%"), Bounds.Width - 4, mid, 10, Ink.Muted,
                alignment: TextAlignment.Right);

            context.DrawLine(Ink.Pen(Ink.BorderSubtle, 1), new Point(plot.Left, mid), new Point(plot.Right, mid));

            for (var band = 0; band < Bands; band++)
            {
                var above = BandPoints(series, plot, mid, scale, step, band, true);
                var below = BandPoints(series, plot, mid, scale, step, band, false);
                var alpha = 0.20 + band * 0.20;
                if (above.Count > 1)
                {
                    context.DrawGeometry(Ink.Alpha(Ink.Series(0), alpha), null,
                        DataStage.AreaGeometry(above, mid));
                }

                if (below.Count > 1)
                {
                    context.DrawGeometry(Ink.Alpha(Ink.Series(2), alpha), null,
                        DataStage.AreaGeometry(below, mid));
                }
            }

            if (_crosshair >= 0 && _crosshair < _labels.Count)
            {
                var x = plot.Left + _crosshair / (double)Math.Max(1, _labels.Count - 1) * plot.Width;
                context.DrawLine(Ink.Pen(Ink.Alpha(Ink.BorderStrong, 0.75), 1, true),
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
            }
        }

        Text(context, _labels[0], LabelWidth, Bounds.Height - 8, 9, Ink.Muted);
        Text(context, _labels[^1], Bounds.Width - ValueWidth, Bounds.Height - 8, 9, Ink.Muted,
            alignment: TextAlignment.Right);

        if (_crosshair >= 0 && _crosshair < _labels.Count)
        {
            var hovered = _hoverRow >= 0 && _hoverRow < ordered.Count ? ordered[_hoverRow] : ordered[0];
            var value = hovered.Values[Math.Min(_crosshair, hovered.Values.Length - 1)];
            Tooltip(context, Bounds.Size, new Point(Bounds.Width * 0.45, Bounds.Height * 0.3),
                _labels[_crosshair], new[]
                {
                    (hovered.Name, DataKit.Signed(value, "%"), (IBrush?)null),
                    ("Band", value >= 0 ? "above baseline" : "below baseline", (IBrush?)null)
                });
        }
    }

    private List<Point> BandPoints(HorizonSeries series, Rect plot, double mid, double scale, double step, int band, bool above)
    {
        var points = new List<Point>();
        for (var index = 0; index < series.Values.Length; index++)
        {
            var value = series.Values[index];
            var magnitude = above ? Math.Max(0, value) : Math.Max(0, -value);
            var bandValue = Math.Clamp(magnitude - band * step, 0, step);
            var x = plot.Left + index / (double)Math.Max(1, series.Values.Length - 1) * plot.Width;
            var y = above ? mid - bandValue * scale : mid + bandValue * scale;
            points.Add(new Point(x, y));
        }

        return points;
    }
}
