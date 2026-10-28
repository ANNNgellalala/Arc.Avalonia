using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace Arc.Avalonia.Charts;

/// <summary>
/// A 270 degree gauge for one value against a known range. Thresholds change the ring tone and
/// name the band in text, so meaning never rests on colour alone. A target draws a radial marker.
/// </summary>
public class ArcGauge : ArcChartBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcGauge, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ArcGauge, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ArcGauge, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcGauge, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<ArcGauge, string?>(nameof(Detail));

    public static readonly StyledProperty<ChartTone> ToneProperty =
        AvaloniaProperty.Register<ArcGauge, ChartTone>(nameof(Tone));

    public static readonly StyledProperty<IReadOnlyList<GaugeThreshold>?> ThresholdsProperty =
        AvaloniaProperty.Register<ArcGauge, IReadOnlyList<GaugeThreshold>?>(nameof(Thresholds));

    public static readonly StyledProperty<double?> TargetProperty =
        AvaloniaProperty.Register<ArcGauge, double?>(nameof(Target));

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcGauge, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcGauge, Func<double, string>?>(nameof(ValueFormat));

    static ArcGauge()
    {
        AffectsRender<ArcGauge>(
            ValueProperty,
            MinimumProperty,
            MaximumProperty,
            LabelProperty,
            DetailProperty,
            ToneProperty,
            ThresholdsProperty,
            TargetProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>What is measured, shown above the value.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Secondary caption under the value.</summary>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Ring tone when no threshold applies.</summary>
    public ChartTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Bands by starting value. The highest band reached supplies the tone and label.</summary>
    public IReadOnlyList<GaugeThreshold>? Thresholds
    {
        get => GetValue(ThresholdsProperty);
        set => SetValue(ThresholdsProperty, value);
    }

    /// <summary>Optional target marker on the same scale.</summary>
    public double? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Unit after the value, such as "%".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Optional custom format for the centre value.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 200;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var span = Maximum - Minimum;
        if (span <= 0)
        {
            span = 1;
        }

        var value = Math.Clamp(Value, Minimum, Maximum);
        var ratio = (value - Minimum) / span;
        var diameter = Math.Min(Bounds.Width - 58, Bounds.Height - 42);
        diameter = Math.Max(60, diameter);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2 - 3);
        var radius = diameter / 2;
        var thickness = Math.Clamp(diameter * 0.075, 8, 14);
        const double start = 135;
        const double sweep = 270;

        var track = ChartGeometry.Arc(center, radius, start, sweep);
        context.DrawGeometry(
            null,
            new Pen(ChartPalette.Border, thickness, null, PenLineCap.Round),
            track);

        var thresholds = (Thresholds ?? Array.Empty<GaugeThreshold>())
            .Where(item => item.From > Minimum && item.From < Maximum)
            .OrderBy(item => item.From)
            .ToList();
        foreach (var threshold in thresholds)
        {
            var angle = start + (threshold.From - Minimum) / span * sweep;
            var inner = ChartGeometry.PointOnCircle(center, radius - thickness / 2 - 2, angle);
            var outer = ChartGeometry.PointOnCircle(center, radius + thickness / 2 + 3, angle);
            context.DrawLine(
                new Pen(ChartPalette.Tone(threshold.Tone), 2.5, null, PenLineCap.Round),
                inner,
                outer);
        }

        var activeThreshold = thresholds.LastOrDefault(item => item.From <= value);
        var tone = activeThreshold?.Tone ?? Tone;
        var ringBrush = ChartPalette.Tone(tone);
        if (ratio > 0)
        {
            context.DrawGeometry(
                null,
                new Pen(ringBrush, thickness, null, PenLineCap.Round),
                ChartGeometry.Arc(center, radius, start, Math.Max(1.5, sweep * ratio)));
        }

        if (Target is { } target)
        {
            var targetValue = Math.Clamp(target, Minimum, Maximum);
            var angle = start + (targetValue - Minimum) / span * sweep;
            var inner = ChartGeometry.PointOnCircle(center, radius - thickness / 2 - 4, angle);
            var outer = ChartGeometry.PointOnCircle(center, radius + thickness / 2 + 4, angle);
            context.DrawLine(
                new Pen(ChartPalette.Foreground, 1.5, null, PenLineCap.Round),
                inner,
                outer);
            var labelPoint = ChartGeometry.PointOnCircle(center, radius + 24, angle);
            ChartText.Draw(
                context,
                "target",
                Math.Clamp(labelPoint.X, 22, Bounds.Width - 22),
                Math.Clamp(labelPoint.Y, 12, Bounds.Height - 22),
                12,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
        }

        var startPoint = ChartGeometry.PointOnCircle(center, radius, start);
        var endPoint = ChartGeometry.PointOnCircle(center, radius, start + sweep);
        ChartText.Draw(
            context,
            FormatNumber(Minimum),
            Math.Max(2, startPoint.X + 2),
            Math.Min(Bounds.Height - 10, startPoint.Y + 15),
            12,
            ChartPalette.Muted,
            alignment: TextAlignment.Left);
        ChartText.Draw(
            context,
            FormatNumber(Maximum),
            Math.Min(Bounds.Width - 2, endPoint.X - 2),
            Math.Min(Bounds.Height - 10, endPoint.Y + 15),
            12,
            ChartPalette.Muted,
            alignment: TextAlignment.Right);

        ChartText.Draw(
            context,
            Label ?? string.Empty,
            center.X,
            center.Y - 33,
            12,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: diameter * 0.7);
        ChartText.Draw(
            context,
            FormatValue(value),
            center.X,
            center.Y - 5,
            28,
            ChartPalette.Foreground,
            FontWeight.Medium,
            TextAlignment.Center);
        var detailY = center.Y + 24;
        if (!string.IsNullOrWhiteSpace(Detail))
        {
            ChartText.Draw(
                context,
                Detail,
                center.X,
                detailY,
                12,
                ChartPalette.Secondary,
                alignment: TextAlignment.Center,
                maxWidth: diameter * 0.78);
            detailY += 16;
        }

        if (!string.IsNullOrWhiteSpace(activeThreshold?.Label))
        {
            ChartText.Draw(
                context,
                activeThreshold!.Label!,
                center.X,
                detailY,
                12,
                ChartPalette.Tone(activeThreshold.Tone),
                FontWeight.Medium,
                TextAlignment.Center,
                maxWidth: diameter * 0.78);
        }
    }

    private string FormatValue(double value)
    {
        if (ValueFormat is not null)
        {
            return ValueFormat(value);
        }

        var formatted = ChartFormat.Number(value, Math.Abs(value - Math.Round(value)) < 0.05 ? 0 : 1);
        return formatted + ValueSuffix;
    }

    private string FormatNumber(double value)
        => ChartFormat.Compact(value);
}
