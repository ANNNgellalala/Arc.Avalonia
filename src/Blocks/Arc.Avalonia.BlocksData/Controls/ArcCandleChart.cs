using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.BlocksData;

/// <summary>One candlestick with volume.</summary>
internal sealed class Candle
{
    public Candle(string label, double open, double high, double low, double close, double volume)
    {
        Label = label;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public string Label { get; }

    public double Open { get; }

    public double High { get; }

    public double Low { get; }

    public double Close { get; }

    public double Volume { get; }

    public bool Up => Close >= Open;
}

/// <summary>
/// A canvas candlestick chart with volume, two moving averages and an OHLC crosshair readout. The last
/// <see cref="Visible"/> candles are shown; plus and minus zoom the window, the arrow keys move the crosshair
/// and Home or End jump to the ends. Prices are seeded sample data.
/// </summary>
internal sealed class ArcCandleChart : DataStage
{
    private readonly IReadOnlyList<Candle> _candles;
    private int _visible = 44;
    private int _crosshair = -1;
    private bool _dragging;

    public ArcCandleChart(IReadOnlyList<Candle> candles)
    {
        _candles = candles;
        Height = 330;
    }

    public int Visible
    {
        get => _visible;
        set
        {
            var next = Math.Clamp(value, 16, _candles.Count);
            if (next == _visible)
            {
                return;
            }

            _visible = next;
            InvalidateVisual();
        }
    }

    public event EventHandler<Candle>? Inspected;

    protected override double DefaultHeight => 330;

    private IReadOnlyList<Candle> Window => _candles.Skip(_candles.Count - _visible).ToList();

    private Rect PricePlot => new(52, 14, Math.Max(10, Bounds.Width - 120), Math.Max(10, Bounds.Height * 0.66));

    private Rect VolumePlot => new(52, Bounds.Height * 0.72, Math.Max(10, Bounds.Width - 120), Math.Max(8, Bounds.Height * 0.17));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var plot = PricePlot;
        if (point.X < plot.Left || point.X > plot.Right || point.Y < 0 || point.Y > Bounds.Height)
        {
            if (_crosshair != -1 && !_dragging)
            {
                _crosshair = -1;
                InvalidateVisual();
            }

            return;
        }

