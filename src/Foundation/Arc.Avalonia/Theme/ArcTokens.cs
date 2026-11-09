namespace Arc.Avalonia.Theme;

/// <summary>Every resource key the Arc styles and components resolve with DynamicResource.</summary>
public static class ArcTokens
{
    // Surfaces and text
    public const string Background = "ArcBackground";
    public const string Surface = "ArcSurface";
    public const string SurfaceRaised = "ArcSurfaceRaised";
    public const string SurfaceMuted = "ArcSurfaceMuted";
    public const string Foreground = "ArcForeground";
    public const string TextSecondary = "ArcTextSecondary";
    public const string TextMuted = "ArcTextMuted";

    // Lines
    public const string Border = "ArcBorder";
    public const string BorderSubtle = "ArcBorderSubtle";
    public const string BorderStrong = "ArcBorderStrong";

    // Accent
    public const string Accent = "ArcAccent";
    public const string AccentStrong = "ArcAccentStrong";
    public const string AccentSubtle = "ArcAccentSubtle";
    public const string AccentForeground = "ArcAccentForeground";

    // Status
    public const string Success = "ArcSuccess";
    public const string Warning = "ArcWarning";
    public const string Danger = "ArcDanger";
    public const string SuccessSubtle = "ArcSuccessSubtle";
    public const string WarningSubtle = "ArcWarningSubtle";
    public const string DangerSubtle = "ArcDangerSubtle";
    public const string DangerForeground = "ArcDangerForeground";

    /// <summary>Dimming layer behind dialogs, drawers and sheets.</summary>
    public const string Scrim = "ArcScrim";

    // Selection controls
    public const string ControlOn = "ArcControlOn";
    public const string ControlGlyph = "ArcControlGlyph";
    public const string ControlTrack = "ArcControlTrack";
    public const string ControlTrackHover = "ArcControlTrackHover";
    public const string ControlThumb = "ArcControlThumb";
    public const string ControlThumbOn = "ArcControlThumbOn";
    public const string ControlThumbShadow = "ArcControlThumbShadow";
    public const string ControlOnSubtle = "ArcControlOnSubtle";
    public const string ControlFill = "ArcControlFill";

    // Focus
    public const string FocusOutline = "ArcFocusOutline";

    // Elevation
    public const string ShadowResting = "ArcShadowResting";
    public const string ShadowRaised = "ArcShadowRaised";
    public const string ShadowFloating = "ArcShadowFloating";

    // Data
    public const string Series1 = "ArcSeries1";
    public const string Series2 = "ArcSeries2";
    public const string Series3 = "ArcSeries3";
    public const string Series4 = "ArcSeries4";

    // Brand wash
    public const string GradientFrom = "ArcGradientFrom";
    public const string GradientTo = "ArcGradientTo";
    public const string GradientSoft = "ArcGradientSoft";
    public const string GradientInk = "ArcGradientInk";
    public const string GradientForeground = "ArcGradientForeground";

    // Type
    public const string FontDisplay = "ArcFontDisplay";
    public const string FontBody = "ArcFontBody";
    public const string TextXs = "ArcTextXs";
    public const string TextSm = "ArcTextSm";
    public const string TextBase = "ArcTextBase";
    public const string TextLg = "ArcTextLg";
    public const string TextXl = "ArcTextXl";
    public const string Text2xl = "ArcText2xl";
    public const string Text3xl = "ArcText3xl";
    public const string Text4xl = "ArcText4xl";
    public const string Text5xl = "ArcText5xl";
    public const string TrackingDisplay = "ArcTrackingDisplay";
    public const string TrackingBody = "ArcTrackingBody";
    public const string LeadingDisplay = "ArcLeadingDisplay";
    public const string LeadingBody = "ArcLeadingBody";

    // Radii
    public const string RadiusControl = "ArcRadiusControl";
    public const string RadiusPanel = "ArcRadiusPanel";
    public const string RadiusSurface = "ArcRadiusSurface";
    public const string RadiusPill = "ArcRadiusPill";

    // Sizing
    public const string ControlHeightSm = "ArcControlHeightSm";
    public const string ControlHeightMd = "ArcControlHeightMd";
    public const string ControlHeightLg = "ArcControlHeightLg";
    public const string Space1 = "ArcSpace1";
    public const string Space2 = "ArcSpace2";
    public const string Space3 = "ArcSpace3";
    public const string Space4 = "ArcSpace4";
    public const string Space5 = "ArcSpace5";
    public const string Space6 = "ArcSpace6";
    public const string Space8 = "ArcSpace8";
    public const string Space10 = "ArcSpace10";
    public const string Space12 = "ArcSpace12";
    public const string Space16 = "ArcSpace16";
    public const string Space20 = "ArcSpace20";
    public const string Space24 = "ArcSpace24";

    // Motion
    public const string DurationInstant = "ArcDurationInstant";
    public const string DurationFast = "ArcDurationFast";
    public const string DurationStandard = "ArcDurationStandard";
    public const string DurationConsidered = "ArcDurationConsidered";

    // Touch (M2-T8)
    /// <summary>Minimum hit target in px (44); deliberately not scaled by the density preset.</summary>
    public const string HitTargetMin = "ArcHitTargetMin";

    /// <summary>Comfortable hit target in px (48); used by primary mobile actions.</summary>
    public const string HitTargetComfortable = "ArcHitTargetComfortable";

    /// <summary>Top safe-area inset; 0 by default, hosts override the token when they reserve space.</summary>
    public const string SafeAreaTop = "ArcSafeAreaTop";

    /// <summary>Bottom safe-area inset; 0 by default, hosts override the token when they reserve space.</summary>
    public const string SafeAreaBottom = "ArcSafeAreaBottom";
}

