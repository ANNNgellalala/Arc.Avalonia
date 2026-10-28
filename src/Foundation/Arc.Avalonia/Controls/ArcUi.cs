using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Controls;

/// <summary>
/// Small composition helpers shared by the P0 UI controls. Everything resolves through semantic tokens, so a
/// control built from these helpers follows light/dark themes and all eight accents without any XAML theme file.
/// This keeps composite controls (dashboard cards, transcript rows, agenda blocks) consistent with the
/// templated foundation while avoiding per-project theme-entry edits.
/// </summary>
public static class ArcUi
{
    /// <summary>Resolves a semantic token brush.</summary>
    public static IBrush Brush(string key) => ArcResources.Brush(key);

    /// <summary>A text block on the Arc type scale; classes is a space separated arc-* class list.</summary>
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

    public static TextBlock Heading(string text, double maxWidth = 520) => Text(text, "arc-heading", maxWidth);

    public static TextBlock Strong(string text, double maxWidth = 0) => Text(text, "arc-body arc-medium", maxWidth);

    public static TextBlock Mono(string text) => Text(text, "arc-caption arc-mono");

    public static StackPanel Stack(double spacing = 12, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel HStack(double spacing = 8, params Control[] children)
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

    public static WrapPanel Wrap(double spacing = 8, params Control[] children)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, spacing, spacing);
            panel.Children.Add(child);
        }

        return panel;
    }

    public static ArcIcon Icon(string name, double size = 16, string colorKey = ArcTokens.TextSecondary) =>
        new()
        {
            Icon = name,
            Size = size,
            Foreground = Brush(colorKey),
            VerticalAlignment = VerticalAlignment.Center
        };

    /// <summary>A flat surface: hairline border, panel radius, no shadow unless the caller adds one.</summary>
    public static Border Surface(Control child, double padding = 16, string? backgroundKey = null,
        string? borderKey = null, double radius = 0)
    {
        var corner = radius > 0 ? new CornerRadius(radius) : ArcResources.Radius(ArcTokens.RadiusPanel);
        return new Border
        {
            Background = Brush(backgroundKey ?? ArcTokens.Surface),
            BorderBrush = Brush(borderKey ?? ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = corner,
            Padding = new Thickness(padding),
            Child = child
        };
    }

    public static Border Divider(string colorKey = ArcTokens.BorderSubtle) => new()
    {
        Height = 1,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Background = Brush(colorKey),
        Margin = new Thickness(0, 4, 0, 4)
    };

    public static ArcBadge Badge(string text, ArcBadgeTone tone = ArcBadgeTone.Neutral) =>
        new() { Content = text, Tone = tone };

    public static ArcButton Button(string text, ArcButtonVariant variant = ArcButtonVariant.Ghost,
        ArcButtonSize size = ArcButtonSize.Sm, string? icon = null)
    {
        var button = new ArcButton { Variant = variant, Size = size };
        if (icon is null)
        {
            button.Content = text;
        }
        else
        {
            button.Content = HStack(6, Icon(icon, 14, ArcTokens.TextSecondary), Text(text, "arc-body-sm"));
        }

        return button;
    }

    /// <summary>A rounded pill for metadata, optionally with an icon.</summary>
    public static Border Pill(string text, string? icon = null, bool accent = false)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (icon is not null)
        {
            content.Children.Add(Icon(icon, 12, accent ? ArcTokens.AccentStrong : ArcTokens.TextSecondary));
        }

        content.Children.Add(Text(text, "arc-caption", wrap: TextWrapping.NoWrap));
        return new Border
        {
            Background = Brush(accent ? ArcTokens.AccentSubtle : ArcTokens.SurfaceMuted),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(8, 3, 8, 3),
            Child = content
        };
    }

    /// <summary>A small status dot, used beside a label so colour is never the only signal.</summary>
    public static Border Dot(string colorKey, double size = 8) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = Brush(colorKey),
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>Sets the accessible name and optional help text for a composite control.</summary>
    public static void Name(Control control, string name, string? help = null)
    {
        AutomationProperties.SetName(control, name);
        if (!string.IsNullOrWhiteSpace(help))
        {
            AutomationProperties.SetHelpText(control, help);
        }
    }
}
