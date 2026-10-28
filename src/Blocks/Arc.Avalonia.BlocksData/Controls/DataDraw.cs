using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData;

/// <summary>Resolves semantic ink for the custom drawn stages. Every colour is a token.</summary>
internal static class Ink
{
    public static IBrush Surface => ArcResources.Brush(ArcTokens.Surface);
    public static IBrush SurfaceRaised => ArcResources.Brush(ArcTokens.SurfaceRaised);
    public static IBrush SurfaceMuted => ArcResources.Brush(ArcTokens.SurfaceMuted);
    public static IBrush Foreground => ArcResources.Brush(ArcTokens.Foreground);
    public static IBrush Secondary => ArcResources.Brush(ArcTokens.TextSecondary);
    public static IBrush Muted => ArcResources.Brush(ArcTokens.TextMuted);
    public static IBrush Border => ArcResources.Brush(ArcTokens.Border);
    public static IBrush BorderSubtle => ArcResources.Brush(ArcTokens.BorderSubtle);
    public static IBrush BorderStrong => ArcResources.Brush(ArcTokens.BorderStrong);
    public static IBrush Accent => ArcResources.Brush(ArcTokens.Accent);
    public static IBrush Success => ArcResources.Brush(ArcTokens.Success);
    public static IBrush Warning => ArcResources.Brush(ArcTokens.Warning);
    public static IBrush Danger => ArcResources.Brush(ArcTokens.Danger);

    public static IBrush Series(int index) => DataKit.Series(index);

    public static IBrush Alpha(IBrush brush, double alpha)
    {
        if (brush is ISolidColorBrush solid)
        {
            var a = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
            return new SolidColorBrush(Color.FromArgb(a, solid.Color.R, solid.Color.G, solid.Color.B));
        }

        return brush;
    }

    public static Pen Pen(IBrush brush, double thickness = 1, bool dashed = false) => new(
        brush,
        thickness,
        dashed ? new DashStyle(new[] { 4.0, 3.0 }, 0) : null,
        PenLineCap.Round,
        PenLineJoin.Round);

    /// <summary>Text that reads on a filled shape: whichever token has the higher contrast.</summary>
    public static IBrush OnFill(IBrush fill)
    {
        if (fill is not ISolidColorBrush solid)
        {
            return Foreground;
        }

        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                var channel = value / 255.0;
                return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }

        var fillLuminance = Luminance(solid.Color);
        var foreground = Foreground is ISolidColorBrush f ? Luminance(f.Color) : 0;
        var surface = Surface is ISolidColorBrush s ? Luminance(s.Color) : 1;
        var onForeground = (Math.Max(foreground, fillLuminance) + 0.05) / (Math.Min(foreground, fillLuminance) + 0.05);
        var onSurface = (Math.Max(surface, fillLuminance) + 0.05) / (Math.Min(surface, fillLuminance) + 0.05);
        return onSurface > onForeground ? Surface : Foreground;
    }
}

/// <summary>Formatted text on the Inter body face, with tabular figures for numbers.</summary>
internal static class DataText
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

    public static FormattedText Create(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal,
        double maxWidth = double.PositiveInfinity)
    {
        var formatted = new FormattedText(
            text,
            Culture,
            FlowDirection.LeftToRight,
            new Typeface(Font, FontStyle.Normal, weight),
            size,
            brush);
        formatted.SetFontFeatures(Tabular);
        if (!double.IsInfinity(maxWidth) && maxWidth > 0)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }

        return formatted;
    }

    public static void Draw(DrawingContext context, FormattedText text, double x, double centerY,
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

    public static void Draw(DrawingContext context, string text, double x, double centerY, double size, IBrush brush,
        FontWeight weight = FontWeight.Normal, TextAlignment alignment = TextAlignment.Left,
        double maxWidth = double.PositiveInfinity)
        => Draw(context, Create(text, size, brush, weight, maxWidth), x, centerY, alignment);

    public static double Width(string text, double size, FontWeight weight = FontWeight.Normal)
        => Create(text, size, Ink.Foreground, weight).Width;
}

