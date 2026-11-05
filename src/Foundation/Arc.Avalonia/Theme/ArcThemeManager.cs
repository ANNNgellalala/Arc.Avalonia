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

    public static void Apply(Application app, ArcColorScheme scheme, ArcAccent accent)
    {
        Scheme = scheme;
        Accent = accent;

        app.RequestedThemeVariant = scheme == ArcColorScheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var palette = ArcPalette.Build(scheme, accent);
        if (_current is not null)
        {
            app.Resources.MergedDictionaries.Remove(_current);
        }

        app.Resources.MergedDictionaries.Add(_current = palette);
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
    /// Legacy lookup (no explicit theme variant). Kept as-is for source compatibility; see the
    /// variant-aware overloads for the ThemeDictionaries-safe path described in ADR 0003 §4.2.
    /// </summary>
    public static IBrush Brush(string key) => Brush(key, null);

    /// <summary>
    /// Variant-aware lookup: passes <paramref name="variant"/> to the host so resources defined in
    /// <c>ThemeDictionaries</c> resolve for the requested Light/Dark variant. Once tokens move into
    /// ThemeDictionaries, pass <c>ArcResources.Host.ActualThemeVariant</c> (or a known variant) from
    /// code that must follow the active theme; the parameterless overload then keeps defaulting to
    /// the un-targeted lookup for backwards compatibility.
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
        if (variant is not null && _host.TryFindResource(key, variant, out value))
        {
            return true;
        }

        return _host.TryFindResource(key, out value);
    }
}
