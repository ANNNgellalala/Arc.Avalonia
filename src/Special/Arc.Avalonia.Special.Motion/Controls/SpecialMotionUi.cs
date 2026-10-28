using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>
/// Shared plumbing for the Special (motion and interaction) components. Everything is drawn with
/// <see cref="DrawingContext"/> and resolves colours through <see cref="ArcTokens"/>, so both themes and all
/// eight accents follow. Text is measured with the Inter face the rest of Arc uses, which keeps the drawn
/// surfaces consistent with the XAML controls beside them.
/// </summary>
internal static class Sm
{
    public static IBrush Brush(string token) => ArcResources.Brush(token);

    public static Color Color(string token) => ArcResources.Color(token);

    public static double Space(string token) => ArcResources.Double(token);

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static bool Reduced => ArcThemeManager.ReducedMotion;

    public static bool Dark => ArcThemeManager.Scheme == ArcColorScheme.Dark;

    public static readonly FontFamily Mono = new("Consolas, Menlo, monospace");

    public static FontFamily Font(string token) =>
        Application.Current?.FindResource(token) as FontFamily ?? FontFamily.Default;

    public static IBrush Solid(Color color) => new SolidColorBrush(color);

    public static IBrush Alpha(string token, double alpha) => Solid(ArcColor.WithAlpha(Color(token), alpha));

    public static IBrush AlphaOf(Color color, double alpha) => Solid(ArcColor.WithAlpha(color, alpha));

    public static Color Mix(string from, string to, double t) => ArcColor.Mix(Color(from), Color(to), t);

    public static Color Mix(Color from, Color to, double t) => ArcColor.Mix(from, to, t);

    public static Color WithAlpha(Color color, double alpha) => ArcColor.WithAlpha(color, alpha);

    /// <summary>Elevation drawn as translucent rounded layers, because DrawingContext has no box shadow.</summary>
    public static IBrush Shadow(double alpha) => Solid(ArcColor.WithAlpha(Color(ArcTokens.Scrim), alpha));

    /// <summary>Lifts a floating surface with three soft rings standing in for a blur.</summary>
    public static void Lift(DrawingContext context, Rect rect, double radius, double strength = 1)
    {
        var ink = Color(ArcTokens.Scrim);
        for (var layer = 3; layer >= 1; layer--)
        {
            var grow = layer * 3.0;
            var alpha = 0.030 * strength * (4 - layer);
            var shadow = new Rect(rect.X - grow, rect.Y - grow + 9, rect.Width + grow * 2, rect.Height + grow * 2);
            context.DrawRectangle(Solid(ArcColor.WithAlpha(ink, alpha)), null, shadow, radius + grow, radius + grow);
        }
    }

