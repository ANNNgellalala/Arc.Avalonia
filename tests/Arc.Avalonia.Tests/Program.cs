using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feature.DataGrid;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Markdown.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using PuraMarkdown.Agent;
using PuraMarkdown.Agent.Chat;
using PuraMarkdown.Core.Security;
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
        MarkdownChecks();
        ChatChecks();

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
        // 86 declared constants must all materialise in ArcPalette.Build for every scheme/accent combo
        // (ControlThumbShadow was the one that used to be computed but never registered), and the
        // injectable IArcResourceHost must feed ArcResources without a live Application.
        Check("token count, palette coverage and resource host",
            tokenFields == 86 && PaletteCoversAllTokens() && ResourceHostChecks());
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

        if (keys.Length < 86)
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
            left.Messages[0].Markdown == right.Messages[0].Markdown &&
            left.ToolCalls.Count == right.ToolCalls.Count &&
            left.Run.Phase == right.Run.Phase);

        // Out-of-order gating and gap fill.
        var gapped = new Agent.ArcAgentWorkspace();
        var runRef = new Agent.ArcAgentRunRef("run-o", "sess-o");
        Check("agent runner seeds a run", gapped.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-o", 1), at, runRef)) == Agent.ArcApplyResult.Accepted);
        Check("agent runner accepts seq 2", gapped.Apply(new Agent.ArcMessageStartedEvent(Agent.ArcEventStamp.Create("run-o", 2), at, "m", Agent.ArcAgentRole.Assistant)) == Agent.ArcApplyResult.Accepted);
        Check("agent runner buffers a gap", gapped.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-o", 4), at, "m", "d4")) == Agent.ArcApplyResult.OutOfOrder);
        Check("agent runner fills the gap and drains the buffer", gapped.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-o", 3), at, "m", "d3")) == Agent.ArcApplyResult.Accepted);
        Check("agent runner applied both deltas in order", gapped.GetSession("run-o")!.Messages[0].Markdown == "d3d4");

        // Unknown-run buffering (a host may push session events before RunStarted).
        var orphaned = new Agent.ArcAgentWorkspace();
        Check("agent runner buffers unknown-run events", orphaned.Apply(new Agent.ArcMessageStartedEvent(Agent.ArcEventStamp.Create("run-x", 2), at, "m", Agent.ArcAgentRole.Assistant)) == Agent.ArcApplyResult.UnknownRun);
        orphaned.Apply(new Agent.ArcRunStartedEvent(Agent.ArcEventStamp.Create("run-x", 1), at, new Agent.ArcAgentRunRef("run-x", "sess-x")));
        orphaned.Apply(new Agent.ArcMessageDeltaEvent(Agent.ArcEventStamp.Create("run-x", 3), at, "m", "orphan"));
        Check("agent runner drains orphan events when the run starts", orphaned.GetSession("run-x")!.Messages.Count == 1 && orphaned.GetSession("run-x")!.Messages[0].Markdown == "orphan");

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

        // Unified message model + conversation projection (PuraMarkdown.Agent's chat message is the only one).
        var sessionA = first.GetSession("run-a");
        Check("agent session messages use the upstream chat message model",
            sessionA is not null &&
            sessionA.Messages.Count == 1 &&
            sessionA.Messages[0] is PuraChatMessage { Markdown: "hello world" } &&
            sessionA.Messages[0].Role == PuraChatRole.Assistant);

        // Agent tool calls project to the upstream PuraToolCall model (arguments/result/status carried over).
        Check("agent session maps tool calls to the upstream PuraToolCall",
            sessionA is not null &&
            sessionA.ToolCalls.Count == 1 &&
            sessionA.ToolCalls[0] is PuraToolCall
            {
                Id: "call-1",
                Name: "repo.search",
                Status: PuraToolCallStatus.Succeeded,
                Result: "1 match"
            });

        Check("pura tool-call JSON maps to PuraToolCall",
            PuraToolCall.TryParse(
                """{"id":"call-9","name":"repo.search","status":"succeeded","arguments":{"q":"x"},"result":"1 match"}""",
                out var parsedCall,
                out _) &&
            parsedCall is
            {
                Id: "call-9",
                Name: "repo.search",
                Status: PuraToolCallStatus.Succeeded,
                Result: "1 match"
            } &&
            parsedCall.Arguments == """{"q":"x"}""");

        var conversation = new Agent.ArcAgentConversation { Session = sessionA };
        Check("agent conversation projects the full timeline", conversation.Timeline.Count == 14);

        // The conversation now renders through PuraChatView: approvals/questions are synthesized pura-agent
        // fences and tool calls ride on PuraChatMessage.ToolCalls, so assert the upstream projection (the
        // rendered controls need a visual tree, which this headless harness does not create).
        Check("agent conversation projects tool calls as PuraToolCall cards",
            conversation.Chat.Messages.Any(message =>
                message.ToolCalls is { Count: 1 } calls &&
                calls[0] is { Name: "repo.search", Status: PuraToolCallStatus.Succeeded }));
        Check("agent conversation projects approvals as pura-agent confirm components",
            conversation.Chat.Messages.Any(message =>
                string.Equals(message.Id, "arc:approval:approval-1", StringComparison.Ordinal) &&
                message.Markdown.Contains("\"type\":\"confirm\"", StringComparison.Ordinal) &&
                message.Markdown.Contains("approval-1", StringComparison.Ordinal)));

        // View registry defaults + override (messages and tool calls come from PuraMarkdown.Agent).
        var registry = new Agent.ArcAgentItemViewRegistry();
        Check("view registry resolves a message view",
            registry.Resolve(new PuraChatMessage { Id = "m", Role = PuraChatRole.Assistant }) is Control);
        Check("view registry resolves a tool view",
            registry.Resolve(new PuraToolCall { Id = "c", Name = "t" }) is Control);
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

    /// <summary>
    /// Markdown family (docs/21): PuraMarkdown parse → ArcMarkdownView render, the block/inline node matrix,
    /// fence-plugin dispatch with the code-block fallback, streaming stable boundaries plus the
    /// complete-equals-one-shot guarantee, and the chat list contracts. Runs headless through
    /// <see cref="ArcResources.UseHost"/> for tokens and <see cref="Dispatcher.RunJobs"/> for the queued
    /// streaming/chat UI work (no Application, no window).
    /// </summary>
    private static void MarkdownChecks()
    {
        var palette = ArcPalette.Build(ArcColorScheme.Light, ArcAccent.Neutral);
        ArcResources.UseHost(new MarkdownResourceHost(palette));
        var previousStyle = ArcMarkdownStyle.Current;
        try
        {
            ArcMarkdownStyle.Current = new ArcMarkdownStyle();

            var pipeline = ArcMarkdownPipeline.Default;
            Check("markdown default pipeline is commonmark + gfm",
                pipeline.PluginNames.Count == 0 && pipeline.Highlighter.CanHighlight("csharp"));

            // --- node matrix ----------------------------------------------------------
            var document = pipeline.Syntax.ParseDocument(MarkdownSample);
            var blocks = document.Markdown.ToArray();
            Check("markdown parses the node matrix",
                blocks.OfType<HeadingBlock>().Count() == 4 &&
                blocks.OfType<ParagraphBlock>().Any() &&
                blocks.OfType<ListBlock>().Count() >= 2 &&
                blocks.OfType<QuoteBlock>().Any() &&
                blocks.OfType<Table>().Any() &&
                blocks.OfType<CodeBlock>().Any() &&
                blocks.OfType<ThematicBreakBlock>().Any());

            var options = new ArcMarkdownOptions { Pipeline = pipeline };
            var context = new ArcMarkdownRenderContext
            {
                Options = options,
                Renderers = pipeline.Renderers,
                Sanitized = document.Sanitized ?? new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            var rendered = blocks.ToDictionary(block => block, block => context.RenderBlock(block));

            var headings = blocks.OfType<HeadingBlock>().ToArray();
            var headingLevels = headings
                .Select(heading => rendered[heading] is TextBlock text ? AutomationProperties.GetHeadingLevel(text) : -1)
                .ToArray();
            Check("markdown headings render with automation heading levels",
                headingLevels.SequenceEqual(new[] { 1, 2, 3, 4 }));
            Check("markdown heading text renders", TextOf(rendered[headings[0]]).Contains("一级标题"));

            var paragraph = rendered[blocks.OfType<ParagraphBlock>().First()];
            var paragraphText = TextOf(paragraph);
            var runs = AllRuns(paragraph).ToArray();
            Check("markdown paragraph renders emphasis, strong, strike and inline code",
                paragraphText.Contains("粗体") && paragraphText.Contains("斜体") &&
                paragraphText.Contains("删除线") && paragraphText.Contains("code") &&
                runs.Any(run => !run.FontWeight.Equals(FontWeight.Normal)) &&
                runs.Any(run => run.FontStyle == FontStyle.Italic) &&
                runs.Any(run => run.TextDecorations is { Count: > 0 }));
            Check("markdown paragraph renders link and image placeholder",
                Walk(paragraph).OfType<HyperlinkButton>().Any() &&
                Walk(paragraph).OfType<Image>().Any() &&
                paragraphText.Contains("图片"));

            // --- inline-link menu: copy / open in browser -------------------------------
            var linkOptions = new ArcMarkdownOptions { Pipeline = pipeline };
            var linkContext = new ArcMarkdownRenderContext
            {
                Options = linkOptions,
                Renderers = pipeline.Renderers,
                Sanitized = new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            var linkAnchor = new Button();
            var linkMenu = new ArcMarkdownLinkMenu(linkAnchor, "https://uiarc.dev", linkContext);
            var linkMenuContent = linkMenu.CreateContent();
            var linkRows = linkMenu.Rows;
            Check("markdown link menu offers copy and open rows",
                linkRows.Count == 2 &&
                Walk(linkMenuContent).OfType<ArcButton>().Count() == 2 &&
                linkRows[0].Content as string == "复制链接" &&
                linkRows[1].Content as string == "在浏览器中打开");

            Check("markdown link menu enables open for an absolute url",
                linkRows[1].IsEnabled &&
                ArcMarkdownLinkMenu.CanOpenInBrowser("https://uiarc.dev", ArcMarkdownOptions.Default) &&
                ArcMarkdownLinkMenu.CanOpenInBrowser("mailto:hi@uiarc.dev", ArcMarkdownOptions.Default));

            var relativeMenu = new ArcMarkdownLinkMenu(linkAnchor, "#anchor", linkContext);
            relativeMenu.CreateContent();
            var relativeRows = relativeMenu.Rows;
            Check("markdown link menu disables open for a relative target",
                !relativeRows[1].IsEnabled &&
                !ArcMarkdownLinkMenu.CanOpenInBrowser("#anchor", ArcMarkdownOptions.Default) &&
                !ArcMarkdownLinkMenu.CanOpenInBrowser("./local.md", ArcMarkdownOptions.Default));

            Check("markdown link menu keeps the browser row to browser schemes",
                !ArcMarkdownLinkMenu.CanOpenInBrowser("ftp://example.com/file", ArcMarkdownOptions.Default) &&
                !ArcMarkdownLinkMenu.CanOpenInBrowser("file:///C:/tmp/notes.txt", ArcMarkdownOptions.Default) &&
                ArcMarkdownLinkMenu.CanOpenInBrowser("tel:+8610000000000", ArcMarkdownOptions.Default));

            var fakeClipboard = new FakeMarkdownClipboard();
            var copyContext = new ArcMarkdownRenderContext
            {
                Options = new ArcMarkdownOptions { Pipeline = pipeline, Clipboard = fakeClipboard },
                Renderers = pipeline.Renderers,
                Sanitized = new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            var copyMenu = new ArcMarkdownLinkMenu(linkAnchor, "https://uiarc.dev/docs/links", copyContext);
            copyMenu.CreateContent();
            var copyCompleted = PumpDispatcher(copyMenu.CopyLinkAsync(), TimeSpan.FromSeconds(10));
            Check("markdown link menu copy writes the exact url and confirms it",
                copyCompleted &&
                fakeClipboard.Text == "https://uiarc.dev/docs/links" &&
                copyMenu.Rows[0].Content as string == "链接已复制");

            var fakeLinkHandler = new FakeMarkdownLinkHandler();
            var hostContext = new ArcMarkdownRenderContext
            {
                Options = new ArcMarkdownOptions { Pipeline = pipeline, LinkHandler = fakeLinkHandler },
                Renderers = pipeline.Renderers,
                Sanitized = new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            var hostMenu = new ArcMarkdownLinkMenu(linkAnchor, "https://uiarc.dev/docs/links", hostContext);
            hostMenu.CreateContent();
            var hostOpenCompleted = PumpDispatcher(hostMenu.OpenInBrowserAsync(), TimeSpan.FromSeconds(10));
            Check("markdown link menu open delegates to the host link handler",
                hostOpenCompleted &&
                hostMenu.Rows[1].IsEnabled &&
                fakeLinkHandler.Urls.Count == 1 &&
                fakeLinkHandler.Urls[0] == "https://uiarc.dev/docs/links");
            Check("markdown link menu host handler allows a relative target",
                ArcMarkdownLinkMenu.CanOpenInBrowser("./local.md", hostContext.Options));

            var absoluteOpen = ArcMarkdownPlatform.OpenInBrowserAsync(null, "https://uiarc.dev");
            var relativeOpen = ArcMarkdownPlatform.OpenInBrowserAsync(null, "relative/path");
            PumpDispatcher(Task.WhenAll(absoluteOpen, relativeOpen), TimeSpan.FromSeconds(10));
            Check("markdown platform browser launch requires a top level and an absolute uri",
                absoluteOpen.IsCompletedSuccessfully && !absoluteOpen.Result &&
                relativeOpen.IsCompletedSuccessfully && !relativeOpen.Result);

            var chineseLocalizer = ArcMarkdownLocalizer.Default;
            Check("markdown link menu localizes chinese copy",
                chineseLocalizer.Get(ArcMarkdownStringKeys.LinkMenu) == "链接操作" &&
                chineseLocalizer.Get(ArcMarkdownStringKeys.CopyLink) == "复制链接" &&
                chineseLocalizer.Get(ArcMarkdownStringKeys.LinkCopied) == "链接已复制" &&
                chineseLocalizer.Get(ArcMarkdownStringKeys.OpenInBrowser) == "在浏览器中打开" &&
                chineseLocalizer.Get(ArcMarkdownStringKeys.OpenLinkFailed) == "打开链接失败" &&
                chineseLocalizer.Get(ArcMarkdownStringKeys.OpenLinkUnavailable) == "该链接无法在浏览器中打开");

            var englishLocalizer = ArcMarkdownEnglishLocalizer.Instance;
            Check("markdown link menu localizes english copy",
                englishLocalizer.Get(ArcMarkdownStringKeys.LinkMenu) == "Link actions" &&
                englishLocalizer.Get(ArcMarkdownStringKeys.CopyLink) == "Copy link" &&
                englishLocalizer.Get(ArcMarkdownStringKeys.LinkCopied) == "Link copied" &&
                englishLocalizer.Get(ArcMarkdownStringKeys.OpenInBrowser) == "Open in browser" &&
                englishLocalizer.Get(ArcMarkdownStringKeys.OpenLinkFailed) == "Could not open link" &&
                englishLocalizer.Get(ArcMarkdownStringKeys.OpenLinkUnavailable) == "This link cannot be opened in a browser");

            var lists = blocks.OfType<ListBlock>().ToArray();
            var listControls = lists.Select(list => rendered[list]).ToArray();
            var listText = string.Concat(listControls.Select(TextOf));
            Check("markdown lists render bullets, numbers, nesting and task checkboxes",
                listText.Contains('•') && listText.Contains("1.") && listText.Contains("嵌套项") &&
                lists.Any(list => !list.IsOrdered) && lists.Any(list => list.IsOrdered) &&
                listControls.Sum(control => Walk(control).OfType<CheckBox>().Count()) >= 2);

            var quote = rendered[blocks.OfType<QuoteBlock>().First()];
            Check("markdown quote renders a rail and its text",
                TextOf(quote).Contains("引用文本") &&
                Walk(quote).OfType<Border>().Any(border => border.BorderThickness.Left > 0));

            var table = rendered[blocks.OfType<Table>().First()];
            var tableText = TextOf(table);
            Check("markdown GFM table renders header, cells and aligned columns",
                tableText.Contains("左列") && tableText.Contains("中列") && tableText.Contains("右列") &&
                tableText.Contains('A') && tableText.Contains('C') &&
                Walk(table).OfType<TextBlock>().Any(text => text.TextAlignment is TextAlignment.Center or TextAlignment.Right) &&
                table is ScrollViewer);

            var code = rendered[blocks.OfType<CodeBlock>().First()];
            Check("markdown fenced code renders ArcMarkdownCodeBlock with its language",
                code is ArcMarkdownCodeBlock codeBlock &&
                codeBlock.Language == "csharp" && codeBlock.Code.Contains("var x = 1;"));

            Check("markdown thematic break renders a divider control",
                rendered[blocks.OfType<ThematicBreakBlock>().First()] is Border);

            // --- document surface, outline and blocks ---------------------------------
            var fullView = new ArcMarkdownView();
            fullView.RenderDocument(document);
            // Blocks is populated by the visual render pass (OnApplyTemplate); headless we verify the parsed
            // document surface: plain text and outline are derived from the AST and available without a host.
            Check("markdown document view exposes plain text and outline",
                fullView.GetPlainText().Contains("收尾段落") &&
                fullView.GetOutline().Select(heading => heading.Level).SequenceEqual(new[] { 1, 2, 3, 4 }));

            // --- fence dispatch and plugin registry -----------------------------------
            var pluginPipeline = new ArcMarkdownPipelineBuilder()
                .UseCommonMark()
                .UseGfm()
                .UsePlugin(new MarkdownTestFencePlugin())
                .Build();
            var agentDocument = pluginPipeline.Syntax.ParseDocument("```pura-agent\n{\"type\":\"confirm\"}\n```");
            var agentContext = new ArcMarkdownRenderContext
            {
                Options = new ArcMarkdownOptions { Pipeline = pluginPipeline },
                Renderers = pluginPipeline.Renderers,
                Sanitized = agentDocument.Sanitized ?? new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            Check("markdown fence plugin takes over a registered language",
                pluginPipeline.PluginNames.Contains(MarkdownTestFencePlugin.PluginName) &&
                agentContext.RenderBlock(agentDocument.Markdown[0]) is Border { Tag: "test-fence" });

            var fallbackDocument = pipeline.Syntax.ParseDocument("```pura-agent\n{\"type\":\"confirm\"}\n```");
            var fallbackContext = new ArcMarkdownRenderContext
            {
                Options = options,
                Renderers = pipeline.Renderers,
                Sanitized = fallbackDocument.Sanitized ?? new SanitizeResult(),
                Dispatcher = Dispatcher.UIThread,
            };
            Check("markdown unregistered fence falls back to a code block",
                fallbackContext.RenderBlock(fallbackDocument.Markdown[0]) is ArcMarkdownCodeBlock);

            var duplicatePluginRejected = false;
            try
            {
                new ArcMarkdownPipelineBuilder()
                    .UsePlugin(new MarkdownTestFencePlugin())
                    .UsePlugin(new MarkdownTestFencePlugin())
                    .Build();
            }
            catch (InvalidOperationException)
            {
                duplicatePluginRejected = true;
            }

            Check("markdown duplicate plugin names fail fast", duplicatePluginRejected);

            // --- highlighter ----------------------------------------------------------
            var highlighter = pipeline.Highlighter;
            var spans = highlighter.Highlight("var answer = 42; // note", "csharp");
            Check("markdown highlighter orders spans and rejects unknown languages",
                highlighter.CanHighlight("csharp") && highlighter.CanHighlight("json") &&
                !highlighter.CanHighlight("no-such-language") &&
                spans.Count > 0 &&
                spans.Zip(spans.Skip(1)).All(pair => pair.First.Start <= pair.Second.Start));

            // --- streaming: append, stable boundary, completion equals one-shot ------
            var streamingView = new ArcMarkdownView();
            var session = streamingView.BeginStreaming(new ArcMarkdownStreamingOptions { Synchronous = true });
            foreach (var chunk in Chunks(MarkdownSample, 41))
            {
                session.Append(chunk);
            }

            Check("markdown streaming accumulates every appended chunk", session.CurrentText == MarkdownSample);

            var completeTask = session.CompleteAsync();
            var completed = PumpDispatcher(completeTask, TimeSpan.FromSeconds(20));
            var oneShot = new ArcMarkdownView();
            oneShot.RenderDocument(pipeline.Syntax.ParseDocument(MarkdownSample));
            Check("markdown streaming completion equals the one-shot render",
                completed &&
                session.State == ArcMarkdownStreamingState.Completed &&
                streamingView.GetPlainText() == oneShot.GetPlainText() &&
                OutlineSignature(streamingView) == OutlineSignature(oneShot));

            var boundaryView = new ArcMarkdownView();
            var boundarySession = boundaryView.BeginStreaming(new ArcMarkdownStreamingOptions { Synchronous = true });
            const string openFence = "# 标题\n\n```csharp\nvar x = 1;\n";
            boundarySession.Append(openFence);
            var partialStatistics = boundarySession.Statistics;
            Check("markdown streaming keeps an open fence in the tail window",
                partialStatistics.CommittedChars < openFence.Length && partialStatistics.TailChars > 0 &&
                !openFence[..partialStatistics.CommittedChars].Contains("```", StringComparison.Ordinal));
            boundarySession.Append("```\n\n");
            var closedStatistics = boundarySession.Statistics;
            Check("markdown streaming commits once the fence closes",
                closedStatistics.TailChars == 0 &&
                closedStatistics.CommittedChars == boundarySession.CurrentText.Length);

            var abortView = new ArcMarkdownView();
            var abortSession = abortView.BeginStreaming(new ArcMarkdownStreamingOptions { Synchronous = true });
            abortSession.Append("# 已收到\n\n部分内容");
            var abortTask = abortSession.AbortAsync();
            var aborted = PumpDispatcher(abortTask, TimeSpan.FromSeconds(20));
            Check("markdown streaming abort keeps the partial document",
                aborted &&
                abortSession.State == ArcMarkdownStreamingState.Aborted &&
                abortView.GetPlainText().Contains("部分内容"));

        }
        finally
        {
            ArcMarkdownStyle.Current = previousStyle;
            ArcResources.UseHost(null);
        }
    }

    /// <summary>
    /// Chat family after the PuraMarkdown.Agent migration: <see cref="ArcChatView"/> is a thin wrapper over
    /// <see cref="PuraChatView"/>, so the message collection, list CRUD/events, streaming lifecycle and
    /// snapshot round-trip are asserted against the upstream contracts; Arc keeps the composer contract.
    /// Message-level virtualization is a PuraChatView concern and is covered by its own headless probe.
    /// </summary>
    private static void ChatChecks()
    {
        var palette = ArcPalette.Build(ArcColorScheme.Light, ArcAccent.Neutral);
        ArcResources.UseHost(new MarkdownResourceHost(palette));
        try
        {
            var chat = new ArcChatView();
            var added = 0;
            var updated = 0;
            var removed = 0;
            chat.MessageAdded += (_, _) => added++;
            chat.MessageUpdated += (_, _) => updated++;
            chat.MessageRemoved += (_, _) => removed++;

            var firstMessage = new PuraChatMessage
            {
                Id = "user-1",
                Role = PuraChatRole.User,
                Markdown = "**你好**"
            };
            chat.AddMessage(firstMessage);
            chat.InsertMessage(0, new PuraChatMessage
            {
                Id = "system-1",
                Role = PuraChatRole.System,
                Markdown = "系统消息"
            });
            firstMessage.Markdown = "**你好**，Markdown";
            chat.UpdateMessage(firstMessage);
            Check("chat view delegates add/insert/update to the upstream message collection",
                chat.Messages.Count == 2 && added == 2 && updated == 1 &&
                ReferenceEquals(chat.FindMessage("user-1"), firstMessage) &&
                chat.Messages[0].Id == "system-1");

            Check("chat view delegates remove/replace to the upstream list",
                chat.RemoveMessage("user-1") && removed == 1 &&
                chat.Messages.Count == 1 && !chat.RemoveMessage("missing") &&
                ReplaceChatMessages(chat) == 2);
            chat.ClearMessages();
            Check("chat view delegates clear to the upstream list", chat.Messages.Count == 0);

            // 1000 messages must be accepted headless; message-level virtualization is PuraChatView's job.
            var bulk = new ArcChatView();
            for (var index = 0; index < 1000; index++)
            {
                bulk.AddMessage(new PuraChatMessage { Id = $"bulk-{index}", Markdown = $"message {index}" });
            }

            Check("chat view accepts 1000 messages headless", bulk.Messages.Count == 1000 && !bulk.IsStreaming);

            // Streaming lifecycle: PuraChatView keeps the in-flight body on the session and writes it back into
            // the message collection when the stream ends (complete or abort), so both phases are asserted.
            var streamingMessage = new PuraChatMessage { Id = "assistant-1", Role = PuraChatRole.Assistant };
            var session = chat.BeginStreamingMessage(streamingMessage);
            session.Append("## 流式回复\n\n第一段");
            PumpDispatcher(session.FlushAsync(), TimeSpan.FromSeconds(20));
            Check("chat streaming begins through the upstream session",
                chat.IsStreaming &&
                session.State == PuraStreamingState.Streaming &&
                session.CurrentText.Contains("第一段") &&
                streamingMessage.Status == PuraChatMessageStatus.Streaming);
            var chatCompleted = PumpDispatcher(session.CompleteAsync(), TimeSpan.FromSeconds(20));
            Check("chat streaming completion keeps the full markdown and completes the message",
                chatCompleted &&
                session.State == PuraStreamingState.Completed &&
                !chat.IsStreaming &&
                streamingMessage.Status == PuraChatMessageStatus.Complete &&
                streamingMessage.Markdown == "## 流式回复\n\n第一段" &&
                chat.Messages.Contains(streamingMessage));

            var abortChat = new ArcChatView();
            var abortMessage = new PuraChatMessage { Id = "assistant-abort", Role = PuraChatRole.Assistant };
            var abortSession = abortChat.BeginStreamingMessage(abortMessage);
            abortSession.Append("部分回复");
            PumpDispatcher(abortSession.FlushAsync(), TimeSpan.FromSeconds(20));
            abortChat.AbortStreaming();
            var abortWrittenBack = PumpUntil(() => abortChat.Messages.Contains(abortMessage), TimeSpan.FromSeconds(20));
            Check("chat streaming abort keeps the partial body and marks the message aborted",
                abortWrittenBack &&
                abortSession.State == PuraStreamingState.Aborted &&
                abortMessage.Status == PuraChatMessageStatus.Aborted &&
                abortMessage.Markdown == "部分回复" &&
                abortChat.Messages.Contains(abortMessage));

            // Snapshot round-trip through the upstream serializer -> RestoreSnapshot.
            var snapshotView = new ArcChatView();
            snapshotView.AddMessage(new PuraChatMessage { Id = "snap-1", Role = PuraChatRole.User, Markdown = "hello" });
            snapshotView.AddMessage(new PuraChatMessage { Id = "snap-2", Role = PuraChatRole.Assistant, Markdown = "world" });
            var snapshotJson = PuraChatSnapshotSerializer.Serialize(snapshotView.Host.CreateSnapshot());
            var restoredView = new ArcChatView();
            restoredView.RestoreSnapshot(PuraChatSnapshotSerializer.Deserialize(snapshotJson));
            Check("chat snapshot round-trips through Serialize/RestoreSnapshot",
                restoredView.Messages.Count == 2 &&
                restoredView.Messages[0] is { Id: "snap-1", Markdown: "hello" } &&
                restoredView.Messages[1] is { Id: "snap-2", Markdown: "world" });

            // Composer stays an Arc-owned control.
            var composer = new ArcChatComposer { MaxAttachments = 2 };
            ArcChatDraft? draft = null;
            composer.Send += (_, args) => draft = args;
            composer.Text = "ping";
            composer.AddAttachment(new ArcChatAttachment { Name = "a.txt", Size = 1 });
            composer.AddAttachment(new ArcChatAttachment { Name = "b.txt", Size = 2 });
            composer.AddAttachment(new ArcChatAttachment { Name = "c.txt", Size = 3 });
            Check("chat composer caps the attachment tray",
                composer.PendingAttachments.Count == 2 && composer.MaxAttachments == 2);
            composer.Submit();
            Check("chat composer submits a draft and clears the tray",
                draft is { Text: "ping" } &&
                draft.Attachments.Count == 2 &&
                composer.Text.Length == 0 &&
                composer.PendingAttachments.Count == 0);
        }
        finally
        {
            ArcResources.UseHost(null);
        }
    }

    private static int ReplaceChatMessages(ArcChatView chat)
    {
        chat.ReplaceMessages(new[]
        {
            new PuraChatMessage { Id = "replace-a", Markdown = "a" },
            new PuraChatMessage { Id = "replace-b", Markdown = "b" }
        });
        return chat.Messages.Count;
    }

    /// <summary>Walks a rendered node tree and appends every visible text fragment (inlines included).</summary>
    private static string TextOf(Control control)
    {
        var builder = new StringBuilder();
        AppendControlText(control, builder);
        return builder.ToString();
    }

    private static void AppendControlText(Control control, StringBuilder builder)
    {
        switch (control)
        {
            case TextBlock text:
                if (!string.IsNullOrEmpty(text.Text))
                {
                    builder.Append(text.Text);
                }

                if (text.Inlines is { } inlines)
                {
                    foreach (var inline in inlines)
                    {
                        AppendInlineText(inline, builder);
                    }
                }

                builder.Append('\n');
                break;
            case ArcMarkdownCodeBlock code:
                builder.Append(code.Code).Append('\n');
                break;
            case Image:
                builder.Append("[image]");
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    AppendControlText(child, builder);
                }

                break;
            case Decorator { Child: { } decorated }:
                AppendControlText(decorated, builder);
                break;
            case ContentControl { Content: Control content }:
                AppendControlText(content, builder);
                break;
            case ContentControl { Content: string textContent }:
                builder.Append(textContent);
                break;
            case ContentControl { Content: { } other }:
                builder.Append(other);
                break;
        }
    }

    private static void AppendInlineText(Inline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case Run run:
                builder.Append(run.Text);
                break;
            case LineBreak:
                builder.Append('\n');
                break;
            case InlineUIContainer { Child: { } child }:
                AppendControlText(child, builder);
                break;
            case Span span when span.Inlines is { } spanInlines:
                foreach (var nested in spanInlines)
                {
                    AppendInlineText(nested, builder);
                }

                break;
        }
    }

    /// <summary>Every control under the rendered node, including inline-hosted controls.</summary>
    private static IEnumerable<Control> Walk(Control root)
    {
        yield return root;
        switch (root)
        {
            case TextBlock { Inlines: { } inlines }:
                foreach (var inline in inlines)
                {
                    foreach (var nested in ControlsInInline(inline))
                    {
                        foreach (var control in Walk(nested))
                        {
                            yield return control;
                        }
                    }
                }

                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    foreach (var control in Walk(child))
                    {
                        yield return control;
                    }
                }

                break;
            case Decorator { Child: { } decorated }:
                foreach (var control in Walk(decorated))
                {
                    yield return control;
                }

                break;
            case ContentControl { Content: Control content }:
                foreach (var control in Walk(content))
                {
                    yield return control;
                }

                break;
        }
    }

    private static IEnumerable<Control> ControlsInInline(Inline inline)
    {
        switch (inline)
        {
            case InlineUIContainer { Child: { } child }:
                yield return child;
                break;
            case Span { Inlines: { } inlines }:
                foreach (var nested in inlines)
                {
                    foreach (var control in ControlsInInline(nested))
                    {
                        yield return control;
                    }
                }

                break;
        }
    }

    private static IEnumerable<Run> AllRuns(Control control)
    {
        foreach (var candidate in Walk(control))
        {
            if (candidate is not TextBlock { Inlines: { } inlines })
            {
                continue;
            }

            foreach (var inline in inlines)
            {
                foreach (var run in RunsInInline(inline))
                {
                    yield return run;
                }
            }
        }
    }

    private static IEnumerable<Run> RunsInInline(Inline inline)
    {
        switch (inline)
        {
            case Run run:
                yield return run;
                break;
            case Span { Inlines: { } inlines }:
                foreach (var nested in inlines)
                {
                    foreach (var run in RunsInInline(nested))
                    {
                        yield return run;
                    }
                }

                break;
            case InlineUIContainer { Child: { } child }:
                foreach (var run in AllRuns(child))
                {
                    yield return run;
                }

                break;
        }
    }

    private static string OutlineSignature(ArcMarkdownView view)
        => string.Join("|", view.GetOutline().Select(heading => $"{heading.Level}:{heading.Text}:{heading.Anchor}"));

    private static IEnumerable<string> Chunks(string text, int size)
    {
        for (var offset = 0; offset < text.Length; offset += size)
        {
            yield return text.Substring(offset, Math.Min(size, text.Length - offset));
        }
    }

    /// <summary>
    /// Pumps the Avalonia dispatcher until an async Markdown operation completes. Streaming completion parses on
    /// a worker thread and hops back to the UI thread; without a desktop event loop the queued continuation never
    /// runs, so a plain Wait would time out even though the document is correct.
    /// </summary>
    private static bool PumpDispatcher(Task task, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!task.IsCompleted && stopwatch.Elapsed < timeout)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
        return task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
    }

    /// <summary>Pumps the Avalonia dispatcher until <paramref name="condition"/> holds or the timeout elapses.</summary>
    private static bool PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    /// <summary>Slice a palette into the resource host contract the Markdown style reads at construction.</summary>
    private sealed class MarkdownResourceHost(ResourceDictionary palette) : IArcResourceHost
    {
        public ThemeVariant? ActualThemeVariant => ThemeVariant.Light;

        public bool TryFindResource(string key, out object? value) => palette.TryGetValue(key, out value);

        public bool TryFindResource(string key, ThemeVariant variant, out object? value)
            => palette.TryGetValue(key, out value);
    }

    /// <summary>Captures what the Markdown link menu writes through <see cref="IArcMarkdownClipboard"/>.</summary>
    private sealed class FakeMarkdownClipboard : IArcMarkdownClipboard
    {
        public string? Text { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    /// <summary>Captures what the Markdown link menu opens through <see cref="IArcMarkdownLinkHandler"/>.</summary>
    private sealed class FakeMarkdownLinkHandler : IArcMarkdownLinkHandler
    {
        public List<string> Urls { get; } = new();

        public Task OpenAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal fence plugin used to prove IArcMarkdownPlugin/IArcMarkdownFenceRenderer dispatch.</summary>
    private sealed class MarkdownTestFencePlugin : IArcMarkdownPlugin
    {
        public const string PluginName = "docs-qa.test-fence-plugin";

        public string Name => PluginName;

        public string Version => "1.0.0";

        public void Configure(ArcMarkdownPipelineBuilder builder)
            => builder.UseFenceRenderer(new MarkdownTestFenceRenderer());
    }

    private sealed class MarkdownTestFenceRenderer : IArcMarkdownFenceRenderer
    {
        public string Name => "docs-qa.test-fence";

        public bool CanRender(string language, string source, bool isClosed, ArcMarkdownRenderContext context)
            => string.Equals(language, "pura-agent", StringComparison.OrdinalIgnoreCase);

        public Control Render(string language, string source, bool isClosed, ArcMarkdownRenderContext context)
            => new Border { Tag = "test-fence", Child = new TextBlock { Text = source } };
    }

    private const string MarkdownSample = """
        # 一级标题

        ## 二级标题

        ### 三级标题

        #### 四级标题

        正文 **粗体**、*斜体*、~~删除线~~、行内 `code`、[链接](https://uiarc.dev) 与 ![图](https://example.com/a.png) 占位。

        - 项目一
        - 项目二
          - 嵌套项

        1. 有序一
        2. 有序二

        - [x] 已完成
        - [ ] 未完成

        > 引用文本

        | 左列 | 中列 | 右列 |
        | :--- | :---: | ---: |
        | A | B | C |

        ```csharp
        var x = 1;
        ```

        ---

        收尾段落。
        """;

    private static void Check(string name, bool passed)
    {
        _checks++;
        if (!passed)
        {
            Failures.Add(name);
        }
    }
}




