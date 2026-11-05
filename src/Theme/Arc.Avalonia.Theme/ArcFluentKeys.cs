using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Theme;

/// <summary>
/// The Fluent 12.1.4 resource keys the Arc theme overrides, with the 11.x → 12.1.4 name migrations that
/// M0 POC-B measured (docs/adr/0004-top-control-keys.md §4.1). Using a stale name fails silently: the
/// custom key resolves but no control consumes it, so keep this file as the single source of truth and
/// let <c>ArcFluentOverrides</c> build the dictionaries from it.
/// </summary>
/// <remarks>
/// Overridden set: 47 keys across TextBox (11), ComboBox (16), TabControl/TabItem (11), ScrollBar (7) and
/// two global keys. 46/47 are pure key overrides; only the TextBox/ComboBox font size needs a local
/// ControlTheme (tracked in the M1-T3 report, not implemented here).
/// </remarks>
public static class ArcFluentKeys
{
    // ---- Global -------------------------------------------------------------------------------------
    /// <summary>Corner radius shared by text inputs, combo boxes, selected-tab pipe, etc. (Fluent default 3).</summary>
    public const string ControlCornerRadius = "ControlCornerRadius";

    /// <summary>Content font size. NOTE: 11.x name <c>ContentControlThemeFontSize</c> no longer exists.</summary>
    public const string ControlContentThemeFontSize = "ControlContentThemeFontSize";

    // ---- TextBox ------------------------------------------------------------------------------------
    public const string TextControlBackground = "TextControlBackground";
    public const string TextControlBackgroundPointerOver = "TextControlBackgroundPointerOver";
    public const string TextControlBackgroundFocused = "TextControlBackgroundFocused";
    public const string TextControlBorderBrush = "TextControlBorderBrush";
    public const string TextControlBorderBrushPointerOver = "TextControlBorderBrushPointerOver";
    public const string TextControlBorderBrushFocused = "TextControlBorderBrushFocused";
    public const string TextControlForeground = "TextControlForeground";
    public const string TextControlPlaceholderForeground = "TextControlPlaceholderForeground";
    public const string TextControlSelectionHighlightColor = "TextControlSelectionHighlightColor";
    public const string TextControlThemeMinHeight = "TextControlThemeMinHeight";

    /// <summary>NOTE: 11.x name <c>TextControlPadding</c> does not exist in 12.1.4.</summary>
    public const string TextControlThemePadding = "TextControlThemePadding";

    // ---- ComboBox -----------------------------------------------------------------------------------
    public const string ComboBoxBackground = "ComboBoxBackground";
    public const string ComboBoxBackgroundPointerOver = "ComboBoxBackgroundPointerOver";
    public const string ComboBoxBackgroundFocused = "ComboBoxBackgroundFocused";
    public const string ComboBoxBorderBrush = "ComboBoxBorderBrush";
    public const string ComboBoxBorderBrushPointerOver = "ComboBoxBorderBrushPointerOver";
    public const string ComboBoxForeground = "ComboBoxForeground";

    /// <summary>NOTE: the capital H is the Fluent 12.1.4 spelling (<c>ComboBoxPlaceHolderForeground</c>).</summary>
    public const string ComboBoxPlaceHolderForeground = "ComboBoxPlaceHolderForeground";

    public const string ComboBoxDropDownBackground = "ComboBoxDropDownBackground";
    public const string ComboBoxDropDownBorderBrush = "ComboBoxDropDownBorderBrush";
    public const string ComboBoxItemBackgroundPointerOver = "ComboBoxItemBackgroundPointerOver";
    public const string ComboBoxItemBackgroundSelected = "ComboBoxItemBackgroundSelected";
    public const string ComboBoxItemForeground = "ComboBoxItemForeground";
    public const string ComboBoxItemForegroundSelected = "ComboBoxItemForegroundSelected";
    public const string ComboBoxPadding = "ComboBoxPadding";

    /// <summary>Applies to the template's width, not the control's <c>MinWidth</c> property.</summary>
    public const string ComboBoxThemeMinWidth = "ComboBoxThemeMinWidth";

    public const string ComboBoxMinHeight = "ComboBoxMinHeight";

    // ---- TabControl / TabItem -----------------------------------------------------------------------
    /// <summary>NOTE: 11.x name <c>TabItemFontSize</c> does not exist; Fluent's default here is 24 px.</summary>
    public const string TabItemHeaderFontSize = "TabItemHeaderFontSize";

    public const string TabItemMargin = "TabItemMargin";

    /// <summary>NOTE: the value is used as the tab header's Padding (11.x: <c>TabItemHeaderPadding</c>).</summary>
    public const string TabItemHeaderMargin = "TabItemHeaderMargin";

    public const string TabItemMinHeight = "TabItemMinHeight";
    public const string TabItemHeaderBackgroundUnselected = "TabItemHeaderBackgroundUnselected";
    public const string TabItemHeaderBackgroundSelected = "TabItemHeaderBackgroundSelected";
    public const string TabItemHeaderBackgroundUnselectedPointerOver = "TabItemHeaderBackgroundUnselectedPointerOver";
    public const string TabItemHeaderBackgroundSelectedPointerOver = "TabItemHeaderBackgroundSelectedPointerOver";
    public const string TabItemHeaderForegroundUnselected = "TabItemHeaderForegroundUnselected";
    public const string TabItemHeaderForegroundSelected = "TabItemHeaderForegroundSelected";
    public const string TabItemHeaderSelectedPipeFill = "TabItemHeaderSelectedPipeFill";

    // ---- ScrollBar ----------------------------------------------------------------------------------
    public const string ScrollBarSize = "ScrollBarSize";

    /// <summary>
    /// The resting thumb fill. NOTE: 11.x's base <c>ScrollBarThumbFill</c> does not exist in 12.1.4; POC-B's
    /// single-key sweep proved this is the key that actually drives <c>Thumb.Background</c>.
    /// </summary>
    public const string ScrollBarPanningThumbBackground = "ScrollBarPanningThumbBackground";

    public const string ScrollBarThumbFillPointerOver = "ScrollBarThumbFillPointerOver";
    public const string ScrollBarThumbFillPressed = "ScrollBarThumbFillPressed";
    public const string ScrollBarTrackFill = "ScrollBarTrackFill";
    public const string ScrollBarTrackStroke = "ScrollBarTrackStroke";
    public const string ScrollBarBackground = "ScrollBarBackground";

    /// <summary>Every overridden key; tests assert the dictionary covers exactly this list.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(ArcFluentKeys)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();
}
