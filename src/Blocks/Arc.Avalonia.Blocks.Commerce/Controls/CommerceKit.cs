using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using ChartTone = Arc.Avalonia.Charts.ChartTone;

namespace Arc.Avalonia.Blocks.Commerce;

/// <summary>
/// Composition helpers shared by the commerce blocks. A block is a page level product surface, so these are
/// deliberately quiet: one surface radius, one hairline, the Arc type classes, and 4 px spacing. Every colour,
/// radius and elevation resolves through <see cref="ArcTokens"/>; there is no raw colour in this project.
/// </summary>
internal static class CommerceKit
{
    // ------------------------------------------------------------------ tokens

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color TokenColor(string key) => ArcResources.Color(key);

    public static Color TintColor(string key, double alpha)
    {
        var color = ArcResources.Color(key);
        return Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
    }

    public static IBrush Tint(string key, double alpha) => new SolidColorBrush(TintColor(key, alpha));

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    /// <summary>US dollars, invariant, so sample totals are identical on every machine.</summary>
    public static string Money(double amount, int decimals = 0) =>
        "$" + amount.ToString(decimals == 0 ? "N0" : "N" + decimals, CultureInfo.InvariantCulture);

    public static string Number(double value, int decimals = 0) =>
        value.ToString(decimals == 0 ? "N0" : "N" + decimals, CultureInfo.InvariantCulture);

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

    public static TextBlock Body(string text, double maxWidth = 520) => Text(text, "arc-body-sm arc-secondary", maxWidth);

    public static TextBlock Heading(string text, double maxWidth = 520, string classes = "arc-heading") =>
        Text(text, classes, maxWidth);

    public static TextBlock Strong(string text, double maxWidth = 0) => Text(text, "arc-body arc-medium", maxWidth);

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

    /// <summary>A grid from CSS-like column definitions; cells fill columns in order.</summary>
    public static Grid Columns(string definitions, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell is null)
            {
                continue;
            }

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
            if (cells[i] is not { } cell)
            {
                continue;
            }

