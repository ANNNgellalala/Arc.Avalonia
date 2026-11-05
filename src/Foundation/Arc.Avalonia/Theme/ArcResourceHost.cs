using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Resource lookup surface used by <see cref="ArcResources"/>. The default implementation reads the
/// live <see cref="Application"/>; tests and multi-window hosts can install their own with
/// <see cref="ArcResources.UseHost"/> so token lookups also work without a desktop application.
/// </summary>
public interface IArcResourceHost
{
    /// <summary>Theme variant currently in effect, or null when no application/variant is available.</summary>
    ThemeVariant? ActualThemeVariant { get; }

    /// <summary>Looks a key up the way the original code did (no explicit theme variant).</summary>
    bool TryFindResource(string key, out object? value);

    /// <summary>Looks a key up against an explicit theme variant (ThemeDictionaries-aware).</summary>
    bool TryFindResource(string key, ThemeVariant variant, out object? value);
}

/// <summary>
/// Default <see cref="IArcResourceHost"/>: forwards every lookup to <see cref="Application.Current"/>.
/// Stateless, so it picks up the application whenever it is created (components keep working with a
/// null application and simply fall back to their defaults, exactly as before).
/// </summary>
internal sealed class ApplicationResourceHost : IArcResourceHost
{
    public static ApplicationResourceHost Instance { get; } = new();

    private ApplicationResourceHost()
    {
    }

    public ThemeVariant? ActualThemeVariant => Application.Current?.ActualThemeVariant;

    public bool TryFindResource(string key, out object? value)
    {
        value = null;
        return Application.Current is { } app && app.TryFindResource(key, out value);
    }

    public bool TryFindResource(string key, ThemeVariant variant, out object? value)
    {
        value = null;
        return Application.Current is { } app && app.TryFindResource(key, variant, out value);
    }
}
