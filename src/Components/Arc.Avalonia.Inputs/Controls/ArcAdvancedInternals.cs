using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// Spring tweens for the advanced input family. Every animation funnels through <see cref="To"/> so the
/// reduced-motion branch is one check: the final value is applied immediately and callers still get the
/// completion callback.
/// </summary>
internal static class ArcAnim
{
    public static void To(
        double from,
        double to,
        TimeSpan duration,
        Easing? easing,
        Action<double> apply,
        Action? finish = null)
    {
        if (ArcThemeManager.ReducedMotion || Math.Abs(to - from) < 0.0001 || duration <= TimeSpan.Zero)
        {
            apply(to);
            finish?.Invoke();
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        var watch = Stopwatch.StartNew();
        var total = Math.Max(1, duration.TotalMilliseconds);

        timer.Tick += (_, _) =>
        {
            var progress = Math.Clamp(watch.Elapsed.TotalMilliseconds / total, 0, 1);
            var eased = easing?.Ease(progress) ?? progress;
            apply(from + (to - from) * eased);
            if (progress < 1)
            {
                return;
            }

            timer.Stop();
            apply(to);
            finish?.Invoke();
        };

        timer.Start();
    }

    /// <summary>A one-shot callback on the UI thread.</summary>
    public static void Delay(TimeSpan delay, Action action)
    {
        if (delay <= TimeSpan.Zero)
        {
            action();
            return;
        }

        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    public static readonly Easing Smooth = ArcMotion.SpringSmooth;

    public static readonly Easing Snappy = ArcMotion.SpringSnappy;

    public static readonly Easing Morph = ArcMotion.SpringMorph;

    public static TimeSpan SmoothDuration => ArcMotion.SpringSmoothDuration;

    public static TimeSpan SnappyDuration => ArcMotion.SpringSnappyDuration;

    public static TimeSpan MorphDuration => ArcMotion.SpringMorphDuration;
}

/// <summary>Formatted text on the Arc faces, with tabular figures for numbers that roll or line up.</summary>
internal static class ArcText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly FontFeatureCollection Tabular = FontFeatureCollection.Parse("tnum");

    public static FontFamily Body => Family(ArcTokens.FontBody);

    public static FontFamily Display => Family(ArcTokens.FontDisplay);

    private static FontFamily Family(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is FontFamily family
            ? family
            : FontFamily.Default;

    public static FormattedText Create(
        string text,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal,
        bool tabular = true,
        bool display = false)
    {
        var formatted = new FormattedText(
            text,
            Culture,
            FlowDirection.LeftToRight,
            new Typeface(display ? Display : Body, FontStyle.Normal, weight),
            size,
            brush);

        if (tabular)
        {
            formatted.SetFontFeatures(Tabular);
        }

        return formatted;
    }

    public static void Draw(DrawingContext context, FormattedText text, double x, double centerY)
        => context.DrawText(text, new Point(x, centerY - text.Height / 2));
}

/// <summary>Number and text formatting shared by the controls so readouts read the same everywhere.</summary>
internal static class ArcFormat
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>Trims trailing zeros, keeps the requested precision at most.</summary>
    public static string Number(double value, int precision)
    {
        precision = Math.Clamp(precision, 0, 6);
        var text = value.ToString("0." + new string('#', precision), Culture);
        return string.IsNullOrEmpty(text) || text == "-" ? "0" : text;
    }

    public static string Signed(double value, int precision)
    {
        var text = Number(value, precision);
        return value > 0 ? "+" + text : text;
    }

    /// <summary>Spells out a number for screen readers, including percent and degrees.</summary>
    public static string Spoken(double value, string? unit)
    {
        var text = Number(value, 2);
        return string.IsNullOrEmpty(unit) ? text : text + " " + (unit == "%" ? "percent" : unit);
    }
}

/// <summary>A tiny token parser for the keys of a shortcut chord.</summary>
internal static class ArcKeys
{
    public static string KeyName(Key key)
    {
        var name = key.ToString();
        return name switch
        {
            "Return" => "Enter",
            "Escape" => "Esc",
            "Back" => "Backspace",
            "Capital" => "CapsLock",
            "OemPlus" => "Plus",
            "OemMinus" => "Minus",
            "OemComma" => "Comma",
            "OemPeriod" => "Period",
            "OemQuestion" => "Slash",
            "OemSemicolon" => "Semicolon",
            "OemQuotes" => "Quote",
            "OemOpenBrackets" => "BracketLeft",
            "OemCloseBrackets" => "BracketRight",
            "OemPipe" => "Backslash",
            "OemTilde" => "`",
            "D0" => "0",
            "D1" => "1",
            "D2" => "2",
            "D3" => "3",
            "D4" => "4",
            "D5" => "5",
            "D6" => "6",
            "D7" => "7",
            "D8" => "8",
            "D9" => "9",
            _ => name
        };
    }
}