            Grid.SetRow(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    public static Control Gap(double height) => new Border { Height = height };

    /// <summary>A fixed column grid with even gutters, used for product and integration grids.</summary>
    public static Grid GridOf(int columns, double gap, IReadOnlyList<Control> cells, double rowGap = 12)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)))
        };
        for (var index = 0; index < cells.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            if (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var cell = cells[index];
            cell.Margin = new Thickness(
                column == 0 ? 0 : gap / 2,
                row == 0 ? 0 : rowGap,
                column == columns - 1 ? 0 : gap / 2,
                0);
            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, row);
            grid.Children.Add(cell);
        }

        return grid;
    }

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

    // ------------------------------------------------------------------ surfaces

    /// <summary>The specimen shell: a 1000 px product surface at the gallery's content width.</summary>
    public static Border Specimen(Control content, double padding = 20, double width = 1000) => new()
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
    public static Border Card(Control content, double padding = 16, double radius = 18, bool muted = false,
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
    public static Border Tile(Control content, double padding = 12, double radius = 14, double width = 0) => new()
    {
        Width = width > 0 ? width : double.NaN,
        Background = Brush(ArcTokens.SurfaceMuted),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    public static Border Band(Control content, double padding = 14, double radius = 14, double alpha = 0.07) => new()
    {
        Background = Tint(ArcTokens.Accent, alpha),
        BorderBrush = Tint(ArcTokens.Accent, alpha + 0.08),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>
    /// A photo stand-in: a token wash with the product's glyph. Blocks ship photography in the reference; the
    /// port loads no external images, so every photo slot is a deterministic token tile instead.
    /// </summary>
    public static Border Photo(string icon, double height, double width = 0, double iconSize = 30,
        double alpha = 0.2, bool muted = false)
    {
        var tinted = alpha > 0 && !muted;
        var background = tinted
            ? (IBrush)new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(TintColor(ArcTokens.Accent, Math.Min(0.42, alpha + 0.16)), 0),
                    new GradientStop(TintColor(ArcTokens.Accent, 0.03), 1)
                }
            }
            : Brush(ArcTokens.SurfaceMuted);

        return new Border
        {
            Width = width > 0 ? width : double.NaN,
            Height = height,
            Background = tinted ? Brush(ArcTokens.SurfaceMuted) : background,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = new Border
            {
                Background = background,
                Child = new ArcIcon
                {
                    Icon = icon,
                    Size = iconSize,
                    Foreground = tinted ? Tint(ArcTokens.Accent, 0.82) : Brush(ArcTokens.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }

    /// <summary>A shared object that can be raised and lowered by a caller, used for hover photo swaps.</summary>
    public static Border HoverPhoto(string icon, string hoverIcon, double height, double width = 0,
        double iconSize = 30)
    {
        var primary = Photo(icon, height, width, iconSize);
        var alternate = Photo(hoverIcon, height, width, iconSize, alpha: 0.22);
        alternate.Opacity = 0;

        var host = new Panel { Children = { primary, alternate } };
        var frame = new Border
        {
            Width = width > 0 ? width : double.NaN,
            Height = height,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Background = Brush(ArcTokens.SurfaceMuted),
            Child = host
        };

        var fade = new Transitions { new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) } };
        alternate.Transitions = fade;
        frame.PointerEntered += (_, _) => alternate.Opacity = 1;
        frame.PointerExited += (_, _) => alternate.Opacity = 0;
        return frame;
    }

    // ------------------------------------------------------------------ controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Md, Action? onClick = null, string? icon = null,
        string? trailingIcon = null, double height = 0)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Filled buttons set the label explicitly: TextBlock carries its own foreground role, so a label
        // nested in a content panel does not inherit the button's filled-state colour.
        var ink = variant == ArcButtonVariant.Primary
            ? ArcTokens.AccentForeground
            : variant == ArcButtonVariant.Danger
                ? ArcTokens.DangerForeground
                : ArcTokens.Foreground;
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(ink)
        };

        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = icon,
                Size = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(ink)
            });
        }

        content.Children.Add(labelBlock);
        if (!string.IsNullOrWhiteSpace(trailingIcon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = trailingIcon,
                Size = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(ink)
            });
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

    /// <summary>A compact text action with no chrome, for inline "View all", "Clear" and "Edit" links.</summary>
    public static ArcButton Link(string label, Action? onClick = null, string? icon = null)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = Row(5,
                string.IsNullOrWhiteSpace(icon) ? null! : new ArcIcon { Icon = icon, Size = 13 },
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

    public static ArcIconButton IconBtn(string icon, string label, Action? onClick = null, double box = 30,
        bool muted = true)
    {
        var button = new ArcIconButton
        {
            Icon = icon,
            IconSize = 15,
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

    public static ArcAvatar Person(string name, ArcAvatarSize size = ArcAvatarSize.Sm) =>
        new() { DisplayName = name, Size = size };

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

    /// <summary>A round icon disc, the shared visual for a product glyph, topic or integration mark.</summary>
    public static Control Disc(string icon, double size = 36, double iconSize = 16, bool accent = false) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
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

    /// <summary>A square, softly rounded mark in the accent tint, for brands and integration logos.</summary>
    public static Control Mark(string icon, double size = 38, double iconSize = 18) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size * 0.32),
        Background = Tint(ArcTokens.Accent, 0.1),
        BorderBrush = Tint(ArcTokens.Accent, 0.18),
        BorderThickness = new Thickness(1),
        Child = new ArcIcon
        {
            Icon = icon,
            Size = iconSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(ArcTokens.Accent)
        }
    };

    // ------------------------------------------------------------------ fields

    public static Control Field(string label, Control control, string? hint = null, double width = 0)
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            Width = width > 0 ? width : double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(Caption(label));
        panel.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint))
        {
            panel.Children.Add(Caption(hint!));
        }

        return panel;
    }

    public static ArcInput Input(string placeholder, string? value = null, Action<string>? onChanged = null,
        string? leadingIcon = null, double width = 0)
    {
        var input = new ArcInput
        {
            Placeholder = placeholder,
            Value = value,
            LeadingIcon = leadingIcon
        };
        if (width > 0)
        {
            input.Width = width;
            input.HorizontalAlignment = HorizontalAlignment.Left;
        }

        if (onChanged is not null)
        {
            input.ValueChanged += (_, _) => onChanged(input.Value ?? string.Empty);
        }

        return input;
    }

    public static ArcSearchField Search(string placeholder, Action<string>? onChanged = null, double width = 0,
        string? value = null)
    {
        var field = new ArcSearchField { Placeholder = placeholder, Value = value };
        if (width > 0)
        {
            field.Width = width;
            field.HorizontalAlignment = HorizontalAlignment.Left;
        }

        if (onChanged is not null)
        {
            field.ValueChanged += (_, _) => onChanged(field.Value ?? string.Empty);
        }

        return field;
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

    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, string? label = null, double width = 0)
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

    public static ArcChipGroup Chips(IEnumerable<(string Value, string Label)> options, params string[] selected)
    {
        return new ArcChipGroup
        {
            Options = options.Select(option => new ArcChipOption(option.Value, option.Label)).ToList(),
            Value = selected,
            Multiple = selected.Length > 1
        };
    }

    /// <summary>
    /// A compact quantity stepper: minus, the count, plus. The reference uses a number field; inside a card row
    /// this 92 px control keeps the surface readable and the same three affordances.
    /// </summary>
    public static Control Qty(int value, Action<int> onChanged, int min = 0, int max = 9,
        string label = "Quantity")
    {
        var count = new TextBlock
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            Classes = { "arc-body-sm", "arc-medium" },
            Width = 22,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var current = value;
        var minus = IconBtn("minus", $"Decrease {label.ToLowerInvariant()}", () =>
        {
            if (current <= min)
            {
                return;
            }

            current--;
            count.Text = current.ToString(CultureInfo.InvariantCulture);
            onChanged(current);
        }, box: 26);

        var plus = IconBtn("plus", $"Increase {label.ToLowerInvariant()}", () =>
        {
            if (current >= max)
            {
                return;
            }

            current++;
            count.Text = current.ToString(CultureInfo.InvariantCulture);
            onChanged(current);
        }, box: 26);

        return new Border
        {
            Background = Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(3),
            Child = Row(2, minus, count, plus),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    // ------------------------------------------------------------------ readouts

    /// <summary>A label left, value right row for summary lines.</summary>
    public static Control Line(string label, string value, bool strong = false, string? note = null,
        bool accent = false)
    {
        var left = new StackPanel { Spacing = 1 };
        left.Children.Add(strong ? Strong(label) : Body(label, 300));
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
    public static Control Stat(string label, string value, string? note = null)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Caption(label));
        panel.Children.Add(Text(value, "arc-subtitle"));
        if (!string.IsNullOrWhiteSpace(note))
        {
            panel.Children.Add(Caption(note!));
        }

        return panel;
    }

    /// <summary>A horizontal filter chip: selected chips take the control fill, not the accent.</summary>
    public static ArcButton Chip(string label, bool selected, Action onToggle, string? count = null)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "arc-body-sm" },
            Foreground = selected ? Brush(ArcTokens.ControlGlyph) : Brush(ArcTokens.Foreground)
        };
        var countBlock = string.IsNullOrWhiteSpace(count)
            ? null
            : new TextBlock
            {
                Text = count,
                Classes = { "arc-caption" },
                Foreground = selected ? Brush(ArcTokens.ControlGlyph) : Brush(ArcTokens.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };

        var content = Row(6, labelBlock, countBlock!);

        return new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = content,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(11, 0, 11, 0),
            CornerRadius = new CornerRadius(999),
            VerticalContentAlignment = VerticalAlignment.Center
        }.With(button =>
        {
            if (selected)
            {
                button.Background = Brush(ArcTokens.ControlFill);
                button.BorderBrush = Brush(ArcTokens.BorderStrong);
            }
            else
            {
                button.Background = Brush(ArcTokens.SurfaceMuted);
                button.BorderBrush = Brush(ArcTokens.BorderSubtle);
            }

            button.Click += (_, _) => onToggle();
        });
    }

    /// <summary>A colour swatch toggle. Colours come from tokens; selection is a ring, not a second hue.</summary>
    public static ArcButton Swatch(IBrush brush, bool selected, Action onToggle, string name)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = brush,
                BorderBrush = Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1)
            },
            Width = 32,
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(16),
            BorderBrush = selected ? Brush(ArcTokens.Accent) : Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(selected ? 2 : 1)
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => onToggle();
        return button;
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

    public static ArcSparkline Spark(IReadOnlyList<double> values, double height = 36,
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

    // ------------------------------------------------------------------ flow

    /// <summary>A small two or three step progress header used by the multi step blocks.</summary>
    public static Control Steps(IReadOnlyList<string> labels, int current)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var index = 0; index < labels.Count; index++)
        {
            var done = index < current;
            var active = index == current;
            var text = new TextBlock
            {
                Text = labels[index],
                Classes = { "arc-caption" },
                Foreground = active ? Brush(ArcTokens.Accent) : done ? Brush(ArcTokens.Foreground) : Brush(ArcTokens.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };

            var chip = new Border
            {
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(999),
                Background = active ? Tint(ArcTokens.Accent, 0.14) : done ? Tint(ArcTokens.Success, 0.12) : Brush(ArcTokens.SurfaceMuted),
                Child = Row(5,
                    done
                        ? new ArcIcon { Icon = "check", Size = 11, Foreground = Brush(ArcTokens.Success) }
                        : new Border
                        {
                            Width = 5,
                            Height = 5,
                            CornerRadius = new CornerRadius(3),
                            Background = active ? Brush(ArcTokens.Accent) : Brush(ArcTokens.TextMuted)
                        },
                    text)
            };
            row.Children.Add(chip);
            if (index < labels.Count - 1)
            {
                row.Children.Add(new Border
                {
                    Width = 10,
                    Height = 1,
                    Background = Brush(ArcTokens.BorderSubtle),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }

        return row;
    }
}

/// <summary>A tiny fluent helper so a control can be configured inside an object initializer.</summary>
internal static class ControlExtensions
{
    public static T With<T>(this T control, Action<T> configure)
    {
        configure(control);
        return control;
    }
}
