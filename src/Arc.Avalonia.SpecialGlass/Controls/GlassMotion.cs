using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>
/// A time based tween driven by one dispatcher timer. Every helper here collapses to its end state
/// when <see cref="ArcThemeManager.ReducedMotion"/> is set, so a screenshot run always paints a
/// deterministic frame and people who ask for less motion never see travel.
/// </summary>
public sealed class GlassTween : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly TimeSpan _duration;
    private readonly Easing _easing;
    private readonly Action<double> _step;
    private readonly Action? _completed;
    private bool _done;

    private GlassTween(TimeSpan duration, Easing easing, Action<double> step, Action? completed)
    {
        _duration = duration;
        _easing = easing;
        _step = step;
        _completed = completed;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
    }

    public static GlassTween Run(TimeSpan duration, Easing easing, Action<double> step, Action? completed = null)
    {
        var tween = new GlassTween(duration, easing, step, completed);
        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero)
        {
            step(1);
            completed?.Invoke();
            tween._done = true;
            return tween;
        }

        tween._clock.Start();
        tween._timer.Start();
        return tween;
    }

    /// <summary>Runs a three keyframe sequence on one clock: 0 to 0.5 to 1 through the supplied mapping.</summary>
    public static GlassTween Sequence(
        TimeSpan duration,
        Easing easing,
        Action<double> step,
        Action? completed = null) => Run(duration, easing, step, completed);

    private void OnTick(object? sender, EventArgs e)
    {
        if (_done)
        {
            return;
        }

        var raw = _duration.TotalSeconds <= 0 ? 1 : _clock.Elapsed.TotalSeconds / _duration.TotalSeconds;
        var eased = _easing.Ease(Math.Clamp(raw, 0, 1));
        _step(eased);
        if (raw >= 1)
        {
            _done = true;
            _timer.Stop();
            _completed?.Invoke();
        }
    }

    public void Stop()
    {
        _done = true;
        _timer.Stop();
    }

    public void Dispose() => Stop();
}

/// <summary>
/// A critically damped spring for one scalar. Used for pointer tracking (tilt, lens, meter) where the
/// target changes every frame; callers tick it from an <see cref="ArcFrameLoop"/>.
/// </summary>
public sealed class GlassSpring
{
    public GlassSpring(double value = 0, double stiffness = 260)
    {
        Value = value;
        Target = value;
        Stiffness = stiffness;
        Damping = 2 * Math.Sqrt(stiffness);
    }

    public double Value { get; set; }

    public double Target { get; set; }

    public double Velocity { get; set; }

    public double Stiffness { get; set; }

    public double Damping { get; set; }

    public bool Settled => Math.Abs(Velocity) < 0.4 && Math.Abs(Target - Value) < 0.15;

    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public void Update(double dt)
    {
        if (dt <= 0)
        {
            return;
        }

        // Clamp the step so a stalled UI thread cannot explode the spring.
        var step = Math.Min(dt, 1.0 / 30.0);
        var acceleration = (Stiffness * (Target - Value)) - (Damping * Velocity);
        Velocity += acceleration * step;
        Value += Velocity * step;
    }
}

/// <summary>A single 60 fps tick shared by the springs in one control.</summary>
public sealed class ArcFrameLoop : IDisposable
{
    private readonly Action<double> _tick;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private TimeSpan _last;

    public ArcFrameLoop(Action<double> tick)
    {
        _tick = tick;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
    }

    public bool IsRunning => _timer.IsEnabled;

    public void Start()
    {
        if (ArcThemeManager.ReducedMotion || _timer.IsEnabled)
        {
            return;
        }

        _clock.Restart();
        _last = TimeSpan.Zero;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var dt = (now - _last).TotalSeconds;
        _last = now;
        _tick(dt);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}

/// <summary>Small motion and formatting helpers shared by the glass items.</summary>
public static class GlassMotion
{
    public static TimeSpan Smooth => ArcMotion.SpringSmoothDuration;

    public static TimeSpan Snappy => ArcMotion.SpringSnappyDuration;

    public static TimeSpan Morph => ArcMotion.SpringMorphDuration;

    public static Easing SmoothEase => ArcMotion.SpringSmooth;

    public static Easing SnappyEase => ArcMotion.SpringSnappy;

    public static Easing MorphEase => ArcMotion.SpringMorph;

    public static TimeSpan Fast => ArcMotion.Fast;

    public static TimeSpan Standard => ArcMotion.Standard;

    public static TimeSpan Exit => ArcMotion.Exit;

    /// <summary>How far a flick would travel, used to pick the tab a flung lens lands on.</summary>
    public static double Project(double velocityPerSecond, double settleSeconds = 0.18) =>
        velocityPerSecond * settleSeconds;

    public static string Money(double amount, string symbol = "$") =>
        amount < 0 ? $"-{symbol}{Math.Abs(amount):N2}" : $"+{symbol}{Math.Abs(amount):N2}";

    public static string MoneyPlain(double amount, string symbol = "$") => $"{symbol}{amount:N2}";
}


