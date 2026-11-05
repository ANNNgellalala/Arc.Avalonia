using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feature.DataGrid;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;
using Agent = Arc.Avalonia.Agent;
using AgentUi = Arc.Avalonia.AgentUI.Controls;

namespace Arc.Avalonia.Verify;

/// <summary>
/// Headless verification for the bits that must not depend on a desktop: the OKLCH colour engine,
/// the motion tokens, and the public catalog inventory. Kept as a plain console app because the
/// sandbox this repository is developed in cannot start the vstest host process.
/// </summary>
public static class Program
{
    private static readonly List<string> Failures = new();
    private static int _checks;

    public static int Main()
    {
        ColourChecks();
        MotionChecks();
        CatalogChecks();
        ThemeChecks();
        IconChecks();
        ThemeOptionChecks();
        ListChecks();
        AgentChecks();

        Console.WriteLine($"VERIFY: checks={_checks} failures={Failures.Count}");
        foreach (var failure in Failures)
        {
            Console.WriteLine("  FAIL: " + failure);
        }

        return Failures.Count == 0 ? 0 : 1;
    }

    private static void ColourChecks()
    {
        var hex = ArcColor.Hex("#1e90ff");
        Check("hex channels", hex.R == 0x1e && hex.G == 0x90 && hex.B == 0xff && hex.A == 255);

        var alpha = ArcColor.Hex("#80112233");
        Check("hex alpha prefix", alpha.A is >= 127 and <= 129 && alpha.R == 0x11 && alpha.G == 0x22 && alpha.B == 0x33);

        var colour = ArcColor.Oklch(0.6, 0.08, 240, 0.75);
        var (l, c, _) = ArcColor.ToOklch(colour);
        Check("oklch round trip", Math.Abs(l - 0.6) < 0.01 && Math.Abs(c - 0.08) < 0.01 && colour.A is >= 191 and <= 192);

        var black = Colors.Black;
        var white = Colors.White;
        var mixZero = ArcColor.Mix(black, white, 0);
        var mixOne = ArcColor.Mix(black, white, 1);
        var mixHalf = ArcColor.Mix(black, white, 0.5);
        Check("mix keeps endpoints",
            Math.Abs(mixZero.R - black.R) <= 1 && Math.Abs(mixZero.G - black.G) <= 1 && Math.Abs(mixZero.B - black.B) <= 1 &&
            Math.Abs(mixOne.R - white.R) <= 1 && Math.Abs(mixOne.G - white.G) <= 1 && Math.Abs(mixOne.B - white.B) <= 1 &&
            mixHalf.R is >= 90 and <= 210);

        var translucent = ArcColor.WithAlpha(hex, 0.4);
        Check("with alpha keeps channels",
            translucent.R == hex.R && translucent.G == hex.G && translucent.B == hex.B &&
            translucent.A is >= 101 and <= 103);

        var tokenFields = typeof(ArcTokens).GetFields().Count(f => f.IsLiteral && f.FieldType == typeof(string));
        // 82 declared constants must all materialise in ArcPalette.Build for every scheme/accent combo
        // (ControlThumbShadow was the one that used to be computed but never registered), and the
        // injectable IArcResourceHost must feed ArcResources without a live Application.
        Check("token count, palette coverage and resource host",
            tokenFields == 82 && PaletteCoversAllTokens() && ResourceHostChecks());
        Check("token keys",
            ArcTokens.Background == "ArcBackground" &&
            ArcTokens.Surface == "ArcSurface" &&
            ArcTokens.Border == "ArcBorder" &&
            ArcTokens.Accent == "ArcAccent" &&
            ArcTokens.Foreground == "ArcForeground" &&
            ArcTokens.RadiusSurface == "ArcRadiusSurface");
    }

    private static void MotionChecks()
    {
        var easings = new (string Name, Easing Easing)[]
        {
            ("Enter", ArcMotion.EaseEnter),
            ("Standard", ArcMotion.EaseStandard),
            ("Exit", ArcMotion.EaseExit),
            ("InOut", ArcMotion.EaseInOut),
            ("SpringSmooth", ArcMotion.SpringSmooth),
            ("SpringSnappy", ArcMotion.SpringSnappy),
            ("SpringMorph", ArcMotion.SpringMorph),
        };

        foreach (var (name, easing) in easings)
        {
            Check($"{name} pinned at 0", Math.Abs(easing.Ease(0)) < 1e-6);
            Check($"{name} pinned at 1", Math.Abs(easing.Ease(1) - 1) < 1e-6);
        }

        var previous = 0.0;
        var monotonic = true;
        for (var i = 0; i <= 100; i++)
        {
            var value = ArcMotion.SpringSmooth.Ease(i / 100.0);
            monotonic &= value >= 0 && value <= 1 && value >= previous - 1e-9;
            previous = value;
        }

        Check("spring smooth never overshoots", monotonic);
        Check("duration order",
            ArcMotion.Instant < ArcMotion.Fast &&
            ArcMotion.Fast < ArcMotion.Standard &&
            ArcMotion.Standard < ArcMotion.Considered);
        Check("spring durations",
            ArcMotion.SpringSmoothDuration == TimeSpan.FromSeconds(0.40) &&
            ArcMotion.SpringSnappyDuration == TimeSpan.FromSeconds(0.26) &&
            ArcMotion.SpringMorphDuration == TimeSpan.FromSeconds(0.42));
    }

