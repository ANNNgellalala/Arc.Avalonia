using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.AI;

/// <summary>
/// Composition helpers shared by the AI blocks. Blocks are page-level product surfaces, so they are built
/// from ordinary Arc controls plus these quiet layout and formatting helpers: one surface radius, one
/// hairline, the shared type scale and the 4 px grid. Every colour, radius, size and font comes from
/// <see cref="ArcTokens"/>; nothing here hard-codes a hex value, a brand colour or a pixel font size.
/// </summary>
internal static class AiKit
{
    // ------------------------------------------------------------------ tokens

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color TokenColor(string key) => ArcResources.Color(key);

    public static double Size(string key) => ArcResources.Double(key);

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    /// <summary>A token colour at reduced alpha, for washes, tints and status fills.</summary>
    public static IBrush Tint(string key, double alpha)
    {
        var color = ArcResources.Color(key);
        return new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B));
    }

    public static readonly FontFamily MonoFamily = new("Consolas, Menlo, monospace");

    // ------------------------------------------------------------------ text

    public static TextBlock Text(string text, string classes = "arc-body-sm", double maxWidth = 0,
        TextWrapping wrap = TextWrapping.Wrap)
    {
        // Avalonia centres a MaxWidth-constrained element inside a wider slot; product copy always starts at
        // the leading edge, so every text helper sets that explicitly.
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

    public static TextBlock Body(string text, double maxWidth = 560) => Text(text, "arc-body-sm arc-secondary", maxWidth);

    public static TextBlock Heading(string text, double maxWidth = 520, string classes = "arc-subtitle") =>
        Text(text, classes, maxWidth);

    public static TextBlock Title(string text, double maxWidth = 640) => Text(text, "arc-title-sm", maxWidth);

    public static TextBlock Mono(string text, double fontSize = 12, string classes = "arc-secondary")
    {
        var block = Text(text, classes, 0, TextWrapping.NoWrap);
        block.FontFamily = MonoFamily;
        block.FontSize = fontSize;
        return block;
    }

    /// <summary>A truncated single-line label that stays inside its grid column.</summary>
    public static TextBlock Ellipsis(string text, string classes = "arc-body-sm", double maxWidth = 0)
    {
        var block = Text(text, classes, maxWidth, TextWrapping.NoWrap);
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        return block;
    }

    // ------------------------------------------------------------------ layout

    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var child in children)
        {
            if (child is not null)
            {
                panel.Children.Add(child);
            }
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
        foreach (var child in children)
        {
            if (child is not null)
            {
                panel.Children.Add(child);
            }
        }

        return panel;
    }

    public static WrapPanel RowWrap(double spacing, params Control[] children)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is null)
            {
                continue;
            }

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

    public static Grid Columns(string definitions, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is null)
            {
                continue;
            }

            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        return grid;
    }

    public static Grid Rows(string definitions, params Control[] cells)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is null)
            {
                continue;
            }

            Grid.SetRow(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        return grid;
    }

    public static Grid Cell(Control content, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        var grid = new Grid();
        Grid.SetRow(content, row);
        Grid.SetColumn(content, column);
        Grid.SetRowSpan(content, rowSpan);
        Grid.SetColumnSpan(content, columnSpan);
        grid.Children.Add(content);
        return grid;
    }

    // ------------------------------------------------------------------ surfaces

    /// <summary>The shell of a block specimen: panel radius, hairline border, clipped content.</summary>
    public static Border Surface(Control content, Thickness? padding = null, double radius = 26, bool muted = false)
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

    /// <summary>A card inside a block. A card nests one radius step smaller than its surface.</summary>
    public static Border Panel(Control content, double padding = 16, double radius = 18, bool muted = false)
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

    /// <summary>A borderless quiet inset, for code, quoted evidence and inline fields.</summary>
    public static Border Inset(Control content, double padding = 12, double radius = 14, string token = ArcTokens.SurfaceMuted)
    {
        return new Border
        {
            Background = Brush(token),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(padding),
            Child = content
        };
    }

    public static Border TintedInset(Control content, string token, double alpha = 0.08, double padding = 12, double radius = 14)
    {
        return new Border
        {
            Background = Tint(token, alpha),
            BorderBrush = Tint(token, Math.Min(1, alpha + 0.16)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(padding),
            Child = content
        };
    }

    /// <summary>A scroll host with a fixed height, so a block never grows while its content streams.</summary>
    public static ScrollViewer Scroller(Control content, double height, double paddingRight = 4)
    {
        return new ScrollViewer
        {
            Height = height,
            Content = content,
            Padding = new Thickness(0, 0, paddingRight, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    // ------------------------------------------------------------------ controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Sm, Action? onClick = null, string? icon = null,
        string? trailingIcon = null, bool enabled = true)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconBrush = variant switch
        {
            ArcButtonVariant.Primary => Brush(ArcTokens.AccentForeground),
            ArcButtonVariant.Danger => Brush(ArcTokens.DangerForeground),
            _ => Brush(ArcTokens.TextSecondary)
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = icon,
                Size = 14,
                Foreground = iconBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
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
            IsEnabled = enabled,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        AutomationProperties.SetName(button, label);
        return button;
    }

    public static ArcButton IconBtn(string icon, string label, Action? onClick = null,
        ArcButtonVariant variant = ArcButtonVariant.Ghost, bool pressed = false, bool enabled = true, double size = 16)
    {
        var button = new ArcButton
        {
            Variant = variant,
            Size = ArcButtonSize.Sm,
            Compact = true,
            IsEnabled = enabled,
            Content = new ArcIcon
            {
                Icon = icon,
                Size = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        AutomationProperties.SetName(button, label);
        AutomationProperties.SetHelpText(button, pressed ? "On" : null);
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>A rounded token button used for suggestion chips, filters and commands.</summary>
    public static ArcButton Chip(string label, Action? onClick = null, bool selected = false, string? icon = null,
        bool enabled = true)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = icon,
                Size = 12,
                Foreground = selected ? Brush(ArcTokens.AccentForeground) : Brush(ArcTokens.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        content.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var chip = new ArcButton
        {
            Variant = selected ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(10, 0),
            Content = content,
            IsEnabled = enabled,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(chip, label);
        if (onClick is not null)
        {
            chip.Click += (_, _) => onClick();
        }

        return chip;
    }

    public static ArcBadge Pill(string text, ArcBadgeTone tone = ArcBadgeTone.Neutral, bool dot = false) =>
        new() { Content = text, Tone = tone, Dot = dot };

    public static ArcAvatar Avatar(string name, ArcAvatarSize size = ArcAvatarSize.Sm) =>
        new() { DisplayName = name, Size = size, VerticalAlignment = VerticalAlignment.Center };

    public static ArcSwitch Toggle(string label, bool isChecked, Action<bool>? changed = null)
    {
        var toggle = new ArcSwitch { Label = label, Checked = isChecked };
        if (changed is not null)
        {
            toggle.CheckedChanged += (_, _) => changed(toggle.Checked);
        }

        return toggle;
    }

    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? changed = null, string? label = null)
    {
        var control = new ArcSegmentedControl
        {
            Options = options.Select(option => new ArcSegmentOption(option.Value, option.Label)).ToList(),
            Value = selected,
            Label = label
        };
        if (changed is not null)
        {
            control.ValueChanged += (_, _) =>
            {
                if (control.Value is { Length: > 0 } value)
                {
                    changed(value);
                }
            };
        }

        return control;
    }

    public static ArcProgress Bar(double value, double max = 100, ArcProgressSize size = ArcProgressSize.Sm,
        ArcProgressTone tone = ArcProgressTone.Accent, bool showValue = false, string? label = null)
    {
        return new ArcProgress
        {
            Value = value,
            Max = max,
            Size = size,
            Tone = tone,
            ShowValue = showValue,
            Label = label
        };
    }

    /// <summary>A hand-drawn square meter for dense per-case grids, where a full progress control is too tall.</summary>
    public static Control Square(double fraction, string token, double size = 7)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(2),
            Background = fraction <= 0 ? Tint(token, 0.18) : Brush(token),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Control KeyCap(string text)
    {
        return new Border
        {
            Background = Brush(ArcTokens.SurfaceMuted),
            BorderBrush = Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 1, 5, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Classes = { "arc-muted" },
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    // ------------------------------------------------------------------ marks

    private static (string Letter, string Token) ProviderMark(string provider) => provider switch
    {
        "anthropic" => ("C", ArcTokens.Series1),
        "openai" => ("O", ArcTokens.Series2),
        "google" => ("G", ArcTokens.Series3),
        _ => ("M", ArcTokens.Series4)
    };

    /// <summary>
    /// A model or provider mark. The port has no brand glyphs (Lucide is a monochrome line set), so marks are
    /// monogram discs tinted with the series tokens; the model name always sits beside the mark in text.
    /// </summary>
    public static Control Mark(string provider, double size = 22)
    {
        var (letter, token) = ProviderMark(provider);
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.32),
            Background = Tint(token, 0.16),
            BorderBrush = Tint(token, 0.3),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = letter,
                FontSize = size * 0.46,
                FontWeight = FontWeight.Medium,
                Foreground = Brush(token),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    /// <summary>A round icon disc, for tools, connectors and agents.</summary>
    public static Control Disc(string icon, double size = 26, bool accent = false, double iconSize = 14,
        string? token = null)
    {
        var tint = token ?? (accent ? ArcTokens.Accent : ArcTokens.TextMuted);
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = accent ? Tint(ArcTokens.Accent, 0.14) : Brush(ArcTokens.SurfaceMuted),
            BorderBrush = accent ? Tint(ArcTokens.Accent, 0.28) : Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new ArcIcon
            {
                Icon = icon,
                Size = iconSize,
                Foreground = Brush(tint),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    public static Control Meta(string icon, string text, string classes = "arc-caption")
    {
        return Row(5,
            new ArcIcon { Icon = icon, Size = 12, VerticalAlignment = VerticalAlignment.Center },
            Text(text, classes));
    }

    public static Control Dot(string token, double size = 6)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = Brush(token),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>An inline variable token for a prompt template, in one of the four series colours.</summary>
    public static Control Token(string text, int slot)
    {
        var token = slot switch
        {
            1 => ArcTokens.Series2,
            2 => ArcTokens.Series3,
            3 => ArcTokens.Series4,
            _ => ArcTokens.Series1
        };
        return new Border
        {
            Background = Tint(token, 0.14),
            BorderBrush = Tint(token, 0.32),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(1, 2, 1, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontFamily = MonoFamily,
                Foreground = Brush(token),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    public static string SeriesToken(int index) => index switch
    {
        1 => ArcTokens.Series2,
        2 => ArcTokens.Series3,
        3 => ArcTokens.Series4,
        _ => ArcTokens.Series1
    };

    // ------------------------------------------------------------------ composite parts

    /// <summary>
    /// The model chip shared by the AI surfaces: provider mark, short model name, optional effort and a
    /// chevron. Callers swap the model in place instead of rebuilding the button, so its width follows the
    /// new label without snapping.
    /// </summary>
    internal sealed class ModelChip
    {
        private readonly ContentControl _mark = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock? _effort;

        public ModelChip(AiModel model, Action? onClick = null, bool showEffort = false, string effort = "high")
        {
            _mark.Content = Mark(model.Provider, 16);
            _label.Text = model.Short;
            var row = Row(6, _mark, _label);
            if (showEffort)
            {
                _effort = Caption(effort);
                row.Children.Add(_effort);
            }

            row.Children.Add(new ArcIcon { Icon = "chevron-down", Size = 12, VerticalAlignment = VerticalAlignment.Center });
            Button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Padding = new Thickness(8, 0, 8, 0),
                Content = row,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(Button, $"Model: {model.Name}. Change model");
            if (onClick is not null)
            {
                Button.Click += (_, _) => onClick();
            }
        }

        public ArcButton Button { get; }

        public void Set(AiModel model, string? effort = null)
        {
            _mark.Content = Mark(model.Provider, 16);
            _label.Text = model.Short;
            if (_effort is not null && effort is not null)
            {
                _effort.Text = effort;
            }

            AutomationProperties.SetName(Button, $"Model: {model.Name}. Change model");
        }
    }

    // ------------------------------------------------------------------ markdown and inline text

    /// <summary>
    /// A small Markdown-subset paragraph: **bold**, `inline code` and [n] citation chips. Citations become
    /// focusable chips that call <paramref name="onCitation"/>, so a page can light the sentence they support.
    /// </summary>
    public static TextBlock Paragraph(string text, Action<int>? onCitation = null, string classes = "arc-body-sm",
        double maxWidth = 0, double lineHeight = 21)
    {
        var block = Text(string.Empty, classes, maxWidth);
        block.LineHeight = lineHeight;
        AddInline(block, text, onCitation);
        return block;
    }

    /// <summary>A body block: a paragraph, a bulleted list, a short heading or a fenced code block.</summary>
    public static Control Markdown(string markdown, Action<int>? onCitation = null, double maxWidth = 0)
    {
        var stack = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Stretch };
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var code = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.Add(lines[i]);
                    i++;
                }

                stack.Children.Add(new ArcCodeBlock
                {
                    Code = string.Join("\n", code),
                    Language = trimmed.Length > 3 ? trimmed[3..].Trim() : "text",
                    MaxLines = 8
                });
                continue;
            }

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                stack.Children.Add(Text(trimmed[4..], "arc-body arc-medium", maxWidth));
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                stack.Children.Add(Text(trimmed[3..], "arc-heading", maxWidth));
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                stack.Children.Add(Bullet("•", trimmed[2..], onCitation, maxWidth));
                continue;
            }

            var numbered = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
            if (numbered.Success)
            {
                stack.Children.Add(Bullet(numbered.Groups[1].Value + ".", numbered.Groups[2].Value, onCitation, maxWidth));
                continue;
            }

            stack.Children.Add(Paragraph(trimmed, onCitation, "arc-body-sm", maxWidth));
        }

        return stack;
    }

    private static Control Bullet(string marker, string text, Action<int>? onCitation, double maxWidth)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
        var bullet = Text(marker, "arc-body-sm arc-muted");
        bullet.VerticalAlignment = VerticalAlignment.Top;
        row.Children.Add(bullet);
        var paragraph = Paragraph(text, onCitation, "arc-body-sm", maxWidth);
        Grid.SetColumn(paragraph, 1);
        row.Children.Add(paragraph);
        return row;
    }

    private static void AddInline(TextBlock block, string text, Action<int>? onCitation)
    {
        var plain = new System.Text.StringBuilder();

        void Flush()
        {
            if (plain.Length == 0)
            {
                return;
            }

            block.Inlines?.Add(new Run { Text = plain.ToString() });
            plain.Clear();
        }

        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];

            if (current == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end > index + 1)
                {
                    Flush();
                    block.Inlines?.Add(new Run
                    {
                        Text = text[(index + 1)..end],
                        FontFamily = MonoFamily,
                        FontSize = 12.5,
                        Foreground = Brush(ArcTokens.AccentStrong)
                    });
                    index = end + 1;
                    continue;
                }
            }

            if (current == '*' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end > index + 1)
                {
                    Flush();
                    block.Inlines?.Add(new Run
                    {
                        Text = text[(index + 2)..end],
                        FontWeight = FontWeight.Medium,
                        Foreground = Brush(ArcTokens.Foreground)
                    });
                    index = end + 2;
                    continue;
                }
            }

            if (current == '[' && index + 1 < text.Length && char.IsDigit(text[index + 1]))
            {
                var close = text.IndexOf(']', index + 1);
                if (close > index && int.TryParse(text[(index + 1)..close], out var number))
                {
                    Flush();
                    block.Inlines?.Add(new InlineUIContainer { Child = Citation(number, onCitation), BaselineAlignment = BaselineAlignment.Center });
                    index = close + 1;
                    continue;
                }
            }

            plain.Append(current);
            index++;
        }

        Flush();
    }

    /// <summary>An inline citation chip: a focusable tinted number that lights its source on activation.</summary>
    public static Control Citation(int number, Action<int>? onCitation)
    {
        var chip = new Border
        {
            Background = Tint(ArcTokens.Accent, 0.12),
            BorderBrush = Tint(ArcTokens.Accent, 0.28),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Height = 15,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(2, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Child = new TextBlock
            {
                Text = number.ToString(),
                FontSize = 10,
                LineHeight = 13,
                Foreground = Brush(ArcTokens.AccentStrong),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        AutomationProperties.SetName(chip, $"Source {number}");
        if (onCitation is not null)
        {
            chip.PointerPressed += (_, _) => onCitation(number);
            chip.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Enter or Key.Space)
                {
                    onCitation(number);
                    args.Handled = true;
                }
            };
        }

        return chip;
    }

    /// <summary>A source card: number, title, domain and an optional read percentage.</summary>
    public static Control SourceCard(int number, string title, string domain, string initial = "", double read = 1,
        string? token = null)
    {
        var tint = token ?? SeriesToken(number - 1);
        var head = Row(7,
            new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(6),
                Background = Tint(tint, 0.14),
                Child = new TextBlock
                {
                    Text = initial.Length > 0 ? initial : domain[..1].ToUpperInvariant(),
                    FontSize = 10,
                    FontWeight = FontWeight.Medium,
                    Foreground = Brush(tint),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            },
            Ellipsis(title, "arc-body-sm", 190));

        var footer = Row(6, Caption(domain));
        if (read >= 1)
        {
            footer.Children.Add(Dot(ArcTokens.BorderStrong, 2.5));
            footer.Children.Add(Caption("Read"));
        }
        else
        {
            footer.Children.Add(Dot(ArcTokens.BorderStrong, 2.5));
            footer.Children.Add(Caption($"{read * 100:0}%"));
        }

        var body = Stack(3, head, footer);
        return new Border
        {
            Background = Brush(ArcTokens.SurfaceMuted),
            BorderBrush = Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8, 10, 8),
            Child = body
        };
    }

    /// <summary>One line of a unified diff, tinted with the status tokens.</summary>
    public static Control DiffLine(string kind, string text)
    {
        var (marker, token, alpha) = kind switch
        {
            "add" => ("+", ArcTokens.Success, 0.12),
            "del" => ("-", ArcTokens.Danger, 0.12),
            _ => (" ", ArcTokens.TextMuted, 0)
        };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*") };
        var sign = new TextBlock
        {
            Text = marker,
            FontFamily = MonoFamily,
            FontSize = 11.5,
            Foreground = Brush(kind == "ctx" ? ArcTokens.TextMuted : token),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(sign);
        var code = new TextBlock
        {
            Text = text,
            FontFamily = MonoFamily,
            FontSize = 11.5,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = Brush(ArcTokens.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(code, 1);
        row.Children.Add(code);
        return new Border
        {
            Background = alpha > 0 ? Tint(token, alpha) : Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 1),
            Child = row
        };
    }

    /// <summary>A word-level diff line, so a change reads without colour alone.</summary>
    public static TextBlock WordDiff(string text, Action<int>? onCitation = null)
    {
        var block = Text(string.Empty, "arc-body-sm", 0);
        block.LineHeight = 21;
        var parts = text.Split('|');
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (part.StartsWith("+", StringComparison.Ordinal))
            {
                block.Inlines?.Add(new Run
                {
                    Text = part[1..] + " ",
                    Foreground = Brush(ArcTokens.Success),
                    Background = Tint(ArcTokens.Success, 0.14),
                    TextDecorations = TextDecorations.Underline
                });
            }
            else if (part.StartsWith("-", StringComparison.Ordinal))
            {
                block.Inlines?.Add(new Run
                {
                    Text = part[1..] + " ",
                    Foreground = Brush(ArcTokens.Danger),
                    TextDecorations = TextDecorations.Strikethrough
                });
            }
            else
            {
                block.Inlines?.Add(new Run { Text = part + " " });
            }
        }

        return block;
    }

    // ------------------------------------------------------------------ data

    internal sealed record AiModel(string Id, string Name, string Short, string Provider, string Note);

    /// <summary>
    /// Sample model lineup. Names follow the models the public Arc documentation uses in its examples; they
    /// are sample content, not an endorsement, and prices elsewhere on these pages are illustrative.
    /// </summary>
    internal static readonly AiModel[] Models =
    {
        new("claude-opus-5-5", "Claude Opus 5.5", "Opus 5.5", "anthropic", "Deepest reasoning for hard problems"),
        new("claude-sonnet-5-5", "Claude Sonnet 5.5", "Sonnet 5.5", "anthropic", "Balanced speed and depth"),
        new("claude-haiku-5-5", "Claude Haiku 5.5", "Haiku 5.5", "anthropic", "Fastest answers at the lowest cost"),
        new("gpt-6-1-sol", "GPT-6.1 Sol", "GPT-6.1", "openai", "Broad general knowledge"),
        new("gemini-4-argon", "Gemini 4 Argon", "Gemini 4", "google", "Long context with strong recall"),
        new("mistral-large-4", "Mistral Large 4", "Large 4", "mistral", "Open weights, EU hosted")
    };

    public static AiModel Model(string id) => Models.FirstOrDefault(model => model.Id == id) ?? Models[0];

    public static string FormatTokens(double tokens) =>
        tokens >= 1000 ? (tokens / 1000).ToString("0.0") + "k" : tokens.ToString("0");
}
