using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Blocks.Marketing.Controls;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Marketing;

/// <summary>
/// Composition helpers shared by the marketing blocks. Blocks are page-level compositions, so they are built
/// from ordinary Arc controls plus these quiet layout helpers: one surface radius, one hairline, one type
/// scale, and 4 px spacing. Everything resolves through <see cref="ArcTokens"/>; nothing here hard-codes a
/// colour, radius or font size.
/// </summary>
internal static class MarketingKit
{
    // ---------------------------------------------------------------- tokens

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color TokenColor(string key) => ArcResources.Color(key);

    public static double Size(string key) => ArcResources.Double(key);

    /// <summary>A token colour at reduced alpha, for washes and decorative marks.</summary>
    public static IBrush Tint(string key, double alpha)
    {
        var color = ArcResources.Color(key);
        return new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B));
    }

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    // ---------------------------------------------------------------- text

    public static TextBlock Text(string text, string classes = "arc-body-sm", double maxWidth = 0,
        TextWrapping wrap = TextWrapping.Wrap)
    {
        var block = new TextBlock { Text = text, TextWrapping = wrap, HorizontalAlignment = HorizontalAlignment.Left };
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

    public static TextBlock Body(string text, double maxWidth = 560) => Text(text, "arc-body-sm arc-secondary", maxWidth);

    public static TextBlock Heading(string text, double maxWidth = 520, string classes = "arc-subtitle") =>
        Text(text, classes, maxWidth);

    public static TextBlock Title(string text, double maxWidth = 640) => Text(text, "arc-title-sm", maxWidth);

    // ---------------------------------------------------------------- layout

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

    public static Control Spacer(double height) => new Border { Height = height };

    public static Border Divider(double margin = 0) => new()
    {
        Height = 1,
        Background = Brush(ArcTokens.BorderSubtle),
        Margin = new Thickness(0, margin, 0, margin),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static Border Hairline() => new()
    {
        Width = 1,
        Background = Brush(ArcTokens.BorderSubtle),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    /// <summary>A column with a fixed width; grids of blocks are laid out with explicit columns.</summary>
    public static Grid Columns(string definitions, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    public static Grid Rows(string definitions, params Control[] cells)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            Grid.SetRow(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    // ---------------------------------------------------------------- surfaces

    /// <summary>
    /// The outer shell of a block specimen: one panel radius, a hairline border and a clipped surface, so a
    /// page-level composition reads as one product surface at gallery width.
    /// </summary>
    public static Border Surface(Control content, bool muted = false, Thickness? padding = null, double radius = 26)
    {
        return new Border
        {
            Background = Brush(muted ? ArcTokens.SurfaceMuted : ArcTokens.Surface),
            BorderBrush = Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = true,
            Padding = padding ?? default,
            Child = content
        };
    }

    /// <summary>A card inside a block. Cards nest a smaller radius than the surface that holds them.</summary>
    public static Border Panel(Control content, double padding = 20, double radius = 18, bool muted = false)
    {
        return new Border
        {
            Background = Brush(muted ? ArcTokens.SurfaceMuted : ArcTokens.Surface),
            BorderBrush = Brush(muted ? ArcTokens.BorderSubtle : ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(padding),
            Child = content
        };
    }

    /// <summary>A quiet tinted strip, for bands that sit inside a block without a border.</summary>
    public static Border Band(Control content, double padding = 20, double radius = 18, double alpha = 0.06)
    {
        return new Border
        {
            Background = Tint(ArcTokens.Accent, alpha),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(padding),
            Child = content
        };
    }

    /// <summary>An approximation of a mesh gradient: a token wash with three soft radial blooms.</summary>
    public static Control Wash(Control content, double padding = 0, double radius = 26, bool drift = false,
        double intensity = 1)
    {
        var host = new Panel();
        host.Children.Add(new ArcMeshWash { Drift = drift, Intensity = intensity });
        host.Children.Add(new Border
        {
            Padding = new Thickness(padding),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        });

        return new Border
        {
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = true,
            Child = host
        };
    }

    /// <summary>
    /// A website specimen frame: a thin browser strip over the block, so header, footer and hero blocks read
    /// in the context they ship in. The strip is decorative and hidden from assistive technology.
    /// </summary>
    public static Control Frame(string url, Control content, bool chrome = true)
    {
        var body = new DockPanel { LastChildFill = true };
        if (chrome)
        {
            var strip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var _ in Enumerable.Range(0, 3))
            {
                strip.Children.Add(new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(4),
                    Background = Brush(ArcTokens.BorderStrong),
                    Opacity = 0.55,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            strip.Children.Add(new Border { Width = 10 });
            strip.Children.Add(new ArcIcon { Icon = "lock", Size = 11, VerticalAlignment = VerticalAlignment.Center });
            strip.Children.Add(Caption(url));

            var bar = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                Background = Brush(ArcTokens.SurfaceMuted),
                BorderBrush = Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = strip
            };
            DockPanel.SetDock(bar, Dock.Top);
            body.Children.Add(bar);
        }

        body.Children.Add(content);

        return new Border
        {
            Background = Brush(ArcTokens.Surface),
            BorderBrush = Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Child = body
        };
    }

    // ---------------------------------------------------------------- controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Md, Action? onClick = null, string? icon = null,
        string? trailingIcon = null)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon { Icon = icon, Size = 15, VerticalAlignment = VerticalAlignment.Center });
        }

        content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrWhiteSpace(trailingIcon))
        {
            content.Children.Add(new ArcIcon { Icon = trailingIcon, Size = 15, VerticalAlignment = VerticalAlignment.Center });
        }

        var button = new ArcButton
        {
            Variant = variant,
            Size = size,
            Content = content,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    public static ArcButton IconBtn(string icon, string label, Action? onClick = null,        ArcButtonVariant variant = ArcButtonVariant.Ghost)
    {
        var button = new ArcButton
        {
            Variant = variant,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Content = new ArcIcon { Icon = icon, Size = 16, HorizontalAlignment = HorizontalAlignment.Center }
        };
        AutomationProperties.SetName(button, label);        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>A quiet text link aligned to the start of its column, for footer and list groups.</summary>
    public static ArcButton Link(string label, Action? onClick = null, ArcButtonSize size = ArcButtonSize.Sm)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = size,
            Content = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
            Padding = new Thickness(0),
            Height = 26,
            MinHeight = 26,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    public static ArcBadge Pill(string text, ArcBadgeTone tone = ArcBadgeTone.Neutral, bool dot = false) =>
        new() { Content = text, Tone = tone, Dot = dot };

    public static ArcAvatar Avatar(string name, ArcAvatarSize size = ArcAvatarSize.Sm) =>
        new() { DisplayName = name, Size = size };

    /// <summary>A small overlapping member group, used where a block shows people.</summary>
    public static ArcAvatarGroup AvatarGroup(IEnumerable<string> names, ArcAvatarSize size = ArcAvatarSize.Xs,
        int max = 4, string label = "Members")
    {
        var group = new ArcAvatarGroup
        {
            Members = names.Select(name => new ArcAvatarMember { Name = name }).ToList(),
            Max = max,
            Size = size,
            Label = label
        };
        return group;
    }

    public static ArcInput Input(string placeholder, string? value = null, string? leadingIcon = null,
        double width = 0, string? label = null)
    {
        var input = new ArcInput
        {
            Placeholder = placeholder,
            Value = value,
            LeadingIcon = leadingIcon,
            Label = label
        };
        if (width > 0)
        {
            input.Width = width;
        }

        return input;
    }

    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, string? label = null)
    {
        return Segmented(
            options.Select(option => (option.Value, option.Label, (string?)null)),
            selected,
            onChanged,
            label);
    }

    /// <summary>A segmented view switch with optional trailing counts or notes per segment.</summary>
    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label, string? Accessory)> options,
        string? selected, Action<string>? onChanged = null, string? label = null)
    {
        var control = new ArcSegmentedControl
        {
            Options = options.Select(option => new ArcSegmentOption(option.Value, option.Label, option.Accessory)).ToList(),
            Value = selected,
            Label = label
        };
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

    public static ArcChipGroup Chips(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null)
    {
        var control = new ArcChipGroup
        {
            Options = options.Select(option => new ArcChipOption { Value = option.Value, Label = option.Label }).ToList(),
            Value = selected is null ? null : new[] { selected }
        };
        if (onChanged is not null)
        {
            control.ValueChanged += (_, _) =>
            {
                if (control.Value is { Count: > 0 } values)
                {
                    onChanged(values[0]);
                }
            };
        }

        return control;
    }

    /// <summary>A labelled icon in the shared muted tone, for metadata rows.</summary>
    public static Control Meta(string icon, string text, string classes = "arc-caption")
    {
        return Row(6,
            new ArcIcon { Icon = icon, Size = 13, VerticalAlignment = VerticalAlignment.Center },
            Text(text, classes));
    }

    /// <summary>A round icon disc used in feature cells and support channels.</summary>
    public static Control Disc(string icon, double size = 34, double iconSize = 16, bool accent = false)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = accent ? Tint(ArcTokens.Accent, 0.12) : Brush(ArcTokens.SurfaceMuted),
            BorderBrush = accent ? Tint(ArcTokens.Accent, 0.24) : Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            Child = new ArcIcon
            {
                Icon = icon,
                Size = iconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = accent ? Brush(ArcTokens.Accent) : Brush(ArcTokens.TextSecondary)
            }
        };
    }

    public static Control Logo(string name, string icon = "layers", double markSize = 26, double fontSize = 15)
    {
        var mark = new Border
        {
            Width = markSize,
            Height = markSize,
            CornerRadius = new CornerRadius(markSize * 0.34),
            Background = Brush(ArcTokens.Accent),
            Child = new ArcIcon
            {
                Icon = icon,
                Size = markSize * 0.56,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(ArcTokens.AccentForeground)
            }
        };

        return Row(9, mark, new TextBlock
        {
            Text = name,
            FontSize = fontSize,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    /// <summary>A small metric group inside a block: label, value and a sparkline.</summary>
    public static Control Spark(IReadOnlyList<double> values, double height = 38, bool area = true)
    {
        return new ArcSparkline
        {
            Values = values,
            Height = height,
            ShowArea = area,
            ShowLastPoint = true
        };
    }
}
