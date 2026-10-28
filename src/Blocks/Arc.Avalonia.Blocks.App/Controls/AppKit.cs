using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App;

/// <summary>
/// Composition helpers shared by the app-shell blocks. A block here is a product surface: one screen
/// frame at the gallery's content width, a quiet 1 px hairline instead of shadows except on floating
/// layers, the Arc type classes, and 4 px spacing. Every colour, radius and elevation resolves through
/// <see cref="ArcTokens"/>; there is no raw colour anywhere in this project.
/// </summary>
internal static class AppKit
{
    // ------------------------------------------------------------------ tokens

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color TokenColor(string key) => ArcResources.Color(key);

    public static Color TintColor(string key, double alpha)
    {
        var color = TokenColor(key);
        return Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
    }

    public static IBrush Tint(string key, double alpha) => new SolidColorBrush(TintColor(key, alpha));

    public static BoxShadows Shadow(string key = ArcTokens.ShadowFloating) => ArcResources.Shadows(key);

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

    public static TextBlock Mono(string text) => Text(text, "arc-caption arc-mono");

    // ------------------------------------------------------------------ layout

    public static StackPanel Stack(double spacing, params Control?[] children)
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

    public static StackPanel Row(double spacing, params Control?[] children)
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
            child.Margin = new Thickness(0, 0, spacing, spacing);
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A grid from CSS-like column definitions; cells fill columns in order.</summary>
    public static Grid Columns(string definitions, params Control?[] cells)
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

    public static Grid Rows(string definitions, params Control?[] cells)
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

    public static Control Space(double height) => new Border { Height = height };

    public static Control Grow() => new Border { HorizontalAlignment = HorizontalAlignment.Stretch };

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

    public static Control Spacer()
    {
        return new Border { Width = 0 };
    }

    // ------------------------------------------------------------------ surfaces

    /// <summary>
    /// The block frame: a 1000 px product surface at the gallery's content width, with a fixed height so
    /// every page keeps inside the 1400 px shot budget. Content is clipped to the surface radius.
    /// </summary>
    public static Border Screen(Control content, double height, double width = 1000, double radius = 26,
        bool clip = true) => new()
    {
        Width = width,
        Height = height,
        Background = Brush(ArcTokens.Surface),
        BorderBrush = Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        ClipToBounds = clip,
        Child = content
    };

