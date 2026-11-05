using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Runtime verification for the M1-T3 aggregate theme (counterpart of the R4 static gate):
///   * every key in <see cref="ArcFluentKeys"/> resolves for Light and for Dark;
///   * Arc tokens are visible through the variant-aware <see cref="ArcResources"/> path;
///   * the FluentTheme.Palettes palette maps the Arc accent onto Fluent's <c>SystemAccentColor</c>.
/// Usage: <c>Arc.Avalonia.Gallery.dll --theme-report</c> (exit 0 = all checks passed).
/// </summary>
public static class ThemeReport
{
    public static int Run()
    {
        if (Application.Current is not { } app)
        {
            Console.WriteLine("THEME REPORT: no application");
            return 2;
        }

        var failures = new List<string>();
        var schemes = new[] { ArcColorScheme.Light, ArcColorScheme.Dark };

        foreach (var scheme in schemes)
        {
            ArcThemeManager.Apply(app, scheme, ArcAccent.Neutral);
            var variant = app.ActualThemeVariant;
            var missingKeys = ArcFluentKeys.All
                .Where(key => !app.TryGetResource(key, variant, out _))
                .ToList();

            var background = ArcResources.Brush(ArcTokens.Background);
            var radius = ArcResources.Radius(ArcTokens.RadiusControl);
            var corner = app.TryGetResource(ArcFluentKeys.ControlCornerRadius, variant, out var cornerValue)
                ? cornerValue?.ToString()
                : "<missing>";
            var textBackground = app.TryGetResource(ArcFluentKeys.TextControlBackground, variant, out var textValue)
                ? textValue?.ToString()
                : "<missing>";

            Console.WriteLine(
                $"THEME REPORT [{scheme}] variant={variant} fluentKeys={ArcFluentKeys.All.Count - missingKeys.Count}/{ArcFluentKeys.All.Count} " +
                $"ArcBackground={Describe(background)} ArcRadiusControl={radius} ControlCornerRadius={corner} TextControlBackground={textBackground}");

            if (missingKeys.Count > 0)
            {
                failures.Add($"{scheme}: missing Fluent keys -> {string.Join(", ", missingKeys)}");
            }

            if (background is not ISolidColorBrush)
            {
                failures.Add($"{scheme}: ArcBackground did not resolve through ArcResources");
            }

            if (radius == default(CornerRadius))
            {
                failures.Add($"{scheme}: ArcRadiusControl did not resolve through ArcResources");
            }
        }

        // Fluent palettes: the accent must reach SystemAccentColor for both variants.
        foreach (var accent in new[] { ArcAccent.Neutral, ArcAccent.Violet, ArcAccent.Rose })
        {
            ArcThemeManager.Apply(app, ArcColorScheme.Light, accent);
            var expected = ArcPalette.AccentColor(ArcColorScheme.Light, accent);
            Color? actual = null;
            if (app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var accentValue) && accentValue is Color accentColor)
            {
                actual = accentColor;
            }
            Console.WriteLine($"THEME REPORT accent={accent} SystemAccentColor={actual?.ToString() ?? "<missing>"} expected={expected}");
            if (actual != expected)
            {
                failures.Add($"accent {accent}: SystemAccentColor {actual?.ToString() ?? "<missing>"} != {expected}");
            }
        }

        ArcThemeManager.Apply(app, ArcColorScheme.Light, ArcAccent.Neutral);

        if (failures.Count == 0)
        {
            Console.WriteLine("THEME REPORT OK");
            return 0;
        }

        foreach (var failure in failures)
        {
            Console.WriteLine("THEME REPORT FAIL: " + failure);
        }

        return 1;
    }

    private static string Describe(object? value) => value switch
    {
        ISolidColorBrush brush => brush.Color.ToString(),
        null => "<missing>",
        _ => value.ToString() ?? "<null>"
    };
}
