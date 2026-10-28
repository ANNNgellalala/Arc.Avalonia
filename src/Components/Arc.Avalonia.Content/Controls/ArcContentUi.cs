using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

/// <summary>
/// Token-bound building blocks shared by the content controls. The helpers mirror the foundation's
/// conventions: semantic brushes only, a small type scale, and 4 px spacing.
/// </summary>
internal static class ArcContentUi
{
    public static readonly FontFeatureCollection Tabular = CreateTabular();

    public static readonly FontFamily MonoFamily = new("Consolas, Menlo, monospace");

    public static readonly FontFamily EmojiFamily = new("Segoe UI Emoji, Segoe UI Symbol, Segoe UI");

    private static FontFeatureCollection CreateTabular()
    {
        var features = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } };
        return features;
    }

    /// <summary>Binds a brush to a semantic resource so the accent and theme can change under it.</summary>
    public static T Paint<T>(this T control, AvaloniaProperty property, string token) where T : AvaloniaObject
    {
        if (control is Control host)
        {
            host.Bind(property, host.GetResourceObservable(token));
        }

        return control;
    }

    public static IBrush Brush(string token) => ArcResources.Brush(token);

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

    public static Border Divider(string token = ArcTokens.BorderSubtle, double thickness = 1) =>
        new Border { Height = thickness, Background = Brushes.Transparent }
            .Paint(Border.BackgroundProperty, token);

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

    /// <summary>A quiet Arc field; local values keep Fluent chrome off the Arc surface.</summary>
    public static TextBox Input(string? watermark = null, double minHeight = 40)
    {
        var box = new TextBox
        {
            PlaceholderText = watermark,
            MinHeight = minHeight,
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Padding = new Thickness(14, 8, 14, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = Radius(ArcTokens.RadiusControl),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        box.Paint(TextBox.BackgroundProperty, ArcTokens.Surface);
        box.Paint(TextBox.BorderBrushProperty, ArcTokens.Border);
        box.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        box.Paint(TextBox.SelectionBrushProperty, ArcTokens.AccentSubtle);
        box.Paint(TextBox.CaretBrushProperty, ArcTokens.AccentStrong);
        return box;
    }

    /// <summary>An editor surface with no chrome; the caller owns the framing border.</summary>
    public static TextBox PlainInput(string? watermark = null, double minHeight = 40)
    {
        var box = Input(watermark, minHeight);
        box.Background = Brushes.Transparent;
        box.BorderThickness = new Thickness(0);
        box.Padding = new Thickness(0);
        box.FontSize = ArcResources.Double(ArcTokens.TextBase);
        return box;
    }

    /// <summary>
    /// Removes Fluent's focus rectangle from a TextBox. The Arc surface owns the framing border and
    /// draws its own 2 px focus treatment, so the platform accent rectangle would be a second, wrong ring.
    /// </summary>
    public static void BareTextBox(TextBox box)
    {
        box.Classes.Add("arc-bare");
        box.Loaded += (_, _) => TintSelection(box);
    }

    /// <summary>Applies Arc selection colours to the presenter Fluent creates inside the TextBox.</summary>
    public static void TintSelection(TextBox box)
    {
        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter is null)
        {
            return;
        }

        presenter.SelectionBrush = Brush(ArcTokens.AccentSubtle);
        presenter.SelectionForegroundBrush = Brush(ArcTokens.Foreground);
        presenter.CaretBrush = Brush(ArcTokens.AccentStrong);
        presenter.ShowSelectionHighlight = true;
    }

    public static void Name(Control control, string? name) => AutomationProperties.SetName(control, name);

    public static void Help(Control control, string? help) => AutomationProperties.SetHelpText(control, help);

    public static string Initials(string? name)
    {
        var words = (name ?? string.Empty).Split(new[] { ' ', '\t', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        return words.Length == 1
            ? string.Concat(words[0].Take(2)).ToUpperInvariant()
            : $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}";
    }

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

    public static void AddRun(TextBlock block, string text, string? token = null, FontWeight? weight = null,
        FontStyle? style = null, TextDecorationCollection? decorations = null, FontFamily? family = null)
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

        if (style is not null)
        {
            run.FontStyle = style.Value;
        }

        if (decorations is not null)
        {
            run.TextDecorations = decorations;
        }

        if (family is not null)
        {
            run.FontFamily = family;
        }

        block.Inlines?.Add(run);
    }

    /// <summary>Arc's default locale for date labels is en-US, so times read as 4:17 PM everywhere.</summary>
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    public static string FormatTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("h:mm tt", DisplayCulture);

    public static string FormatDay(DateTimeOffset value)
    {
        var local = value.ToLocalTime().Date;
        var today = DateTime.Today;
        if (local == today)
        {
            return "Today";
        }

        if (local == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return value.ToLocalTime().ToString("MMM d", DisplayCulture);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.#} KB";
        }

        return $"{bytes / (1024d * 1024d):0.#} MB";
    }

    public static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..Math.Max(0, length - 1)] + "…";
}

/// <summary>A cancellable eased tween used by the ring and list transitions; reduced motion jumps.</summary>
internal sealed class ArcContentTween
{
    private DispatcherTimer? _timer;

    public void Run(double from, double to, TimeSpan duration, Action<double> apply, Action? completed = null)
    {
        Stop();

        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero || Math.Abs(to - from) < 0.00001)
        {
            apply(to);
            completed?.Invoke();
            return;
        }

        var watch = Stopwatch.StartNew();
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var progress = Math.Clamp(watch.Elapsed.TotalSeconds / duration.TotalSeconds, 0, 1);
            apply(from + (to - from) * ArcMotion.SpringSmooth.Ease(progress));
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
