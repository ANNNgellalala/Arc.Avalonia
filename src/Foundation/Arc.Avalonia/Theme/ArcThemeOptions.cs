using Avalonia.Media;

namespace Arc.Avalonia.Theme;

/// <summary>Control density preset; maps the height/spacing tokens (M2-G3h).</summary>
public enum ArcDensity
{
    /// <summary>Toolbar / data-dense surfaces: shorter controls, tighter spacing.</summary>
    Compact,

    /// <summary>Arc's default rhythm.</summary>
    Default,

    /// <summary>Marketing / touch surfaces: taller controls, generous spacing.</summary>
    Comfortable
}

/// <summary>
/// Full theme request for <see cref="ArcThemeManager.Apply(Avalonia.Application, ArcThemeOptions)"/>:
/// scheme, accent (one of the 8 presets or a custom brand colour), density and an optional radius scale.
/// Every value is immutable; use <c>with</c> to derive variants.
/// </summary>
public sealed record ArcThemeOptions
{
    /// <summary>Light or dark scheme.</summary>
    public ArcColorScheme Scheme { get; init; } = ArcColorScheme.Light;

    /// <summary>One of the 8 Arc accent presets; ignored when <see cref="AccentColor"/> is set.</summary>
    public ArcAccent Accent { get; init; } = ArcAccent.Neutral;

    /// <summary>
    /// Custom brand seed colour. When set, the whole accent ramp (accent / strong / foreground /
    /// selection colours) is derived from it in OKLCH, so it drives both the Arc tokens and Fluent's
    /// <c>SystemAccentColor</c>. No literal colour is stored in the theme implementation.
    /// </summary>
    public Color? AccentColor { get; init; }

    /// <summary>Height/spacing preset.</summary>
    public ArcDensity Density { get; init; } = ArcDensity.Default;

    /// <summary>
    /// Multiplier applied to the corner-radius tokens (1.0 = Arc default). Clamped to 0.5–2.0; the
    /// pill radius token is never scaled.
    /// </summary>
    public double CornerRadiusScale { get; init; } = 1.0;

    /// <summary>
    /// Top safe-area inset in px that the host reserves for system UI (status bar / notch). 0 keeps the
    /// token default; <see cref="ArcTouch.ApplySafeArea"/> adds it to a control's margin.
    /// </summary>
    public double SafeAreaTop { get; init; }

    /// <summary>
    /// Bottom safe-area inset in px that the host reserves for system UI (home indicator / gesture bar).
    /// 0 keeps the token default; <see cref="ArcTouch.ApplySafeArea"/> adds it to a control's margin.
    /// </summary>
    public double SafeAreaBottom { get; init; }

    /// <summary>True when a custom brand colour is in use.</summary>
    public bool HasCustomAccent => AccentColor is not null;
}
