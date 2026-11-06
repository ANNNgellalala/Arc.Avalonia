using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A part-to-whole donut with a centre total, hover emphasis, and a synced legend.
/// The ring starts at twelve o'clock and runs clockwise.
/// </summary>
public class ArcDonutChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartValue>?> SegmentsProperty =
        AvaloniaProperty.Register<ArcDonutChart, IReadOnlyList<ChartValue>?>(nameof(Segments));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcDonutChart, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<string> CenterLabelProperty =
        AvaloniaProperty.Register<ArcDonutChart, string>(nameof(CenterLabel), "Total");

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcDonutChart, string?>(nameof(Unit));

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<ArcDonutChart, double>(nameof(RingThickness), 22);

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcDonutChart, Func<double, string>?>(nameof(ValueFormat));

    private readonly List<(Rect Bounds, int Index)> _legendHits = new();
    private Point _center;
    private double _innerRadius;
    private double _outerRadius;
    private int _activeIndex = -1;

    static ArcDonutChart()
    {
        AffectsRender<ArcDonutChart>(
            SegmentsProperty,
            ShowLegendProperty,
            CenterLabelProperty,
            UnitProperty,
            RingThicknessProperty,
            ValueFormatProperty);
        SegmentsProperty.Changed.AddClassHandler<ArcDonutChart>((chart, _) => chart._activeIndex = -1);
    }

    public ArcDonutChart()
    {
        Focusable = true;
    }

    /// <summary>Parts of the whole, in draw order. Zero and negative values are left out.</summary>
    public IReadOnlyList<ChartValue>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>Shows the swatch and share list beside the ring.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>Label above the total at rest.</summary>
    public string CenterLabel
    {
        get => GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    /// <summary>Unit shown under the total, such as "visits".</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Ring thickness in pixels.</summary>
    public double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    /// <summary>Optional custom number format for centre values.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 208;

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

        var segments = VisibleSegments();
        var distance = Math.Sqrt(
            Math.Pow(point.X - _center.X, 2) + Math.Pow(point.Y - _center.Y, 2));
        if (segments.Count == 0 || distance < _innerRadius - 5 || distance > _outerRadius + 5)
        {
            SetActive(-1);
            return;
        }

        var angle = Math.Atan2(point.Y - _center.Y, point.X - _center.X) * 180 / Math.PI + 90;
        if (angle < 0)
        {
            angle += 360;
        }

        var total = segments.Sum(item => item.Value);
        var running = 0.0;
        for (var i = 0; i < segments.Count; i++)
        {
            var sweep = segments[i].Value / total * 360;
            if (angle >= running && angle <= running + sweep)
            {
                SetActive(i);
                return;
            }

            running += sweep;
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

            var items = VisibleSegments();
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
        var count = VisibleSegments().Count;
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
        _legendHits.Clear();
        var segments = VisibleSegments();
        if (segments.Count == 0)
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

        var showLegend = ShowLegend && Bounds.Width >= 270;
        var ringDiameter = showLegend
            ? Math.Min(Bounds.Height - 28, Bounds.Width * 0.49)
            : Math.Min(Bounds.Width - 16, Bounds.Height - 16);
        ringDiameter = Math.Max(64, ringDiameter);
        var centerX = showLegend ? 10 + ringDiameter / 2 : Bounds.Width / 2;
        _center = new Point(centerX, Bounds.Height / 2);
        _outerRadius = ringDiameter / 2;
        var thickness = Math.Clamp(RingThickness, 10, Math.Max(10, _outerRadius * 0.55));
        _innerRadius = Math.Max(10, _outerRadius - thickness);

        context.DrawEllipse(
            null,
            new Pen(ChartPalette.BorderSubtle, thickness),
            _center,
            (_outerRadius + _innerRadius) / 2,
            (_outerRadius + _innerRadius) / 2);

        var total = segments.Sum(item => item.Value);
        var start = -90.0;
        var activeIndex = _activeIndex;
        var activeSegment = activeIndex >= 0 && activeIndex < segments.Count ? segments[activeIndex] : null;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var sweep = segment.Value / total * 360;
            var gap = Math.Min(2.2, sweep * 0.18);
            var brush = segment.Brush ?? ChartPalette.Series(i);
            if (activeSegment is not null && i != activeIndex)
            {
                brush = ChartPalette.Alpha(brush, 0.38);
            }

            var drawCenter = _center;
            if (i == activeIndex)
            {
                var middle = (start + sweep / 2) * Math.PI / 180;
                drawCenter = new Point(_center.X + Math.Cos(middle) * 3, _center.Y + Math.Sin(middle) * 3);
            }

            var geometry = ChartGeometry.DonutSegment(drawCenter, _outerRadius, _innerRadius, start + gap / 2, Math.Max(0.2, sweep - gap));
            context.DrawGeometry(brush, null, geometry);
            start += sweep;
        }

        DrawCenter(context, segments, total);
        if (showLegend)
        {
            DrawLegend(context, segments, total);
        }
    }

    private void DrawCenter(DrawingContext context, IReadOnlyList<ChartValue> segments, double total)
    {
        var active = _activeIndex >= 0 && _activeIndex < segments.Count ? segments[_activeIndex] : null;
        var label = active?.Name ?? CenterLabel;
        var value = active?.Value ?? total;
        var detail = active is null
            ? Unit ?? $"{segments.Count} parts"
            : ChartFormat.Percent(active.Value / total, 1);

        ChartText.Draw(
            context,
            label,
            _center.X,
            _center.Y - 17,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: _innerRadius * 1.8);
        ChartText.Draw(
            context,
            ValueFormat?.Invoke(value) ?? ChartFormat.Compact(value),
            _center.X,
            _center.Y + 3,
            23,
            ChartPalette.Foreground,
            FontWeight.Medium,
            TextAlignment.Center);
        ChartText.Draw(
            context,
            detail,
            _center.X,
            _center.Y + 25,
            ChartMetrics.LabelFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: _innerRadius * 1.8);
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartValue> segments, double total)
    {
        var x = _center.X + _outerRadius + 22;
        var available = Bounds.Width - x - 6;
        var rowHeight = 24.0;
        var y = _center.Y - segments.Count * rowHeight / 2 + 2;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var row = new Rect(x - 4, y, Math.Max(20, available + 4), rowHeight);
            if (i == _activeIndex)
            {
                context.DrawRectangle(ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.7), null, row, 6, 6);
            }

            var brush = segment.Brush ?? ChartPalette.Series(i);
            context.DrawRectangle(brush, null, new Rect(x, y + 7, 8, 8), 2, 2);
            var name = ChartText.Create(
                segment.Name,
                ChartMetrics.CaptionFontSize,
                i == _activeIndex ? ChartPalette.Foreground : ChartPalette.Secondary,
                i == _activeIndex ? FontWeight.Medium : FontWeight.Normal,
                Math.Max(24, available - 46));
            context.DrawText(name, new Point(x + 14, y + 11 - name.Height / 2));
            ChartText.Draw(
                context,
                ChartFormat.Percent(segment.Value / total, 0),
                x + available,
                y + 11,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Right);
            _legendHits.Add((row, i));
            y += rowHeight;
        }
    }

    private List<ChartValue> VisibleSegments()
        => Segments?.Where(item => item.Value > 0).ToList() ?? new List<ChartValue>();

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
