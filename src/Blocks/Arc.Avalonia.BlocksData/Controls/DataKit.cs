using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData;

/// <summary>
/// Composition helpers shared by the data blocks. A block is a page level product surface, so these stay
/// quiet: one surface radius, one hairline, the Arc type classes and a 4 px spacing grid. Every colour,
/// radius and elevation resolves through <see cref="ArcTokens"/>; there is no raw colour in this project.
/// </summary>
internal static class DataKit
{
    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color Color(string key) => ArcResources.Color(key);

    public static Color TintColor(string key, double alpha)
    {
        var color = ArcResources.Color(key);
        return global::Avalonia.Media.Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
    }

    public static IBrush Tint(string key, double alpha) => new SolidColorBrush(TintColor(key, alpha));

    /// <summary>Series colour by index: four semantic series tokens, then secondary and muted ink.</summary>
    public static IBrush Series(int index) => index switch
    {
        0 => Brush(ArcTokens.Series1),
        1 => Brush(ArcTokens.Series2),
        2 => Brush(ArcTokens.Series3),
        3 => Brush(ArcTokens.Series4),
        4 => Brush(ArcTokens.TextSecondary),
        _ => Brush(ArcTokens.TextMuted)
    };

    public static Color SeriesColor(int index) => index switch
    {
        0 => Color(ArcTokens.Series1),
        1 => Color(ArcTokens.Series2),
        2 => Color(ArcTokens.Series3),
        3 => Color(ArcTokens.Series4),
        4 => Color(ArcTokens.TextSecondary),
        _ => Color(ArcTokens.TextMuted)
    };

    // ------------------------------------------------------------------ numbers

    public static string Money(double amount, int decimals = 0) =>
        "$" + amount.ToString(decimals == 0 ? "N0" : "N" + decimals, CultureInfo.InvariantCulture);

    public static string Number(double value, int decimals = 0) =>
        value.ToString(decimals == 0 ? "N0" : "N" + decimals, CultureInfo.InvariantCulture);

    public static string Percent(double fraction, int decimals = 0) =>
        (fraction * 100).ToString("0." + new string('#', Math.Max(0, decimals)), CultureInfo.InvariantCulture) + "%";

    public static string Signed(double value, string suffix = "", int decimals = 0)
    {
        var sign = value >= 0 ? "+" : "-";
        var magnitude = Math.Abs(value);
        if (magnitude >= 1000)
        {
            return sign + Compact(magnitude) + suffix;
        }

        var places = decimals > 0 ? decimals : magnitude < 10 && magnitude % 1 != 0 ? 1 : 0;
        return sign + Number(magnitude, places) + suffix;
    }

