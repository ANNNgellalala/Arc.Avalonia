using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>Horizontal alignment for the text helper.</summary>
public enum Align
{
    Left,
    Center,
    Right
}

/// <summary>
/// Drawing primitives shared by the AI explainer illustrations. Everything resolves through the Arc token
/// set at draw time, so a theme or accent swap restyles the diagrams without any extra wiring. Shapes use
/// the card radius ladder (14 for inner chips, 18-20 for panels) and only the Inter body face.
/// </summary>
internal static class Ink
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly FontFeatureCollection Tabular = FontFeatureCollection.Parse("tnum");
    private static readonly ConcurrentDictionary<string, Geometry?> IconCache = new(StringComparer.Ordinal);

    public static IBrush B(string key) => ArcResources.Brush(key);

    public static Color C(string key) => ArcResources.Color(key);

    /// <summary>A transparent copy of a token brush, for washes and muted states.</summary>
    public static IBrush A(IBrush brush, double alpha)
    {
        if (brush is ISolidColorBrush solid)
        {
            var color = solid.Color;
            return new ImmutableSolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255),
                color.R,
                color.G,
                color.B));
        }

        return brush;
    }

    public static Pen Stroke(IBrush brush, double thickness = 1) =>
        new(brush, thickness, null, PenLineCap.Round, PenLineJoin.Round);

    /// <summary>
    /// An opaque tint: the token colour mixed into the page surface. Chips drawn with this never let a wire
    /// underneath show through, which matters where status pills sit on top of a connector.
    /// </summary>
    public static IBrush Wash(IBrush tint, double alpha, string baseKey = ArcTokens.Surface)
    {
        if (tint is ISolidColorBrush solid)
        {
            var baseColor = ArcResources.Color(baseKey);
            var top = solid.Color;
            return new ImmutableSolidColorBrush(Color.FromRgb(
                (byte)Math.Clamp(Math.Round(baseColor.R + (top.R - baseColor.R) * alpha), 0, 255),
                (byte)Math.Clamp(Math.Round(baseColor.G + (top.G - baseColor.G) * alpha), 0, 255),
                (byte)Math.Clamp(Math.Round(baseColor.B + (top.B - baseColor.B) * alpha), 0, 255)));
        }

        return tint;
    }

    public static Pen Dashed(IBrush brush, double thickness = 1, double dash = 3, double gap = 3, double offset = 0) =>
        new(brush, thickness, new DashStyle(new[] { dash, gap }, offset), PenLineCap.Round, PenLineJoin.Round);

    // ---- Text ----

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

    public static FormattedText Text(
        string text,
        double size,
        IBrush brush,
        FontWeight? weight = null,
        double maxWidth = double.PositiveInfinity,
        bool wrap = false)
    {
        var typeface = new Typeface(Font, FontStyle.Normal, weight ?? FontWeight.Normal);
        var formatted = new FormattedText(text, Culture, FlowDirection.LeftToRight, typeface, size, brush);
        formatted.SetFontFeatures(Tabular);
        if (!double.IsInfinity(maxWidth) && maxWidth > 2)
        {
            formatted.MaxTextWidth = maxWidth;
            if (!wrap)
            {
                formatted.MaxLineCount = 1;
                formatted.Trimming = TextTrimming.CharacterEllipsis;
            }
        }

        return formatted;
    }

    /// <summary>Draws one line of text positioned by its centre line, the same convention as ChartText.</summary>
    public static void Label(
        DrawingContext context,
        string text,
        double x,
        double centerY,
        double size,
        IBrush brush,
        FontWeight? weight = null,
        Align align = Align.Left,
        double maxWidth = double.PositiveInfinity)
    {
        var formatted = Text(text, size, brush, weight, maxWidth);
        DrawFormatted(context, formatted, x, centerY, align);
    }

    /// <summary>Convenience overload when only the alignment is needed.</summary>
    public static void Label(
        DrawingContext context,
        string text,
        double x,
        double centerY,
        double size,
        IBrush brush,
        Align align,
        double maxWidth = double.PositiveInfinity)
    {
        Label(context, text, x, centerY, size, brush, null, align, maxWidth);
    }

    /// <summary>Draws wrapped text whose top edge sits at <paramref name="top"/>.</summary>
    public static void Paragraph(
        DrawingContext context,
        string text,
        double x,
        double top,
        double size,
        IBrush brush,
        double maxWidth,
        FontWeight? weight = null)
    {
        var formatted = Text(text, size, brush, weight, maxWidth, wrap: true);
        context.DrawText(formatted, new Point(x, top));
    }

    public static void DrawFormatted(DrawingContext context, FormattedText text, double x, double centerY, Align align)
    {
        var dx = align switch
        {
            Align.Center => x - text.Width / 2,
            Align.Right => x - text.Width,
            _ => x
        };
        context.DrawText(text, new Point(dx, centerY - text.Height / 2));
    }

    public static double Width(string text, double size, FontWeight? weight = null) =>
        Text(text, size, B(ArcTokens.Foreground), weight).Width;

    // ---- Surfaces ----

    public static void Fill(DrawingContext context, Rect rect, double radius, IBrush brush) =>
        context.DrawRectangle(brush, null, rect, radius, radius);

    /// <summary>A bordered surface: the standard inner card used by every diagram.</summary>
    public static void Card(
        DrawingContext context,
        Rect rect,
        double radius = 14,
        IBrush? fill = null,
        IBrush? border = null,
        double thickness = 1) =>
        context.DrawRectangle(
            fill ?? B(ArcTokens.Surface),
            border is null ? null : Stroke(border, thickness),
            rect,
            radius,
            radius);

    public static void DashedRect(DrawingContext context, Rect rect, double radius, IBrush border, double dash = 6, double gap = 5)
        => context.DrawRectangle(null, Dashed(border, 1, dash, gap), rect, radius, radius);

    public static void StatusDot(DrawingContext context, double x, double y, IBrush brush, double radius = 3)
        => context.DrawEllipse(brush, null, new Point(x, y), radius, radius);

    /// <summary>A pill that fills and returns its width; used for statuses, tags and counters.</summary>
    public static double Chip(
        DrawingContext context,
        double x,
        double y,
        string text,
        IBrush fill,
        IBrush ink,
        double height = 22,
        double size = 11.5,
        IBrush? border = null,
        FontWeight? weight = null,
        bool dot = false)
    {
        var textWidth = Width(text, size, weight);
        var width = textWidth + (dot ? 30 : 18);
        var rect = new Rect(x, y, width, height);
        context.DrawRectangle(fill, border is null ? null : Stroke(border), rect, height / 2, height / 2);
        var textX = x + (dot ? 20 : 9);
        if (dot)
        {
            StatusDot(context, x + 12, y + height / 2, ink, 2.5);
        }

        Label(context, text, textX, y + height / 2, size, ink, weight ?? FontWeight.Medium);
        return width;
    }

    public static double ChipWidth(string text, double size = 11.5, FontWeight? weight = null, bool dot = false) =>
        Width(text, size, weight) + (dot ? 30 : 18);

    // ---- Icons ----

    /// <summary>Draws a Lucide path from the shared icon table, centred on a point.</summary>
    public static void Icon(
        DrawingContext context,
        string name,
        double centerX,
        double centerY,
        double size,
        IBrush brush,
        double stroke = 1.6)
    {
        var geometry = IconCache.GetOrAdd(name, static key =>
        {
            if (!ArcIconData.Paths.TryGetValue(key, out var data) || string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            try
            {
                return Geometry.Parse(data);
            }
            catch
            {
                return null;
            }
        });

        if (geometry is null)
        {
            return;
        }

        var scale = size / 24.0;
        using (context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(centerX - size / 2, centerY - size / 2)))
        {
            context.DrawGeometry(null, new Pen(brush, stroke, null, PenLineCap.Round, PenLineJoin.Round), geometry);
        }
    }

    // ---- Geometry ----

    public static StreamGeometry Poly(params Point[] points)
    {
        var geometry = new StreamGeometry();
        if (points.Length < 2)
        {
            return geometry;
        }

        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
        {
            ctx.LineTo(points[i]);
        }

        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>An S curve between two points, entering and leaving horizontally.</summary>
    public static StreamGeometry Flow(Point a, Point b, double bow = 0, double tension = 0.45)
    {
        var dx = b.X - a.X;
        var c1 = new Point(a.X + dx * tension, a.Y + bow);
        var c2 = new Point(b.X - dx * tension, b.Y - bow);
        return Cubic(a, c1, c2, b);
    }

    public static StreamGeometry Cubic(Point a, Point c1, Point c2, Point b)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(a, false);
        ctx.CubicBezierTo(c1, c2, b);
        ctx.EndFigure(false);
        return geometry;
    }

    public static Point CubicAt(Point a, Point c1, Point c2, Point b, double t)
    {
        var u = 1 - t;
        var uu = u * u;
        var tt = t * t;
        return new Point(
            uu * u * a.X + 3 * uu * t * c1.X + 3 * u * tt * c2.X + tt * t * b.X,
            uu * u * a.Y + 3 * uu * t * c1.Y + 3 * u * tt * c2.Y + tt * t * b.Y);
    }

    /// <summary>Point along the standard S flow, so packets and their wires always agree.</summary>
    public static Point FlowAt(Point a, Point b, double t, double bow = 0, double tension = 0.45)
    {
        var dx = b.X - a.X;
        return CubicAt(a, new Point(a.X + dx * tension, a.Y + bow), new Point(b.X - dx * tension, b.Y - bow), b, t);
    }

    public static Point OnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    /// <summary>A stroked arc from a start angle, clockwise, for dials and rings.</summary>
    public static StreamGeometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        var geometry = new StreamGeometry();
        if (radius <= 0 || Math.Abs(sweepDegrees) < 0.1)
        {
            return geometry;
        }

        using var ctx = geometry.Open();
        ctx.BeginFigure(OnCircle(center, radius, startDegrees), false);
        ctx.ArcTo(
            OnCircle(center, radius, startDegrees + sweepDegrees),
            new Size(radius, radius),
            0,
            Math.Abs(sweepDegrees) > 180,
            sweepDegrees >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        ctx.EndFigure(false);
        return geometry;
    }

    public static void ArrowHead(DrawingContext context, Point tip, Point from, IBrush brush, double size = 5.5)
    {
        var angle = Math.Atan2(tip.Y - from.Y, tip.X - from.X);
        var left = new Point(tip.X - size * Math.Cos(angle - 0.5), tip.Y - size * Math.Sin(angle - 0.5));
        var right = new Point(tip.X - size * Math.Cos(angle + 0.5), tip.Y - size * Math.Sin(angle + 0.5));
        context.DrawGeometry(brush, null, Poly(tip, left, right));
    }

    /// <summary>A card with an icon tile, a label and an optional meta line; returns the icon tile centre.</summary>
    public static Point Node(
        DrawingContext context,
        Rect rect,
        string icon,
        string label,
        string? meta,
        IBrush accent,
        bool active = false,
        bool muted = false,
        double radius = 14)
    {
        var surface = muted ? A(B(ArcTokens.SurfaceRaised), 0.55) : B(ArcTokens.SurfaceRaised);
        var border = active ? accent : B(ArcTokens.Border);
        context.DrawRectangle(surface, Stroke(border, active ? 1.4 : 1), rect, radius, radius);

        var tile = new Rect(rect.X + 10, rect.Y + rect.Height / 2 - 13, 26, 26);
        context.DrawRectangle(A(accent, active ? 0.18 : 0.10), null, tile, 8, 8);
        Icon(context, icon, tile.Center.X, tile.Center.Y, 15, active ? accent : A(accent, 0.95));

        var textX = tile.Right + 10;
        var textWidth = rect.Right - textX - 8;
        if (string.IsNullOrEmpty(meta))
        {
            Label(context, label, textX, rect.Center.Y, 13, active ? B(ArcTokens.Foreground) : B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: textWidth);
        }
        else
        {
            Label(context, label, textX, rect.Center.Y - 8, 13, B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: textWidth);
            Label(context, meta, textX, rect.Center.Y + 9, 11, B(ArcTokens.TextMuted), maxWidth: textWidth);
        }

        return tile.Center;
    }

    // ---- Easing and timing ----

    public static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    public static double Smooth(double t)
    {
        t = Clamp01(t);
        return t * t * (3 - 2 * t);
    }

    public static double EaseOut(double t)
    {
        t = Clamp01(t);
        return 1 - Math.Pow(1 - t, 3);
    }

    public static double EaseIn(double t)
    {
        t = Clamp01(t);
        return t * t * t;
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Progress of a sub-span in a normalised 0..1 timeline.</summary>
    public static double Span(double t, double start, double end)
    {
        if (end <= start)
        {
            return t >= end ? 1 : 0;
        }

        return Clamp01((t - start) / (end - start));
    }

    public static double Wave(double t, double cycles = 1) => 0.5 - 0.5 * Math.Cos(t * cycles * Math.PI * 2);

    // ---- Numbers ----

    public static string Money(double value, string currency = "USD")
    {
        var symbol = currency switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            _ => currency + " "
        };

        return symbol + value.ToString("N2", Culture);
    }

    public static string Compact(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000)
        {
            return (value / 1_000_000).ToString("0.#", Culture) + "M";
        }

        if (abs >= 1_000)
        {
            return (value / 1_000).ToString("0.#", Culture) + "K";
        }

        return value.ToString("N0", Culture);
    }

    public static string Number(double value, int decimals = 0) => value.ToString("N" + Math.Max(0, decimals), Culture);

    public static string Percent(double value, int decimals = 0) =>
        (value * 100).ToString("0." + new string('#', Math.Max(0, decimals)), Culture) + "%";
}
