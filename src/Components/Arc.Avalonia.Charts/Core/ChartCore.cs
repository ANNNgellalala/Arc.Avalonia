using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>One line, stream, ridge, or sparkline series.</summary>
/// <param name="Name">Series name, shown in tooltips and legends.</param>
/// <param name="Values">Values in display order. Missing points are treated as zero.</param>
/// <param name="Brush">Optional series colour. Defaults to the Arc series palette by index.</param>
/// <param name="Dashed">Draws a dashed line, useful for a target or previous period.</param>
public sealed record ChartSeries(
    string Name,
    IReadOnlyList<double> Values,
    IBrush? Brush = null,
    bool Dashed = false);

/// <summary>A named value, used by donut, waffle, treemap, and split bar charts.</summary>
public sealed record ChartValue(string Name, double Value, IBrush? Brush = null);

/// <summary>A before and after item for the slope chart.</summary>
public sealed record SlopeItem(string Name, double Start, double End, IBrush? Brush = null);

/// <summary>Semantic tones available to gauges and sparklines.</summary>
public enum ChartTone
{
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>One gauge threshold; the highest band reached supplies the tone and label.</summary>
public sealed record GaugeThreshold(double From, ChartTone Tone, string? Label = null);

/// <summary>Base for the custom drawn charts: deterministic measure plus crisp pixel helpers.</summary>
public abstract partial class ArcChartBase : Control
{
    protected ArcChartBase()
    {
        ClipToBounds = true;
    }

    /// <summary>Height used when the normal Avalonia Height property is unset.</summary>
    protected abstract double DefaultHeight { get; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var height = double.IsNaN(Height) ? DefaultHeight : Height;
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>Pixel centre for a 1 px horizontal gridline.</summary>
    protected static double Crisp(double value) => Math.Round(value) + 0.5;
}

/// <summary>Resolves semantic chart colours at render time so both themes and accent swaps work.</summary>
internal static class ChartPalette
{
    public static IBrush Series(int index) => index switch
    {
        0 => ArcResources.Brush(ArcTokens.Series1),
        1 => ArcResources.Brush(ArcTokens.Series2),
        2 => ArcResources.Brush(ArcTokens.Series3),
        3 => ArcResources.Brush(ArcTokens.Series4),
        4 => ArcResources.Brush(ArcTokens.TextSecondary),
        _ => ArcResources.Brush(ArcTokens.TextMuted)
    };

    public static IBrush Tone(ChartTone tone) => tone switch
    {
        ChartTone.Success => ArcResources.Brush(ArcTokens.Success),
        ChartTone.Warning => ArcResources.Brush(ArcTokens.Warning),
        ChartTone.Danger => ArcResources.Brush(ArcTokens.Danger),
        _ => Series(0)
    };

    public static IBrush Alpha(IBrush brush, double alpha)
    {
        if (brush is ISolidColorBrush solid)
        {
            var color = solid.Color;
            var a = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
            return new SolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B));
        }

        return brush;
    }

    public static IBrush Surface => ArcResources.Brush(ArcTokens.Surface);
    public static IBrush SurfaceRaised => ArcResources.Brush(ArcTokens.SurfaceRaised);
    public static IBrush Foreground => ArcResources.Brush(ArcTokens.Foreground);
    public static IBrush Secondary => ArcResources.Brush(ArcTokens.TextSecondary);
    public static IBrush Muted => ArcResources.Brush(ArcTokens.TextMuted);
    public static IBrush Border => ArcResources.Brush(ArcTokens.Border);
    public static IBrush BorderSubtle => ArcResources.Brush(ArcTokens.BorderSubtle);
    public static IBrush BorderStrong => ArcResources.Brush(ArcTokens.BorderStrong);
    public static IBrush Accent => ArcResources.Brush(ArcTokens.Accent);

    public static Pen Pen(IBrush brush, double thickness = ChartMetrics.Hairline, bool dashed = false) => new(
        brush,
        thickness,
        dashed ? new DashStyle(new[] { 4.0, 3.0 }, 0) : null,
        PenLineCap.Round,
        PenLineJoin.Round);
}

