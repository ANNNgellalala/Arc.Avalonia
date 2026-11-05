using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Maps the Arc accent (one of the 8 presets or a custom brand seed derived in OKLCH) onto Fluent's public
/// <see cref="FluentTheme.Palettes"/> API (<see cref="ColorPaletteResources"/>), one entry per theme
/// variant. POC-A proved this updates <c>SystemAccentColor</c> and every Fluent accent brush immediately;
/// the alternative (overriding a bare <c>SystemAccentColor</c> resource) does not reach the derived brushes.
/// </summary>
public static class ArcThemePalettes
{
    public static void Apply(FluentTheme theme, ArcThemeOptions options)
    {
        // ArcPalette owns the derivation, so a custom brand colour produces exactly the same accent the
        // token dictionary uses - SystemAccentColor can never drift from ArcAccent.
        theme.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = ArcPalette.AccentColor(ArcColorScheme.Light, options)
        };
        theme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = ArcPalette.AccentColor(ArcColorScheme.Dark, options)
        };
    }
}
