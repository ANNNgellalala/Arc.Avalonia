using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team;

/// <summary>
/// Composition helpers shared by the team and collaboration blocks. Blocks are page-level surfaces, so they
/// are assembled from ordinary Arc controls plus these quiet helpers: one panel radius, one hairline border,
/// 4 px spacing and the documented type scale. Every colour resolves through <see cref="ArcTokens"/>, so
/// light, dark and all accents come from the palette rather than hard-coded values.
/// </summary>
internal static class TeamKit
{
    // ---------------------------------------------------------------- tokens

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static Color Color(string key) => ArcResources.Color(key);

    /// <summary>A token colour at reduced alpha, for washes, selected rows and decorative marks.</summary>
    public static IBrush Tint(string key, double alpha)
    {
        var color = Color(key);
        return new SolidColorBrush(global::Avalonia.Media.Color.FromArgb(
            (byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B));
    }

    public static CornerRadius Radius(double value) => new(value);

    // ---------------------------------------------------------------- text

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

    public static TextBlock Body(string text, double maxWidth = 0) => Text(text, "arc-body-sm arc-secondary", maxWidth);

    public static TextBlock Heading(string text, double maxWidth = 0) => Text(text, "arc-heading", maxWidth);

    public static TextBlock Subtitle(string text, double maxWidth = 0) => Text(text, "arc-subtitle", maxWidth);

    public static TextBlock Mono(string text, double maxWidth = 0) => new()
    {
        Text = text,
        Classes = { "arc-mono" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = maxWidth > 0 ? maxWidth : double.PositiveInfinity
    };

    // ---------------------------------------------------------------- layout

    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var panel = new StackPanel
        {
            Spacing = spacing,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
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

    /// <summary>A horizontal row whose children share the available width.</summary>
    public static Grid Columns(string definitions, params Control[] cells)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };
        for (var index = 0; index < cells.Length; index++)
        {
            Grid.SetColumn(cells[index], index);
            grid.Children.Add(cells[index]);
        }

        return grid;
    }

    public static Grid Rows(string definitions, params Control[] cells)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions(definitions) };
        for (var index = 0; index < cells.Length; index++)
        {
            Grid.SetRow(cells[index], index);
            grid.Children.Add(cells[index]);
        }