    private static void CatalogChecks()
    {
        var items = ArcCatalog.AllItems;
        Check("catalog total", items.Count == 294);
        Check("catalog components", ArcCatalog.ComponentGroups.Sum(g => g.Items.Count) == 128);
        Check("catalog blocks", ArcCatalog.BlockGroups.Sum(g => g.Items.Count) == 111);
        Check("catalog specials", ArcCatalog.SpecialGroups.Sum(g => g.Items.Count) == 55);
        Check("catalog component categories", ArcCatalog.ComponentGroups.Count == 23);
        Check("catalog special groups", ArcCatalog.SpecialGroups.Count == 5 && ArcCatalog.SpecialGroups.All(g => g.Items.Count == 11));
        Check("catalog block groups",
            ArcCatalog.BlockGroups.Select(g => g.Items.Count).SequenceEqual(new[] { 18, 18, 21, 16, 20, 18 }));
        Check("catalog slugs unique", items.Select(i => i.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count);
        Check("catalog descriptions", items.All(i => !string.IsNullOrWhiteSpace(i.Description)));

        var button = ArcCatalog.Get("button");
        var glass = ArcCatalog.Get("glass-menu");
        var siteHeader = ArcCatalog.Get("site-header");
        Check("catalog resolves known items",
            button is { Category: "Buttons" } &&
            glass is { Kind: "special" } &&
            siteHeader is { Kind: "block" });
    }

    /// <summary>
    /// M1-T3: tokens live in ThemeDictionaries (Light/Dark) and the 47 Fluent keys are covered per variant,
    /// so both the token surface and the bare-Fluent reskin follow the active theme and accent.
    /// </summary>
    private static void ThemeChecks()
    {
        var tokenKeys = TokenKeys();
        var neutral = ArcPalette.BuildThemeDictionaries(ArcAccent.Neutral);

        Check("token dictionaries have Light and Dark",
            neutral.ThemeDictionaries.ContainsKey(ThemeVariant.Light) &&
            neutral.ThemeDictionaries.ContainsKey(ThemeVariant.Dark));
        Check("token dictionaries cover every token (both variants)",
            CoversAllKeys(neutral, tokenKeys));

        var neutralLight = Variant(neutral, ThemeVariant.Light);
        var neutralDark = Variant(neutral, ThemeVariant.Dark);
        Check("token dictionaries differ per variant",
            BrushColor(neutralLight, ArcTokens.Background) != BrushColor(neutralDark, ArcTokens.Background) &&
            BrushColor(neutralLight, ArcTokens.Background) is not null);

        // At least two accents must visibly swap the accent token in both schemes (M1-T3 DoD).
        foreach (var accent in new[] { ArcAccent.Violet, ArcAccent.Rose })
        {
            var themed = ArcPalette.BuildThemeDictionaries(accent);
            Check($"accent {accent} swaps the accent token (both variants)",
                BrushColor(Variant(themed, ThemeVariant.Light), ArcTokens.Accent) != BrushColor(neutralLight, ArcTokens.Accent) &&
                BrushColor(Variant(themed, ThemeVariant.Dark), ArcTokens.Accent) != BrushColor(neutralDark, ArcTokens.Accent));
        }

        Check("Fluent key table has 47 entries", ArcFluentKeys.All.Count == 47);

        var overrides = ArcFluentOverrides.Build(new ArcThemeOptions { Accent = ArcAccent.Neutral });
        Check("Fluent overrides have Light and Dark",
            overrides.ThemeDictionaries.ContainsKey(ThemeVariant.Light) &&
            overrides.ThemeDictionaries.ContainsKey(ThemeVariant.Dark));
        Check("Fluent overrides cover every declared key (both variants)",
            CoversAllKeys(overrides, ArcFluentKeys.All));

        var lightOverrides = Variant(overrides, ThemeVariant.Light);
        var darkOverrides = Variant(overrides, ThemeVariant.Dark);
        Check("Fluent overrides differ per variant",
            BrushColor(lightOverrides, ArcFluentKeys.TextControlBackground) !=
            BrushColor(darkOverrides, ArcFluentKeys.TextControlBackground));

        var expectedRadius = ArcPalette.Build(ArcColorScheme.Light, ArcAccent.Neutral)[ArcTokens.RadiusControl];
        Check("ControlCornerRadius follows the Arc radius token",
            lightOverrides[ArcFluentKeys.ControlCornerRadius] is CornerRadius corner &&
            expectedRadius is CornerRadius expected &&
            corner == expected);

        var violetOverrides = Variant(ArcFluentOverrides.Build(new ArcThemeOptions { Accent = ArcAccent.Violet }), ThemeVariant.Light);
        Check("accent reaches the Fluent overrides",
            ColorOf(lightOverrides[ArcFluentKeys.TextControlSelectionHighlightColor]) !=
            ColorOf(violetOverrides[ArcFluentKeys.TextControlSelectionHighlightColor]));

        Check("accent colours differ between schemes",
            ArcPalette.AccentColor(ArcColorScheme.Light, ArcAccent.Neutral) !=
            ArcPalette.AccentColor(ArcColorScheme.Dark, ArcAccent.Neutral));
    }

    /// <summary>
    /// M2-G3g: the icon table carries the high-frequency additions, every path is a valid geometry, and the
    /// runtime registry (registration / override / provider / unknown fallback) behaves as documented.
    /// </summary>
    private static void IconChecks()
    {
        // 161 original Arc icons + 103 high-frequency Lucide additions fetched from lucide-static 0.545.0.
        Check("icon table has the high-frequency additions", ArcIconData.Paths.Count >= 264);
        Check("every icon path parses", AllIconPathsParse());

        var builtInCheck = ArcIconData.Paths["check"];
        ArcIcon.Register("test-spark", "M12 2v20");
        Check("runtime icon registration",
            ArcIcon.TryGetPath("test-spark", out var spark) && spark == "M12 2v20");
        Check("ArcIconData.Paths stays read-only for runtime additions", !ArcIconData.Paths.ContainsKey("test-spark"));

        ArcIcon.Register("check", "M4 4 20 20");
        Check("runtime registration overrides a built-in",
            ArcIcon.TryGetPath("check", out var overridden) && overridden == "M4 4 20 20");
        ArcIcon.Unregister("check");
        Check("unregister restores the built-in",
            ArcIcon.TryGetPath("check", out var restored) && restored == builtInCheck);
        ArcIcon.Unregister("test-spark");

        Check("unknown icon falls back to nothing", !ArcIcon.TryGetPath("no-such-icon", out _));

        var provider = new FakeIconProvider();
        ArcIcon.Register(provider);
        try
        {
            Check("provider supplies new icons",
                ArcIcon.TryGetPath("provider-icon", out var providerPath) && providerPath == "M2 2h20v20");
            Check("provider does not shadow a built-in",
                ArcIcon.TryGetPath("check", out var providerCheck) && providerCheck == builtInCheck);
            ArcIcon.Register("check", "M1 1 2 2");
            Check("registration wins over provider and built-in",
                ArcIcon.TryGetPath("check", out var registered) && registered == "M1 1 2 2");
        }
        finally
        {
            ArcIcon.Unregister("check");
            ArcIconRegistry.Unregister(provider);
            ArcIconRegistry.ClearProviders();
        }

        var rejected = false;
        try
        {
            ArcIcon.Register("  ", "M0 0");
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check("blank icon names are rejected", rejected);
    }

    private static bool AllIconPathsParse()
    {
        // The console harness has no Avalonia platform, so Geometry.Parse (which needs
        // IPlatformRenderInterface) is unavailable here; validate the path grammar structurally instead.
        // The Gallery icons page renders the same table on the UI thread (screenshot in
        // .recon/theme-shots/), which is the real "does it parse and draw" check.
        foreach (var (name, data) in ArcIconData.Paths)
        {
            if (!IconPathLooksValid(data))
            {
                Console.WriteLine($"  ICON PATH INVALID: {name}: '{data}'");
                return false;
            }
        }

        return true;
    }

    /// <summary>Structural check for Lucide-style path data: move command first, known commands only.</summary>
    private static bool IconPathLooksValid(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        var trimmed = data.TrimStart();
        if (trimmed[0] is not ('M' or 'm'))
        {
            return false;
        }

        const string commands = "MmZzLlHhVvCcSsQqTtAa";
        foreach (var character in trimmed)
        {
            if (commands.IndexOf(character) >= 0 || char.IsDigit(character) ||
                character is ' ' or ',' or '.' or '-' or '+')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// M2-G3h: custom brand accents are derived in OKLCH (hue kept, foreground chosen by contrast),
    /// density reorders height/spacing tokens, and the radius scale leaves the pill radius alone.
    /// </summary>
    private static void ThemeOptionChecks()
    {
        var seeds = new[]
        {
            Color.FromRgb(0x6C, 0x4B, 0xF4), Colors.Black, Colors.White, Color.FromRgb(0x00, 0xFF, 0x88)
        };

        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            foreach (var seed in seeds)
            {
                var options = new ArcThemeOptions { Scheme = scheme, AccentColor = seed };
                var palette = ArcPalette.Build(scheme, options);
                var accent = BrushColor(palette, ArcTokens.Accent);
                var foreground = BrushColor(palette, ArcTokens.AccentForeground);
                Check($"custom accent resolves {scheme}/{seed}", accent is not null && foreground is not null);
                if (accent is Color accentColour && foreground is Color foregroundColour)
                {
                    Check($"custom accent contrast {scheme}/{seed} >= 4.5",
                        ArcColor.ContrastRatio(accentColour, foregroundColour) >= 4.5);
                }

                // The value that drives Fluent's SystemAccentColor must be the same colour as the token.
                Check($"custom accent drives the palette accent {scheme}/{seed}",
                    ArcPalette.AccentColor(scheme, options) == accent);
            }
        }

        var brand = Color.FromRgb(0x6C, 0x4B, 0xF4);
        var derived = BrushColor(ArcPalette.Build(ArcColorScheme.Light, new ArcThemeOptions { AccentColor = brand }), ArcTokens.Accent);
        if (derived is Color derivedColour)
        {
            var (_, seedChroma, seedHue) = ArcColor.ToOklch(brand);
            var (_, derivedChroma, derivedHue) = ArcColor.ToOklch(derivedColour);
            var hueDelta = Math.Abs(seedHue - derivedHue);
            hueDelta = Math.Min(hueDelta, 360 - hueDelta);
            Check("custom accent keeps the seed hue",
                seedChroma > 0.05 && derivedChroma > 0.02 && hueDelta < 30);
        }

        double HeightOf(ArcDensity density) =>
            ArcPalette.Build(ArcColorScheme.Light, new ArcThemeOptions { Density = density })[ArcTokens.ControlHeightMd] is double value ? value : 0;
        double SpaceOf(ArcDensity density) =>
            ArcPalette.Build(ArcColorScheme.Light, new ArcThemeOptions { Density = density })[ArcTokens.Space4] is double value ? value : 0;

        Check("density orders control heights",
            HeightOf(ArcDensity.Compact) < HeightOf(ArcDensity.Default) &&
            HeightOf(ArcDensity.Default) < HeightOf(ArcDensity.Comfortable));
        Check("density orders spacing",
            SpaceOf(ArcDensity.Compact) < SpaceOf(ArcDensity.Default) &&
            SpaceOf(ArcDensity.Default) < SpaceOf(ArcDensity.Comfortable));

        var scaled = ArcPalette.Build(ArcColorScheme.Light, new ArcThemeOptions { CornerRadiusScale = 1.5 });
        var baseRadius = ArcPalette.Build(ArcColorScheme.Light, ArcAccent.Neutral)[ArcTokens.RadiusControl];
        Check("corner radius scale multiplies RadiusControl",
            baseRadius is CornerRadius baseCorner &&
            scaled[ArcTokens.RadiusControl] is CornerRadius scaledCorner &&
            Math.Abs(scaledCorner.TopLeft - Math.Round(baseCorner.TopLeft * 1.5)) < 0.01);
        Check("corner radius scale leaves the pill radius alone",
            scaled[ArcTokens.RadiusPill] is CornerRadius pill && pill.TopLeft > 1000);

        Check("theme option dictionaries cover every token",
            CoversAllKeys(
                ArcPalette.BuildThemeDictionaries(new ArcThemeOptions
                {
                    AccentColor = brand, Density = ArcDensity.Compact, CornerRadiusScale = 1.2
                }),
                TokenKeys()));

        Check("legacy preset overload still matches the options path",
            ArcPalette.AccentColor(ArcColorScheme.Light, ArcAccent.Violet) ==
            ArcPalette.AccentColor(ArcColorScheme.Light, new ArcThemeOptions { Accent = ArcAccent.Violet }));
    }

    private sealed class FakeIconProvider : IArcIconProvider
    {
        public bool TryGetPath(string name, out string? pathData)
        {
            if (name == "provider-icon")
            {
                pathData = "M2 2h20v20";
                return true;
            }

            pathData = null;
            return false;
        }
    }

    private static IReadOnlyList<string> TokenKeys() => typeof(ArcTokens)
        .GetFields()
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    private static ResourceDictionary Variant(ResourceDictionary themeAware, ThemeVariant variant) =>
        (ResourceDictionary)themeAware.ThemeDictionaries[variant];

    private static bool CoversAllKeys(ResourceDictionary themeAware, IReadOnlyList<string> keys)
    {
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dictionary = Variant(themeAware, variant);
            foreach (var key in keys)
            {
                if (!dictionary.ContainsKey(key))
                {
                    Console.WriteLine($"  MISSING KEY: {key} [{variant}]");
                    return false;
                }
            }
        }

        return true;
    }

    private static Color? BrushColor(ResourceDictionary dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is ISolidColorBrush brush ? brush.Color : null;

    private static Color? ColorOf(object? value) =>
        value is Color color ? color : value is ISolidColorBrush brush ? brush.Color : null;

    /// <summary>Every ArcTokens constant must be registered for Light/Dark x every accent (16 palettes).</summary>
    private static bool PaletteCoversAllTokens()
    {
        var keys = typeof(ArcTokens)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        if (keys.Length < 82)
        {
            Console.WriteLine($"  MISSING TOKENS: only {keys.Length} declared, expected at least 82");
            return false;
        }

        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            foreach (var accent in Enum.GetValues<ArcAccent>())
            {
                var palette = ArcPalette.Build(scheme, accent);
                foreach (var key in keys)
                {
                    if (!palette.ContainsKey(key))
                    {
                        Console.WriteLine($"  MISSING TOKEN: {key} in ArcPalette.Build({scheme}, {accent})");
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>ArcResources must consult the injected host (default host = Application.Current).</summary>
    private static bool ResourceHostChecks()
    {
        var host = new FakeResourceHost();
        host.Values[ArcTokens.Background] = Brushes.Red;
        host.Values[ArcTokens.TextBase] = 42.0;
        host.Values[ArcTokens.RadiusControl] = new CornerRadius(7);
        host.Values[ArcTokens.DurationFast] = TimeSpan.FromSeconds(1.5);
        host.Values[ArcTokens.ShadowFloating] = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 2,
            Blur = 4,
            Color = Colors.Black
        });
        host.DarkOnly[ArcTokens.Series1] = Brushes.Blue;

        ArcResources.UseHost(host);
        try
        {
            var ok = true;

            void Expect(string label, bool passed, object? actual = null)
            {
                if (!passed)
                {
                    ok = false;
                    Console.WriteLine($"  RESOURCE HOST: {label} failed (actual={actual})");
                }
            }

            Expect("Brush", ReferenceEquals(ArcResources.Brush(ArcTokens.Background), Brushes.Red),
                ArcResources.Brush(ArcTokens.Background));
            Expect("Color", ArcResources.Color(ArcTokens.Background) == Colors.Red,
                ArcResources.Color(ArcTokens.Background));
            Expect("Double", ArcResources.Double(ArcTokens.TextBase) == 42.0,
                ArcResources.Double(ArcTokens.TextBase));
            Expect("Radius", ArcResources.Radius(ArcTokens.RadiusControl) == new CornerRadius(7),
                ArcResources.Radius(ArcTokens.RadiusControl));
            Expect("Duration", ArcResources.Duration(ArcTokens.DurationFast) == TimeSpan.FromSeconds(1.5),
                ArcResources.Duration(ArcTokens.DurationFast));
            Expect("Shadows", ArcResources.Shadows(ArcTokens.ShadowFloating).Count == 1,
                ArcResources.Shadows(ArcTokens.ShadowFloating).Count);
            // Variant-aware lookups reach ThemeDictionaries-style entries (ADR 0003 §4.2). The untargeted
            // overload must follow the host's ActualThemeVariant (M1-T3 fix) instead of silently missing
            // every theme-dictionary token.
            Expect("untargeted lookup follows the active variant",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1), Brushes.Blue),
                ArcResources.Brush(ArcTokens.Series1));
            Expect("explicit light lookup skips dark-only entries",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Light), Brushes.Transparent),
                ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Light));
            Expect("variant lookup uses theme-dictionary entries",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Dark), Brushes.Blue),
                ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Dark));

            return ok;
        }
        finally
        {
            ArcResources.UseHost(null);
        }
    }

    private sealed class FakeResourceHost : IArcResourceHost
    {
        public Dictionary<string, object?> Values { get; } = new();
        public Dictionary<string, object?> DarkOnly { get; } = new();

        public ThemeVariant? ActualThemeVariant => ThemeVariant.Dark;

        public bool TryFindResource(string key, out object? value) => Values.TryGetValue(key, out value);

        public bool TryFindResource(string key, ThemeVariant variant, out object? value)
        {
            if (variant.Equals(ThemeVariant.Dark) && DarkOnly.TryGetValue(key, out value))
            {
                return true;
            }

            return Values.TryGetValue(key, out value);
        }
    }

    /// <summary>
    /// M2-G3c: the tree table's window arithmetic, the bounded row-reuse cache and the Feature DataGrid
    /// host's paging contract. All three are pure logic, so they run in this no-UI console harness.
    /// </summary>
    private static void ListChecks()
    {
        // Tree table window: 10,000 rows, 44 px rows, 600 px viewport, 6 rows overscan.
        const int total = 10_000;
        const double rowHeight = 44;
        const double viewport = 600;
        const int overscan = ArcTreeTableWindow.DefaultOverscan;
        var viewportRows = (int)Math.Ceiling(viewport / rowHeight);

        var (first, last) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, 0, overscan);
        var realized = last - first + 1;
        Check("tree window starts at the top", first == 0 && last >= viewportRows - 1);
        Check("tree window is viewport-sized, not 10k", realized <= viewportRows + 2 * overscan + 1);
        Check("tree window within 2x viewport budget", realized <= viewportRows * 2);

        var (midFirst, midLast) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, 220_000, overscan);
        Check("tree window follows the scroll offset", midFirst > 4_900 && midLast < 5_100);
        Check("tree window at mid is viewport-sized", midLast - midFirst + 1 <= viewportRows + 2 * overscan + 1);

        var (endFirst, endLast) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, total * rowHeight - viewport, overscan);
        Check("tree window clamps to the last row", endLast == total - 1 && endFirst < endLast);

        var (emptyFirst, emptyLast) = ArcTreeTableWindow.Compute(0, rowHeight, viewport, 0, overscan);
        Check("tree window empty data", emptyFirst == 0 && emptyLast == -1);
        var (tinyFirst, tinyLast) = ArcTreeTableWindow.Compute(3, rowHeight, viewport, 0, overscan);
        Check("tree window tiny data", tinyFirst == 0 && tinyLast == 2);

        // Row reuse cache: re-entry returns the same instance, capacity evicts the oldest entry.
        var cache = new ArcRowWindowCache(2);
        var firstControl = new Border();
        var secondControl = new Border();
        var thirdControl = new Border();
        cache.Return(1, firstControl);
        cache.Return(2, secondControl);
        _ = secondControl;
        cache.Return(3, thirdControl);
        Check("row cache evicts the oldest entry", cache.Count == 2 && !cache.TryTake(1, out _));
        Check("row cache reuses the same control instance",
            cache.TryTake(3, out var reused) && ReferenceEquals(reused, thirdControl));

        // Feature DataGrid host: defaults, page count and slicing are public contract.
        var host = new ArcDataGridHost();
        Check("feature host defaults", host.PageSize == 100 && host.PageIndex == 0 && host.TotalItems == -1 && host.AutoPage);
        host.ItemsSource = Enumerable.Range(0, 250).Cast<object?>().ToArray();
        Check("feature host page count", host.PageCount == 3);
        host.NextPage();
        Check("feature host slices the middle page", host.CurrentPageItems.Count == 100 && host.PageIndex == 1);
        host.LastPage();
        Check("feature host last page is short", host.CurrentPageItems.Count == 50 && host.PageCount == 3);
    }

    /// <summary>
    /// M2-G3b: the service layer's headless contract. A fake presenter, sink and file picker exercise the
    /// generic dialog result plumbing, the dialog queue, notification de-duplication and file-picker injection
    /// without a platform window.
    /// </summary>
    private static void ServiceChecks()
    {
        ArcServiceProvider.Reset();

        // Registry round trip + fake file picker injection (what the file controls resolve at click time).
        var picker = new FakeFilePickerService();
        ArcServiceProvider.Register<IFilePickerService>(picker);
        var found = ArcServiceProvider.TryGet<IFilePickerService>(out var resolved) && ReferenceEquals(resolved, picker);
        var files = resolved!.OpenFileAsync(new FilePickerOpenOptions { AllowMultiple = true }).GetAwaiter().GetResult();
        Check("service provider registration round trip", found);
        Check("file picker fake is called through the service", picker.Calls == 1 && files.Count == 0);

        // Dialog result plumbing + one-at-a-time queue.
        var presenter = new FakeDialogPresenter();
        var dialogs = new ArcDialogService(presenter);
        var confirmTask = dialogs.ConfirmAsync(new ArcConfirmRequest { Title = "Delete?", Message = "Sure?" });
        var renameTask = dialogs.ShowAsync(new ArcDialogRequest<string>
        {
            Title = "Rename",
            PrimaryResult = "renamed"
        });
        var confirmed = confirmTask.GetAwaiter().GetResult();
        var renamed = renameTask.GetAwaiter().GetResult();
        Check("dialog confirm returns the confirm value", confirmed);
        Check("dialog generic result round trip", renamed == "renamed" && presenter.Requests == 2);
        Check("dialog service serialises one dialog at a time", presenter.MaxConcurrent == 1);

        // Notification de-duplication + lifecycle.
        var sink = new FakeNotificationSink();
        var notifications = new ArcNotificationService(sink);
        var dismissed = 0;
        notifications.Dismissed += (_, _) => dismissed++;
        var first = notifications.Show(new ArcNotification { Title = "Saved", Key = "save" });
        var second = notifications.Show(new ArcNotification { Title = "Saved again", Key = "save" });
        var dismissedByKey = notifications.Dismiss("save");
        Check("notification dedup by key", first && !second && sink.Presented == 1);
        Check("notification lifecycle", dismissedByKey && notifications.Active.Count == 0 && dismissed == 1);

        // Navigation stack over the minimal frame.
        var frame = new ContentControl();
        var navigation = new ArcNavigationService();
        navigation.Attach(frame);
        navigation.Navigate("a");
        navigation.Navigate("b");
        var wentBack = navigation.GoBack();
        navigation.Replace("c");
        Check("navigation stack pushes, pops and replaces",
            wentBack && navigation.Stack.Count == 1 && (string?)navigation.Current == "c" &&
            ReferenceEquals(frame.Content, "c"));

        ArcServiceProvider.Reset();
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<IStorageFile>> OpenFileAsync(
            FilePickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFile>>(Array.Empty<IStorageFile>());
        }

        public Task<IStorageFile?> SaveFileAsync(
            FilePickerSaveOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IStorageFile?>(null);
        }

        public Task<IReadOnlyList<IStorageFolder>> PickFolderAsync(
            FolderPickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFolder>>(Array.Empty<IStorageFolder>());
        }
    }

    private sealed class FakeDialogPresenter : IArcDialogPresenter
    {
        private int _active;
        private int _requests;

        public int Requests => _requests;

        public int MaxConcurrent { get; private set; }

        public async Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken)
        {
            _requests++;
            _active++;
            MaxConcurrent = Math.Max(MaxConcurrent, _active);
            try
            {
                await Task.Delay(5, cancellationToken);
                return request.PrimaryResult;
            }
            finally
            {
                _active--;
            }
        }
    }

    private sealed class FakeNotificationSink : IArcNotificationSink
    {
        public int Presented { get; private set; }

        public void Present(ArcNotification notification, Action dismiss) => Presented++;

        public void Clear()
        {
        }
    }

    /// <summary>
    /// M2-G4: the Agent top layer must be a deterministic pure reducer with explicit sequence handling,
    /// cross-run queues and an incremental transcript that never rebuilds while streaming.
    /// </summary>
    private static void AgentChecks()
    {
        var at = DateTimeOffset.UtcNow;
        static Agent.ArcAgentEvent[] BuildStream(string runId, string sessionId)
        {
            var now = DateTimeOffset.UtcNow;
            Agent.ArcEventStamp Stamp(long seq) => Agent.ArcEventStamp.Create(runId, seq, $"{runId}:{seq}");
            var run = new Agent.ArcAgentRunRef(runId, sessionId);
            return new Agent.ArcAgentEvent[]
            {
                new Agent.ArcRunStartedEvent(Stamp(1), now, run),
                new Agent.ArcStepStartedEvent(Stamp(2), now, "step-1", "Plan"),
                new Agent.ArcStepFinishedEvent(Stamp(3), now, "step-1", Agent.ArcStepStatus.Done),
                new Agent.ArcMessageStartedEvent(Stamp(4), now, "msg-1", Agent.ArcAgentRole.Assistant),
                new Agent.ArcMessageDeltaEvent(Stamp(5), now, "msg-1", "hello "),
                new Agent.ArcMessageDeltaEvent(Stamp(6), now, "msg-1", "world"),
                new Agent.ArcMessageEndedEvent(Stamp(7), now, "msg-1"),
                new Agent.ArcToolCallStartedEvent(Stamp(8), now, "call-1", "repo.search", "find", Agent.ArcRiskLevel.Low),
                new Agent.ArcToolArgsDeltaEvent(Stamp(9), now, "call-1", "{\"q\":\"x\"}"),
                new Agent.ArcToolCallEndedEvent(Stamp(10), now, "call-1", Agent.ArcToolCallStatus.Succeeded, "1 match"),
                new Agent.ArcApprovalRequestedEvent(Stamp(11), now, new Agent.ArcApprovalRequest
                {
                    RequestId = "approval-1",
                    RunId = runId,
                    CallId = "call-1",
                    ToolName = "shell.exec",
                    Risk = Agent.ArcRiskLevel.High
                }),
                new Agent.ArcAttentionRaisedEvent(Stamp(12), now, new Agent.ArcAttentionEntry
                {
                    EntryId = "attention-1",
                    RunId = runId,
                    Kind = Agent.ArcAttentionKind.Approval,
                    Title = "Approve",
                    Level = Agent.ArcAttentionLevel.Blocking,
                    State = Agent.ArcAttentionState.New,
                    CreatedAt = now
                }),
                new Agent.ArcArtifactPublishedEvent(Stamp(13), now, new Agent.ArcArtifact
                {
                    ArtifactId = "artifact-1",
                    Kind = Agent.ArcArtifactKind.Diff,
                    Title = "diff",
                    At = now
                }),
                new Agent.ArcRunPhaseChangedEvent(Stamp(14), now, Agent.ArcRunPhase.AwaitingApproval)
            };
        }

        // Replay determinism + idempotency.
        var first = new Agent.ArcAgentWorkspace();
        var results = BuildStream("run-a", "sess-a").Select(first.Apply).ToArray();
        Check("agent reducer accepts a contiguous stream", results.All(result => result == Agent.ArcApplyResult.Accepted));
        var duplicate = first.Apply(BuildStream("run-a", "sess-a")[4]);
        Check("agent reducer is idempotent by event id", duplicate == Agent.ArcApplyResult.Duplicate);

        var second = new Agent.ArcAgentWorkspace();
        foreach (var @event in BuildStream("run-a", "sess-a"))
        {
            second.Apply(@event);
        }

        var left = first.GetSession("run-a");
        var right = second.GetSession("run-a");
        Check("agent replay is deterministic",
            left is not null && right is not null &&
            left.Messages.Count == right.Messages.Count &&
            left.Messages[0].Text == right.Messages[0].Text &&
            left.ToolCalls.Count == right.ToolCalls.Count &&
            left.Run.Phase == right.Run.Phase);

        // Out-of-order gating and gap fill.
        var gapped = new Agent.ArcAgentWorkspace();
        var runRef = new Agent.ArcAgentRunRef("run-o", "sess-o");
        Check("agent runner seeds a run", gapped.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-o", 1), at, runRef)) == Agent.ArcApplyResult.Accepted);
        Check("agent runner accepts seq 2", gapped.Apply(new Agent.ArcMessageStartedEvent(Agent.ArcEventStamp.Create("run-o", 2), at, "m", Agent.ArcAgentRole.Assistant)) == Agent.ArcApplyResult.Accepted);
        Check("agent runner buffers a gap", gapped.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-o", 4), at, "m", "d4")) == Agent.ArcApplyResult.OutOfOrder);
        Check("agent runner fills the gap and drains the buffer", gapped.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-o", 3), at, "m", "d3")) == Agent.ArcApplyResult.Accepted);
        Check("agent runner applied both deltas in order", gapped.GetSession("run-o")!.Messages[0].Text == "d3d4");

        // Unknown-run buffering (a host may push session events before RunStarted).
        var orphaned = new Agent.ArcAgentWorkspace();
        Check("agent runner buffers unknown-run events", orphaned.Apply(new Agent.ArcMessageStartedEvent(Agent.ArcEventStamp.Create("run-x", 2), at, "m", Agent.ArcAgentRole.Assistant)) == Agent.ArcApplyResult.UnknownRun);
        orphaned.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-x", 1), at, new Agent.ArcAgentRunRef("run-x", "sess-x")));
        orphaned.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-x", 3), at, "m", "orphan"));
        Check("agent runner drains orphan events when the run starts", orphaned.GetSession("run-x")!.Messages.Count == 1 && orphaned.GetSession("run-x")!.Messages[0].Text == "orphan");

        // Multi-run routing and background dock data.
        var multi = new Agent.ArcAgentWorkspace();
        multi.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-fg", 1), at, new Agent.ArcAgentRunRef("run-fg", "sess-fg")));
        multi.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-bg", 1), at, new Agent.ArcAgentRunRef("run-bg", "sess-bg", Agent.ArcAgentRunKind.Background, Label: "bg")));
        Check("agent workspace routes two runs", multi.Runs.Count == 2 && multi.GetSession("run-bg")?.SessionId == "sess-bg");
        Check("agent workspace exposes background runs", multi.BackgroundRuns.Count == 1 && multi.BackgroundRuns[0].RunId == "run-bg");
        Check("agent workspace activates a session", multi.Activate("sess-fg") && multi.Active?.SessionId == "sess-fg");

        // Approval queue serialises and de-duplicates.
        var approvals = new Agent.ArcApprovalQueue();
        Check("approval queue enqueues", approvals.Enqueue(new Agent.ArcApprovalRequest { RequestId = "a", RunId = "r", CallId = "c", ToolName = "t" }));
        Check("approval queue rejects duplicate ids", !approvals.Enqueue(new Agent.ArcApprovalRequest { RequestId = "a", RunId = "r", CallId = "c", ToolName = "t" }));
        Check("approval queue enqueues second", approvals.Enqueue(new Agent.ArcApprovalRequest { RequestId = "b", RunId = "r", CallId = "c", ToolName = "t" }));
        Check("approval queue is serial (current is first)", approvals.Current?.RequestId == "a" && approvals.Count == 2);
        Check("approval queue advances after resolve", approvals.Resolve("a") && approvals.Current?.RequestId == "b");

        // Attention queue de-duplicates and sorts blocking first.
        var attention = new Agent.ArcAttentionQueue();
        attention.Upsert(new Agent.ArcAttentionEntry { EntryId = "i", RunId = "r", Kind = Agent.ArcAttentionKind.Suggestion, Title = "info", Level = Agent.ArcAttentionLevel.Info, CreatedAt = at });
        attention.Upsert(new Agent.ArcAttentionEntry { EntryId = "b", RunId = "r", Kind = Agent.ArcAttentionKind.Approval, Title = "block", Level = Agent.ArcAttentionLevel.Blocking, CreatedAt = at });
        attention.Upsert(new Agent.ArcAttentionEntry { EntryId = "i", RunId = "r", Kind = Agent.ArcAttentionKind.Suggestion, Title = "info2", Level = Agent.ArcAttentionLevel.Info, CreatedAt = at.AddSeconds(1) });
        Check("attention queue de-duplicates", attention.Entries.Count == 2);
        Check("attention queue sorts blocking first", attention.Entries[0].EntryId == "b" && attention.BlockingCount == 1);

        // Artifact index identity.
        var artifacts = new Agent.ArcArtifactIndex();
        Check("artifact index adds", artifacts.Add(new Agent.ArcArtifact { ArtifactId = "x", Kind = Agent.ArcArtifactKind.File, Title = "x", At = at }));
        Check("artifact index de-duplicates", !artifacts.Add(new Agent.ArcArtifact { ArtifactId = "x", Kind = Agent.ArcArtifactKind.File, Title = "x", At = at }));

        // Dispatcher batching: N enqueues collapse to one flush.
        var dispatcher = new Agent.QueuedUiDispatcher();
        var coalescer = new Agent.ArcDeltaCoalescer(dispatcher);
        var flushes = 0;
        var batched = 0;
        coalescer.Flush += (_, batch) =>
        {
            flushes++;
            batched += batch.Events.Count;
        };
        for (var index = 0; index < 5; index++)
        {
            coalescer.Enqueue(new Agent.ArcCustomEvent(Agent.ArcEventStamp.Create("run-c", index + 1), at, "test", "x"));
        }

        Check("coalescer batches until the dispatcher runs", flushes == 0 && coalescer.PendingCount == 5);
        dispatcher.RunPending();
        Check("coalescer flushes a batch once", flushes == 1 && batched == 5 && coalescer.FlushCount == 1);

        // Incremental transcript: 10k appends, no full rebuild, bounded realized window.
        var transcript = new AgentUi.ArcAgentTranscript();
        transcript.ResetIncremental();
        var rebuildsBefore = transcript.FullRebuildCount;
        var allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 10_000; index++)
        {
            // 200 live messages, each receiving 50 streaming deltas: realistic high-frequency updates.
            transcript.Append(new AgentUi.ArcAgentEvent
            {
                Id = $"message-{index / 50}",
                Kind = AgentUi.ArcAgentEventKind.Message,
                Title = $"streamed delta {index}",
                Status = AgentUi.ArcAgentEventStatus.Running,
                Timestamp = at
            });
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
        Check("agent transcript never rebuilds while appending", transcript.FullRebuildCount == rebuildsBefore);
        Check("agent transcript bounds realized rows to the viewport window",
            transcript.RenderedRowCount <= transcript.MaxRenderedRows &&
            transcript.RenderedRowCount <= transcript.ViewportRows * 2);
        Check("agent transcript counts all events", transcript.TotalEventCount == 10_000);
        Check("agent transcript drops old rows", transcript.DroppedRowCount > 0);
        Check("agent transcript allocation stays bounded", allocated < 256L * 1024 * 1024);
        Check("agent transcript append is fast enough for a headless guard", stopwatch.Elapsed < TimeSpan.FromSeconds(30));

        var rowsBeforeUpdate = transcript.RenderedRowCount;
        var totalBeforeUpdate = transcript.TotalEventCount;
        var update = new AgentUi.ArcAgentEvent
        {
            Id = "message-199",
            Kind = AgentUi.ArcAgentEventKind.Message,
            Title = "updated",
            Status = AgentUi.ArcAgentEventStatus.Done,
            Timestamp = at
        };
        for (var updateIndex = 0; updateIndex < 5; updateIndex++)
        {
            transcript.Append(update);
        }

        Check("agent transcript updates a streaming row in place",
            transcript.TotalEventCount == totalBeforeUpdate + 5 &&
            transcript.RenderedRowCount == rowsBeforeUpdate &&
            transcript.FullRebuildCount == rebuildsBefore);

        // View registry defaults + override.
        var registry = new Agent.ArcAgentItemViewRegistry();
        Check("view registry resolves a message view",
            registry.Resolve(new Agent.ArcAgentMessage { Id = "m", Role = Agent.ArcAgentRole.Assistant }) is Control);
        Check("view registry resolves a tool view",
            registry.Resolve(new Agent.ArcToolCall { Id = "c", ToolName = "t" }) is Control);
        Check("view registry resolves an approval view",
            registry.Resolve(new Agent.ArcApprovalRequest { RequestId = "a", RunId = "r", CallId = "c", ToolName = "t" }) is Control);
        Check("view registry resolves an artifact view",
            registry.Resolve(new Agent.ArcArtifact { ArtifactId = "a", Kind = Agent.ArcArtifactKind.File, Title = "f" }) is Control);
        Check("view registry creates a template", registry.CreateTemplate() is not null);

        // Sample workspace is host-free and deterministic.
        var sample = Agent.ArcAgentWorkspace.CreateSample();
        Check("sample workspace has foreground + background runs", sample.Sessions.Count == 2 && sample.BackgroundRuns.Count == 1);
        Check("sample workspace has a pending approval and attention entry",
            sample.PendingApprovals.Count == 1 && sample.Attention.Entries.Count == 1 && sample.Artifacts.Items.Count == 1);
    }

    private static void Check(string name, bool passed)
    {
        _checks++;
        if (!passed)
        {
            Failures.Add(name);
        }
    }
}


