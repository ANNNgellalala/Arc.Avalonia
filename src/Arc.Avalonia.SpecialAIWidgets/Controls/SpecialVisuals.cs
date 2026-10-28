using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIWidgets.Controls;

/// <summary>
/// Small shared helpers for the AI widget illustrations: token-bound brushes, typography, pills and the
/// number formats the widgets report. Keeping the token lookup here means every generated child follows a
/// theme or accent swap without a rebuild.
/// </summary>
internal static class Sv
{
    public static readonly FontFeatureCollection Tabular = CreateTabular();

    public static readonly FontFamily MonoFamily = new("Consolas, Menlo, monospace");

    /// <summary>The body face from the token set, so drawn text matches the page's typography.</summary>
    public static FontFamily BodyFamily =>
        Application.Current?.FindResource(ArcTokens.FontBody) as FontFamily ?? FontFamily.Default;

    private static FontFeatureCollection CreateTabular()
    {
        var features = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } };
        return features;
    }

    /// <summary>Binds a property to a semantic token so it tracks theme and accent swaps.</summary>
    public static T Paint<T>(this T control, AvaloniaProperty property, string token) where T : AvaloniaObject
    {
        if (control is Control host)
        {
            host.Bind(property, host.GetResourceObservable(token));
        }

        return control;
    }

    public static IBrush Brush(string token) => ArcResources.Brush(token);

    public static Color Color(string token) => ArcResources.Color(token);

    /// <summary>A token colour with its alpha replaced; used for quiet fills and hairlines.</summary>
    public static Color Alpha(string token, double alpha) => ArcColor.WithAlpha(ArcResources.Color(token), alpha);

    /// <summary>A token colour with its alpha replaced, as a brush for drawing.</summary>
    public static IBrush AlphaBrush(string token, double alpha) => new ImmutableSolidColorBrush(Alpha(token, alpha));



    public static Color Mix(string fromToken, string toToken, double t) =>
        ArcColor.Mix(ArcResources.Color(fromToken), ArcResources.Color(toToken), t);

    public static double Size(string token) => ArcResources.Double(token);

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static TextBlock Text(string text, params string[] classes)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        foreach (var name in classes)
        {
            block.Classes.Add(name);
        }

        return block;
    }

    public static TextBlock Label(string text) => Text(text, "arc-label");

    public static TextBlock Caption(string text) => Text(text, "arc-caption");



    public static TextBlock Numeric(string text, params string[] classes)
    {
        var block = Text(text, classes);
        block.FontFeatures = Tabular;
        return block;
    }

    public static TextBlock MonoText(string text, double fontSize = 12)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = MonoFamily,
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        block.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        return block;
    }

    public static ArcIcon Icon(string name, double size, string? token = null, double strokeWidth = 1.75)
    {
        var icon = new ArcIcon { Icon = name, Size = size, StrokeWidth = strokeWidth };
        if (token is not null)
        {
            icon.Paint(ArcIcon.ForegroundProperty, token);
        }

        return icon;
    }

    /// <summary>A quiet pill used for counts, badges and metadata.</summary>
    public static Border Pill(Control content, string backgroundToken, string foregroundToken, double horizontal = 8, double vertical = 2)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(horizontal, vertical),
            Child = content,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Paint(Border.BackgroundProperty, backgroundToken);

        switch (content)
        {
            case TextBlock text:
                text.Paint(TextBlock.ForegroundProperty, foregroundToken);
                break;
            case ArcIcon icon:
                icon.Paint(ArcIcon.ForegroundProperty, foregroundToken);
                break;
        }

        return border;
    }

    public static Border Divider(string token, double thickness = 1) =>
        new Border { Height = thickness, VerticalAlignment = VerticalAlignment.Center }
            .Paint(Border.BackgroundProperty, token);

    /// <summary>
    /// Removes a control from its current logical parent, so a widget that rebuilds its tree can keep one drawn
    /// child (and its animation state) across rebuilds without tripping Avalonia's single parent rule.
    /// </summary>
    public static void Detach(Control? control)
    {
        switch (control?.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, control):
                content.Content = null;
                break;
        }
    }

    public static Border Card(Control child, double padding = 16, string background = ArcTokens.Surface)
    {
        var border = new Border
        {
            Padding = new Thickness(padding),
            Child = child,
            CornerRadius = Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1)
        };
        border.Paint(Border.BackgroundProperty, background);
        border.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        return border;
    }

    public static StackPanel Row(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel Column(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>Elapsed time in the short form the widgets show: 640ms, 1.2s, 1m 12s.</summary>
    public static string Duration(double seconds)
    {
        if (seconds < 1)
        {
            return $"{Math.Max(1, Math.Round(seconds * 1000))}ms";
        }

        if (seconds < 60)
        {
            return seconds < 10 ? $"{seconds:0.0}s" : $"{Math.Round(seconds)}s";
        }

        var minutes = (int)(seconds / 60);
        var rest = (int)Math.Round(seconds - minutes * 60);
        return rest == 0 ? $"{minutes}m" : $"{minutes}m {rest}s";
    }

    public static string Tokens(double tokens)
    {
        if (tokens < 1000)
        {
            return Math.Round(tokens).ToString(CultureInfo.InvariantCulture);
        }

        if (tokens >= 1_000_000)
        {
            var millions = tokens / 1_000_000.0;
            return millions < 10 ? $"{millions:0.#}M" : $"{Math.Round(millions)}M";
        }

        var thousands = tokens / 1000.0;
        return thousands < 100 ? $"{thousands:0.#}k" : $"{Math.Round(thousands)}k";
    }

    public static string Money(double dollars) =>
        dollars < 0.1 ? $"${dollars:0.00}" : $"${dollars:0.00}";

    public static double Clamp01(double value) => value <= 0 ? 0 : value >= 1 ? 1 : value;

    public static double Lerp(double from, double to, double t) => from + (to - from) * t;


}

/// <summary>
/// A property-style tween used by the widgets for a single value change: it never overshoots, lands in the
/// given time and jumps straight to the target under reduced motion.
/// </summary>
internal sealed class SvTween
{
    private DispatcherTimer? _timer;

    public bool IsRunning => _timer is not null;

    public void Run(double from, double to, double durationMs, Action<double> apply, Action? done = null)
    {
        Stop();
        if (ArcThemeManager.ReducedMotion || durationMs <= 1 || Math.Abs(to - from) < 0.0001)
        {
            apply(to);
            done?.Invoke();
            return;
        }

        var started = DateTime.UtcNow;
        apply(from);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var t = Sv.Clamp01((DateTime.UtcNow - started).TotalMilliseconds / durationMs);
            apply(Sv.Lerp(from, to, ArcMotion.EaseStandard.Ease(t)));
            if (t >= 1)
            {
                Stop();
                apply(to);
                done?.Invoke();
            }
        });
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}