/// <summary>Formatted text on the Inter body face, with tabular figures for every number.</summary>
internal static class ChartText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly FontFeatureCollection Tabular = FontFeatureCollection.Parse("tnum");

    public static FontFamily Font
    {
        get
        {
            if (Application.Current is { } app &&
                app.TryFindResource(ArcTokens.FontBody, out var value) &&
                value is FontFamily family)
            {
                return family;
            }

            return FontFamily.Default;
        }
    }

    public static FormattedText Create(
        string text,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal,
        double maxWidth = double.PositiveInfinity,
        bool tabular = true)
    {
        var formatted = new FormattedText(
            text,
            Culture,
            FlowDirection.LeftToRight,
            new Typeface(Font, FontStyle.Normal, weight),
            size,
            brush);

        if (tabular)
        {
            formatted.SetFontFeatures(Tabular);
        }

        if (!double.IsInfinity(maxWidth) && maxWidth > 0)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    public static void Draw(
        DrawingContext context,
        FormattedText text,
        double x,
        double centerY,
        TextAlignment alignment = TextAlignment.Left)
    {
        var dx = alignment switch
        {
            TextAlignment.Center => x - text.Width / 2,
            TextAlignment.Right => x - text.Width,
            _ => x
        };

        context.DrawText(text, new Point(dx, centerY - text.Height / 2));
    }

    public static void Draw(
        DrawingContext context,
        string text,
        double x,
        double centerY,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal,
        TextAlignment alignment = TextAlignment.Left,
        bool tabular = true,
        double maxWidth = double.PositiveInfinity)
    {
        var formatted = Create(text, size, brush, weight, maxWidth, tabular);
        Draw(context, formatted, x, centerY, alignment);
    }

    public static double Width(string text, double size, FontWeight weight = FontWeight.Normal)
        => Create(text, size, ChartPalette.Foreground, weight).Width;
}

/// <summary>Number and geometry helpers shared by the charts.</summary>
internal static class ChartFormat
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Compact(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000_000)
        {
            return (value / 1_000_000_000).ToString("0.#", Culture) + "B";
        }

        if (abs >= 1_000_000)
        {
            return (value / 1_000_000).ToString("0.#", Culture) + "M";
        }

        if (abs >= 1_000)
        {
            return (value / 1_000).ToString("0.#", Culture) + "K";
        }

        return abs >= 100 || Math.Abs(value - Math.Round(value)) < 0.05
            ? value.ToString("N0", Culture)
            : value.ToString("N1", Culture);
    }

    public static string Number(double value, int decimals = 0)
        => value.ToString("N" + Math.Max(0, decimals), Culture);

    public static string Percent(double value, int decimals = 0)
        => (value * 100).ToString("0." + new string('#', Math.Max(0, decimals)), Culture) + "%";

    public static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    public static double Map(double value, double inMin, double inMax, double outMin, double outMax)
    {
        if (Math.Abs(inMax - inMin) < 1e-9)
        {
            return (outMin + outMax) / 2;
        }

        return outMin + (value - inMin) / (inMax - inMin) * (outMax - outMin);
    }
}

