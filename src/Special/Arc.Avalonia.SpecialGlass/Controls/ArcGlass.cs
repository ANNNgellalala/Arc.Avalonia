using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>
/// The glass material shared by every item in this project.
///
/// Avalonia has no backdrop filter, so a glass surface cannot sample what is painted behind it. Each
/// surface is therefore built from layered translucency: a two-stop fill that is slightly lighter at
/// the top, a rim gradient that is bright where light would catch it, and, on floating layers, Arc's
/// floating shadow. Every colour comes from a semantic token; the tints are OKLCH lighten/darken steps
/// of the token, so both themes and all eight accents stay in step with the rest of the port.
/// </summary>
public static class ArcGlass
{
    public static bool Dark => ArcThemeManager.Scheme == ArcColorScheme.Dark;

    public static Color Color(string key) => ArcResources.Color(key);

    public static Color Alpha(string key, double alpha) => ArcColor.WithAlpha(Color(key), alpha);

    public static double Radius(string key) => ArcResources.Radius(key).TopLeft;

    /// <summary>Lightens (positive) or darkens (negative) a token colour in OKLCH, keeping its hue.</summary>
    public static Color Lift(Color color, double delta)
    {
        var (lightness, chroma, hue) = ArcColor.ToOklch(color);
        return ArcColor.ReOklch(color, Math.Clamp(lightness + delta, 0, 1), chroma, hue);
    }

    /// <summary>Rotates a token colour's hue and rescales its chroma, used by the holo foil.</summary>
    public static Color Turn(Color color, double hueDelta, double chromaScale = 1)
    {
        var (lightness, chroma, hue) = ArcColor.ToOklch(color);
        return ArcColor.ReOklch(color, lightness, Math.Clamp(chroma * chromaScale, 0, 0.37), hue + hueDelta);
    }

    public static IBrush Solid(Color color) => new ImmutableSolidColorBrush(color);

    public static (RelativePoint Start, RelativePoint End) Direction(double angle)
    {
        var radians = angle * Math.PI / 180.0;
        var dx = Math.Sin(radians);
        var dy = -Math.Cos(radians);
        return (
            new RelativePoint(0.5 - (dx / 2), 0.5 - (dy / 2), RelativeUnit.Relative),
            new RelativePoint(0.5 + (dx / 2), 0.5 + (dy / 2), RelativeUnit.Relative));
    }

