using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Maps the 8 Arc accents onto Fluent's public <see cref="FluentTheme.Palettes"/> API
/// (<see cref="ColorPaletteResources"/>), one entry per theme variant. POC-A proved this updates
/// <c>SystemAccentColor</c> and every Fluent accent brush immediately; the alternative (overriding a bare
/// <c>SystemAccentColor</c> resource) does not reach the theme's derived brushes.
/// </summary>
public static class ArcThemePalettes
{
    public static void Apply(FluentTheme theme, ArcAccent accent)
    {
        theme.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = ArcPalette.AccentColor(ArcColorScheme.Light, accent)
        };
        theme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = ArcPalette.AccentColor(ArcColorScheme.Dark, accent)
        };
    }
}