/// <summary>Stream geometry builders for lines, areas, and arcs.</summary>
internal static class ChartGeometry
{
    public static StreamGeometry Line(IReadOnlyList<Point> points, bool smooth)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(points[0], false);
        AppendSegments(context, points, smooth);
        context.EndFigure(false);
        return geometry;
    }

    public static StreamGeometry Area(IReadOnlyList<Point> points, double baseline, bool smooth)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].X, baseline), true);
        context.LineTo(points[0]);
        AppendSegments(context, points, smooth);
        context.LineTo(new Point(points[^1].X, baseline));
        context.EndFigure(true);
        return geometry;
    }

    public static StreamGeometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        var geometry = new StreamGeometry();
        if (radius <= 0 || Math.Abs(sweepDegrees) < 0.1)
        {
            return geometry;
        }

        var start = PointOnCircle(center, radius, startDegrees);
        var end = PointOnCircle(center, radius, startDegrees + sweepDegrees);
        using var context = geometry.Open();
        context.BeginFigure(start, false);
        context.ArcTo(
            end,
            new Size(radius, radius),
            0,
            Math.Abs(sweepDegrees) > 180,
            sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        context.EndFigure(false);
        return geometry;
    }

    public static StreamGeometry DonutSegment(
        Point center,
        double outerRadius,
        double innerRadius,
        double startDegrees,
        double sweepDegrees)
    {
        var geometry = new StreamGeometry();
        if (sweepDegrees >= 359.99)
        {
            using var full = geometry.Open();
            full.BeginFigure(PointOnCircle(center, outerRadius, startDegrees), true);
            full.ArcTo(
                PointOnCircle(center, outerRadius, startDegrees + 180),
                new Size(outerRadius, outerRadius),
                0,
                true,
                SweepDirection.Clockwise);
            full.ArcTo(
                PointOnCircle(center, outerRadius, startDegrees + 359.99),
                new Size(outerRadius, outerRadius),
                0,
                true,
                SweepDirection.Clockwise);
            full.LineTo(PointOnCircle(center, innerRadius, startDegrees + 359.99));
            full.ArcTo(
                PointOnCircle(center, innerRadius, startDegrees + 180),
                new Size(innerRadius, innerRadius),
                0,
                true,
                SweepDirection.CounterClockwise);
            full.ArcTo(
                PointOnCircle(center, innerRadius, startDegrees),
                new Size(innerRadius, innerRadius),
                0,
                true,
                SweepDirection.CounterClockwise);
            full.EndFigure(true);
            return geometry;
        }

        var large = Math.Abs(sweepDegrees) > 180;
        var direction = sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
        var reverse = sweepDegrees >= 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise;
        var outerStart = PointOnCircle(center, outerRadius, startDegrees);
        var outerEnd = PointOnCircle(center, outerRadius, startDegrees + sweepDegrees);
        var innerEnd = PointOnCircle(center, innerRadius, startDegrees + sweepDegrees);
        var innerStart = PointOnCircle(center, innerRadius, startDegrees);

        using var context = geometry.Open();
        context.BeginFigure(outerStart, true);
        context.ArcTo(outerEnd, new Size(outerRadius, outerRadius), 0, large, direction);
        context.LineTo(innerEnd);
        context.ArcTo(innerStart, new Size(innerRadius, innerRadius), 0, large, reverse);
        context.EndFigure(true);
        return geometry;
    }

    public static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static void AppendSegments(
        StreamGeometryContext context,
        IReadOnlyList<Point> points,
        bool smooth)
    {
        if (points.Count == 1)
        {
            return;
        }

        if (!smooth || points.Count == 2)
        {
            for (var i = 1; i < points.Count; i++)
            {
                context.LineTo(points[i]);
            }

            return;
        }

        var tangents = MonotoneTangents(points);
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[i];
            var p1 = points[i + 1];
            var dx = (p1.X - p0.X) / 3;
            var c1 = new Point(p0.X + dx, p0.Y + tangents[i] * dx);
            var c2 = new Point(p1.X - dx, p1.Y - tangents[i + 1] * dx);
            context.CubicBezierTo(c1, c2, p1);
        }
    }

    /// <summary>Fritsch-Carlson tangents, so a smooth line never swings past its data.</summary>
    private static double[] MonotoneTangents(IReadOnlyList<Point> points)
    {
        var count = points.Count;
        var deltas = new double[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            var dx = Math.Max(0.001, points[i + 1].X - points[i].X);
            deltas[i] = (points[i + 1].Y - points[i].Y) / dx;
        }

        var tangents = new double[count];
        tangents[0] = deltas[0];
        tangents[^1] = deltas[^1];
        for (var i = 1; i < count - 1; i++)
        {
            tangents[i] = (deltas[i - 1] + deltas[i]) / 2;
        }

        for (var i = 0; i < deltas.Length; i++)
        {
            if (Math.Abs(deltas[i]) < 1e-9)
            {
                tangents[i] = 0;
                tangents[i + 1] = 0;
                continue;
            }

            var a = tangents[i] / deltas[i];
            var b = tangents[i + 1] / deltas[i];
            if (a < 0)
            {
                tangents[i] = 0;
                a = 0;
            }

            if (b < 0)
            {
                tangents[i + 1] = 0;
                b = 0;
            }

            var length = a * a + b * b;
            if (length > 9)
            {
                var tau = 3 / Math.Sqrt(length);
                tangents[i] = tau * a * deltas[i];
                tangents[i + 1] = tau * b * deltas[i];
            }
        }

        return tangents;
    }
}

