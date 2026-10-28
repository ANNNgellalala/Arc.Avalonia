using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// Shared plumbing for the Special (media and input) components: token lookups that follow theme and
/// accent swaps, text and shape helpers, a critically damped spring, and fold geometry used by the page
/// curl and sticker components. Everything here is deliberately dependency free: these components draw
/// themselves with <see cref="DrawingContext"/> instead of shipping XAML templates.
/// </summary>
internal static class SpecialUi
{
    public static IBrush Brush(string token) => ArcResources.Brush(token);

    public static Color Color(string token) => ArcResources.Color(token);

    public static double Space(string token) => ArcResources.Double(token);

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static TimeSpan Duration(string token) => ArcResources.Duration(token);

    public static bool Reduced => ArcThemeManager.ReducedMotion;

    public static bool Dark => ArcThemeManager.Scheme == ArcColorScheme.Dark;

    public static FontFamily Font(string token) =>
        Application.Current?.FindResource(token) as FontFamily ?? FontFamily.Default;

    public static IBrush Solid(Color color) => new SolidColorBrush(color);

    public static IBrush Alpha(string token, double alpha) => Solid(ArcColor.WithAlpha(Color(token), alpha));

    public static IBrush Alpha(Color color, double alpha) => Solid(ArcColor.WithAlpha(color, alpha));

    /// <summary>Elevation drawn as translucent rounded layers, since DrawingContext has no box shadow.</summary>
    public static IBrush ShadowBrush(double alpha) =>
        Solid(ArcColor.WithAlpha(Color(ArcTokens.Scrim), alpha));

