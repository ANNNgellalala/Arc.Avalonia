using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Theme;

namespace ArcThemePoc;

/// <summary>
/// POC-A harness: applies Light/Dark x Neutral/Violet through the single-line theme, asserts that the
/// nested FluentTheme + token ThemeDictionaries + component themes all resolved, and writes the PNGs.
/// </summary>
public static class ThemeShotRunner
{
    private sealed record Shot(string File, ArcColorScheme Scheme, ArcAccent Accent);

    private static readonly Shot[] Shots =
    {
        new("theme-light-neutral.png", ArcColorScheme.Light, ArcAccent.Neutral),
        new("theme-dark-neutral.png", ArcColorScheme.Dark, ArcAccent.Neutral),
        new("theme-light-violet.png", ArcColorScheme.Light, ArcAccent.Violet)
    };

    private static int _failures;

    public static async Task<int> RunAsync(Application app, MainWindow window, string outDir)
    {
        _failures = 0;
        ArcThemeManager.ReducedMotion = true;

        var root = window.FindControl<Border>("Root")
                   ?? throw new InvalidOperationException("MainWindow root border not found");

        // Probe once before the first shot: keep the raw FluentTheme/Palettes surface on record.
        ProbeFluentSurface(app);

        foreach (var shot in Shots)
        {
            ArcThemeManager.Apply(app, shot.Scheme, shot.Accent);
            var accentApplied = ApplyFluentAccent(app, shot);
            window.SetThemeLine($"{shot.Scheme.ToString().ToLowerInvariant()} / {shot.Accent.ToString().ToLowerInvariant()}"
                                 + $" · requested={app.RequestedThemeVariant} actual={app.ActualThemeVariant}");
            await SettleAsync();

            AssertShot(app, window, shot, accentApplied);

            var (width, height) = Render(root, Path.Combine(outDir, shot.File));
            Console.WriteLine($"POC-A SHOT {shot.File} {width}x{height} {new FileInfo(Path.Combine(outDir, shot.File)).Length / 1024}KB");
        }

        Console.WriteLine(_failures == 0
            ? "POC-A: all assertions passed"
            : $"POC-A: {_failures} assertion(s) failed");
        return _failures;
    }

    private static void AssertShot(Application app, MainWindow window, Shot shot, string accentApplied)
    {
        var dark = shot.Scheme == ArcColorScheme.Dark;
        var expectedVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var expectedRadius = new CornerRadius(dark ? 10 : 14);

        // 1) The nested <FluentTheme/> is registered as a style, not merely constructed.
        var fluent = FindFluentTheme(app.Styles);
        Check(fluent is not null, "nested <FluentTheme/> discovered inside App.Styles");

        // 2) Theme variant plumbing.
        Check(app.ActualThemeVariant == expectedVariant, $"ActualThemeVariant == {expectedVariant} (got {app.ActualThemeVariant})");

        // 3) Token ThemeDictionaries actually key off Light/Dark (14 vs 10) and beat Fluent's own value.
        var radiusOk = TryFind(app, "ControlCornerRadius", out var radiusValue)
                       && radiusValue is CornerRadius radius
                       && radius == expectedRadius;
        Check(radiusOk, $"ControlCornerRadius == {expectedRadius} (got {Describe(app, "ControlCornerRadius")}; Fluent default is 3)");

        // 4) Font override from the same dictionary (this is a real Fluent key: default 14, Arc body 15).
        var fontOk = TryFind(app, "ControlContentThemeFontSize", out var fontSize)
                     && fontSize is double size
                     && Math.Abs(size - 15) < 0.001;
        Check(fontOk, $"ControlContentThemeFontSize == 15 (got {Describe(app, "ControlContentThemeFontSize")})");

        // 5) A second token proves the Light/Dark dictionaries switch, not just the first key.
        var expectedToken = dark ? Color.Parse("#8FA8FF") : Color.Parse("#3366FF");
        var tokenOk = TryFind(app, "PocTokenBrush", out var tokenBrush)
                      && tokenBrush is ISolidColorBrush brush
                      && brush.Color == expectedToken;
        Check(tokenOk, $"PocTokenBrush == {expectedToken} (got {Describe(app, "PocTokenBrush")})");

        // 6) Arc palette still applied by ArcThemeManager through the same Application.Resources chain.
        var palette = ArcPalette.Build(shot.Scheme, shot.Accent);
        var expectedBackground = (palette[ArcTokens.Background] as ISolidColorBrush)?.Color;
        var backgroundOk = app.TryFindResource(ArcTokens.Background, out var background)
                           && background is ISolidColorBrush backgroundBrush
                           && backgroundBrush.Color == expectedBackground;
        Check(backgroundOk, $"ArcBackground == {expectedBackground} (got {Describe(app, ArcTokens.Background)})");

        // 7) Bare Fluent controls got a ControlTheme (the strongest single signal that nesting worked).
        foreach (var name in new[] { "BareTextBox", "BareComboBox", "BareTabControl" })
        {
            var control = window.FindControl<TemplatedControl>(name);
            Check(control?.Template is not null, $"{name} resolved a ControlTheme from the nested FluentTheme");
        }

        // 8) Arc components resolved their own component themes inside the same Styles aggregate.
        foreach (var name in new[] { "ArcPrimary", "ArcSecondary", "ArcGhost", "ArcField", "ArcPassword" })
        {
            var control = window.FindControl<TemplatedControl>(name);
            Check(control?.Template is not null, $"{name} resolved its Arc component theme");
        }

        // 9) DynamicResource from the Fluent ControlTheme actually resolved on the bare control.
        var textBox = window.FindControl<TextBox>("BareTextBox");
        Check(textBox?.Background is not null, $"BareTextBox.Background resolved ({textBox?.Background})");
        var comboBox = window.FindControl<ComboBox>("BareComboBox");
        Check(comboBox?.Background is not null, $"BareComboBox.Background resolved ({comboBox?.Background})");

        // 10) The Fluent template really consumed the Arc value, not just the resource dictionary.
        var borders = textBox?.GetVisualDescendants().OfType<Border>().ToList() ?? new List<Border>();
        var corners = string.Join("/", borders.Select(b => b.CornerRadius.ToString()));
        Check(borders.Any(b => b.CornerRadius == expectedRadius),
            $"BareTextBox template border CornerRadius contains {expectedRadius} (border corners: [{corners}])");

        Console.WriteLine($"POC-OBSERVE accent-mapping[{shot.Accent}] {accentApplied}");
    }