/// <summary>Number and geometry helpers shared by the stages.</summary>
internal static class DataMath
{
    public static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    public static double Map(double value, double inMin, double inMax, double outMin, double outMax)
        => Math.Abs(inMax - inMin) < 1e-9
            ? (outMin + outMax) / 2
            : outMin + (value - inMin) / (inMax - inMin) * (outMax - outMin);

    public static double Ease(double t)
    {
        var clamped = Clamp01(t);
        return 1 - Math.Pow(1 - clamped, 3);
    }

    /// <summary>A pleasant maximum above the data, so axes end on a round number.</summary>
    public static double NiceMax(double value)
    {
        if (value <= 0)
        {
            return 1;
        }

        value *= 1.06;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }
}

/// <summary>
/// Base for the custom drawn data stages: a deterministic measure plus crisp pixel and typography helpers.
/// Stages are focusable so every one of them supports arrow key inspection, and they paint a static frame
/// (the shot harness runs with reduced motion on).
/// </summary>
internal abstract class DataStage : Control
{
    protected DataStage()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    protected virtual double DefaultHeight => 220;

    protected double StageWidth => Bounds.Width;

    protected double StageHeight => Bounds.Height;

    protected static double Crisp(double value) => Math.Round(value) + 0.5;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var height = double.IsNaN(Height) ? DefaultHeight : Height;
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    protected static void Text(DrawingContext context, string text, double x, double centerY, double size,
        IBrush brush, FontWeight weight = FontWeight.Normal, TextAlignment alignment = TextAlignment.Left,
        double maxWidth = double.PositiveInfinity)
        => DataText.Draw(context, text, x, centerY, size, brush, weight, alignment, maxWidth);

    protected static void DashedLine(DrawingContext context, IBrush brush, Point from, Point to, double thickness = 1)
        => context.DrawLine(Ink.Pen(brush, thickness, true), from, to);

    /// <summary>A small floating readout that stays inside the stage bounds.</summary>
    protected static void Tooltip(DrawingContext context, Size bounds, Point anchor, string title,
        IReadOnlyList<(string Label, string Value, IBrush? Brush)> rows)
    {
        const double padding = 8;
        const double rowHeight = 16;
        var titleText = DataText.Create(title, 12, Ink.Foreground, FontWeight.Medium);
        var width = titleText.Width;
        var rowTexts = new List<(FormattedText Label, FormattedText Value, IBrush? Brush)>();
        foreach (var row in rows)
        {
            var label = DataText.Create(row.Label, 12, Ink.Muted, maxWidth: 130);
            var value = DataText.Create(row.Value, 12, Ink.Foreground, FontWeight.Medium);
            rowTexts.Add((label, value, row.Brush));
            width = Math.Max(width, label.Width + value.Width + 14);
        }

        width = Math.Min(bounds.Width - 12, Math.Ceiling(width) + padding * 2);
        var height = 15 + rowHeight * rowTexts.Count + padding * 2 - 4;
        var x = anchor.X + 12;
        if (x + width > bounds.Width - 6)
        {
            x = anchor.X - width - 12;
        }

        x = Math.Clamp(x, 6, Math.Max(6, bounds.Width - width - 6));
        var y = Math.Clamp(anchor.Y - height / 2, 6, Math.Max(6, bounds.Height - height - 6));
        var rect = new Rect(x, y, width, height);
        context.DrawRectangle(Ink.SurfaceRaised, Ink.Pen(Ink.Border, 1), rect, 8, 8);
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
            context.DrawText(row.Value, new Point(rect.Right - padding - row.Value.Width, rowY - 2));
            rowY += rowHeight;
        }
    }

    protected static StreamGeometry LineGeometry(IReadOnlyList<Point> points, bool smooth = true)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(points[0], false);
        for (var i = 1; i < points.Count; i++)
        {
            context.LineTo(points[i]);
        }

        context.EndFigure(false);
        return geometry;
    }

    protected static StreamGeometry AreaGeometry(IReadOnlyList<Point> points, double baseline)
    {
        var geometry = new StreamGeometry();
        if (points.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(new Point(points[0].X, baseline), true);
        foreach (var point in points)
        {
            context.LineTo(point);
        }

        context.LineTo(new Point(points[^1].X, baseline));
        context.EndFigure(true);
        return geometry;
    }
}
