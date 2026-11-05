using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Builds the theme-aware dictionary that re-skins the bare Fluent controls with Arc tokens
/// (docs/adr/0004-top-control-keys.md): 47 keys, Light and Dark variants, all values derived from
/// <see cref="ArcPalette"/> so no literal colour ever appears here (R6 token-only).
/// </summary>
/// <remarks>
/// 46/47 of these keys are pure overrides verified by M0 POC-B. The one gap is the TextBox/ComboBox font
/// size: Fluent hard-codes the template size, so those two controls still need a local ControlTheme
/// (tracked in the M1-T3 report). Filled in by <see cref="ArcTheme"/> on every
/// <see cref="ArcThemeManager.Apply"/> so accent changes reach the Fluent surface too.
/// </remarks>
public static class ArcFluentOverrides
{
    /// <summary>Input vertical padding used by the Arc field family (spacing tokens 10 + horizontal 16).</summary>
    private static readonly Thickness InputPadding = new(16, 10, 16, 10);

    /// <summary>ComboBox keeps a slightly narrower trailing edge for the drop-down glyph.</summary>
    private static readonly Thickness ComboBoxInputPadding = new(16, 10, 12, 10);

    /// <summary>Arc's tab strip uses trailing space instead of symmetric padding.</summary>
    private static readonly Thickness TabHeaderPadding = new(0, 0, 16, 0);

    private static readonly Thickness NoMargin = new(0);

    public static ResourceDictionary Build(ArcAccent accent) => new()
    {
        ThemeDictionaries =
        {
            [ThemeVariant.Light] = ForScheme(ArcColorScheme.Light, accent),
            [ThemeVariant.Dark] = ForScheme(ArcColorScheme.Dark, accent)
        }
    };

    private static ResourceDictionary ForScheme(ArcColorScheme scheme, ArcAccent accent)
    {
        var palette = ArcPalette.Build(scheme, accent);
        var dictionary = new ResourceDictionary();

        IBrush Brush(string token) => palette[token] as IBrush ?? Brushes.Transparent;
        double Number(string token) => palette[token] is double number ? number : 0;
        Color Colour(string token) => palette[token] is ISolidColorBrush solid ? solid.Color : Colors.Gray;
        CornerRadius Radius(string token) => palette[token] is CornerRadius radius ? radius : default;

        // ---- Global ----
        dictionary[ArcFluentKeys.ControlCornerRadius] = Radius(ArcTokens.RadiusControl);
        dictionary[ArcFluentKeys.ControlContentThemeFontSize] = Number(ArcTokens.TextBase);

        // ---- TextBox ----
        dictionary[ArcFluentKeys.TextControlBackground] = Brush(ArcTokens.Surface);
        dictionary[ArcFluentKeys.TextControlBackgroundPointerOver] = Brush(ArcTokens.Surface);
        dictionary[ArcFluentKeys.TextControlBackgroundFocused] = Brush(ArcTokens.Surface);
        dictionary[ArcFluentKeys.TextControlBorderBrush] = Brush(ArcTokens.Border);
        dictionary[ArcFluentKeys.TextControlBorderBrushPointerOver] = Brush(ArcTokens.BorderStrong);
        dictionary[ArcFluentKeys.TextControlBorderBrushFocused] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.TextControlForeground] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.TextControlPlaceholderForeground] = Brush(ArcTokens.TextMuted);
        dictionary[ArcFluentKeys.TextControlSelectionHighlightColor] =
            ArcColor.WithAlpha(Colour(ArcTokens.Accent), 0.35);
        dictionary[ArcFluentKeys.TextControlThemeMinHeight] = Number(ArcTokens.ControlHeightMd);
        dictionary[ArcFluentKeys.TextControlThemePadding] = InputPadding;

        // ---- ComboBox ----
        dictionary[ArcFluentKeys.ComboBoxBackground] = Brush(ArcTokens.Surface);
        dictionary[ArcFluentKeys.ComboBoxBackgroundPointerOver] = Brush(ArcTokens.SurfaceMuted);
        dictionary[ArcFluentKeys.ComboBoxBackgroundFocused] = Brush(ArcTokens.Surface);
        dictionary[ArcFluentKeys.ComboBoxBorderBrush] = Brush(ArcTokens.Border);
        dictionary[ArcFluentKeys.ComboBoxBorderBrushPointerOver] = Brush(ArcTokens.BorderStrong);
        dictionary[ArcFluentKeys.ComboBoxForeground] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.ComboBoxPlaceHolderForeground] = Brush(ArcTokens.TextMuted);
        dictionary[ArcFluentKeys.ComboBoxDropDownBackground] = Brush(ArcTokens.SurfaceRaised);
        dictionary[ArcFluentKeys.ComboBoxDropDownBorderBrush] = Brush(ArcTokens.Border);
        dictionary[ArcFluentKeys.ComboBoxItemBackgroundPointerOver] = Brush(ArcTokens.SurfaceMuted);
        dictionary[ArcFluentKeys.ComboBoxItemBackgroundSelected] = Brush(ArcTokens.SurfaceMuted);
        dictionary[ArcFluentKeys.ComboBoxItemForeground] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.ComboBoxItemForegroundSelected] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.ComboBoxPadding] = ComboBoxInputPadding;
        dictionary[ArcFluentKeys.ComboBoxThemeMinWidth] = 140d;
        dictionary[ArcFluentKeys.ComboBoxMinHeight] = Number(ArcTokens.ControlHeightMd);

        // ---- TabControl / TabItem ----
        dictionary[ArcFluentKeys.TabItemHeaderFontSize] = Number(ArcTokens.TextSm);
        dictionary[ArcFluentKeys.TabItemMargin] = NoMargin;
        dictionary[ArcFluentKeys.TabItemHeaderMargin] = TabHeaderPadding;
        dictionary[ArcFluentKeys.TabItemMinHeight] = 40d;
        dictionary[ArcFluentKeys.TabItemHeaderBackgroundUnselected] = Brushes.Transparent;
        dictionary[ArcFluentKeys.TabItemHeaderBackgroundSelected] = Brushes.Transparent;
        dictionary[ArcFluentKeys.TabItemHeaderBackgroundUnselectedPointerOver] = Brush(ArcTokens.SurfaceMuted);
        dictionary[ArcFluentKeys.TabItemHeaderBackgroundSelectedPointerOver] = Brushes.Transparent;
        dictionary[ArcFluentKeys.TabItemHeaderForegroundUnselected] = Brush(ArcTokens.TextSecondary);
        dictionary[ArcFluentKeys.TabItemHeaderForegroundSelected] = Brush(ArcTokens.Foreground);
        dictionary[ArcFluentKeys.TabItemHeaderSelectedPipeFill] = Brush(ArcTokens.Foreground);

        // ---- ScrollBar ----
        dictionary[ArcFluentKeys.ScrollBarSize] = 10d;
        dictionary[ArcFluentKeys.ScrollBarPanningThumbBackground] = Brush(ArcTokens.BorderStrong);
        dictionary[ArcFluentKeys.ScrollBarThumbFillPointerOver] = Brush(ArcTokens.TextMuted);
        dictionary[ArcFluentKeys.ScrollBarThumbFillPressed] = Brush(ArcTokens.TextSecondary);
        dictionary[ArcFluentKeys.ScrollBarTrackFill] = Brushes.Transparent;
        dictionary[ArcFluentKeys.ScrollBarTrackStroke] = Brushes.Transparent;
        dictionary[ArcFluentKeys.ScrollBarBackground] = Brushes.Transparent;

        return dictionary;
    }
}
