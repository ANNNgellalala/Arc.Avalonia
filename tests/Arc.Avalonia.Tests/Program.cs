using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

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
            // Variant-aware overload reaches ThemeDictionaries-style entries; the untargeted
            // overload must not see them (ADR 0003 §4.2).
            Expect("untargeted lookup ignores theme-dictionary entries",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1), Brushes.Transparent),
                ArcResources.Brush(ArcTokens.Series1));
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

    private static void Check(string name, bool passed)
    {
        _checks++;
        if (!passed)
        {
            Failures.Add(name);
        }
    }
}
