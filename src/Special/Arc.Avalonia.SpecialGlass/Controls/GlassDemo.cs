using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>Layout helpers shared by the glass item pages.</summary>
public static class GlassDemo
{
    /// <summary>
    /// A rounded stage with a painted backdrop and the demo floating on it. Glass needs something
    /// colourful behind it, so every demo owns a stage instead of sitting on the gallery's muted canvas.
    /// </summary>
    public static Border Stage(
        Control content,
        ArcGlassScene scene = ArcGlassScene.Coast,
        double height = 420,
        double padding = 24,
        int seed = 0)
    {
        content.Margin = new Thickness(padding);
        var host = new Grid
        {
            Children =
            {
                new ArcGlassBackdrop { Scene = scene, Corner = 26, Seed = seed, Vignette = 0.55 },
                content
            }
        };

        return new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Child = host
        };
    }

    /// <summary>A neutral framed surface for demos that do not need their own backdrop.</summary>
    public static Border Frame(Control content, double width, double height, Thickness? padding = null)
    {
        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(26),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.SurfaceMuted)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Padding = padding ?? default,
            Child = content
        };
    }

    /// <summary>One quiet line under a demo that explains the interactions.</summary>
    public static TextBlock Hint(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 820,
        Margin = new Thickness(0, 10, 0, 0)
    };

    /// <summary>A short label used above a demo variant.</summary>
    public static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        HorizontalAlignment = HorizontalAlignment.Center
    };

    public static ArcIcon Icon(string name, double size = 16, IBrush? foreground = null) =>
        ArcGlass.Icon(name, size, foreground);

    /// <summary>A glass text field style shell used by the composer and share demos.</summary>
    public static Border Field(Control content, double radius = 18)
    {
        var border = ArcGlass.Inset(radius, alpha: 0.5);
        border.Padding = new Thickness(14, 10);
        border.Child = content;
        return border;
    }

    /// <summary>A small status chip used for "Copied", "Posted" and other in-place confirmations.</summary>
    public static Border Chip(string text, string icon = "check", bool accent = true)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(ArcGlass.Icon(
            icon,
            12,
            ArcGlass.Solid(ArcGlass.Color(accent ? ArcTokens.AccentStrong : ArcTokens.TextSecondary))));
        row.Children.Add(new TextBlock
        {
            Text = text,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(accent ? ArcTokens.AccentStrong : ArcTokens.TextSecondary)),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(8, 3),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(accent ? ArcTokens.Accent : ArcTokens.TextSecondary, 0.14)),
            Child = row
        };
    }
}
