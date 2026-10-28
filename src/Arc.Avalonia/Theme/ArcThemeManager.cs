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
    public static IBrush Brush(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;

    public static Color Color(string key) =>
        Brush(key) is ISolidColorBrush solid ? solid.Color : Colors.Black;

    public static double Double(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is double number ? number : 0;

    public static CornerRadius Radius(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is CornerRadius radius ? radius : default;

    public static BoxShadows Shadows(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is BoxShadows shadows ? shadows : default;

    public static TimeSpan Duration(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is TimeSpan time ? time : TimeSpan.Zero;
}