    public static IBrush Gradient(double angle, params GradientStop[] stops)
    {
        var (start, end) = Direction(angle);
        var brush = new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end
        };
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(stop);
        }

        return brush;
    }

    public static IBrush Gradient(double angle, Color from, Color to) =>
        Gradient(angle, new GradientStop(from, 0), new GradientStop(to, 1));

    /// <summary>A surface fill: near-transparent at the bottom so content behind stays legible.</summary>
    public static IBrush GlassFill(string? tintKey = null, double alpha = 0.62)
    {
        var surface = Color(ArcTokens.Surface);
        var tint = tintKey is null ? surface : ArcColor.Mix(surface, Color(tintKey), 0.16);
        return Gradient(
            135,
            new GradientStop(ArcColor.WithAlpha(Lift(tint, 0.03), Math.Clamp(alpha, 0, 1)), 0),
            new GradientStop(ArcColor.WithAlpha(Lift(tint, -0.05), Math.Clamp(alpha - 0.10, 0.15, 1)), 1));
    }

    /// <summary>A one pixel rim that catches the light along its top left edge.</summary>
    public static IBrush RimBrush(string? tintKey = null, double strength = 1)
    {
        var surface = Color(ArcTokens.Surface);
        var tint = tintKey is null ? surface : ArcColor.Mix(surface, Color(tintKey), 0.16);
        return Gradient(
            135,
            new GradientStop(ArcColor.WithAlpha(Lift(tint, 0.55), 0.52 * strength), 0),
            new GradientStop(ArcColor.WithAlpha(Lift(tint, 0.30), 0.10 * strength), 0.48),
            new GradientStop(ArcColor.WithAlpha(Lift(tint, 0.42), 0.34 * strength), 1));
    }

    /// <summary>
    /// The standard glass panel: a translucent rounded surface with a rim and Arc elevation.
    /// Content can be assigned through <see cref="Border.Child"/> or the padding is used by a caller.
    /// </summary>
    public static Border Panel(
        double radius = 26,
        Thickness? padding = null,
        bool floating = true,
        string? tintKey = null,
        double fillAlpha = 0.62)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(radius),
            Padding = padding ?? default,
            Background = GlassFill(tintKey, fillAlpha),
            BorderBrush = RimBrush(tintKey),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcResources.Shadows(floating ? ArcTokens.ShadowFloating : ArcTokens.ShadowRaised),
            ClipToBounds = false
        };
    }

    /// <summary>A quiet inner surface used inside a glass panel, with a hairline border only.</summary>
    public static Border Inset(double radius = 18, string? tintKey = null, double alpha = 0.45)
    {
        var tint = tintKey is null ? Color(ArcTokens.Surface) : ArcColor.Mix(Color(ArcTokens.Surface), Color(tintKey), 0.2);
        return new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = Solid(ArcColor.WithAlpha(tint, alpha * 0.5)),
            BorderBrush = Solid(ArcColor.WithAlpha(Lift(tint, 0.4), 0.24)),
            BorderThickness = new Thickness(1)
        };
    }

    /// <summary>Pointer-following accent tint for an interactive row inside glass.</summary>
    public static IBrush HoverTint(string key = ArcTokens.Accent, double alpha = 0.16) =>
        Gradient(90,
            new GradientStop(ArcAlpha(key, alpha), 0),
            new GradientStop(ArcAlpha(key, alpha * 0.55), 1));

    public static Color ArcAlpha(string key, double alpha) => ArcColor.WithAlpha(Color(key), alpha);

    /// <summary>A legibility scrim that is dark in both themes, for text over artwork.</summary>
    public static IBrush PhotoShade(double alpha = 0.55, bool fromTop = false)
    {
        var shade = Dark ? Color(ArcTokens.Background) : Color(ArcTokens.Foreground);
        return Gradient(
            fromTop ? 0 : 180,
            new GradientStop(ArcColor.WithAlpha(shade, 0), 0),
            new GradientStop(ArcColor.WithAlpha(shade, alpha), 1));
    }

    /// <summary>Ink for text painted over artwork: near-white in both themes.</summary>
    public static Color PhotoInk => Dark
        ? Lift(Color(ArcTokens.Foreground), 0.03)
        : ArcColor.Mix(Color(ArcTokens.Foreground), Color(ArcTokens.Background), 0.94);

    public static Color PhotoMuted => ArcColor.WithAlpha(PhotoInk, 0.78);

    public static BoxShadows Shadow(string key) => ArcResources.Shadows(key);

    public static FontFamily Font =>
        Application.Current is { } app && app.TryFindResource(ArcTokens.FontBody, out var value) && value is FontFamily family
            ? family
            : FontFamily.Default;

    public static ArcIcon Icon(string name, double size = 16, IBrush? foreground = null)
    {
        var icon = new ArcIcon { Icon = name, Size = size, StrokeWidth = 1.75, VerticalAlignment = VerticalAlignment.Center };
        if (foreground is not null)
        {
            icon.Foreground = foreground;
        }

        return icon;
    }

    public static ArcIcon AccentIcon(string name, double size = 16) =>
        Icon(name, size, Solid(Color(ArcTokens.Accent)));

    public static TextBlock Text(string text, string classes, IBrush? foreground = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap };
        foreach (var cls in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            block.Classes.Add(cls);
        }

        if (foreground is not null)
        {
            block.Foreground = foreground;
        }

        return block;
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
            panel.Children.Add(child);
        }

        return panel;
    }

    public static StackPanel Column(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>Initials for an avatar, matching the Arc hover card's two letter rule.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }

    /// <summary>A tinted initial avatar; the tint is the accent, so people read as one family.</summary>
    public static Control Avatar(string? name, double size = 32, bool strong = false)
    {
        var background = strong
            ? ArcColor.WithAlpha(Color(ArcTokens.Accent), 0.30)
            : ArcColor.WithAlpha(Color(ArcTokens.Accent), 0.16);
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = Solid(background),
            BorderBrush = Solid(ArcColor.WithAlpha(Color(ArcTokens.Accent), 0.38)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = Initials(name),
                Classes = { "arc-caption", "arc-medium" },
                Foreground = Solid(Color(ArcTokens.Foreground)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        AutomationProperties.SetName(border, name ?? "Person");
        return border;
    }

    /// <summary>A small rounded tile behind an icon, the recurring glass "glyph button" background.</summary>
    public static Border GlyphTile(string icon, double size = 34, bool accent = false, double iconSize = 16)
    {
        var box = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(Math.Max(9, size * 0.3)),
            Background = Solid(ArcColor.WithAlpha(Color(accent ? ArcTokens.Accent : ArcTokens.Surface), accent ? 0.26 : 0.30)),
            BorderBrush = Solid(ArcColor.WithAlpha(Color(ArcTokens.Surface), 0.32)),
            BorderThickness = new Thickness(1),
            Child = Icon(icon, iconSize, Solid(Color(accent ? ArcTokens.AccentStrong : ArcTokens.Foreground)))
        };
        return box;
    }

    /// <summary>Sets a rounded clipping geometry on a border's child content.</summary>
    public static Border Clip(Border border, double radius)
    {
        border.ClipToBounds = true;
        border.CornerRadius = new CornerRadius(radius);
        return border;
    }
}