        return grid;
    }

    public static Control Spacer(double height) => new Border { Height = height };

    public static Border Divider(double margin = 0) => new()
    {
        Height = 1,
        Background = Brush(ArcTokens.BorderSubtle),
        Margin = new Thickness(0, margin, 0, margin),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static Border VerticalDivider(double margin = 0) => new()
    {
        Width = 1,
        Background = Brush(ArcTokens.BorderSubtle),
        Margin = new Thickness(margin, 0, margin, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    // ---------------------------------------------------------------- surfaces

    /// <summary>The outer shell of a block: a clipped panel-radius surface on a hairline border.</summary>
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

    /// <summary>A card inside a block; the radius steps down from the surface that holds it.</summary>
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

    /// <summary>A quiet tinted strip for notices inside a block.</summary>
    public static Border Band(Control content, double padding = 14, double radius = 18, string tone = ArcTokens.Accent,
        double alpha = 0.08)
    {
        return new Border
        {
            Background = Tint(tone, alpha),
            BorderBrush = Tint(tone, alpha + 0.12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = new Thickness(padding),
            Child = content
        };
    }

    // ---------------------------------------------------------------- controls

    public static ArcButton Btn(string label, ArcButtonVariant variant = ArcButtonVariant.Primary,
        ArcButtonSize size = ArcButtonSize.Sm, Action? onClick = null, string? icon = null,
        string? trailingIcon = null)
    {
        // Text and icons inside the button set their own colour, so pick the colour the variant needs
        // instead of relying on inheritance (the global TextBlock style resets Foreground).
        var foreground = variant switch
        {
            ArcButtonVariant.Primary => Brush(ArcTokens.AccentForeground),
            ArcButtonVariant.Danger => Brush(ArcTokens.DangerForeground),
            ArcButtonVariant.Secondary => Brush(ArcTokens.Foreground),
            _ => Brush(ArcTokens.TextSecondary)
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = icon,
                Size = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = foreground
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = foreground
        });
        if (!string.IsNullOrWhiteSpace(trailingIcon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = trailingIcon,
                Size = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = foreground
            });
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

    public static ArcButton IconBtn(string icon, string label, Action? onClick = null,
        ArcButtonVariant variant = ArcButtonVariant.Ghost, double box = 30, ArcButtonSize size = ArcButtonSize.Sm)
    {
        var button = new ArcButton
        {
            Variant = variant,
            Size = size,
            Compact = true,
            Width = box,
            Height = box,
            MinHeight = box,
            MinWidth = box,
            Padding = new Thickness(0),
            Content = new ArcIcon
            {
                Icon = icon,
                Size = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(button, label);
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>A small round or pill toggle such as an environment scope.</summary>
    public static ArcButton ScopePill(string label, bool on, Action onToggle, string? icon = null)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(icon))
        {
            content.Children.Add(new ArcIcon
            {
                Icon = icon,
                Size = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = on ? Brush(ArcTokens.AccentForeground) : Brush(ArcTokens.TextMuted)
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = on ? Brush(ArcTokens.AccentForeground) : Brush(ArcTokens.TextSecondary)
        });

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 26,
            MinHeight = 26,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(999),
            Background = on ? Brush(ArcTokens.Accent) : Brush(ArcTokens.SurfaceMuted),
            BorderBrush = on ? Brush(ArcTokens.AccentStrong) : Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            Content = content,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(button, label);
        AutomationProperties.SetHelpText(button, on ? "On. Activate to turn off." : "Off. Activate to turn on.");
        button.Click += (_, _) => onToggle();
        return button;
    }

    public static ArcBadge Pill(string text, ArcBadgeTone tone = ArcBadgeTone.Neutral, bool dot = false) =>
        new() { Content = text, Tone = tone, Dot = dot };

    public static ArcBadge TeamPill(string team) => team switch
    {
        "Design" => Pill(team, ArcBadgeTone.Accent),
        "Engineering" => Pill(team, ArcBadgeTone.Neutral),
        _ => Pill(team, ArcBadgeTone.Neutral)
    };

    public static ArcAvatar Avatar(string name, ArcAvatarSize size = ArcAvatarSize.Sm,
        ArcAvatarStatus status = ArcAvatarStatus.None) => new()
    {
        DisplayName = name,
        Size = size,
        Status = status
    };

    public static ArcAvatarGroup AvatarGroup(IEnumerable<string> names, ArcAvatarSize size = ArcAvatarSize.Xs,
        int max = 4, string label = "Members", bool fan = false)
    {
        return new ArcAvatarGroup
        {
            Members = names.Select(name => new ArcAvatarMember { Name = name }).ToList(),
            Max = max,
            Size = size,
            Label = label,
            FanOnHover = fan
        };
    }

    public static ArcCheckbox Check(string label, bool value, Action<bool>? onChanged = null, string? description = null)
    {
        var checkbox = new ArcCheckbox
        {
            Label = label,
            Description = description,
            Checked = value
        };
        if (onChanged is not null)
        {
            checkbox.CheckedChanged += (_, _) => onChanged(checkbox.Checked == true);
        }

        return checkbox;
    }

    public static ArcSwitch Toggle(string label, bool value, Action<bool>? onChanged = null)
    {
        var toggle = new ArcSwitch { Label = label, Checked = value };
        if (onChanged is not null)
        {
            toggle.CheckedChanged += (_, _) => onChanged(toggle.Checked);
        }

        return toggle;
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

    public static ArcSearchField Search(string placeholder, string? value = null, double width = 0)
    {
        var search = new ArcSearchField { Placeholder = placeholder, Value = value };
        if (width > 0)
        {
            search.Width = width;
        }

        return search;
    }

    public static ArcTextarea Textarea(string placeholder, string? value = null, int height = 90, double width = 0)
    {
        var area = new ArcTextarea { Placeholder = placeholder, Value = value, Height = height };
        if (width > 0)
        {
            area.Width = width;
        }

        return area;
    }

    public static ArcSelect Select(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, double width = 0)
    {
        var select = new ArcSelect
        {
            Options = options.Select(option => new ArcSelectOption(option.Value, option.Label)).ToList(),
            Value = selected,
            Size = ArcFieldSize.Sm
        };
        if (width > 0)
        {
            select.Width = width;
        }

        if (onChanged is not null)
        {
            select.ValueChanged += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(select.Value))
                {
                    onChanged(select.Value!);
                }
            };
        }

        return select;
    }

    public static ArcSegmentedControl Segmented(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, string? label = null)
    {
        var control = new ArcSegmentedControl
        {
            Options = options.Select(option => new ArcSegmentOption(option.Value, option.Label)).ToList(),
            Value = selected,
            Label = label
        };
        if (onChanged is not null)
        {
            control.ValueChanged += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(control.Value))
                {
                    onChanged(control.Value!);
                }
            };
        }

        return control;
    }

    public static ArcChipGroup Chips(IEnumerable<(string Value, string Label)> options, string? selected,
        Action<string>? onChanged = null, string? label = null)
    {
        var control = new ArcChipGroup
        {
            Options = options.Select(option => new ArcChipOption(option.Value, option.Label)).ToList(),
            Value = selected is null ? null : new[] { selected },
            Label = label,
            Multiple = false
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

    /// <summary>A labelled inline fact: muted icon plus short text, for metadata rows.</summary>
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
            Text(text, "arc-caption"));
    }

    /// <summary>A round icon disc for block headers and empty rows.</summary>
    public static Control Disc(string icon, double size = 36, double iconSize = 16, bool accent = false)
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
                Foreground = accent ? Brush(ArcTokens.AccentStrong) : Brush(ArcTokens.TextSecondary)
            }
        };
    }

    /// <summary>
    /// A portrait stand-in for sample people: the brand's soft gradient wash with initials in the ink token.
    /// Real deployments swap in licensed photographs; the layout reserves the same box.
    /// </summary>
    public static Border Portrait(TeamPerson person, double width, double height, double radius = 18,
        double initialsSize = 30)
    {
        var tint = new Grid();
        tint.Children.Add(new Border
        {
            Background = Brush(ArcTokens.GradientSoft),
            CornerRadius = new CornerRadius(radius)
        });
        tint.Children.Add(new TextBlock
        {
            Text = person.Initials,
            FontSize = initialsSize,
            FontWeight = FontWeight.Medium,
            Foreground = Brush(ArcTokens.GradientForeground),
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        tint.Children.Add(new Border
        {
            BorderBrush = Tint(ArcTokens.GradientForeground, 0.08),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius)
        });

        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = true,
            Child = tint
        };
    }

    /// <summary>A slim progress track drawn with the shared Feedback track control.</summary>
    public static ArcProgressTrack Meter(double fraction, double thickness = 6, string? tone = null)
    {
        return new ArcProgressTrack
        {
            Fraction = Math.Clamp(fraction, 0, 1),
            Thickness = thickness,
            TrackBrush = Brush(ArcTokens.ControlTrack),
            FillBrush = Brush(tone ?? ArcTokens.Accent),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>A label/value pair for summary strips.</summary>
    public static Control Stat(string value, string label, string? note = null)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Classes = { "arc-heading" }
        });
        stack.Children.Add(Text(label, "arc-caption"));
        if (!string.IsNullOrWhiteSpace(note))
        {
            stack.Children.Add(Text(note!, "arc-caption arc-muted"));
        }

        return stack;
    }

    /// <summary>A labelled column, the standard field layout for the invite and key forms.</summary>
    public static Control Field(string label, Control control, double width = 0, string? hint = null)
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        if (width > 0)
        {
            panel.Width = width;
        }

        panel.Children.Add(Text(label, "arc-label"));
        panel.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint))
        {
            panel.Children.Add(Text(hint!, "arc-caption"));
        }

        return panel;
    }

    // ---------------------------------------------------------------- selectable rows

    /// <summary>
    /// A selectable, keyboard-reachable surface used for directory rows, cards and list items. The ring lives
    /// outside the body so focus never shifts the layout, pointer focus stays quiet, and Enter or Space selects.
    /// Nested buttons keep their own click without selecting the row.
    /// </summary>
    public static Control Selectable(Control content, Action onSelect, bool selected = false,
        string? name = null, double radius = 18, Thickness? padding = null)
    {
        var body = new Border
        {
            Background = selected ? Tint(ArcTokens.Accent, 0.10) : Brush(ArcTokens.Surface),
            BorderBrush = selected ? Tint(ArcTokens.Accent, 0.34) : Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = padding ?? new Thickness(10, 8),
            Child = content,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true
        };
        var ring = new Border
        {
            Margin = new Thickness(-4),
            BorderThickness = new Thickness(2),
            BorderBrush = Brush(ArcTokens.FocusOutline),
            CornerRadius = new CornerRadius(radius + 4),
            IsVisible = false,
            IsHitTestVisible = false
        };
        var host = new Panel { ClipToBounds = false };
        host.Children.Add(ring);
        host.Children.Add(body);

        if (!string.IsNullOrWhiteSpace(name))
        {
            AutomationProperties.SetName(body, selected ? $"{name}, selected" : name);
            AutomationProperties.SetHelpText(body, selected ? "Selected. Activate to change." : "Activate to select.");
        }

        body.PointerEntered += (_, _) =>
        {
            if (!selected)
            {
                body.Background = Brush(ArcTokens.SurfaceMuted);
            }
        };
        body.PointerExited += (_, _) => body.Background = selected
            ? Tint(ArcTokens.Accent, 0.10)
            : Brush(ArcTokens.Surface);
        var pointerFocus = false;
        body.PointerPressed += (_, args) =>
        {
            if (args.Source is Visual source && source.FindAncestorOfType<Button>() is not null)
            {
                return;
            }

            pointerFocus = true;
            body.Focus();
            onSelect();
        };
        body.KeyDown += (_, args) =>
        {
            pointerFocus = false;
            if (args.Key is Key.Enter or Key.Space)
            {
                onSelect();
                args.Handled = true;
            }
        };
        body.GotFocus += (_, _) => ring.IsVisible = !pointerFocus;
        body.LostFocus += (_, _) =>
        {
            ring.IsVisible = false;
            pointerFocus = false;
        };

        return host;
    }

    /// <summary>
    /// The page-level root: the block fills the gallery's 1068 px content column, which is the width the
    /// reference block previews are designed for.
    /// </summary>
    public static Control Root(Control content) => new Border
    {
        Width = 1068,
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = content
    };
}
