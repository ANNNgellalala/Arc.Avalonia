using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>
/// Shared plumbing for the media and gesture components: token lookups that follow theme and accent
/// swaps, the type scale helpers, the numeric treatment for counts, and one cancellable tween so a
/// reduced-motion build can jump instead of travelling.
/// </summary>
internal static class ArcMediaUi
{
    public static readonly FontFeatureCollection Tabular = CreateTabular();

    private static FontFeatureCollection CreateTabular()
    {
        var features = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } };
        return features;
    }

    public static IBrush Brush(string token) => ArcResources.Brush(token);

    public static Color Color(string token) => ArcResources.Color(token);

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static double Space(string token) => ArcResources.Double(token);

    public static TimeSpan Duration(string token) => ArcResources.Duration(token);

    public static FontFamily Font(string token) => ArcResources.Font(token);

    /// <summary>Binds a brush property to a semantic token so it tracks theme and accent swaps.</summary>
    public static T Paint<T>(this T control, AvaloniaProperty property, string token) where T : AvaloniaObject
    {
        if (control is Control host)
        {
            host.Bind(property, host.GetResourceObservable(token));
        }

        return control;
    }

    /// <summary>A translucent version of a token colour, for washes and focus echoes.</summary>
    public static IBrush Alpha(string token, double alpha) =>
        new SolidColorBrush(ArcColor.WithAlpha(Color(token), alpha));

    public static ArcIcon Icon(string? name, double size = 16, string? token = null)
    {
        var icon = new ArcIcon
        {
            Icon = name,
            Size = size,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (token is not null)
        {
            icon.Paint(ArcIcon.ForegroundProperty, token);
        }

        return icon;
    }

    public static TextBlock Text(string text, params string[] classes)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        foreach (var name in classes)
        {
            block.Classes.Add(name);
        }

        return block;
    }

    public static TextBlock Caption(string text) => Text(text, "arc-caption");

    public static TextBlock Body(string text, double maxWidth = 640)
    {
        var block = Text(text, "arc-body-sm", "arc-secondary");
        block.TextWrapping = TextWrapping.Wrap;
        block.MaxWidth = maxWidth;
        block.HorizontalAlignment = HorizontalAlignment.Left;
        return block;
    }

    public static TextBlock Label(string text) => Text(text, "arc-label");

    /// <summary>A number that can change: tabular figures and the medium weight keep its width still.</summary>
    public static TextBlock Numeric(string text, params string[] classes)
    {
        var block = Text(text, classes);
        block.FontFeatures = Tabular;
        return block;
    }

    public static StackPanel Row(double spacing, params Control[] children)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing,
            VerticalAlignment = VerticalAlignment.Center
        };
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

    /// <summary>Inner radius = outer radius - inset, clamped to 6, so nested arcs stay parallel.</summary>
    public static CornerRadius Concentric(CornerRadius outer, double inset)
    {
        inset = Math.Max(0, inset);
        return new CornerRadius(
            Math.Max(6, outer.TopLeft - inset),
            Math.Max(6, outer.TopRight - inset),
            Math.Max(6, outer.BottomRight - inset),
            Math.Max(6, outer.BottomLeft - inset));
    }

    /// <summary>True when <paramref name="source"/> sits inside an interactive button of this template.</summary>
    public static bool InsideButton(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button)
            {
                return true;
            }
        }

        return false;
    }

    public static FormattedText Formatted(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
    {
        return new FormattedText(
            text,
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface(Font(ArcTokens.FontBody), FontStyle.Normal, weight),
            size,
            brush);
    }

    /// <summary>Draws the shared 2 px focus ring inside the control's own rounded frame.</summary>
    public static void DrawFocusRing(DrawingContext context, Size size, double radius, double thickness = 2)
    {
        var inset = thickness / 2;
        var rect = new Rect(inset, inset, Math.Max(0, size.Width - thickness), Math.Max(0, size.Height - thickness));
        context.DrawRectangle(null, new Pen(Brush(ArcTokens.FocusOutline), thickness), rect, radius, radius);
    }

    public static bool KeyboardFocused(Control control) =>
        control.IsFocused || control.IsKeyboardFocusWithin;
}

/// <summary>
/// A cancellable eased tween on the UI thread. Reduced motion applies the target immediately, which is
/// what the screenshot harness runs with, so a still frame always shows the settled state.
/// </summary>
internal sealed class ArcMediaTween
{
    private DispatcherTimer? _timer;

    public bool IsRunning => _timer is not null;

    public void Run(double from, double to, TimeSpan duration, Action<double> apply, Action? completed = null, Func<double, double>? ease = null)
    {
        Stop();

        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero || Math.Abs(to - from) < 0.0001)
        {
            apply(to);
            completed?.Invoke();
            return;
        }

        var easing = ease ?? ArcMotion.SpringSmooth.Ease;
        var watch = Stopwatch.StartNew();
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var progress = Math.Clamp(watch.Elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
            apply(from + (to - from) * easing(progress));
            if (progress < 1)
            {
                return;
            }

            timer?.Stop();
            if (ReferenceEquals(_timer, timer))
            {
                _timer = null;
            }

            completed?.Invoke();
        });

        _timer = timer;
        timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}