    public static FormattedText Text(
        string text,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal,
        double maxWidth = double.PositiveInfinity,
        double maxLines = 1,
        TextAlignment align = TextAlignment.Left)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface(Font(ArcTokens.FontBody), FontStyle.Normal, weight),
            size,
            brush)
        {
            TextAlignment = align
        };

        if (!double.IsInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(8, maxWidth);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            formatted.MaxLineCount = Math.Max(1, (int)maxLines);
        }

        return formatted;
    }

    public static void DrawTextCentered(DrawingContext context, FormattedText text, Rect rect)
    {
        context.DrawText(text, new Point(
            rect.X + (rect.Width - text.Width) / 2,
            rect.Y + (rect.Height - text.Height) / 2));
    }

    public static void DrawTextRight(DrawingContext context, FormattedText text, Rect rect)
    {
        context.DrawText(text, new Point(
            rect.Right - text.Width,
            rect.Y + (rect.Height - text.Height) / 2));
    }

    public static void RoundRect(DrawingContext context, Rect rect, double radius, IBrush? fill, IPen? pen = null)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(fill, pen, rect, radius, radius);
    }

    /// <summary>Soft, downward shadow under a floating surface. Three translucent layers approximate a blur.</summary>
    public static void Lift(DrawingContext context, Rect rect, double radius, double strength = 1)
    {
        var ink = Color(ArcTokens.Scrim);
        for (var layer = 3; layer >= 1; layer--)
        {
            var grow = layer * 3.0;
            var alpha = 0.035 * strength * (4 - layer);
            var shadow = new Rect(rect.X - grow, rect.Y - grow + 8, rect.Width + grow * 2, rect.Height + grow * 2);
            context.DrawRectangle(Solid(ArcColor.WithAlpha(ink, alpha)), null, shadow, radius + grow, radius + grow);
        }
    }

    public static LinearGradientBrush Gradient(Color from, Color to, bool vertical = true) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(from, 0),
            new GradientStop(to, 1)
        }
    };

    public static LinearGradientBrush Fade(Color color, bool vertical = true) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(ArcColor.WithAlpha(color, 0), 0),
            new GradientStop(color, 1)
        }
    };

    /// <summary>Focus ring: 2 px ArcFocusOutline offset 2 px outside the focused region.</summary>
    public static void FocusRing(DrawingContext context, Rect region, double radius)
    {
        var ring = new Rect(region.X - 3, region.Y - 3, region.Width + 6, region.Height + 6);
        context.DrawRectangle(null, new Pen(Brush(ArcTokens.FocusOutline), 2), ring, radius + 3, radius + 3);
    }

    public static void FocusRingInset(DrawingContext context, Rect region, double radius)
    {
        var ring = new Rect(region.X + 1, region.Y + 1, Math.Max(1, region.Width - 2), Math.Max(1, region.Height - 2));
        context.DrawRectangle(null, new Pen(Brush(ArcTokens.FocusOutline), 2), ring, radius, radius);
    }

    public static void Name(Control control, string name, string? help = null)
    {
        AutomationProperties.SetName(control, name);
        if (!string.IsNullOrWhiteSpace(help))
        {
            AutomationProperties.SetHelpText(control, help);
        }
    }

    /// <summary>Lightness shift in OKLCH, used to build tints of a single token colour.</summary>
    public static Color Lighten(Color color, double delta)
    {
        var (lightness, chroma, hue) = ArcColor.ToOklch(color);
        return ArcColor.Oklch(Math.Clamp(lightness + delta, 0.02, 0.99), chroma, hue, color.A / 255.0);
    }

    public static Color Mix(Color a, Color b, double t) => ArcColor.Mix(a, b, t);

    public static Color WithAlpha(Color color, double alpha) => ArcColor.WithAlpha(color, alpha);

    /// <summary>Polygon from points. Used for the fold flaps of page curl and sticker peel.</summary>
    public static StreamGeometry Polygon(IReadOnlyList<Point> points, bool close = true)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(points[0], close);
        for (var index = 1; index < points.Count; index++)
        {
            context.LineTo(points[index]);
        }

        if (close)
        {
            context.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>A rect as a polygon with rounded corners, in clockwise order.</summary>
    public static List<Point> RoundedPolygon(Rect rect, double radius, int steps = 4)
    {
        var points = new List<Point>();
        radius = Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2);
        if (radius < 0.5)
        {
            points.Add(rect.TopLeft);
            points.Add(rect.TopRight);
            points.Add(rect.BottomRight);
            points.Add(rect.BottomLeft);
            return points;
        }

        AddArc(points, new Point(rect.Right - radius, rect.Top + radius), radius, -90, 0, steps);
        AddArc(points, new Point(rect.Right - radius, rect.Bottom - radius), radius, 0, 90, steps);
        AddArc(points, new Point(rect.Left + radius, rect.Bottom - radius), radius, 90, 180, steps);
        AddArc(points, new Point(rect.Left + radius, rect.Top + radius), radius, 180, 270, steps);
        return points;
    }

    public static List<Point> EllipsePolygon(Point center, double rx, double ry, int steps = 40)
    {
        var points = new List<Point>(steps);
        for (var index = 0; index < steps; index++)
        {
            var angle = index * Math.PI * 2 / steps;
            points.Add(new Point(center.X + Math.Cos(angle) * rx, center.Y + Math.Sin(angle) * ry));
        }

        return points;
    }

    private static void AddArc(List<Point> points, Point center, double radius, double from, double to, int steps)
    {
        for (var index = 0; index <= steps; index++)
        {
            var angle = (from + (to - from) * index / steps) * Math.PI / 180;
            points.Add(new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }
    }

    /// <summary>Keeps the part of a polygon on the negative side of the plane dot(p, normal) = offset.</summary>
    public static List<Point> ClipHalfPlane(IReadOnlyList<Point> polygon, Point normal, double offset)
    {
        var result = new List<Point>(polygon.Count + 2);
        if (polygon.Count == 0)
        {
            return result;
        }

        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Count];
            var currentSide = current.X * normal.X + current.Y * normal.Y - offset;
            var nextSide = next.X * normal.X + next.Y * normal.Y - offset;

            if (currentSide <= 0)
            {
                result.Add(current);
            }

            if ((currentSide < 0 && nextSide > 0) || (currentSide > 0 && nextSide < 0))
            {
                var t = currentSide / (currentSide - nextSide);
                result.Add(new Point(
                    current.X + (next.X - current.X) * t,
                    current.Y + (next.Y - current.Y) * t));
            }
        }

        return result;
    }

    public static Point Reflect(Point point, Point normal, double offset)
    {
        var distance = point.X * normal.X + point.Y * normal.Y - offset;
        return new Point(point.X - 2 * distance * normal.X, point.Y - 2 * distance * normal.Y);
    }

    public static Point Normalize(Point point)
    {
        var length = Math.Sqrt(point.X * point.X + point.Y * point.Y);
        return length < 0.0001 ? new Point(1, 0) : new Point(point.X / length, point.Y / length);
    }

    public static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y;

    public static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static Rect Lerp(Rect a, Rect b, double t) => new(
        Lerp(a.X, b.X, t),
        Lerp(a.Y, b.Y, t),
        Lerp(a.Width, b.Width, t),
        Lerp(a.Height, b.Height, t));

    /// <summary>Deterministic hash in [0, 1) for seeded sample imagery and simulated audio.</summary>
    public static double Noise(int seed)
    {
        var value = unchecked(seed * 374761393 + 668265263);
        value = unchecked((value ^ (value >> 13)) * 1274126177);
        value = unchecked(value ^ (value >> 16));
        return (value & 0x7fffffff) / (double)0x7fffffff;
    }

    public static string Time(double seconds)
    {
        var total = Math.Max(0, (int)Math.Round(seconds));
        return $"{total / 60}:{total % 60:00}";
    }
}