    /// <summary>A card inside a block: the 18 px panel radius nests inside the 26 px surface.</summary>
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
    public static Border Tile(Control content, double padding = 12, double radius = 14, double width = 0,
        bool hairline = false) => new()
    {
        Width = width > 0 ? width : double.NaN,
        Background = Brush(ArcTokens.SurfaceMuted),
        BorderBrush = hairline ? Brush(ArcTokens.BorderSubtle) : null,
        BorderThickness = hairline ? new Thickness(1) : default,
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    public static Border AccentBand(Control content, double padding = 14, double radius = 14, double alpha = 0.07) => new()
    {
        Background = Tint(ArcTokens.Accent, alpha),
        BorderBrush = Tint(ArcTokens.Accent, alpha + 0.08),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>
    /// A floating layer (palette, popover, save bar, selection bar): the one place that may carry an
    /// elevation shadow, at the panel radius inside the frame.
    /// </summary>
    public static Border Floating(Control content, double padding = 10, double radius = 18, double width = 0,
        IBrush? background = null) => new()
    {
        Width = width > 0 ? width : double.NaN,
        Background = background ?? Brush(ArcTokens.SurfaceRaised),
        BorderBrush = Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        BoxShadow = Shadow(),
        Padding = new Thickness(padding),
        ClipToBounds = false,
        Child = content
    };

    /// <summary>A diagonal accent wash standing in for the reference's mesh gradients.</summary>
    public static IBrush Wash(double alpha = 0.28, double falloff = 0.04, bool diagonal = true)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = diagonal ? new RelativePoint(1, 1, RelativeUnit.Relative) : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(TintColor(ArcTokens.Accent, alpha), 0),
                new GradientStop(TintColor(ArcTokens.GradientTo, Math.Max(falloff, 0.02)), 1)
            }
        };
    }

    /// <summary>A brand wash using the gradient tokens, for photo stand-ins and auth backdrops.</summary>
    public static IBrush BrandWash(double from = 1, double to = 1, bool diagonal = true)
    {
        var start = TokenColor(ArcTokens.GradientFrom);
        var end = TokenColor(ArcTokens.GradientTo);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = diagonal ? new RelativePoint(1, 1, RelativeUnit.Relative) : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * Math.Clamp(from, 0, 1)), start.R, start.G, start.B), 0),
                new GradientStop(Color.FromArgb((byte)(255 * Math.Clamp(to, 0, 1)), end.R, end.G, end.B), 1)
            }
        };
    }

    /// <summary>A soft radial glow, used for the dusk and ring backdrops.</summary>
    public static IBrush Glow(string key, double alpha, double radius = 0.7)
    {
        return new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(TintColor(key, alpha), 0),
                new GradientStop(TintColor(key, 0), 1)
            }
        };
    }

    /// <summary>
    /// A photo stand-in: a token wash with a product glyph. The reference ships photography; the port
    /// loads no external images, so every photo slot is a deterministic gradient instead.
    /// </summary>
    public static Border Photo(string icon, double height, double width = 0, double iconSize = 26,
        double alpha = 0.3, bool muted = false, double radius = 14)
    {
        var background = muted ? Brush(ArcTokens.SurfaceMuted) : Wash(alpha);
        return new Border
        {
            Width = width > 0 ? width : double.NaN,
            Height = height,
            Background = background,
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = true,
            Child = new ArcIcon
            {
                Icon = icon,
                Size = iconSize,
                Foreground = muted ? Brush(ArcTokens.TextMuted) : Tint(ArcTokens.Accent, 0.8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    // ------------------------------------------------------------------ controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Md, Action? onClick = null, string? icon = null,
        string? trailingIcon = null, double height = 0, double width = 0, bool stretch = false)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // A TextBlock carries its own foreground role, so a label inside a content panel does not inherit
        // the filled button's ink; set it explicitly.
        var ink = variant == ArcButtonVariant.Primary
            ? ArcTokens.AccentForeground
            : variant == ArcButtonVariant.Danger
                ? ArcTokens.DangerForeground
                : ArcTokens.Foreground;

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

        content.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush(ink)
        });

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
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left
        };
        if (height > 0)
        {
            button.Height = height;
            button.MinHeight = height;
        }

        if (width > 0)
        {
            button.Width = width;
        }

        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>A compact text action with no chrome, for inline View all, Clear and Edit links.</summary>
    public static ArcButton Link(string label, Action? onClick = null, string? icon = null, string? trailingIcon = null)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = Row(5,
                string.IsNullOrWhiteSpace(icon) ? null : new ArcIcon { Icon = icon, Size = 13 },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
                string.IsNullOrWhiteSpace(trailingIcon) ? null : new ArcIcon { Icon = trailingIcon, Size = 13 }),
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

    public static Control Meta(string icon, string text, double iconSize = 13)
    {
        return Row(6,
            new ArcIcon
            {
                Icon = icon,
                Size = iconSize,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(ArcTokens.TextMuted)
            },
            Caption(text));
    }

    /// <summary>A round icon disc: the shared visual for a product glyph, folder or file.</summary>
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

    /// <summary>A square, softly rounded mark in the accent tint, for workspace and app marks.</summary>
    public static Control Mark(string icon, double size = 34, double iconSize = 16) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size * 0.3),
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

    /// <summary>A small status dot; always paired with text, never the only signal.</summary>
    public static Control Dot(string key = ArcTokens.Success, double size = 7) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = Brush(key),
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>
    /// A navigation row: icon, label, optional count. Selected rows take the accent tint plus the accent
    /// ink, which is the one place accent is allowed to decorate in these blocks.
    /// </summary>
    public static Border NavRow(string icon, string label, bool selected, Action? onClick = null,
        string? count = null, string? shortcut = null, bool compact = false, double iconSize = 16)
    {
        var text = new TextBlock
        {
            Text = label,
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = selected ? Brush(ArcTokens.Foreground) : Brush(ArcTokens.TextSecondary)
        };
        if (selected)
        {
            text.Classes.Add("arc-medium");
        }

        return Pressable(new Border
        {
            Background = selected ? Tint(ArcTokens.Accent, 0.12) : Brushes.Transparent,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(compact ? 7 : 9, 6, 9, 6),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 9,
                Children =
                {
                    new ArcIcon
                    {
                        Icon = icon,
                        Size = iconSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = selected ? Brush(ArcTokens.Accent) : Brush(ArcTokens.TextSecondary)
                    },
                    Place(text, 1),
                    Place(count is null
                        ? shortcut is null
                            ? (Control)new Border { Width = 0 }
                            : Mono(shortcut)
                        : Caption(count), 2)
                }
            }
        }, onClick);
    }

    /// <summary>A row that behaves like a button: hand cursor, pressed wash, keyboard activation.</summary>
    public static T Pressable<T>(T control, Action? onClick) where T : Control
    {
        if (onClick is null)
        {
            return control;
        }

        control.Cursor = new Cursor(StandardCursorType.Hand);
        control.AddHandler(InputElement.PointerPressedEvent, (_, _) => control.Opacity = 0.72, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            control.Opacity = 1;
            onClick();
        }, RoutingStrategies.Tunnel);
        control.PointerExited += (_, _) => control.Opacity = 1;
        control.Focusable = true;
        control.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Enter or Key.Space)
            {
                onClick();
                args.Handled = true;
            }
        };
        return control;
    }

    // ------------------------------------------------------------------ fields

    public static Control Field(string label, Control control, string? hint = null, double width = 0)
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            Width = width > 0 ? width : double.NaN,
            HorizontalAlignment = width > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Stretch
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
        string? value = null, string? shortcut = null)
    {
        var field = new ArcSearchField { Placeholder = placeholder, Value = value, ShortcutHint = shortcut };
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

    /// <summary>
    /// The single sign-on row shared by the auth blocks. Brand marks are not reproduced: each provider is a
    /// labelled button with a neutral glyph, which keeps the row honest and readable.
    /// </summary>
    public static Control Providers(Action<string> onProvider, double height = 36)
    {
        var providers = new (string Name, string Icon)[]
        {
            ("Google", "globe"),
            ("Apple", "circle"),
            ("GitHub", "code")
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 6 };
        for (var index = 0; index < providers.Length; index++)
        {
            var provider = providers[index];
            var button = Btn(provider.Name, ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => onProvider(provider.Name),
                icon: provider.Icon, height: height, stretch: true);
            Grid.SetColumn(button, index);
            grid.Children.Add(button);
        }

        return grid;
    }

    /// <summary>A caption plus a danger message under a field, used by the auth forms.</summary>
    public static Control FieldError(string message)
    {
        var block = Caption(message);
        block.Foreground = Brush(ArcTokens.Danger);
        block.IsVisible = !string.IsNullOrWhiteSpace(message);
        AutomationProperties.SetLiveSetting(block, AutomationLiveSetting.Polite);
        return block;
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

    public static ArcCheckbox Check(string label, bool isChecked, Action<bool>? onChanged = null,
        string? description = null)
    {
        var check = new ArcCheckbox { Label = label, Description = description, Checked = isChecked };
        if (onChanged is not null)
        {
            check.CheckedChanged += (_, _) => onChanged(check.Checked == true);
        }

        return check;
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
            control.HorizontalAlignment = HorizontalAlignment.Left;
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
            control.HorizontalAlignment = HorizontalAlignment.Left;
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

    /// <summary>A horizontal filter chip: selected chips take the control fill, not a second hue.</summary>
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

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = Row(6, labelBlock, countBlock),
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(11, 0, 11, 0),
            CornerRadius = new CornerRadius(999),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = selected ? Brush(ArcTokens.ControlFill) : Brush(ArcTokens.SurfaceMuted),
            BorderBrush = selected ? Brush(ArcTokens.BorderStrong) : Brush(ArcTokens.BorderSubtle)
        };
        button.Click += (_, _) => onToggle();
        return button;
    }

    // ------------------------------------------------------------------ readouts

    /// <summary>A label left, value right row for summary lines.</summary>
    public static Control Line(string label, string value, bool strong = false, string? note = null,
        bool accent = false)
    {
        var left = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
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

    /// <summary>A thin determinate bar, used where a full ArcProgress would be too tall.</summary>
    public static Control Bar(double fraction, string key = ArcTokens.Accent, double height = 4)
    {
        return new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = Brush(ArcTokens.ControlTrack),
            ClipToBounds = true,
            Child = new Border
            {
                Width = Math.Clamp(fraction, 0, 1) * 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(height / 2),
                Background = Brush(key)
            }
        };
    }

    /// <summary>A keycap, for shortcut hints in palettes and footers.</summary>
    public static Control Keycap(string text) => new Border
    {
        Background = Brush(ArcTokens.SurfaceMuted),
        BorderBrush = Brush(ArcTokens.BorderSubtle),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(5, 1, 5, 1),
        Child = new TextBlock { Text = text, Classes = { "arc-caption" } }
    };

    public static string Count(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ live region

    /// <summary>An inline status line: every block reports its last action here.</summary>
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

    // ------------------------------------------------------------------ routing and keys

    /// <summary>
    /// Adds a block-level keyboard shortcut. The handler runs from the tunnel phase so it reaches the
    /// block wherever focus sits, but it never fires while a text field has focus (Arc's rule for the
    /// bracket rail shortcut and the triage letter keys).
    /// </summary>
    public static void Shortcut(InputElement root, Key key, Action run, Func<KeyModifiers, bool>? modifiers = null)
    {
        root.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key != key || args.Handled)
            {
                return;
            }

            if (modifiers is not null && !modifiers(args.KeyModifiers))
            {
                return;
            }

            if (IsTextInput(args.Source as IInputElement) || TopLevelOf(root)?.FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            run();
            args.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    public static bool IsTextInput(IInputElement? element) => element is TextBox or Arc.Avalonia.Inputs.Controls.ArcTextFieldBase;

    public static TopLevel? TopLevelOf(Visual visual) => TopLevel.GetTopLevel(visual);

    // ------------------------------------------------------------------ pieces

    /// <summary>A page frame body: a header bar over a scroll-free region, used by most shell blocks.</summary>
    public static Border TopBar(Control left, Control right, double height = 52, double padding = 14) => new()
    {
        Height = height,
        Padding = new Thickness(padding, 0, padding, 0),
        Background = Brush(ArcTokens.Surface),
        BorderBrush = Brush(ArcTokens.BorderSubtle),
        BorderThickness = new Thickness(0, 0, 0, 1),
        Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { left, Place(right, 1) }
        }
    };

    /// <summary>An empty state for a filtered or cleared collection.</summary>
    public static Control Empty(string icon, string title, string description, Control? action = null)
    {
        return new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Disc(icon, 40, 18),
                Text(title, "arc-body-sm arc-medium"),
                Caption(description, 280),
                action ?? Space(0)
            }
        };
    }

    /// <summary>A highlighted match: the query terms are marked with the accent tint, never colour alone.</summary>
    public static Control Highlighted(string text, string query, double maxWidth = 0)
    {
        var panel = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Classes = { "arc-body-sm" },
            MaxWidth = maxWidth > 0 ? maxWidth : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var index = string.IsNullOrWhiteSpace(query)
            ? -1
            : text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            panel.Inlines!.Add(new global::Avalonia.Controls.Documents.Run(text));
            return panel;
        }

        panel.Inlines!.Add(new global::Avalonia.Controls.Documents.Run(text[..index]));
        panel.Inlines!.Add(new global::Avalonia.Controls.Documents.Run(text.Substring(index, query.Length))
        {
            Background = Tint(ArcTokens.Accent, 0.22),
            FontWeight = FontWeight.Medium
        });
        panel.Inlines!.Add(new global::Avalonia.Controls.Documents.Run(text[(index + query.Length)..]));

        return panel;
    }

    /// <summary>Places a control in a grid column, for terse object initializers.</summary>
    public static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    public static T Place<T>(T control, int column, int row) where T : Control
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        return control;
    }
}

/// <summary>A tiny fluent helper so a control can be configured inside an object initializer.</summary>
internal static class AppKitExtensions
{
    public static T With<T>(this T control, Action<T> configure)
    {
        configure(control);
        return control;
    }
}