    public static FormattedText Text(
        string text,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal,
        double maxWidth = double.PositiveInfinity,
        int maxLines = 1,
        TextAlignment align = TextAlignment.Left,
        FontFamily? family = null)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface(family ?? Font(ArcTokens.FontBody), FontStyle.Normal, weight),
            size,
            brush)
        {
            TextAlignment = align
        };

        if (!double.IsInfinity(maxWidth))
        {
            formatted.MaxTextWidth = Math.Max(8, maxWidth);
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            formatted.MaxLineCount = Math.Max(1, maxLines);
        }

        return formatted;
    }

    public static FormattedText MonoText(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        Text(text, size, brush, weight, family: Mono);

    public static double MeasureWidth(string text, double size, FontWeight weight = FontWeight.Normal, FontFamily? family = null) =>
        string.IsNullOrEmpty(text) ? 0 : Text(text, size, Brushes.Transparent, weight, family: family).Width;

    public static void TextAt(DrawingContext context, FormattedText text, double x, double centerY) =>
        context.DrawText(text, new Point(x, centerY - text.Height / 2));

    public static void TextRight(DrawingContext context, FormattedText text, double right, double centerY) =>
        context.DrawText(text, new Point(right - text.Width, centerY - text.Height / 2));

    public static void TextCentered(DrawingContext context, FormattedText text, double centerX, double centerY) =>
        context.DrawText(text, new Point(centerX - text.Width / 2, centerY - text.Height / 2));

    public static void Round(DrawingContext context, Rect rect, double radius, IBrush? fill, IPen? pen = null)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(fill, pen, rect, radius, radius);
    }

    public static void Circle(DrawingContext context, Point center, double radius, IBrush? fill, IPen? pen = null) =>
        context.DrawEllipse(fill, pen, center, radius, radius);

    /// <summary>Focus ring: 2 px ArcFocusOutline offset 2 px outside the focused region.</summary>
    public static void FocusRing(DrawingContext context, Rect region, double radius)
    {
        var ring = new Rect(region.X - 3, region.Y - 3, region.Width + 6, region.Height + 6);
        context.DrawRectangle(null, new Pen(Brush(ArcTokens.FocusOutline), 2), ring, radius + 3, radius + 3);
    }

    public static void Name(Control control, string name, string? help = null)
    {
        AutomationProperties.SetName(control, name);
        if (!string.IsNullOrWhiteSpace(help))
        {
            AutomationProperties.SetHelpText(control, help!);
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

    public static LinearGradientBrush Gradient(string fromToken, string toToken, bool vertical = true) =>
        Gradient(Color(fromToken), Color(toToken), vertical);

    public static StreamGeometry Polyline(IReadOnlyList<Point> points, bool close = false, bool filled = false)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(points[0], filled);
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

    public static StreamGeometry ArcSegment(Point from, Point to, double radius, bool clockwise, bool largeArc)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(from, false);
        context.ArcTo(to, new Size(radius, radius), 0, largeArc, clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        context.EndFigure(false);
        return geometry;
    }

    public static double Clamp01(double value) => value <= 0 ? 0 : value >= 1 ? 1 : value;

    public static double Lerp(double from, double to, double t) => from + (to - from) * t;

    public static Point Lerp(Point from, Point to, double t) =>
        new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);

    public static double Ease(double t, Easing easing) => easing.Ease(Clamp01(t));

    /// <summary>Deterministic hash in [0, 1): the seeded cadence and sample art never change between runs.</summary>
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

    // ---- Icons -------------------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<string, Geometry?> IconCache = new(StringComparer.Ordinal);

    /// <summary>Draws a Lucide stroke icon centred in a box, keeping the 1.75 stroke weight at any size.</summary>
    public static void Icon(DrawingContext context, string name, Rect box, IBrush brush, double stroke = 1.75)
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

        var size = Math.Min(box.Width, box.Height);
        if (size <= 0)
        {
            return;
        }

        var scale = size / 24.0;
        var pen = new Pen(brush, stroke / scale, null, PenLineCap.Round, PenLineJoin.Round);
        using (context.PushTransform(
                   Matrix.CreateScale(scale, scale) *
                   Matrix.CreateTranslation(box.X + (box.Width - size) / 2, box.Y + (box.Height - size) / 2)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    public static void Icon(DrawingContext context, string name, Point center, double size, IBrush brush, double stroke = 1.75) =>
        Icon(context, name, new Rect(center.X - size / 2, center.Y - size / 2, size, size), brush, stroke);
}

/// <summary>A one shot value tween owned by an <see cref="ArcMotionCanvas"/>; reduced motion lands at once.</summary>
internal sealed class SmTween
{
    public double Elapsed;
    public double Duration;
    public Easing Easing = ArcMotion.EaseStandard;
    public Action<double> Apply = _ => { };
    public Action? Done;
    public bool Finished;
}

/// <summary>
/// Base for the Special motion components. One control draws a whole interactive surface, so it owns a
/// single focus stop, an internal hit model, pointer capture, one frame clock and frame driven tweens. The
/// clock only runs while something is moving and never under reduced motion, which is what makes the
/// screenshot harness deterministic: every component renders a curated, settled frame.
/// </summary>
public abstract class ArcMotionCanvas : Control
{
    private readonly List<SmTween> _tweens = new();
    private readonly List<HitRegion> _hits = new();
    private DispatcherTimer? _timer;
    private System.Diagnostics.Stopwatch? _clock;
    private double _lastFrame;
    private double _now;
    private bool _focusVisible;

    protected ArcMotionCanvas()
    {
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerEntered += (_, e) =>
        {
            Hovering = true;
            Pointer = e.GetPosition(this);
            UpdateHover();
            InvalidateVisual();
        };
        PointerExited += (_, _) =>
        {
            Hovering = false;
            HoverId = -1;
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

    /// <summary>Desired width when the page does not constrain the control.</summary>
    protected virtual double DefaultWidth => 960;

    /// <summary>Desired height when the page does not constrain the control.</summary>
    protected virtual double DefaultHeight => 400;

    /// <summary>True while the keyboard focus ring should be painted.</summary>
    protected bool ShowFocusRing => _focusVisible && IsFocused;

    protected Point Pointer { get; private set; }

    protected bool Hovering { get; private set; }

    /// <summary>Id of the region under the pointer, or -1; ids come from <see cref="Hit"/>.</summary>
    protected int HoverId { get; private set; } = -1;

    /// <summary>Id of the region the pointer pressed on, or -1 while not pressed.</summary>
    protected int PressId { get; private set; } = -1;

    /// <summary>True while any tween is still running.</summary>
    protected bool HasTweens => _tweens.Exists(tween => !tween.Finished);

    /// <summary>Seconds since the control last started its clock; drives slow loops such as orbit turns.</summary>
    protected double Now => _now;

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? DefaultWidth : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? DefaultHeight : availableSize.Height);

    // ---- Hit model ---------------------------------------------------------------------------------

    /// <summary>Records a hit target for the frame being drawn; later regions win because they draw on top.</summary>
    protected void Hit(Rect rect, int id) => _hits.Add(new HitRegion(rect, id, false));

    protected void HitCircle(Point center, double radius, int id) =>
        _hits.Add(new HitRegion(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), id, true));

    protected int HitAt(Point point)
    {
        for (var index = _hits.Count - 1; index >= 0; index--)
        {
            var region = _hits[index];
            if (region.Circle)
            {
                var center = region.Rect.Center;
                var dx = point.X - center.X;
                var dy = point.Y - center.Y;
                if (dx * dx + dy * dy <= (region.Rect.Width / 2) * (region.Rect.Width / 2))
                {
                    return region.Id;
                }
            }
            else if (region.Rect.Contains(point))
            {
                return region.Id;
            }
        }

        return -1;
    }

    private void UpdateHover()
    {
        var next = HitAt(Pointer);
        if (next == HoverId)
        {
            return;
        }

        HoverId = next;
        OnHoverChanged(next);
    }

    // ---- Pointer -----------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Pointer = e.GetPosition(this);
        UpdateHover();
        if (PressId >= 0)
        {
            OnDrag(PressId, Pointer);
        }

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        Pointer = e.GetPosition(this);
        PressId = HitAt(Pointer);
        if (PressId >= 0)
        {
            e.Pointer.Capture(this);
            OnPress(PressId, Pointer);
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (PressId < 0)
        {
            return;
        }

        var id = PressId;
        PressId = -1;
        e.Pointer.Capture(null);
        OnRelease(id, e.GetPosition(this));
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (PressId >= 0)
        {
            PressId = -1;
            CancelPress();
            InvalidateVisual();
        }
    }

    protected virtual void OnPress(int id, Point point)
    {
    }

    protected virtual void OnDrag(int id, Point point)
    {
    }

    protected virtual void OnRelease(int id, Point point)
    {
    }

    protected virtual void CancelPress()
    {
    }

    protected virtual void OnHoverChanged(int id)
    {
    }

    // ---- Animation ---------------------------------------------------------------------------------

    /// <summary>Runs a value tween on the shared clock. Under reduced motion it applies the final value at once.</summary>
    protected void Animate(double durationMs, Easing? easing, Action<double> apply, Action? done = null)
    {
        if (Sm.Reduced || durationMs <= 1)
        {
            apply(1);
            done?.Invoke();
            return;
        }

        _tweens.RemoveAll(tween => tween.Finished);
        _tweens.Add(new SmTween
        {
            Elapsed = 0,
            Duration = durationMs,
            Easing = easing ?? ArcMotion.EaseStandard,
            Apply = apply,
            Done = done
        });
        apply(0);
        EnsureFrame();
    }

    /// <summary>True when the component wants a frame clock even with nothing tween driven (rings, caret, particles).</summary>
    protected virtual bool NeedsFrame => HasTweens || _loop;

    private bool _loop;

    /// <summary>Starts a continuous frame clock for components that animate on their own.</summary>
    protected void RunLoop()
    {
        _loop = true;
        EnsureFrame();
    }

    protected void StopLoop()
    {
        _loop = false;
    }

    /// <summary>Starts the frame clock; a no-op under reduced motion, where the still frame is the design.</summary>
    protected void EnsureFrame()
    {
        if (Sm.Reduced)
        {
            return;
        }

        if (_timer is not null)
        {
            return;
        }

        _clock = new System.Diagnostics.Stopwatch();
        _clock.Start();
        _lastFrame = 0;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Frame());
        _timer.Start();
    }

    protected void StopFrame()
    {
        _timer?.Stop();
        _timer = null;
        _clock = null;
    }

    private void Frame()
    {
        var now = _clock?.Elapsed.TotalSeconds ?? 0;
        var dt = Math.Clamp(now - _lastFrame, 0, 0.05);
        _lastFrame = now;
        _now = now;

        for (var index = _tweens.Count - 1; index >= 0; index--)
        {
            var tween = _tweens[index];
            tween.Elapsed += dt;
            var progress = tween.Duration <= 0 ? 1 : Math.Min(1, tween.Elapsed / tween.Duration);
            tween.Apply(tween.Easing.Ease(progress));
            if (progress >= 1)
            {
                tween.Finished = true;
                tween.Done?.Invoke();
            }
        }

        OnFrame(dt);
        InvalidateVisual();

        if (!NeedsFrame)
        {
            StopFrame();
        }
    }

    /// <summary>Advances the component by one frame while the clock runs.</summary>
    protected virtual void OnFrame(double dt)
    {
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopFrame();
    }

    // ---- Render ------------------------------------------------------------------------------------

    public sealed override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 1 || size.Height <= 1)
        {
            return;
        }

        _hits.Clear();
        OnRender(context, size);
    }

    /// <summary>Draws the whole component at the current bounds and records its hit regions.</summary>
    protected abstract void OnRender(DrawingContext context, Size size);

    private readonly record struct HitRegion(Rect Rect, int Id, bool Circle);
}
