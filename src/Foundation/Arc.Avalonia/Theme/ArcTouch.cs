using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Shared touch primitives for the mobile story (M2-T8): physical hit targets, gesture thresholds and
/// safe-area helpers. Every member is a pure calculation, a constant, or a one-shot resource lookup — no
/// timers, no render hooks — so components (Media / Chat / Overlays) can share one set of numbers instead
/// of each inventing its own magic values.
/// </summary>
/// <remarks>
/// The numeric constants are mirrored into the palette as <see cref="ArcTokens.HitTargetMin"/> /
/// <see cref="ArcTokens.HitTargetComfortable"/> so XAML can consume them through <c>DynamicResource</c>;
/// the drag/swipe thresholds are code-only because no theme needs them.
/// </remarks>
public static class ArcTouch
{
    /// <summary>Minimum hit target in px. 44 matches Apple HIG and Material's minimum touch target.</summary>
    public const double HitTargetMin = 44;

    /// <summary>Comfortable hit target in px. 48 is the primary-action size on touch-first surfaces.</summary>
    public const double HitTargetComfortable = 48;

    /// <summary>Pointer travel in px before a press is treated as a drag rather than a tap.</summary>
    public const double DragThreshold = 8;

    /// <summary>Fraction of the viewport a swipe must cover (0.35 = 35%) to count as a page swipe.</summary>
    public const double SwipeThreshold = 0.35;

    /// <summary>Default safe-area inset; the host overrides the tokens when it reserves system space.</summary>
    public const double SafeAreaDefault = 0;

    private static Func<Visual?, bool>? _touchPrimaryProbe;

    /// <summary>
    /// Test/host hook for <see cref="IsTouchPrimary"/>: pass a probe to force the answer, or <c>null</c> to
    /// restore platform detection. Injectable so headless tests and kiosk hosts do not depend on the OS.
    /// </summary>
    public static void UseTouchPrimaryProbe(Func<Visual?, bool>? probe) => _touchPrimaryProbe = probe;

    /// <summary>
    /// Best-effort "is this surface touch-first?" probe. Mobile OSes answer true; elsewhere the platform's
    /// tap tolerances are compared (a touch-first platform reserves a larger touch tolerance than the mouse
    /// one). Returns false when no platform settings are available yet (e.g. before the app is initialised).
    /// </summary>
    public static bool IsTouchPrimary(Visual? target = null)
    {
        if (_touchPrimaryProbe is { } probe)
        {
            return probe(target);
        }

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return true;
        }

        IPlatformSettings? settings = target is not null ? target.GetPlatformSettings() : Application.Current?.PlatformSettings;
        if (settings is null)
        {
            return false;
        }

        var touch = settings.GetTapSize(PointerType.Touch);
        var mouse = settings.GetTapSize(PointerType.Mouse);
        return touch.Width > mouse.Width && touch.Height > mouse.Height;
    }

    /// <summary>True once the pointer moved far enough from <paramref name="start"/> to be a drag.</summary>
    public static bool IsDragGesture(Point start, Point end, double threshold = DragThreshold)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return (dx * dx) + (dy * dy) >= threshold * threshold;
    }

    /// <summary>True when the horizontal movement dominates the vertical one (swipe direction test).</summary>
    public static bool ExceedsHorizontalDominance(double dx, double dy) => Math.Abs(dx) > Math.Abs(dy);

    /// <summary>Vector overload of <see cref="ExceedsHorizontalDominance(double, double)"/>.</summary>
    public static bool ExceedsHorizontalDominance(Vector delta) => ExceedsHorizontalDominance(delta.X, delta.Y);

    /// <summary>True when a horizontal delta covers at least <paramref name="threshold"/> of the viewport width.</summary>
    public static bool IsHorizontalSwipe(Vector delta, double viewportWidth, double threshold = SwipeThreshold) =>
        viewportWidth > 0 &&
        Math.Abs(delta.X) >= viewportWidth * threshold &&
        ExceedsHorizontalDominance(delta.X, delta.Y);

    /// <summary>True when a vertical delta covers at least <paramref name="threshold"/> of the viewport height.</summary>
    public static bool IsVerticalSwipe(Vector delta, double viewportHeight, double threshold = SwipeThreshold) =>
        viewportHeight > 0 &&
        Math.Abs(delta.Y) >= viewportHeight * threshold &&
        !ExceedsHorizontalDominance(delta.X, delta.Y);

    /// <summary>Current top safe-area inset in px (0 unless the host overrides the token).</summary>
    public static double SafeAreaTop => ArcResources.Double(ArcTokens.SafeAreaTop);

    /// <summary>Current bottom safe-area inset in px (0 unless the host overrides the token).</summary>
    public static double SafeAreaBottom => ArcResources.Double(ArcTokens.SafeAreaBottom);

    /// <summary>
    /// Thickness carrying the current safe-area insets, for hosts that prefer to apply them themselves
    /// (e.g. to a root grid's <c>Padding</c> or <c>Margin</c>).
    /// </summary>
    public static Thickness SafeAreaThickness(bool bottom = true, bool top = false) =>
        new(0, top ? SafeAreaTop : 0, 0, bottom ? SafeAreaBottom : 0);

    /// <summary>
    /// Adds the safe-area insets to a control's margin. No-op while the tokens are 0 (the default), and
    /// intended to be called once on attach; repeated calls accumulate, so keep it to a single attach hook.
    /// Margin is used instead of Padding because <see cref="Control"/> itself has no Padding (only
    /// TemplatedControl/Decorator do), and reserved system space belongs outside the control's own chrome.
    /// </summary>
    public static void ApplySafeArea(Control target, bool bottom = true, bool top = false)
    {
        ArgumentNullException.ThrowIfNull(target);

        var margin = target.Margin;
        var bottomInset = bottom ? SafeAreaBottom : 0;
        var topInset = top ? SafeAreaTop : 0;
        if (bottomInset <= 0 && topInset <= 0)
        {
            return;
        }

        target.Margin = new Thickness(margin.Left, margin.Top + topInset, margin.Right, margin.Bottom + bottomInset);
    }
}
