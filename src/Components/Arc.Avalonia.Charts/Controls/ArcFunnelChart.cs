using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A conversion funnel drawn as one tapered band: each stage is flat across its height and eases into the
/// next, so the drop between steps is the shape itself. Stage names and counts sit to the left, the share
/// kept at every step to the right, and a hovered or focused stage dims the rest and reads out in a tooltip.
/// </summary>
/// <remarks>
/// Approximation: Arc Pro can split every stage by a segment (device, plan) and morph between a total and a
/// split view. That view switch needs a second data dimension, so this port draws one band per stage and
/// keeps the split comparison for the legend-free tooltip. Streaming dots are omitted.
/// </remarks>
public class ArcFunnelChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcFunnelStage>?> StagesProperty =
        AvaloniaProperty.Register<ArcFunnelChart, IReadOnlyList<ArcFunnelStage>?>(nameof(Stages));

    public static readonly StyledProperty<bool> ShowConversionProperty =
        AvaloniaProperty.Register<ArcFunnelChart, bool>(nameof(ShowConversion), true);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcFunnelChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcFunnelChart, Func<double, string>?>(nameof(ValueFormat));

    private readonly List<StageRow> _rows = new();
    private Point _bandCenter;
    private int _active = -1;
    private bool _keyboardFocus;

    static ArcFunnelChart()
    {
        AffectsRender<ArcFunnelChart>(
            StagesProperty,
            ShowConversionProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        StagesProperty.Changed.AddClassHandler<ArcFunnelChart>((chart, _) => chart._active = -1);
    }

    public ArcFunnelChart()
    {
        Focusable = true;
    }

    /// <summary>Ordered stages. The first stage is the whole; each later stage is a subset of it.</summary>
    public IReadOnlyList<ArcFunnelStage>? Stages
    {
        get => GetValue(StagesProperty);
        set => SetValue(StagesProperty, value);
    }

    /// <summary>Prints the share kept from the previous stage at every step boundary.</summary>
    public bool ShowConversion
    {
        get => GetValue(ShowConversionProperty);
        set => SetValue(ShowConversionProperty, value);
    }

    /// <summary>Unit after counts, such as " sessions".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Formats stage counts and the tooltip.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 244;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        var index = IndexAt(position);
        SetActive(index);
        if (index >= 0 && Stages is { } stages && index < stages.Count)
        {
            RaiseItemClick(index, stages[index].Label, stages[index].Value, position);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsFocused || !_keyboardFocus)
        {
            SetActive(-1);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_keyboardFocus)
        {
            _keyboardFocus = false;
            InvalidateVisual();
        }

        var position = e.GetPosition(this);
        var index = IndexAt(position);
        SetActive(index);
        if (index >= 0 && Stages is { } stages && index < stages.Count)
        {
            RaiseItemClick(index, stages[index].Label, stages[index].Value, position);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Stages?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        var next = _active < 0 ? 0 : _active;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Right:
                next = Math.Min(count - 1, next + 1);
                break;
            case Key.Up:
            case Key.Left:
                next = Math.Max(0, next - 1);
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
        _rows.Clear();

        var stages = Stages?.Where(stage => stage.Value >= 0).ToList() ?? new List<ArcFunnelStage>();
        if (stages.Count == 0 || stages[0].Value <= 0)
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

        var conversion = ShowConversion && Bounds.Width >= 380 && stages.Count > 1;
        var labelWidth = Math.Min(140, Bounds.Width * 0.3);
        var conversionWidth = conversion ? Math.Min(74, Bounds.Width * 0.17) : 10;
        var bandLeft = labelWidth;
        var bandRight = Bounds.Width - conversionWidth;
        var bandWidth = Math.Max(40, bandRight - bandLeft);
        _bandCenter = new Point(bandLeft + bandWidth / 2, Bounds.Height / 2);

        var top = 9.0;
        var bottom = Math.Max(top + 20, Bounds.Height - 9);
        var rowHeight = (bottom - top) / stages.Count;
        var maximum = stages.Max(stage => stage.Value);
        var first = stages[0].Value;
        var ease = Math.Clamp(rowHeight * 0.3, 6, 18);

        for (var index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            var y0 = top + index * rowHeight;
            var y1 = index == stages.Count - 1 ? bottom : y0 + rowHeight;
            var halfTop = bandWidth * Math.Max(0, stage.Value) / maximum / 2;
            var halfBottom = index == stages.Count - 1
                ? Math.Max(6, halfTop * 0.94)
                : bandWidth * Math.Max(0, stages[index + 1].Value) / maximum / 2;
            _rows.Add(new StageRow(index, new Rect(2, y0, Math.Max(10, Bounds.Width - 4), rowHeight), y0, y1, halfTop, halfBottom, stage));
        }

        // Row highlight behind everything for the stage in focus.
        if (_active >= 0 && _active < _rows.Count)
        {
            var row = _rows[_active];
            context.DrawRectangle(
                ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.6),
                null,
                new RoundedRect(new Rect(0, row.Y0 + 1, Bounds.Width, Math.Max(2, row.Y1 - row.Y0 - 2)), 9));
        }

        foreach (var row in _rows)
        {
            var brush = ChartPalette.Series(row.Index % 4);
            if (_active >= 0 && _active != row.Index)
            {
                brush = ChartPalette.Alpha(brush, 0.34);
            }
            else if (_active < 0)
            {
                brush = ChartPalette.Alpha(brush, 0.9);
            }

            var geometry = Band(
                _bandCenter.X,
                row.Y0 + 2,
                row.Y1 - 1,
                row.HalfTop,
                row.HalfBottom,
                ease);
            context.DrawGeometry(brush, new Pen(ChartPalette.Surface, 1.5), geometry);
        }

        DrawStages(context, stages, bandLeft - 12, conversion);
        DrawConversions(context, stages, bandRight, conversion);

        if (_active >= 0 && _active < stages.Count)
        {
            var stage = stages[_active];
            var share = stage.Value / first;
            var rows = new List<(string Label, string Value, IBrush? Brush)>
            {
                ("Count", FormatValue(stage.Value), (IBrush?)ChartPalette.Series(_active % 4)),
                ("Of first step", ChartFormat.Percent(share, 1), null)
            };
            if (_active > 0)
            {
                var previous = stages[_active - 1].Value;
                rows.Add(("Kept from previous", previous <= 0 ? "0%" : ChartFormat.Percent(stage.Value / previous, 0), null));
                rows.Add(("Dropped", FormatValue(Math.Max(0, previous - stage.Value)), null));
            }

            var row = _rows[_active];
            ChartTooltip.Draw(
                context,
                Bounds,
                new Point(_bandCenter.X + row.HalfTop, row.Y0 + rowHeight / 2),
                stage.Label,
                rows);
        }

        if (_keyboardFocus && IsFocused)
        {
            context.DrawRectangle(
                null,
                new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2),
                new RoundedRect(
                    new Rect(1.5, 1.5, Math.Max(0, Bounds.Width - 3), Math.Max(0, Bounds.Height - 3)),
                    12));
        }
    }

    private void DrawStages(
        DrawingContext context,
        IReadOnlyList<ArcFunnelStage> stages,
        double labelRight,
        bool conversion)
    {
        for (var index = 0; index < _rows.Count && index < stages.Count; index++)
        {
            var row = _rows[index];
            var stage = stages[index];
            var centerY = (row.Y0 + row.Y1) / 2;
            var active = index == _active;
            var name = ChartText.Create(
                stage.Label,
                ChartMetrics.CaptionFontSize,
                active ? ChartPalette.Foreground : ChartPalette.Secondary,
                active ? FontWeight.Medium : FontWeight.Normal,
                Math.Max(40, labelRight - 4));
            ChartText.Draw(context, name, labelRight, centerY - 8, TextAlignment.Right);
            ChartText.Draw(
                context,
                FormatValue(stage.Value),
                labelRight,
                centerY + 8,
                ChartMetrics.TitleFontSize,
                active ? ChartPalette.Foreground : ChartPalette.Muted,
                FontWeight.Medium,
                TextAlignment.Right);
        }
    }

    private void DrawConversions(DrawingContext context, IReadOnlyList<ArcFunnelStage> stages, double bandRight, bool conversion)
    {
        if (!conversion)
        {
            return;
        }

        var x = bandRight + 10;
        for (var index = 1; index < _rows.Count && index < stages.Count; index++)
        {
            var previous = stages[index - 1].Value;
            var ratio = previous <= 0 ? 0 : stages[index].Value / previous;
            var y = _rows[index].Y0 + 1;
            var color = index == _active
                ? ChartPalette.Foreground
                : ChartPalette.Muted;
            ChartText.Draw(
                context,
                ChartFormat.Percent(ratio, 0),
                x,
                y - 5,
                ChartMetrics.CaptionFontSize,
                color,
                FontWeight.Medium);
            ChartText.Draw(
                context,
                "kept",
                x,
                y + 5,
                ChartMetrics.CaptionFontSize,
                ChartPalette.Muted);
        }
    }

    private int IndexAt(Point point)
    {
        foreach (var row in _rows)
        {
            if (row.Bounds.Contains(point))
            {
                return row.Index;
            }
        }

        return -1;
    }

    private void SetActive(int index)
    {
        if (_active == index)
        {
            return;
        }

        _active = index;
        InvalidateVisual();
    }

    private static StreamGeometry Band(double centerX, double top, double bottom, double halfTop, double halfBottom, double ease)
    {
        var geometry = new StreamGeometry();
        var knee = Math.Max(top, bottom - ease);
        var leftTop = centerX - halfTop;
        var rightTop = centerX + halfTop;
        var leftBottom = centerX - halfBottom;
        var rightBottom = centerX + halfBottom;
        using var context = geometry.Open();
        context.BeginFigure(new Point(leftTop, top), true);
        context.LineTo(new Point(rightTop, top));
        context.LineTo(new Point(rightTop, knee));
        context.CubicBezierTo(
            new Point(rightTop, knee + ease * 0.55),
            new Point(rightBottom, bottom - ease * 0.45),
            new Point(rightBottom, bottom));
        context.LineTo(new Point(leftBottom, bottom));
        context.CubicBezierTo(
            new Point(leftBottom, bottom - ease * 0.45),
            new Point(leftTop, knee + ease * 0.55),
            new Point(leftTop, knee));
        context.EndFigure(true);
        return geometry;
    }

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 0);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }

    private sealed record StageRow(
        int Index,
        Rect Bounds,
        double Y0,
        double Y1,
        double HalfTop,
        double HalfBottom,
        ArcFunnelStage Stage);
}