/// <summary>A frame clock for the continuously animated widgets; it does not run under reduced motion.</summary>
internal sealed class SvClock
{
    private DispatcherTimer? _timer;

    public bool Running => _timer is not null;

    public void Start(Action<double> tick, double intervalMs = 16)
    {
        Stop();
        if (ArcThemeManager.ReducedMotion)
        {
            tick(0);
            return;
        }

        var started = DateTime.UtcNow;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(intervalMs), DispatcherPriority.Render, (_, _) =>
            tick((DateTime.UtcNow - started).TotalSeconds));
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}

/// <summary>Drawing primitives the widgets share: rounded blocks, arcs, tick rings and plain text runs.</summary>
internal static class SvDraw
{
    public static void Fill(DrawingContext context, Rect rect, double radius, IBrush brush) =>
        context.DrawRectangle(brush, null, rect, radius, radius);

    public static void Outline(DrawingContext context, Rect rect, double radius, IBrush brush, double thickness) =>
        context.DrawRectangle(null, new Pen(brush, thickness), rect.Deflate(thickness / 2), radius, radius);

    public static void Ellipse(DrawingContext context, Point center, double radiusX, double radiusY, IBrush brush) =>
        context.DrawEllipse(brush, null, center, radiusX, radiusY);

    public static void Arc(DrawingContext context, Point center, double radius, double startDegrees, double sweepDegrees, IPen pen)
    {
        if (radius <= 0 || Math.Abs(sweepDegrees) < 0.05)
        {
            return;
        }

        if (Math.Abs(sweepDegrees) >= 359.5)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = startDegrees * Math.PI / 180.0;
        var end = (startDegrees + sweepDegrees) * Math.PI / 180.0;
        var from = new Point(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start));
        var to = new Point(center.X + radius * Math.Cos(end), center.Y + radius * Math.Sin(end));

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(from, false);
            figure.ArcTo(to, new Size(radius, radius), 0, Math.Abs(sweepDegrees) > 180, sweepDegrees > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Short radial dashes around a circle, the tick language the activity rings use.</summary>
    public static void Ticks(
        DrawingContext context,
        Point center,
        double innerRadius,
        double outerRadius,
        int count,
        IBrush brush,
        double thickness,
        double fromDegrees = 0,
        double toDegrees = 360)
    {
        if (count <= 0 || outerRadius <= innerRadius)
        {
            return;
        }

        var pen = new Pen(brush, thickness, null, PenLineCap.Round);
        var span = toDegrees - fromDegrees;
        var step = span / count;
        for (var i = 0; i < count; i++)
        {
            var angle = (fromDegrees + step * i) * Math.PI / 180.0;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var a = new Point(center.X + innerRadius * cos, center.Y + innerRadius * sin);
            var b = new Point(center.X + outerRadius * cos, center.Y + outerRadius * sin);
            context.DrawLine(pen, a, b);
        }
    }

    public static FormattedText Text(string text, double fontSize, IBrush brush, FontWeight weight = FontWeight.Normal, FontFamily? family = null) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(family ?? Sv.BodyFamily, FontStyle.Normal, weight),
            fontSize,
            brush);