/// <summary>A small tooltip that stays inside the chart bounds.</summary>
internal static class ChartTooltip
{
    public static void Draw(
        DrawingContext context,
        Rect bounds,
        Point anchor,
        string title,
        IReadOnlyList<(string Label, string Value, IBrush? Brush)> rows)
    {
        var padding = ChartMetrics.TooltipPadding;
        var rowHeight = ChartMetrics.TooltipRowHeight;
        var titleText = ChartText.Create(title, ChartMetrics.LabelFontSize, ChartPalette.Foreground, FontWeight.Medium);
        var width = titleText.Width;
        var rowTexts = new List<(FormattedText Label, FormattedText Value, IBrush? Brush)>();
        foreach (var row in rows)
        {
            var label = ChartText.Create(row.Label, ChartMetrics.LabelFontSize, ChartPalette.Muted, maxWidth: 116);
            var value = ChartText.Create(row.Value, ChartMetrics.LabelFontSize, ChartPalette.Foreground, FontWeight.Medium);
            rowTexts.Add((label, value, row.Brush));
            width = Math.Max(width, label.Width + value.Width + 14);
        }

        width = Math.Min(bounds.Width - 12, Math.Ceiling(width) + padding * 2);
        var height = 15 + rowHeight * rowTexts.Count + padding * 2 - 4;
        var x = anchor.X + 12;
        if (x + width > bounds.Right - 6)
        {
            x = anchor.X - width - 12;
        }

        x = Math.Clamp(x, bounds.X + 6, Math.Max(bounds.X + 6, bounds.Right - width - 6));
        var y = Math.Clamp(anchor.Y - height / 2, bounds.Y + 6, Math.Max(bounds.Y + 6, bounds.Bottom - height - 6));
        var rect = new Rect(x, y, width, height);
        context.DrawRectangle(
            ChartPalette.SurfaceRaised,
            new Pen(ChartPalette.Border),
            rect,
            ChartMetrics.SmallCorner,
            ChartMetrics.SmallCorner);
        context.DrawText(titleText, new Point(rect.X + padding, rect.Y + padding - 2));

        var rowY = rect.Y + padding + 14;
        foreach (var row in rowTexts)
        {
            if (row.Brush is not null)
            {
                context.DrawEllipse(row.Brush, null, new Point(rect.X + padding + 3, rowY + 6), 3, 3);
            }

            var labelX = row.Brush is null ? rect.X + padding : rect.X + padding + 11;
            context.DrawText(row.Label, new Point(labelX, rowY - 2));
            context.DrawText(
                row.Value,
                new Point(rect.Right - padding - row.Value.Width, rowY - 2));
            rowY += rowHeight;
        }
    }
}

/// <summary>Squarified treemap layout over a flat list of positive values.</summary>
internal static class TreemapLayout
{
    public static List<(ChartValue Item, Rect Rect)> Compute(IReadOnlyList<ChartValue> items, Rect bounds)
    {
        var result = new List<(ChartValue, Rect)>();
        var values = items.Where(item => item.Value > 0).OrderByDescending(item => item.Value).ToList();
        var total = values.Sum(item => item.Value);
        if (total <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return result;
        }

        var work = values.Select(item => new Entry(item, item.Value / total * bounds.Width * bounds.Height)).ToList();
        var remaining = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var index = 0;
        while (index < work.Count)
        {
            if (index == work.Count - 1)
            {
                result.Add((work[index].Item, remaining));
                break;
            }

            var horizontal = remaining.Width >= remaining.Height;
            var side = horizontal ? remaining.Height : remaining.Width;
            var row = new List<Entry>();
            var rowArea = 0.0;
            var bestRatio = double.PositiveInfinity;
            while (index < work.Count)
            {
                var candidate = work[index];
                var candidateArea = rowArea + candidate.Area;
                var thickness = candidateArea / side;
                var candidateWorst = WorstRatio(row, candidate, thickness);
                if (row.Count > 0 && candidateWorst > bestRatio)
                {
                    break;
                }

                row.Add(candidate);
                rowArea = candidateArea;
                bestRatio = candidateWorst;
                index++;
            }

            var rowThickness = rowArea / side;
            if (horizontal)
            {
                var y = remaining.Y;
                foreach (var entry in row)
                {
                    var height = rowThickness == 0 ? 0 : entry.Area / rowThickness;
                    result.Add((entry.Item, new Rect(remaining.X, y, rowThickness, height)));
                    y += height;
                }

                remaining = new Rect(
                    remaining.X + rowThickness,
                    remaining.Y,
                    Math.Max(0, remaining.Width - rowThickness),
                    remaining.Height);
            }
            else
            {
                var x = remaining.X;
                foreach (var entry in row)
                {
                    var width = rowThickness == 0 ? 0 : entry.Area / rowThickness;
                    result.Add((entry.Item, new Rect(x, remaining.Y, width, rowThickness)));
                    x += width;
                }

                remaining = new Rect(
                    remaining.X,
                    remaining.Y + rowThickness,
                    remaining.Width,
                    Math.Max(0, remaining.Height - rowThickness));
            }
        }

        return result;
    }

    private static double WorstRatio(List<Entry> row, Entry candidate, double thickness)
    {
        if (thickness <= 0)
        {
            return double.PositiveInfinity;
        }

        var worst = 0.0;
        foreach (var entry in row.Append(candidate))
        {
            var length = entry.Area / thickness;
            if (length <= 0)
            {
                continue;
            }

            worst = Math.Max(worst, Math.Max(thickness / length, length / thickness));
        }

        return worst;
    }

    private sealed record Entry(ChartValue Item, double Area);
}
