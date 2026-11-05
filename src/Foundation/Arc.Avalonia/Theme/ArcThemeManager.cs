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

    public static ArcColorScheme Scheme { get; private set; } = ArcColorScheme.Light;

    public static ArcAccent Accent { get; private set; } = ArcAccent.Neutral;

    /// <summary>When true components skip travel animation, matching Arc's reduced-motion branch.</summary>
    public static bool ReducedMotion { get; set; }

    /// <summary>
    /// Raised after <see cref="Apply"/> swapped the palette (arguments: application, scheme, accent).
    /// The aggregated theme (src/Theme/Arc.Avalonia.Theme) subscribes to keep Fluent's
    /// <c>FluentTheme.Palettes</c> and the Fluent resource-key overrides in sync, so the Foundation
    /// layer does not need a dependency on Avalonia.Themes.Fluent.
    /// </summary>
    public static event Action<Application, ArcColorScheme, ArcAccent>? Applied;

    public static void Apply(Application app, ArcColorScheme scheme, ArcAccent accent)
    {
        Scheme = scheme;
        Accent = accent;

        app.RequestedThemeVariant = scheme == ArcColorScheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        // Tokens live in ThemeDictionaries (Light + Dark), so a bare variant switch restyles the whole tree
        // and only an accent change needs a rebuild (ADR 0003 §4.2).
        var palette = ArcPalette.BuildThemeDictionaries(accent);
        if (_current is not null)
        {
            app.Resources.MergedDictionaries.Remove(_current);
        }

        app.Resources.MergedDictionaries.Add(_current = palette);

        Applied?.Invoke(app, scheme, accent);
    }

    public static void SetScheme(Application app, ArcColorScheme scheme) => Apply(app, scheme, Accent);

    public static void ToggleScheme(Application app) =>
        Apply(app, Scheme == ArcColorScheme.Light ? ArcColorScheme.Dark : ArcColorScheme.Light, Accent);

    public static void SetAccent(Application app, ArcAccent accent) => Apply(app, Scheme, accent);
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

    private static bool TryFind(string key, ThemeVariant? variant, out object? value)
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