    public static void DrawText(DrawingContext context, FormattedText text, double x, double top) =>
        context.DrawText(text, new Point(x, top));

    public static void DrawTextRight(DrawingContext context, FormattedText text, double right, double top) =>
        context.DrawText(text, new Point(right - text.Width, top));

    public static void DrawTextCentered(DrawingContext context, FormattedText text, double centerX, double top) =>
        context.DrawText(text, new Point(centerX - text.Width / 2, top));

    /// <summary>A filled four point star, the generic agent and model mark.</summary>
    public static void Mark(DrawingContext context, Point center, double radius, IBrush brush, double thickness)
    {
        if (radius <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            for (var i = 0; i < 8; i++)
            {
                var angle = (i * 45 - 90) * Math.PI / 180.0;
                var r = i % 2 == 0 ? radius : radius * 0.32;
                var point = new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle));
                if (i == 0)
                {
                    figure.BeginFigure(point, true);
                }
                else
                {
                    figure.LineTo(point);
                }
            }

            figure.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    public static void Check(DrawingContext context, Point center, double size, IBrush brush, double thickness)
    {
        var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Round);
        var a = new Point(center.X - size * 0.5, center.Y);
        var b = new Point(center.X - size * 0.1, center.Y + size * 0.38);
        var c = new Point(center.X + size * 0.55, center.Y - size * 0.4);
        context.DrawLine(pen, a, b);
        context.DrawLine(pen, b, c);
    }

    public static void Cross(DrawingContext context, Point center, double size, IBrush brush, double thickness)
    {
        var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Round);
        var half = size * 0.42;
        context.DrawLine(pen, new Point(center.X - half, center.Y - half), new Point(center.X + half, center.Y + half));
        context.DrawLine(pen, new Point(center.X + half, center.Y - half), new Point(center.X - half, center.Y + half));
    }
}