/// <summary>
/// A critically damped spring on one value. Components keep a handful of these and integrate them in
/// <see cref="ArcSpecialCanvas.OnFrame"/>; under reduced motion every spring jumps to its target.
/// </summary>
internal struct Spring
{
    public double Value;
    public double Velocity;
    public double Stiffness;
    public double Damping;

    public static Spring At(double value, double stiffness = 180, double damping = 26) => new()
    {
        Value = value,
        Velocity = 0,
        Stiffness = stiffness,
        Damping = damping
    };

    public void Jump(double target)
    {
        Value = target;
        Velocity = 0;
    }

    /// <summary>Integrates one frame. Returns true while the spring is still moving.</summary>
    public bool Step(double target, double dt)
    {
        if (SpecialUi.Reduced)
        {
            Value = target;
            Velocity = 0;
            return false;
        }

        if (Math.Abs(Value - target) < 0.001 && Math.Abs(Velocity) < 0.01)
        {
            Value = target;
            Velocity = 0;
            return false;
        }

        var acceleration = -Stiffness * (Value - target) - Damping * Velocity;
        Velocity += acceleration * dt;
        Value += Velocity * dt;
        return true;
    }
}

/// <summary>
/// Base for the Special media components. One control draws a whole interactive surface, so it owns a
/// single focus stop, an internal focus model, pointer capture and one frame clock. The clock only runs
/// while something is moving, and never under reduced motion, which is what makes the screenshot harness
/// deterministic: every component renders a curated, settled frame.
/// </summary>
public abstract class ArcSpecialCanvas : Control
{
    private DispatcherTimer? _timer;
    private readonly Stopwatch _clock = new();
    private double _lastFrame;
    private bool _focusVisible;

    protected ArcSpecialCanvas()
    {
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerEntered += (_, e) =>
        {
            Hovering = true;
            Pointer = e.GetPosition(this);
            OnPointerMoved(e);
            OnHoverChanged();
            InvalidateVisual();
        };
        PointerExited += (_, _) =>
        {
            Hovering = false;
            OnHoverChanged();
            InvalidateVisual();
        };
        GotFocus += (_, e) =>
        {
            _focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
            InvalidateVisual();
        };
        LostFocus += (_, _) =>
        {
            _focusVisible = false;
            InvalidateVisual();
        };
    }

    /// <summary>Desired width when the page does not set one.</summary>
    protected virtual double DefaultWidth => 900;

    /// <summary>Desired height when the page does not set one.</summary>
    protected virtual double DefaultHeight => 520;

    /// <summary>True when the keyboard focus ring should be drawn.</summary>
    protected bool ShowFocusRing => _focusVisible && IsFocused;

    /// <summary>Current pointer position in control coordinates, valid while <see cref="Hovering"/>.</summary>
    protected Point Pointer { get; private set; }

    /// <summary>True while the pointer is over the control.</summary>
    protected bool Hovering { get; private set; }

    /// <summary>True while a frame clock is running.</summary>
    protected bool Ticking => _timer is not null;

    protected override Size MeasureOverride(Size availableSize) => new(DefaultWidth, DefaultHeight);

    /// <summary>Starts the frame clock; a no-op under reduced motion, where the still frame is the design.</summary>
    protected void EnsureFrame()
    {
        if (SpecialUi.Reduced || _timer is not null)
        {
            return;
        }

        _clock.Restart();
        _lastFrame = 0;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Frame());
        _timer.Start();
    }

    protected void StopFrame()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastFrame, 0, 0.05);
        _lastFrame = now;
        OnFrame(dt);
        InvalidateVisual();
        if (!NeedsFrame)
        {
            StopFrame();
        }
    }

    /// <summary>Advances the component by one frame; only called while the clock runs.</summary>
    protected virtual void OnFrame(double dt)
    {
    }

    /// <summary>Whether anything is still moving; false stops the clock.</summary>
    protected virtual bool NeedsFrame => false;

    protected virtual void OnHoverChanged()
    {
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopFrame();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Pointer = e.GetPosition(this);
    }

    public sealed override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 1 || size.Height <= 1)
        {
            return;
        }

        OnRender(context, size);
    }

    /// <summary>Draws the whole component at the current bounds.</summary>
    protected abstract void OnRender(DrawingContext context, Size size);
}
