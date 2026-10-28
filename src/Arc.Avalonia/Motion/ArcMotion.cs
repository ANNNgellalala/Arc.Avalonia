using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;

namespace Arc.Avalonia.Motion;

/// <summary>
/// Arc's motion presets. Springs are sampled from a damped oscillator so panels and state changes
/// land without overshoot; only the playful presets bounce slightly.
/// </summary>
public static class ArcMotion
{
    public static readonly Easing EaseEnter = new SplineEasing(0.16, 1, 0.30, 1);
    public static readonly Easing EaseStandard = new SplineEasing(0.22, 1, 0.36, 1);
    public static readonly Easing EaseExit = new SplineEasing(0.70, 0, 0.84, 0);
    public static readonly Easing EaseInOut = new SplineEasing(0.65, 0, 0.35, 1);

    /// <summary>Panels, height, drawers, progress: 0.4s, no overshoot.</summary>
    public static readonly Easing SpringSmooth = new ArcSpringEasing(0.40, 1.00);

    /// <summary>Press feedback, small indicators, icon swaps: 0.26s, the faintest bounce.</summary>
    public static readonly Easing SpringSnappy = new ArcSpringEasing(0.26, 0.62);

    /// <summary>Shared highlights that glide between positions: 0.42s, a little bounce.</summary>
    public static readonly Easing SpringMorph = new ArcSpringEasing(0.42, 0.55);

    public static TimeSpan Instant => TimeSpan.FromSeconds(0.12);
    public static TimeSpan Fast => TimeSpan.FromSeconds(0.16);
    public static TimeSpan Exit => TimeSpan.FromSeconds(0.18);
    public static TimeSpan Standard => TimeSpan.FromSeconds(0.24);
    public static TimeSpan Considered => TimeSpan.FromSeconds(0.48);

    public static TimeSpan SpringSmoothDuration => TimeSpan.FromSeconds(0.40);
    public static TimeSpan SpringSnappyDuration => TimeSpan.FromSeconds(0.26);
    public static TimeSpan SpringMorphDuration => TimeSpan.FromSeconds(0.42);

    internal static bool Reduced => Theme.ArcThemeManager.ReducedMotion;

    /// <summary>A smooth spring transition for the given animated property.</summary>
    public static Transitions Smooth(AvaloniaProperty property) =>
        Reduced ? new Transitions() : new Transitions { new DoubleTransition { Property = property, Duration = SpringSmoothDuration, Easing = SpringSmooth } };
}

/// <summary>Critically damped to slightly underdamped spring, evaluated on normalized time.</summary>
public sealed class ArcSpringEasing : Easing
{
    private readonly double _duration;
    private readonly double _zeta;

    public ArcSpringEasing(double durationSeconds, double dampingRatio)
    {
        _duration = durationSeconds;
        _zeta = dampingRatio;
    }

    public override double Ease(double t)
    {
        if (Theme.ArcThemeManager.ReducedMotion)
        {
            return t < 1 ? 1 : 1;
        }

        if (t <= 0)
        {
            return 0;
        }

        if (t >= 1)
        {
            return 1;
        }

        // Oscillator settles to 1 within the token duration; omega is chosen so the tail is visually done.
        var omega = 7.5 / _duration;
        var x = t * _duration;
        var zeta = Math.Clamp(_zeta, 0.05, 1.0);

        if (zeta >= 0.999)
        {
            var value = 1 - (1 + omega * x) * Math.Exp(-omega * x);
            return Math.Clamp(value, 0, 1);
        }

        var omegaD = omega * Math.Sqrt(1 - zeta * zeta);
        var envelope = Math.Exp(-zeta * omega * x);
        var value2 = 1 - envelope * (Math.Cos(omegaD * x) + (zeta * omega / omegaD) * Math.Sin(omegaD * x));
        return value2;
    }
}

