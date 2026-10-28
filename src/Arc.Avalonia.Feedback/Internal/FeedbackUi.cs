using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>
/// Small shared helpers for the feedback components: token-bound brushes, typography, and the
/// bits of chrome that every composite control needs. Keeping the token lookup in one place means
/// every generated child follows a theme or accent swap without a rebuild.
/// </summary>
internal static class FeedbackUi
{
    public static readonly FontFeatureCollection Tabular = CreateTabular();

    public static readonly FontFamily MonoFamily = new("Consolas, Menlo, monospace");
    public static readonly FontFamily EmojiFamily = new("Segoe UI Emoji, Segoe UI Symbol, Segoe UI");

    private static FontFeatureCollection CreateTabular()
    {
        var features = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } };
        return features;
    }

    /// <summary>Binds a brush property to a semantic token so it tracks theme and accent swaps.</summary>
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

    public static FontFamily Font(string token) =>
        Application.Current?.FindResource(token) as FontFamily ?? FontFamily.Default;

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static double Space(string token) => ArcResources.Double(token);

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

    public static TextBlock Body(string text) => Text(text, "arc-body-sm", "arc-secondary");

    public static TextBlock Label(string text) => Text(text, "arc-label");

    /// <summary>A text block with tabular figures, used for every number that can change.</summary>
    public static TextBlock Numeric(string text, params string[] classes)
    {
        var block = Text(text, classes);
        block.FontFeatures = Tabular;
        return block;
    }

    public static ArcIcon Icon(string name, double size = 16, string? token = null)
    {
        var icon = new ArcIcon { Icon = name, Size = size, VerticalAlignment = VerticalAlignment.Center };
        if (token is not null)
        {
            icon.Paint(ArcIcon.ForegroundProperty, token);
        }

        return icon;
    }

    /// <summary>A 1 px divider that owns its own token binding.</summary>
    public static Border Divider(string token = ArcTokens.BorderSubtle, double thickness = 1)
    {
        return new Border { Height = thickness, Background = Brushes.Transparent }
            .Paint(Border.BackgroundProperty, token);
    }

    /// <summary>A quiet pill used for badges, counts, and metadata.</summary>
    public static Border Pill(Control content, string background, string foreground, double horizontal = 10, double vertical = 3)
    {
        var border = new Border
        {
            CornerRadius = Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(horizontal, vertical),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content
        };
        border.Paint(Border.BackgroundProperty, background);
        if (content is TextBlock text)
        {
            text.Paint(TextBlock.ForegroundProperty, foreground);
            text.FontSize = ArcResources.Double(ArcTokens.TextXs);
            text.FontWeight = FontWeight.Medium;
        }
        else if (content is ArcIcon icon)
        {
            icon.Paint(ArcIcon.ForegroundProperty, foreground);
        }

        return border;
    }

    /// <summary>A minimal quiet text field; local values keep Fluent chrome out of the Arc surface.</summary>
    public static TextBox Input(string? watermark = null, double minHeight = 40)
    {
        var box = new TextBox
        {
            PlaceholderText = watermark,            MinHeight = minHeight,
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Padding = new Thickness(14, 8, 14, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = Radius(ArcTokens.RadiusControl),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        box.Paint(TextBox.BackgroundProperty, ArcTokens.Surface);
        box.Paint(TextBox.BorderBrushProperty, ArcTokens.Border);
        box.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        return box;
    }

    public static void Name(Control control, string? name) => AutomationProperties.SetName(control, name);

    public static void Help(Control control, string? help) => AutomationProperties.SetHelpText(control, help);

    public static List<string> InitialsList(string? name)
    {
        var result = new List<string>();
        var words = (name ?? string.Empty).Split(new[] { ' ', '\t', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            result.Add("?");
            return result;
        }

        result.Add(char.ToUpperInvariant(words[0][0]).ToString());
        if (words.Length > 1)
        {
            result.Add(char.ToUpperInvariant(words[^1][0]).ToString());
        }
        else if (words[0].Length > 1)
        {
            result.Add(char.ToUpperInvariant(words[0][1]).ToString());
        }

        return result;
    }

    public static string Initials(string? name) => string.Concat(InitialsList(name));

    /// <summary>Compact number copy in the Arc voice: 8.4k, 1.2M.</summary>
    public static string CompactNumber(double value)
    {
        var absolute = Math.Abs(value);
        return absolute switch
        {
            >= 1_000_000_000 => Trim(value / 1_000_000_000) + "B",
            >= 1_000_000 => Trim(value / 1_000_000) + "M",
            >= 1_000 => Trim(value / 1_000) + "k",
            _ => Trim(value)
        };
    }

    private static string Trim(double value)
    {
        var text = value.ToString("0.#", CultureInfo.InvariantCulture);
        return text;
    }

    public static string FormatNumber(double value, int decimals)
    {
        var format = decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>The data series token for a segment index; extra segments reuse the last hue.</summary>
    public static string SeriesKey(int index) => Math.Clamp(index, 0, 3) switch
    {
        0 => ArcTokens.Series1,
        1 => ArcTokens.Series2,
        2 => ArcTokens.Series3,
        _ => ArcTokens.Series4
    };

    public static string Plural(double count, string one, string other) =>
        Math.Abs(count - 1) < 0.0001 ? one : other;

    public static StackPanel Row(double spacing = 8, params Control[] children)
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

    public static StackPanel Column(double spacing = 8, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>Adds an inline run, optionally tinted, to a text block.</summary>
    public static void AddRun(TextBlock block, string text, string? token = null, FontWeight? weight = null)
    {
        var run = new Run { Text = text };
        if (token is not null)
        {
            run.Bind(TextElement.ForegroundProperty, block.GetResourceObservable(token));
        }

        if (weight is not null)
        {
            run.FontWeight = weight.Value;
        }

        block.Inlines?.Add(run);
    }

    /// <summary>Turns a control on when the navigation action is available.</summary>
    public static void Cursor(Control control, bool hand) =>
        control.Cursor = hand ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
}

/// <summary>
/// A cancellable eased tween on the UI thread. Every feedback component that reports a value
/// uses this so the number and its graphic always read the same, and reduced motion jumps.
/// </summary>
internal sealed class ArcTween
{
    private DispatcherTimer? _timer;

    public void Run(double from, double to, TimeSpan duration, Action<double> apply, Action? completed = null, Func<double, double>? ease = null)
    {
        Stop();

        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero || Math.Abs(to - from) < 0.00001)
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
            if (progress >= 1)
            {
                timer?.Stop();
                if (ReferenceEquals(_timer, timer))
                {
                    _timer = null;
                }

                completed?.Invoke();
            }
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