        var window = Window;
        var index = Math.Clamp((int)((point.X - plot.Left) / plot.Width * window.Count), 0, window.Count - 1);
        if (index != _crosshair)
        {
            _crosshair = index;
            InvalidateVisual();
            Inspected?.Invoke(this, window[index]);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
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
        var window = Window;
        var next = _crosshair < 0 ? window.Count - 1 : _crosshair;
        switch (e.Key)
        {
            case Key.Left:
                next = Math.Max(0, next - 1);
                break;
            case Key.Right:
                next = Math.Min(window.Count - 1, next + 1);
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = window.Count - 1;
                break;
            case Key.Add:
            case Key.OemPlus:
                Visible -= 6;
                e.Handled = true;
                return;
            case Key.Subtract:
            case Key.OemMinus:
                Visible += 6;
                e.Handled = true;
                return;
            default:
                return;
        }

        _crosshair = next;
        InvalidateVisual();
        Inspected?.Invoke(this, window[next]);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var window = Window;
        if (window.Count < 2)
        {
            return;
        }

        var plot = PricePlot;
        var volume = VolumePlot;
        var max = window.Max(candle => candle.High) * 1.01;
        var min = window.Min(candle => candle.Low) * 0.99;
        var maxVolume = window.Max(candle => candle.Volume);

        for (var tick = 0; tick <= 4; tick++)
        {
            var value = min + (max - min) * tick / 4;
            var y = Crisp(DataMath.Map(value, min, max, plot.Bottom, plot.Top));
            context.DrawLine(Ink.Pen(Ink.BorderSubtle, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            Text(context, DataKit.Money(value, 2), plot.Left - 6, y, 10, Ink.Muted, alignment: TextAlignment.Right);
        }

        var slot = plot.Width / window.Count;
        var bodyWidth = Math.Max(2, slot * 0.58);
        var volumeSlot = volume.Width / window.Count;
        for (var index = 0; index < window.Count; index++)
        {
            var candle = window[index];
            var x = plot.Left + slot * index + slot / 2;
            var up = candle.Up;
            var brush = up ? Ink.Series(0) : Ink.Series(3);
            context.DrawLine(Ink.Pen(brush, 1),
                new Point(x, DataMath.Map(candle.High, min, max, plot.Bottom, plot.Top)),
                new Point(x, DataMath.Map(candle.Low, min, max, plot.Bottom, plot.Top)));
            var openY = DataMath.Map(candle.Open, min, max, plot.Bottom, plot.Top);
            var closeY = DataMath.Map(candle.Close, min, max, plot.Bottom, plot.Top);
            var body = new Rect(x - bodyWidth / 2, Math.Min(openY, closeY), bodyWidth,
                Math.Max(1.5, Math.Abs(closeY - openY)));
            context.DrawRectangle(brush, null, body, 1, 1);

            var volumeHeight = candle.Volume / Math.Max(1, maxVolume) * volume.Height;
            context.DrawRectangle(Ink.Alpha(brush, 0.42), null,
                new Rect(volume.Left + volumeSlot * index + volumeSlot * 0.2, volume.Bottom - volumeHeight,
                    Math.Max(1, volumeSlot * 0.6), volumeHeight), 1, 1);
        }

        DrawAverage(context, window, plot, min, max, 5, Ink.Secondary, false);
        DrawAverage(context, window, plot, min, max, 20, Ink.Series(2), true);

        var last = window[^1];
        var lastY = DataMath.Map(last.Close, min, max, plot.Bottom, plot.Top);
        context.DrawLine(Ink.Pen(Ink.Alpha(Ink.Accent, 0.6), 1, true), new Point(plot.Left, lastY),
            new Point(Bounds.Width - 62, lastY));
        var quote = new Rect(Bounds.Width - 60, lastY - 9, 58, 18);
        context.DrawRectangle(Ink.Accent, null, quote, 4, 4);
        Text(context, DataKit.Money(last.Close, 2), quote.Center.X, quote.Center.Y, 10,
            Ink.OnFill(Ink.Accent), FontWeight.Medium, TextAlignment.Center);

        if (_crosshair >= 0 && _crosshair < window.Count)
        {
            var candle = window[_crosshair];
            var x = plot.Left + slot * _crosshair + slot / 2;
            context.DrawLine(Ink.Pen(Ink.Alpha(Ink.Foreground, 0.35), 1, true), new Point(x, plot.Top),
                new Point(x, volume.Bottom));
            Tooltip(context, Bounds.Size, new Point(x, plot.Top + 40), candle.Label, new[]
            {
                ("Open", DataKit.Money(candle.Open, 2), (IBrush?)null),
                ("High", DataKit.Money(candle.High, 2), (IBrush?)null),
                ("Low", DataKit.Money(candle.Low, 2), (IBrush?)null),
                ("Close", DataKit.Money(candle.Close, 2), (IBrush?)null),
                ("Volume", DataKit.Compact(candle.Volume), (IBrush?)null)
            });
        }

        for (var index = 0; index < window.Count; index += 6)
        {
            var x = plot.Left + slot * index + slot / 2;
            Text(context, window[index].Label, x, Bounds.Height - 10, 9, Ink.Muted, alignment: TextAlignment.Center);
        }

        Text(context, "Volume", 6, volume.Center.Y, 9, Ink.Muted);
        Text(context, "MA 5", Bounds.Width - 118, plot.Top + 4, 9, Ink.Secondary);
        Text(context, "MA 20", Bounds.Width - 118, plot.Top + 16, 9, Ink.Muted);
    }

    private static void DrawAverage(DrawingContext context, IReadOnlyList<Candle> window, Rect plot,
        double min, double max, int period, IBrush brush, bool dashed)
    {
        if (window.Count < period)
        {
            return;
        }

        var slot = plot.Width / window.Count;
        var points = new List<Point>();
        for (var index = period - 1; index < window.Count; index++)
        {
            var average = 0.0;
            for (var back = 0; back < period; back++)
            {
                average += window[index - back].Close;
            }

            average /= period;
            points.Add(new Point(
                plot.Left + slot * index + slot / 2,
                DataMath.Map(average, min, max, plot.Bottom, plot.Top)));
        }

        context.DrawGeometry(null, Ink.Pen(Ink.Alpha(brush, 0.85), 1.2, dashed), DataStage.LineGeometry(points));
    }
}