    public static string Compact(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000_000)
        {
            return (value / 1_000_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "B";
        }

        if (abs >= 1_000_000)
        {
            return (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (abs >= 1_000)
        {
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return Number(value, Math.Abs(value - Math.Round(value)) < 0.05 ? 0 : 1);
    }

    public static string MoneyCompact(double value) => "$" + Compact(value);

    // ------------------------------------------------------------------ text

    public static TextBlock Text(string text, string classes = "arc-body-sm", double maxWidth = 0,
        TextWrapping wrap = TextWrapping.Wrap)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = wrap,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            block.Classes.Add(name);
        }

        if (maxWidth > 0)
        {
            block.MaxWidth = maxWidth;
        }

        return block;
    }

    public static TextBlock Caption(string text, double maxWidth = 0) => Text(text, "arc-caption", maxWidth);

    public static TextBlock Label(string text, double maxWidth = 0) => Text(text, "arc-label", maxWidth);

    public static TextBlock Body(string text, double maxWidth = 420) => Text(text, "arc-body-sm arc-secondary", maxWidth);

    public static TextBlock Heading(string text, double maxWidth = 520, string classes = "arc-heading") =>
        Text(text, classes, maxWidth);

    public static TextBlock Strong(string text, double maxWidth = 0) => Text(text, "arc-body arc-medium", maxWidth);

    public static TextBlock Figure(string text, string classes = "arc-subtitle") => new()
    {
        Text = text,
        Classes = { classes },
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>A value that also carries a direction in words, so a delta never relies on colour.</summary>
    public static TextBlock Delta(double value, string suffix = "%", int decimals = 1)
    {
        var up = value >= 0;
        var block = Text((up ? "up " : "down ") + Math.Abs(value).ToString("0." + new string('#', Math.Max(0, decimals)), CultureInfo.InvariantCulture) + suffix,
            "arc-caption");
        block.Foreground = up ? Brush(ArcTokens.Success) : Brush(ArcTokens.Danger);
        return block;
    }

    // ------------------------------------------------------------------ layout

    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var child in children.Where(child => child is not null))
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel Row(double spacing, params Control[] children)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = spacing,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var child in children.Where(child => child is not null))
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static WrapPanel RowWrap(double spacing, params Control[] children)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, spacing, spacing);
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A grid from CSS-like column and row definitions; cells fill left to right, top to bottom.</summary>
    public static Grid Columns(string definitions, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is not { } cell)
            {
                continue;
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    public static T AtColumn<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    public static T AtRow<T>(T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }

    public static Control Gap(double height) => new Border { Height = height };

    public static Border Divider(double margin = 0) => new()
    {
        Height = 1,
        Background = Brush(ArcTokens.BorderSubtle),
        Margin = new Thickness(0, margin, 0, margin),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static Border Hairline(double height = 0) => new()
    {
        Width = 1,
        Height = height > 0 ? height : double.NaN,
        Background = Brush(ArcTokens.BorderSubtle),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    // ------------------------------------------------------------------ surfaces

    /// <summary>The specimen shell: a 1000 px product surface at the gallery's content width.</summary>
    public static Border Specimen(Control content, double padding = 16, double width = 1000) => new()
    {
        Width = width,
        Background = Brush(ArcTokens.Surface),
        BorderBrush = Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(26),
        ClipToBounds = true,
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>A card inside a block; nests the 18 px panel radius inside the 26 px surface.</summary>
    public static Border Card(Control content, double padding = 14, double radius = 18, bool muted = false,
        double width = 0) => new()
    {
        Width = width > 0 ? width : double.NaN,
        Background = Brush(muted ? ArcTokens.SurfaceMuted : ArcTokens.SurfaceRaised),
        BorderBrush = Brush(muted ? ArcTokens.BorderSubtle : ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>A flat tile over the muted surface, for dense inner regions.</summary>
    public static Border Tile(Control content, double padding = 10, double radius = 14, double width = 0) => new()
    {
        Width = width > 0 ? width : double.NaN,
        Background = Brush(ArcTokens.SurfaceMuted),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    // ------------------------------------------------------------------ controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Sm, Action? onClick = null, string? icon = null,
        string? trailingIcon = null, double height = 0)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon { Icon = icon, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        }

        content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrWhiteSpace(trailingIcon))
        {
            content.Children.Add(new ArcIcon { Icon = trailingIcon, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        }

        var button = new ArcButton
        {
            Variant = variant,
            Size = size,
            Content = content,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (height > 0)
        {
            button.Height = height;
            button.MinHeight = height;
        }

        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>A compact text action with no chrome, for inline "View all" and "Clear" links.</summary>
    public static ArcButton Link(string label, Action? onClick = null, string? icon = null)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = Row(5,
                string.IsNullOrWhiteSpace(icon) ? null! : new ArcIcon { Icon = icon, Size = 12 },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }),
            Padding = new Thickness(0),
            Height = 24,
            MinHeight = 24,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    public static ArcIconButton IconBtn(string icon, string label, Action? onClick = null, double box = 28,
        bool muted = true)
    {
        var button = new ArcIconButton
        {
            Icon = icon,
            IconSize = 14,
            Label = label,
            BoxSize = box,
            Muted = muted,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    public static ArcBadge Pill(string text, ArcBadgeTone tone = ArcBadgeTone.Neutral, bool dot = false) =>
        new() { Content = text, Tone = tone, Dot = dot };

    public static Control Meta(string icon, string text)
    {
        return Row(6,
            new ArcIcon
            {
                Icon = icon,
                Size = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(ArcTokens.TextMuted)
            },
            Caption(text));
    }

    public static Control Dot(IBrush brush, double size = 7) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = brush,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, double width = 0, string? label = null)
    {
        var control = new ArcSegmentedControl
        {
            Options = options.Select(option => new ArcSegmentOption(option.Value, option.Label)).ToList(),
            Value = selected,
            Label = label
        };
        if (width > 0)
        {
            control.Width = width;
        }

        if (onChanged is not null)
        {
            control.ValueChanged += (_, _) =>
            {
                if (control.Value is { Length: > 0 } value)
                {
                    onChanged(value);
                }
            };
        }

        return control;
    }

    /// <summary>A pill-shaped toggle chip; selection uses the control fill, never a second accent.</summary>
    public static ArcButton Chip(string label, bool selected, Action onToggle, string? count = null)
    {
        var content = Row(6,
            selected ? Dot(Brush(ArcTokens.Accent), 5) : null!,
            new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Classes = { "arc-body-sm" } },
            string.IsNullOrWhiteSpace(count)
                ? null!
                : new TextBlock
                {
                    Text = count,
                    Classes = { "arc-caption", "arc-muted" },
                    VerticalAlignment = VerticalAlignment.Center
                });

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = content,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(999),
            Opacity = selected ? 1 : 0.55,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => onToggle();
        return button;
    }

    public static ArcSwitch Toggle(string label, bool isChecked, Action<bool>? onChanged = null)
    {
        var toggle = new ArcSwitch { Label = label, Checked = isChecked };
        if (onChanged is not null)
        {
            toggle.CheckedChanged += (_, _) => onChanged(toggle.Checked);
        }

        return toggle;
    }

    /// <summary>A compact select for a short, closed list such as a country or a view.</summary>
    public static ArcSelect Select(IEnumerable<(string Value, string Label)> options, string? value,
        Action<string>? onChanged = null, double width = 0)
    {
        var control = new ArcSelect
        {
            Options = options.Select(option => new ArcSelectOption(option.Value, option.Label)).ToList(),
            Value = value
        };
        if (width > 0)
        {
            control.Width = width;
        }

        if (onChanged is not null)
        {
            control.ValueChanged += (_, _) =>
            {
                if (control.Value is { Length: > 0 } next)
                {
                    onChanged(next);
                }
            };
        }

        return control;
    }

    public static ArcSlider Slider(string? label, double value, double min, double max, double step,
        Action<double>? onChanged = null, bool showValue = false)
    {
        var slider = new ArcSlider
        {
            Label = label,
            Value = value,
            Min = min,
            Max = max,
            Step = step,
            ShowValue = showValue
        };
        if (onChanged is not null)
        {
            slider.PropertyChanged += (_, args) =>
            {
                if (args.Property == ArcSlider.ValueProperty)
                {
                    onChanged(slider.Value);
                }
            };
        }

        return slider;
    }

    // ------------------------------------------------------------------ readouts

    /// <summary>A label left, value right row for summary lines.</summary>
    public static Control Line(string label, string value, bool strong = false, string? note = null,
        bool accent = false)
    {
        var left = new StackPanel { Spacing = 1 };
        left.Children.Add(strong ? Strong(label) : Body(label, 280));
        if (!string.IsNullOrWhiteSpace(note))
        {
            left.Children.Add(Caption(note!));
        }

        var right = new TextBlock
        {
            Text = value,
            Classes = { strong ? "arc-body arc-medium" : "arc-body-sm" },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (accent)
        {
            right.Foreground = Brush(ArcTokens.Accent);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(left);
        Grid.SetColumn(right, 1);
        row.Children.Add(right);
        return row;
    }

    /// <summary>A small metric: caption above, figure below.</summary>
    public static Control Stat(string label, string value, string? note = null, double valueSize = 0)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Caption(label));
        var figure = Text(value, "arc-subtitle");
        if (valueSize > 0)
        {
            figure.FontSize = valueSize;
        }

        panel.Children.Add(figure);
        if (!string.IsNullOrWhiteSpace(note))
        {
            panel.Children.Add(Caption(note!));
        }

        return panel;
    }

    /// <summary>A block header: title, one-line context, optional right-hand controls.</summary>
    public static Control Header(string title, string subtitle, Control? right = null)
    {
        var left = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = title, Classes = { "arc-subtitle" } },
                Caption(subtitle)
            }
        };

        if (right is null)
        {
            return left;
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        grid.Children.Add(left);
        right.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>An inline status line with a polite live region: every block reports its last action here.</summary>
    public static TextBlock Status(string text = "")
    {
        var block = Caption(text);
        block.IsVisible = !string.IsNullOrEmpty(text);
        block.Classes.Add("arc-secondary");
        AutomationProperties.SetLiveSetting(block, AutomationLiveSetting.Polite);
        return block;
    }

    public static void Say(TextBlock line, string text)
    {
        line.Text = text;
        line.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    // ------------------------------------------------------------------ charts

    public static ArcSparkline Spark(IReadOnlyList<double> values, double height = 34,
        ChartTone tone = ChartTone.Accent)
    {
        return new ArcSparkline
        {
            Values = values,
            Height = height,
            ShowArea = true,
            ShowLastPoint = true,
            Tone = tone
        };
    }

    public static ArcLineChart Line(IEnumerable<ChartSeries> series, IEnumerable<string> labels, double height = 150,
        bool showArea = true, bool showGrid = true, bool showLegend = false)
    {
        return new ArcLineChart
        {
            Series = series.ToList(),
            Labels = labels.ToList(),
            Height = height,
            ShowArea = showArea,
            ShowGrid = showGrid,
            ShowLegend = showLegend,
            Smooth = true
        };
    }

    public static ArcProgress Progress(double value, double max, ArcProgressTone tone = ArcProgressTone.Accent,
        bool showValue = false)
    {
        return new ArcProgress
        {
            Value = value,
            Max = max,
            Tone = tone,
            ShowValue = showValue,
            Size = ArcProgressSize.Sm
        };
    }

    // ------------------------------------------------------------------ deterministic noise

    /// <summary>
    /// A seeded random source (mulberry32). Every block seeds its sample data, so a screenshot is the same
    /// on every machine and every run.
    /// </summary>
    public static Func<double> Random(int seed)
    {
        var state = (uint)seed;
        return () =>
        {
            state += 0x6D2B79F5u;
            var t = state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
        };
    }
}
