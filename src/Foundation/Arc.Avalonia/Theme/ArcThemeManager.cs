using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Applies the Arc palette to an Avalonia application and swaps theme/accent at runtime.
/// Components resolve every colour through DynamicResource, so a swap restyles the whole tree.
/// </summary>
public static class ArcThemeManager
{
    private static ResourceDictionary? _current;

    public static ArcColorScheme Scheme => Options.Scheme;

    /// <summary>The preset accent of the active theme (a custom brand colour is reflected in <see cref="Options"/>).</summary>
    public static ArcAccent Accent => Options.Accent;

    /// <summary>The full theme request applied most recently (scheme, accent/seed, density, radius scale).</summary>
    public static ArcThemeOptions Options { get; private set; } = new();

    /// <summary>When true components skip travel animation, matching Arc's reduced-motion branch.</summary>
    public static bool ReducedMotion { get; set; }

    /// <summary>
    /// Raised after <see cref="Apply(Application, ArcThemeOptions)"/> swapped the palette (arguments:
    /// application, scheme, options). The aggregated theme (src/Theme/Arc.Avalonia.Theme) subscribes to
    /// keep Fluent's <c>FluentTheme.Palettes</c> and the Fluent resource-key overrides in sync, so the
    /// Foundation layer does not need a dependency on Avalonia.Themes.Fluent.
    /// </summary>
    public static event Action<Application, ArcColorScheme, ArcThemeOptions>? Applied;

    /// <summary>Compatibility overload: applies a preset scheme + accent with the default density/radii.</summary>
    public static void Apply(Application app, ArcColorScheme scheme, ArcAccent accent) =>
        Apply(app, new ArcThemeOptions { Scheme = scheme, Accent = accent });

    /// <summary>
    /// Applies a full theme request: preset or custom brand accent, density preset and radius scale.
    /// </summary>
    public static void Apply(Application app, ArcThemeOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        Options = options;

        app.RequestedThemeVariant = options.Scheme == ArcColorScheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        // Tokens live in ThemeDictionaries (Light + Dark), so a bare variant switch restyles the whole tree
        // and only an accent/density change needs a rebuild (ADR 0003 §4.2). A custom brand colour is
        // derived in OKLCH inside ArcPalette - no literal colour ever enters the theme.
        var palette = ArcPalette.BuildThemeDictionaries(options);
        if (_current is not null)
        {
            app.Resources.MergedDictionaries.Remove(_current);
        }

        app.Resources.MergedDictionaries.Add(_current = palette);

        Applied?.Invoke(app, options.Scheme, options);
    }

    public static void SetScheme(Application app, ArcColorScheme scheme) => Apply(app, Options with { Scheme = scheme });

    public static void ToggleScheme(Application app) =>
        Apply(app, Options with { Scheme = Options.Scheme == ArcColorScheme.Light ? ArcColorScheme.Dark : ArcColorScheme.Light });

    /// <summary>Switches to a preset accent (clears any custom brand seed).</summary>
    public static void SetAccent(Application app, ArcAccent accent) =>
        Apply(app, Options with { Accent = accent, AccentColor = null });

    /// <summary>Switches to a custom brand accent, derived from the seed colour in OKLCH.</summary>
    public static void SetAccent(Application app, Color accentColor) =>
        Apply(app, Options with { AccentColor = accentColor });
}

/// <summary>Typed lookups for components that need token values from code (charts, custom drawing).</summary>
public static class ArcResources
{
    private static IArcResourceHost _host = ApplicationResourceHost.Instance;

    /// <summary>
    /// Resource host the typed lookups read from. Defaults to an <see cref="Application.Current"/>-backed
    /// host so existing behaviour is unchanged; tests and multi-window shells can swap it via
    /// <see cref="UseHost"/>.
    /// </summary>
    public static IArcResourceHost Host => _host;

    /// <summary>Installs a resource host. Pass <c>null</c> to restore the default Application-backed host.</summary>
    public static void UseHost(IArcResourceHost? host) => _host = host ?? ApplicationResourceHost.Instance;

    /// <summary>
    /// Standard lookup. Tokens now live in <c>ThemeDictionaries</c>, so the lookup follows the host's
    /// active <see cref="IArcResourceHost.ActualThemeVariant"/>; without that it would silently miss every
    /// token and hand callers the fallback value (ADR 0003 §4.2).
    /// </summary>
    public static IBrush Brush(string key) => Brush(key, null);

    /// <summary>
    /// Variant-aware lookup: passes <paramref name="variant"/> to the host so resources defined in
    /// <c>ThemeDictionaries</c> resolve for the requested Light/Dark variant. Pass <c>null</c> to follow
    /// the host's active variant (the default path used by <see cref="Brush(string)"/> and friends).
    /// </summary>
    public static IBrush Brush(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is IBrush brush ? brush : Brushes.Transparent;

    public static Color Color(string key) => Color(key, null);

    public static Color Color(string key, ThemeVariant? variant) =>
        Brush(key, variant) is ISolidColorBrush solid ? solid.Color : Colors.Black;

    public static double Double(string key) => Double(key, null);

    public static double Double(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is double number ? number : 0;

    public static CornerRadius Radius(string key) => Radius(key, null);

    public static CornerRadius Radius(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is CornerRadius radius ? radius : default;

    public static BoxShadows Shadows(string key) => Shadows(key, null);

    public static BoxShadows Shadows(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is BoxShadows shadows ? shadows : default;

    public static TimeSpan Duration(string key) => Duration(key, null);

    public static TimeSpan Duration(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is TimeSpan time ? time : TimeSpan.Zero;

    /// <summary>Resolves a font-family token (Inter), falling back to the platform default.</summary>
    public static FontFamily Font(string key) => Font(key, null);

    /// <summary>Variant-aware font-family token lookup.</summary>
    public static FontFamily Font(string key, ThemeVariant? variant) =>
        TryFind(key, variant, out var value) && value is FontFamily family ? family : FontFamily.Default;

    /// <summary>
    /// Variant-aware untyped lookup, for callers that need whatever the token is. Use this instead of
    /// <c>Application.FindResource</c>: tokens live in <c>ThemeDictionaries</c>, which an untargeted lookup
    /// cannot see, so the untargeted path silently misses every token (ADR 0003 §4.2).
    /// </summary>
    public static bool TryFind(string key, out object? value) => TryFind(key, null, out value);

    /// <summary>Variant-aware untyped lookup against an explicit theme variant.</summary>
    public static bool TryFind(string key, ThemeVariant? variant, out object? value)
    {
        // ThemeDictionaries entries are only visible to a variant-targeted lookup, so default to the
        // host's active variant before falling back to the legacy untargeted probe.
        var effective = variant ?? _host.ActualThemeVariant;
        if (effective is not null && _host.TryFindResource(key, effective, out value))
        {
            return true;
        }

        return _host.TryFindResource(key, out value);
    }
}