    /// <summary>
    /// POC-A hypothesis 2 (docs/14 §5.1): 8 Arc accents mapped through FluentTheme.Palettes instead of a
    /// bare SystemAccentColor. Returns a human-readable observation, never throws.
    /// </summary>
    private static string ApplyFluentAccent(Application app, Shot shot)
    {
        var fluent = FindFluentTheme(app.Styles);
        if (fluent is null)
        {
            return "skipped (no FluentTheme)";
        }

        var accentColor = (ArcPalette.Build(shot.Scheme, shot.Accent)[ArcTokens.Accent] as ISolidColorBrush)?.Color
                          ?? Colors.Gray;
        try
        {
            fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = accentColor };
            fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = accentColor };
        }
        catch (Exception exception)
        {
            return $"Palettes set threw {exception.GetType().Name}: {exception.Message}";
        }

        var found = TryFind(app, "SystemAccentColor", out var systemAccent);
        var actual = systemAccent is Color color ? $"Color {color}" : systemAccent?.ToString() ?? "<missing>";
        return $"placed Accent={accentColor}; SystemAccentColor={(found ? actual : "<missing>")}";
    }

    /// <summary>Diagnostic entry point (<c>--probe</c>): explains why a resource key did or did not resolve.</summary>
    public static void Probe(Application app) => ProbeFluentSurface(app);

    private static void ProbeFluentSurface(Application app)
    {
        var fluentType = typeof(FluentTheme);
        Console.WriteLine($"POC-PROBE FluentTheme properties: {string.Join(", ", fluentType.GetProperties().Select(p => p.Name))}");
        Console.WriteLine($"POC-PROBE ColorPaletteResources properties: {string.Join(", ", typeof(ColorPaletteResources).GetProperties().Select(p => p.Name))}");
        var fluent = FindFluentTheme(app.Styles);
        Console.WriteLine($"POC-PROBE nested FluentTheme instance: {(fluent is null ? "none" : fluent.GetType().FullName)}");
        Console.WriteLine($"POC-PROBE App.Styles children: {string.Join(", ", app.Styles.Select(s => s.GetType().FullName))}");
        Console.WriteLine($"POC-PROBE app.ActualThemeVariant={app.ActualThemeVariant}, app.RequestedThemeVariant={app.RequestedThemeVariant}");

        // Why are the aggregated token keys not resolving through some code paths? Separate "nested Styles
        // resources are not in the app chain" from "ThemeDictionaries need an explicit theme variant".
        var aggregate = app.Styles.FirstOrDefault();
        if (aggregate is Styles aggregateStyles)
        {
            Console.WriteLine($"POC-PROBE ArcTheme.Resources.Count = {aggregateStyles.Resources.Count}");
            Console.WriteLine($"POC-PROBE ArcTheme.Resources keys = {string.Join(", ", aggregateStyles.Resources.Keys)}");
            Console.WriteLine($"POC-PROBE ArcTheme.Resources.ThemeDictionaries = {string.Join(", ", aggregateStyles.Resources.ThemeDictionaries.Keys)}");
            foreach (var theme in new ThemeVariant?[] { null, ThemeVariant.Light, ThemeVariant.Dark })
            {
                var provider = (IResourceProvider)aggregateStyles;
                var plain = provider.TryGetResource("PocPlainBrush", theme, out var plainValue);
                var token = provider.TryGetResource("PocTokenBrush", theme, out var tokenValue);
                Console.WriteLine($"POC-PROBE direct ArcTheme lookup [{theme?.ToString() ?? "null"}]: PocPlainBrush={plain}({plainValue}) PocTokenBrush={token}({tokenValue})");
            }
        }

        foreach (var theme in new ThemeVariant?[] { null, ThemeVariant.Light, ThemeVariant.Dark })
        {
            var stylesProvider = (IResourceProvider)app.Styles;
            var stylesPlain = stylesProvider.TryGetResource("PocPlainBrush", theme, out _);
            var stylesToken = stylesProvider.TryGetResource("PocTokenBrush", theme, out _);
            var appPlain = app.Resources.TryGetResource("PocPlainBrush", theme, out _);
            Console.WriteLine($"POC-PROBE chain lookup [{theme?.ToString() ?? "null"}]: App.Styles PocPlainBrush={stylesPlain} PocTokenBrush={stylesToken}; App.Resources PocPlainBrush={appPlain}");
        }

        // The public no-variant helper used by production code (ArcResources.Brush & co) vs. the
        // variant-aware overload. This is the difference that makes theme-dictionary tokens invisible.
        Console.WriteLine($"POC-PROBE app.TryFindResource(PocPlainBrush) = {app.TryFindResource("PocPlainBrush", out var plainOut)} value={plainOut}");
        foreach (var theme in new ThemeVariant?[] { null, ThemeVariant.Light, ThemeVariant.Dark })
        {
            var embed = app.TryFindResource("PocTokenBrush", theme, out var embedValue);
            var radius = app.TryFindResource("ControlCornerRadius", theme, out var radiusValue);
            Console.WriteLine($"POC-PROBE app.TryFindResource(theme={theme?.ToString() ?? "null"}): PocTokenBrush={embed}({embedValue}) ControlCornerRadius={radius}({radiusValue})");
        }
    }

    private static bool TryFind(Application app, string key, out object? value) =>
        app.TryFindResource(key, app.ActualThemeVariant, out value);

    private static FluentTheme? FindFluentTheme(IEnumerable<IStyle> styles) => FindNested<FluentTheme>(styles).FirstOrDefault();

    private static IEnumerable<T> FindNested<T>(IEnumerable<IStyle> styles) where T : class, IStyle
    {
        foreach (var style in styles)
        {
            if (style is T match)
            {
                yield return match;
            }

            if (style is Styles nested)
            {
                foreach (var child in FindNested<T>(nested))
                {
                    yield return child;
                }
            }
        }
    }

    private static string Describe(Application app, string key) =>
        TryFind(app, key, out var value) ? value?.ToString() ?? "<null>" : "<missing>";

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine($"POC-A ASSERT PASS: {message}");
            return;
        }

        _failures++;
        Console.WriteLine($"POC-A ASSERT FAIL: {message}");
    }

    private static async Task SettleAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(140);
    }

    private static (int Width, int Height) Render(Control root, string path)
    {
        var width = Math.Max(1, (int)Math.Round(root.Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(root.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path);

        if (Math.Max(width, height) > 1200)
        {
            Console.WriteLine($"POC-A WARN image long side {Math.Max(width, height)} > 1200");
        }

        return (width, height);
    }
}
