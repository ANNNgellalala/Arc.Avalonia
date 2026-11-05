using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Arc.Avalonia.Theme;

public enum ArcColorScheme
{
    Light,
    Dark
}

public enum ArcAccent
{
    Neutral,
    Violet,
    Blue,
    Green,
    Amber,
    Orange,
    Coral,
    Rose
}

/// <summary>
/// Builds the Arc design tokens (from uiarc.dev foundation.css) as an Avalonia resource dictionary.
/// Values are defined in OKLCH exactly like the original and converted to sRGB here.
/// </summary>
public static class ArcPalette
{
    private const string InterFont = "avares://Avalonia.Fonts.Inter/Assets#Inter";

    /// <summary>
    /// Pairs the Light and Dark token palettes into one theme-aware dictionary, so a single merge into
    /// <c>Application.Resources</c> makes every token resolve from <c>ThemeDictionaries</c> for the active
    /// variant (ADR 0003 §4.2). Rebuilt whenever the accent changes.
    /// </summary>
    public static ResourceDictionary BuildThemeDictionaries(ArcAccent accent) => new()
    {
        ThemeDictionaries =
        {
            [ThemeVariant.Light] = Build(ArcColorScheme.Light, accent),
            [ThemeVariant.Dark] = Build(ArcColorScheme.Dark, accent)
        }
    };

    /// <summary>Accent colour used when mapping an Arc accent onto Fluent's <c>ColorPaletteResources</c>.</summary>
    public static Color AccentColor(ArcColorScheme scheme, ArcAccent accent) =>
        Build(scheme, accent)[ArcTokens.Accent] is ISolidColorBrush brush ? brush.Color : Colors.Gray;

    public static ResourceDictionary Build(ArcColorScheme scheme, ArcAccent accent)
    {
        var dark = scheme == ArcColorScheme.Dark;
        var dictionary = new ResourceDictionary();
        var a = AccentFor(accent, dark);

        // Page, surfaces, text, lines. Light inherits the neutral ramp; dark uses its own steps.
        var background = dark ? ArcColor.Oklch(0.19, 0, 0) : ArcColor.Oklch(1.0, 0, 0);
        var surface = dark ? ArcColor.Oklch(0.215, 0, 0) : ArcColor.Oklch(1.0, 0, 0);
        var surfaceRaised = dark ? ArcColor.Oklch(0.24, 0, 0) : ArcColor.Oklch(1.0, 0, 0);
        var surfaceMuted = dark ? ArcColor.Oklch(0.26, 0, 0) : ArcColor.Oklch(0.978, 0, 0);
        var foreground = dark ? ArcColor.Oklch(0.96, 0, 0) : ArcColor.Oklch(0.15, 0, 0);
        var secondary = dark ? ArcColor.Oklch(0.77, 0, 0) : ArcColor.Oklch(0.46, 0, 0);
        var muted = dark ? ArcColor.Oklch(0.64, 0, 0) : ArcColor.Oklch(0.59, 0, 0);
        var border = dark ? ArcColor.Oklch(0.29, 0, 0) : ArcColor.Oklch(0.935, 0, 0);
        var borderSubtle = dark ? ArcColor.Oklch(0.25, 0, 0) : ArcColor.Oklch(0.965, 0, 0);
        var borderStrong = dark ? ArcColor.Oklch(0.40, 0, 0) : ArcColor.Oklch(0.82, 0, 0);

        var success = dark ? ArcColor.Oklch(0.78, 0.18, 150) : ArcColor.Oklch(0.49, 0.19, 150);
        var warning = dark ? ArcColor.Oklch(0.83, 0.17, 80) : ArcColor.Oklch(0.58, 0.18, 75);
        var danger = dark ? ArcColor.Oklch(0.76, 0.20, 25) : ArcColor.Oklch(0.54, 0.21, 25);

        // Selection controls: tuned per accent so white thumbs keep contrast on every hue.
        var controlOn = dark ? a.ControlOnDark : a.ControlOnLight;
        var controlGlyph = dark
            ? (a.GlyphDark ?? background)
            : (a.GlyphLight ?? Colors.White);
        var controlTrack = dark ? ArcColor.Oklch(0.33, 0, 0) : ArcColor.Oklch(0.895, 0, 0);
        var controlTrackHover = dark ? ArcColor.Oklch(0.37, 0, 0) : ArcColor.Oklch(0.86, 0, 0);
        var controlThumb = dark ? ArcColor.Oklch(0.97, 0, 0) : Colors.White;
        var controlThumbOn = dark ? a.ThumbOnDark ?? Colors.White : a.ThumbOnLight ?? Colors.White;
        var controlThumbShadow = dark
            ? Shadows(
                new BoxShadow { OffsetX = 0, OffsetY = 0, Blur = 0, Spread = 0.5, Color = ArcColor.Rgb(0, 0, 0, 0.30) },
                new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 2, Color = ArcColor.Rgb(0, 0, 0, 0.35) },
                new BoxShadow { OffsetX = 0, OffsetY = 2, Blur = 6, Color = ArcColor.Rgb(0, 0, 0, 0.20) })
            : Shadows(
                new BoxShadow { OffsetX = 0, OffsetY = 0, Blur = 0, Spread = 0.5, Color = ArcColor.Rgb(0, 0, 0, 0.07) },
                new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 2, Color = ArcColor.Rgb(0, 0, 0, 0.14) },
                new BoxShadow { OffsetX = 0, OffsetY = 2, Blur = 6, Color = ArcColor.Rgb(0, 0, 0, 0.06) });

        var (series1, series2, series3, series4) = Series(a.Accent, dark);
        var controlFill = dark ? a.ControlFillDark ?? controlOn : controlOn;

        var resting = dark
            ? Shadows(new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 2, Color = ArcColor.Rgb(0, 0, 0, 0.18) })
            : Shadows(new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 2, Color = ArcColor.Rgb(0, 0, 0, 0.035) });
        var raised = dark
            ? Shadows(new BoxShadow { OffsetX = 0, OffsetY = 8, Blur = 22, Color = ArcColor.Rgb(0, 0, 0, 0.22) })
            : Shadows(
                new BoxShadow { OffsetX = 0, OffsetY = 6, Blur = 18, Color = ArcColor.Rgb(0, 0, 0, 0.065) },
                new BoxShadow { OffsetX = 0, OffsetY = 1, Blur = 3, Color = ArcColor.Rgb(0, 0, 0, 0.035) });
        var floating = dark
            ? Shadows(
                new BoxShadow { OffsetX = 0, OffsetY = 22, Blur = 55, Color = ArcColor.Rgb(0, 0, 0, 0.34) },
                new BoxShadow { OffsetX = 0, OffsetY = 3, Blur = 10, Color = ArcColor.Rgb(0, 0, 0, 0.18) })
            : Shadows(
                new BoxShadow { OffsetX = 0, OffsetY = 20, Blur = 48, Color = ArcColor.Rgb(0, 0, 0, 0.105) },
                new BoxShadow { OffsetX = 0, OffsetY = 3, Blur = 10, Color = ArcColor.Rgb(0, 0, 0, 0.045) });

        var gradFrom = dark ? ArcColor.Oklch(0.38, 0.05, 300) : ArcColor.Oklch(0.90, 0.045, 300);
        var gradTo = dark ? ArcColor.Oklch(0.40, 0.045, 60) : ArcColor.Oklch(0.92, 0.045, 65);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(gradFrom, 0),
                new GradientStop(gradTo, 1)
            }
        };

        var soft = dark
            ? new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.Oklch(0.235, 0.016, 300), 0),
                    new GradientStop(ArcColor.Oklch(0.24, 0.014, 65), 1)
                }
            }
            : new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.Oklch(0.975, 0.016, 300), 0),
                    new GradientStop(ArcColor.Oklch(0.98, 0.016, 65), 1)
                }
            };

        var inkFrom = dark ? ArcColor.Oklch(0.82, 0.07, 300) : ArcColor.Oklch(0.47, 0.10, 300);
        var inkTo = dark ? ArcColor.Oklch(0.86, 0.065, 70) : ArcColor.Oklch(0.50, 0.09, 55);
        var ink = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(inkFrom, 0), new GradientStop(inkTo, 1) }
        };

        var focus = ArcColor.WithAlpha(a.Strong, 0.72);

        // ---- Surfaces and text ----
        Set(dictionary, ArcTokens.Background, background);
        Set(dictionary, ArcTokens.Surface, surface);
        Set(dictionary, ArcTokens.SurfaceRaised, surfaceRaised);
        Set(dictionary, ArcTokens.SurfaceMuted, surfaceMuted);
        Set(dictionary, ArcTokens.Foreground, foreground);
        Set(dictionary, ArcTokens.TextSecondary, secondary);
        Set(dictionary, ArcTokens.TextMuted, muted);

        // ---- Lines ----
        Set(dictionary, ArcTokens.Border, border);
        Set(dictionary, ArcTokens.BorderSubtle, borderSubtle);
        Set(dictionary, ArcTokens.BorderStrong, borderStrong);

        // ---- Accent ----
        Set(dictionary, ArcTokens.Accent, a.Accent);
        Set(dictionary, ArcTokens.AccentStrong, a.Strong);
        Set(dictionary, ArcTokens.AccentSubtle, ArcColor.WithAlpha(a.Accent, a.SubtleAlpha));
        Set(dictionary, ArcTokens.AccentForeground, a.Foreground);

        // ---- Status ----
        Set(dictionary, ArcTokens.Success, success);
        Set(dictionary, ArcTokens.Warning, warning);
        Set(dictionary, ArcTokens.Danger, danger);
        Set(dictionary, ArcTokens.SuccessSubtle, ArcColor.WithAlpha(success, dark ? 0.18 : 0.12));
        Set(dictionary, ArcTokens.WarningSubtle, ArcColor.WithAlpha(warning, dark ? 0.18 : 0.12));
        Set(dictionary, ArcTokens.DangerSubtle, ArcColor.WithAlpha(danger, dark ? 0.18 : 0.12));
        Set(dictionary, ArcTokens.DangerForeground, dark ? background : Colors.White);
        Set(dictionary, ArcTokens.Scrim, dark ? ArcColor.Oklch(0.0, 0, 0, 0.55) : ArcColor.Oklch(0.09, 0.004, 280, 0.42));

        // ---- Selection controls ----
        Set(dictionary, ArcTokens.ControlOn, controlOn);
        Set(dictionary, ArcTokens.ControlGlyph, controlGlyph);
        Set(dictionary, ArcTokens.ControlTrack, controlTrack);
        Set(dictionary, ArcTokens.ControlTrackHover, controlTrackHover);
        Set(dictionary, ArcTokens.ControlThumb, controlThumb);
        Set(dictionary, ArcTokens.ControlThumbOn, controlThumbOn);
        Set(dictionary, ArcTokens.ControlThumbShadow, controlThumbShadow);
        Set(dictionary, ArcTokens.ControlOnSubtle, ArcColor.WithAlpha(controlOn, 0.10));
        Set(dictionary, ArcTokens.ControlFill, controlFill);

        // ---- Elevation ----
        Set(dictionary, ArcTokens.ShadowResting, resting);
        Set(dictionary, ArcTokens.ShadowRaised, raised);
        Set(dictionary, ArcTokens.ShadowFloating, floating);

        // ---- Data series ----
        Set(dictionary, ArcTokens.Series1, series1);
        Set(dictionary, ArcTokens.Series2, series2);
        Set(dictionary, ArcTokens.Series3, series3);
        Set(dictionary, ArcTokens.Series4, series4);

        // ---- Brand wash ----
        Set(dictionary, ArcTokens.GradientFrom, gradFrom);
        Set(dictionary, ArcTokens.GradientTo, gradTo);
        Set(dictionary, ArcTokens.GradientSoft, soft);
        Set(dictionary, ArcTokens.GradientInk, ink);
        Set(dictionary, ArcTokens.GradientForeground, dark ? ArcColor.Oklch(0.96, 0.005, 300) : ArcColor.Oklch(0.22, 0.01, 300));

        // ---- Focus ----
        Set(dictionary, ArcTokens.FocusOutline, focus);

        // ---- Type ----
        dictionary[ArcTokens.FontDisplay] = new FontFamily(InterFont);
        dictionary[ArcTokens.FontBody] = new FontFamily(InterFont);
        dictionary[ArcTokens.TextXs] = 12.0;
        dictionary[ArcTokens.TextSm] = 14.0;
        dictionary[ArcTokens.TextBase] = 16.0;
        dictionary[ArcTokens.TextLg] = 18.0;
        dictionary[ArcTokens.TextXl] = 22.0;
        dictionary[ArcTokens.Text2xl] = 28.0;
        dictionary[ArcTokens.Text3xl] = 36.0;
        dictionary[ArcTokens.Text4xl] = 52.0;
        dictionary[ArcTokens.Text5xl] = 72.0;
        dictionary[ArcTokens.TrackingDisplay] = -0.03;
        dictionary[ArcTokens.TrackingBody] = -0.01;
        dictionary[ArcTokens.LeadingDisplay] = 1.1;
        dictionary[ArcTokens.LeadingBody] = 1.4;

        // ---- Radii ----
        dictionary[ArcTokens.RadiusControl] = new CornerRadius(18);
        dictionary[ArcTokens.RadiusPanel] = new CornerRadius(26);
        dictionary[ArcTokens.RadiusSurface] = new CornerRadius(34);
        dictionary[ArcTokens.RadiusPill] = new CornerRadius(9999);

        // ---- Sizing ----
        dictionary[ArcTokens.ControlHeightSm] = 36.0;
        dictionary[ArcTokens.ControlHeightMd] = 44.0;
        dictionary[ArcTokens.ControlHeightLg] = 50.0;
        dictionary[ArcTokens.Space1] = 4.0;
        dictionary[ArcTokens.Space2] = 8.0;
        dictionary[ArcTokens.Space3] = 12.0;
        dictionary[ArcTokens.Space4] = 16.0;
        dictionary[ArcTokens.Space5] = 20.0;
        dictionary[ArcTokens.Space6] = 24.0;
        dictionary[ArcTokens.Space8] = 32.0;
        dictionary[ArcTokens.Space10] = 40.0;
        dictionary[ArcTokens.Space12] = 48.0;
        dictionary[ArcTokens.Space16] = 64.0;
        dictionary[ArcTokens.Space20] = 80.0;
        dictionary[ArcTokens.Space24] = 96.0;

        // ---- Motion (durations in seconds for Avalonia transitions) ----
        dictionary[ArcTokens.DurationInstant] = TimeSpan.FromMilliseconds(120);
        dictionary[ArcTokens.DurationFast] = TimeSpan.FromMilliseconds(160);
        dictionary[ArcTokens.DurationStandard] = TimeSpan.FromMilliseconds(240);
        dictionary[ArcTokens.DurationConsidered] = TimeSpan.FromMilliseconds(480);

        return dictionary;
    }

    private static void Set(ResourceDictionary dictionary, string key, Color color) =>
        dictionary[key] = new ImmutableSolidColorBrush(color);

    private static void Set(ResourceDictionary dictionary, string key, IBrush brush) =>
        dictionary[key] = brush;

    private static void Set(ResourceDictionary dictionary, string key, BoxShadows shadows) =>
        dictionary[key] = shadows;

    private static BoxShadows Shadows(BoxShadow first, params BoxShadow[] rest) =>
        rest.Length == 0 ? new BoxShadows(first) : new BoxShadows(first, rest);

    private sealed record AccentSet(
        Color Accent,
        Color Strong,
        double SubtleAlpha,
        Color Foreground,
        Color ControlOnLight,
        Color? GlyphLight,
        Color? ThumbOnLight,
        Color ControlOnDark,
        Color? GlyphDark,
        Color? ThumbOnDark,
        Color? ControlFillDark);

    private static AccentSet AccentFor(ArcAccent accent, bool dark)
    {
        switch (accent)
        {
            case ArcAccent.Neutral when dark:
                return new AccentSet(
                    ArcColor.Oklch(0.80, 0, 0), ArcColor.Oklch(0.91, 0, 0), 0.13, ArcColor.Oklch(0.19, 0, 0),
                    ArcColor.Oklch(0.23, 0, 0), null, null,
                    ArcColor.Oklch(0.93, 0, 0), ArcColor.Oklch(0.19, 0, 0), ArcColor.Oklch(0.19, 0, 0), null);
            case ArcAccent.Neutral:
                return new AccentSet(
                    ArcColor.Oklch(0.33, 0, 0), ArcColor.Oklch(0.24, 0, 0), 0.10, ArcColor.Oklch(1.0, 0, 0),
                    ArcColor.Oklch(0.23, 0, 0), null, null,
                    ArcColor.Oklch(0.93, 0, 0), ArcColor.Oklch(0.19, 0, 0), ArcColor.Oklch(0.19, 0, 0), null);
        }

        var light = LightAccent(accent);
        return dark
            ? new AccentSet(
                ArcColor.Hex(DarkAccentHex(accent)), ArcColor.Hex(DarkStrongHex(accent)), DarkSubtle(accent),
                ArcColor.Oklch(0.19, 0, 0), light.ControlOnLight, light.GlyphLight, null,
                ArcColor.Hex(DarkControlOn(accent)), DarkGlyph(accent), Colors.White, ArcColor.Hex(DarkControlOn(accent)))
            : new AccentSet(
                ArcColor.Hex(LightAccentHex(accent)), ArcColor.Hex(LightStrongHex(accent)), LightSubtle(accent),
                light.Foreground, light.ControlOnLight, light.GlyphLight, null,
                ArcColor.Oklch(0.93, 0, 0), ArcColor.Oklch(0.19, 0, 0), ArcColor.Oklch(0.19, 0, 0), null);
    }

    private static AccentSet LightAccent(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => new AccentSet(Colors.Transparent, Colors.Transparent, 0.13, Colors.White, ArcColor.Hex("#7747ff"), null, null, Colors.Transparent, null, null, null),
        ArcAccent.Blue => new AccentSet(Colors.Transparent, Colors.Transparent, 0.12, Colors.White, ArcColor.Hex("#0562ef"), null, null, Colors.Transparent, null, null, null),
        ArcAccent.Green => new AccentSet(Colors.Transparent, Colors.Transparent, 0.13, ArcColor.Hex("#102016"), ArcColor.Hex("#10a870"), null, null, Colors.Transparent, null, null, null),
        ArcAccent.Amber => new AccentSet(Colors.Transparent, Colors.Transparent, 0.16, ArcColor.Hex("#20190d"), ArcColor.Hex("#eda11a"), ArcColor.Hex("#2a1c03"), null, Colors.Transparent, null, null, null),
        ArcAccent.Orange => new AccentSet(Colors.Transparent, Colors.Transparent, 0.13, ArcColor.Hex("#17130f"), ArcColor.Hex("#f07818"), null, null, Colors.Transparent, null, null, null),
        ArcAccent.Coral => new AccentSet(Colors.Transparent, Colors.Transparent, 0.14, ArcColor.Hex("#21110f"), ArcColor.Hex("#ec564b"), null, null, Colors.Transparent, null, null, null),
        ArcAccent.Rose => new AccentSet(Colors.Transparent, Colors.Transparent, 0.14, ArcColor.Hex("#250f1b"), ArcColor.Hex("#e64494"), null, null, Colors.Transparent, null, null, null),
        _ => new AccentSet(Colors.Transparent, Colors.Transparent, 0.10, Colors.White, ArcColor.Oklch(0.23, 0, 0), null, null, Colors.Transparent, null, null, null)
    };

    private static string LightAccentHex(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => "#7747ff",
        ArcAccent.Blue => "#0562ef",
        ArcAccent.Green => "#0db879",
        ArcAccent.Amber => "#f3ad20",
        ArcAccent.Orange => "#f48120",
        ArcAccent.Coral => "#f15f55",
        ArcAccent.Rose => "#ed4e9d",
        _ => "#545454"
    };

    private static string LightStrongHex(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => "#5528ce",
        ArcAccent.Blue => "#074db7",
        ArcAccent.Green => "#087c54",
        ArcAccent.Amber => "#9c6300",
        ArcAccent.Orange => "#aa4700",
        ArcAccent.Coral => "#b92c27",
        ArcAccent.Rose => "#ae2570",
        _ => "#3d3d3d"
    };

    private static string DarkAccentHex(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => "#a78bff",
        ArcAccent.Blue => "#6ba3ff",
        ArcAccent.Green => "#53dca6",
        ArcAccent.Amber => "#ffcb62",
        ArcAccent.Orange => "#ffad5e",
        ArcAccent.Coral => "#ff9386",
        ArcAccent.Rose => "#ff8bc5",
        _ => "#cccccc"
    };

    private static string DarkStrongHex(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => "#c7b7ff",
        ArcAccent.Blue => "#a4c7ff",
        ArcAccent.Green => "#93efd0",
        ArcAccent.Amber => "#ffe0a1",
        ArcAccent.Orange => "#ffd0a0",
        ArcAccent.Coral => "#ffc1ba",
        ArcAccent.Rose => "#ffc0df",
        _ => "#e8e8e8"
    };

    private static string DarkControlOn(ArcAccent accent) => accent switch
    {
        ArcAccent.Violet => "#8a66ff",
        ArcAccent.Blue => "#3b82f6",
        ArcAccent.Green => "#1fae78",
        ArcAccent.Amber => "#e8a52a",
        ArcAccent.Orange => "#ef873a",
        ArcAccent.Coral => "#ec6a60",
        ArcAccent.Rose => "#e457a0",
        _ => "#ededed"
    };

    private static Color? DarkGlyph(ArcAccent accent) => accent switch
    {
        ArcAccent.Amber => ArcColor.Hex("#241803"),
        ArcAccent.Orange => ArcColor.Hex("#17130f"),
        _ => Colors.White
    };

    private static double LightSubtle(ArcAccent accent) => accent switch
    {
        ArcAccent.Amber => 0.16,
        ArcAccent.Neutral => 0.10,
        _ => accent is ArcAccent.Blue ? 0.12 : 0.13
    };

    private static double DarkSubtle(ArcAccent accent) => accent switch
    {
        ArcAccent.Neutral => 0.13,
        _ => 0.20
    };

    /// <summary>
    /// Chart series colours: the accent itself plus three hues rotated around it at lightness steps,
    /// mirroring the CSS okLCH derivation in foundation.css. A neutral accent falls back to blue.
    /// </summary>
    private static (Color S1, Color S2, Color S3, Color S4) Series(Color accentColor, bool dark)
    {
        var (l, c, h) = ArcColor.ToOklch(accentColor);
        if (c < 0.02)
        {
            return dark
                ? (ArcColor.Hex("#55adff"), ArcColor.Hex("#9c37be"), ArcColor.Hex("#e66e00"), ArcColor.Hex("#00a861"))
                : (ArcColor.Hex("#1f7dcf"), ArcColor.Hex("#7128a5"), ArcColor.Hex("#e58f00"), ArcColor.Hex("#009e84"));
        }

        if (!dark)
        {
            var s1 = ArcColor.Oklch(l, c, h);
            var s2 = ArcColor.Oklch(Math.Clamp(l - 0.25, 0.45, 0.76), 0.19, h + 57);
            var s3 = ArcColor.Oklch(Math.Clamp(l + 0.14, 0.45, 0.76), 0.18, h + 184);
            var s4 = ArcColor.Oklch(Math.Clamp(l, 0.45, 0.76), 0.21, h + 292);
            return (s1, s2, s3, s4);
        }

        var d1 = ArcColor.Oklch(l, c, h);
        var d2 = ArcColor.Oklch(Math.Clamp(l - 0.19, 0.485, 0.665), 0.21, h + 66);
        var d3 = ArcColor.Oklch(Math.Clamp(l + 0.02, 0.485, 0.665), 0.21, h + 175);
        var d4 = ArcColor.Oklch(Math.Clamp(l - 0.10, 0.485, 0.665), 0.18, h + 270);
        return (d1, d2, d3, d4);
    }
}




