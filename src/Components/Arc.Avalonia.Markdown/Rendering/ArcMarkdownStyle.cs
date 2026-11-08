using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Resolved Arc tokens for the Markdown renderers. Renderers never hard-code a colour or size: every
/// brush, font and spacing value comes from here so light/dark and the eight accents stay consistent.
/// Brushes resolve through <see cref="ArcResources"/> on every access (variant-aware), so a scheme or
/// accent switch is picked up by <see cref="ArcMarkdownView.RefreshTheme"/>.
/// </summary>
public sealed class ArcMarkdownStyle
{
    private static readonly FontFamily MonoFallback = new("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

    /// <summary>Token snapshot; replaceable for tests and refreshed on theme change.</summary>
    public static ArcMarkdownStyle Current { get; set; } = new();

    /// <summary>Body font family (<c>ArcFontBody</c>).</summary>
    public FontFamily BodyFont { get; init; } = ArcResources.Font(ArcTokens.FontBody);

    /// <summary>Display font family (<c>ArcFontDisplay</c>).</summary>
    public FontFamily DisplayFont { get; init; } = ArcResources.Font(ArcTokens.FontDisplay);

    /// <summary>Monospace family for code and inline code (no Arc token; a platform stack).</summary>
    public FontFamily MonoFont { get; init; } = MonoFallback;

    /// <summary>Body text size (<c>ArcTextBase</c>).</summary>
    public double BodySize { get; init; } = Positive(ArcResources.Double(ArcTokens.TextBase), 14);

    /// <summary>Small text size (<c>ArcTextSm</c>).</summary>
    public double SmallSize { get; init; } = Positive(ArcResources.Double(ArcTokens.TextSm), 13);

    /// <summary>Caption text size (<c>ArcTextXs</c>).</summary>
    public double CaptionSize { get; init; } = Positive(ArcResources.Double(ArcTokens.TextXs), 12);

    /// <summary>Heading size by level (1-6).</summary>
    public double HeadingSize(int level) => level switch
    {
        1 => Positive(ArcResources.Double(ArcTokens.Text3xl), 28),
        2 => Positive(ArcResources.Double(ArcTokens.Text2xl), 22),
        3 => Positive(ArcResources.Double(ArcTokens.TextXl), 18),
        4 => Positive(ArcResources.Double(ArcTokens.TextLg), 16),
        5 => BodySize,
        _ => SmallSize,
    };

    /// <summary>Foreground brush (<c>ArcForeground</c>).</summary>
    public IBrush Foreground => ArcResources.Brush(ArcTokens.Foreground);

    /// <summary>Secondary text (<c>ArcTextSecondary</c>).</summary>
    public IBrush TextSecondary => ArcResources.Brush(ArcTokens.TextSecondary);

    /// <summary>Muted text (<c>ArcTextMuted</c>).</summary>
    public IBrush TextMuted => ArcResources.Brush(ArcTokens.TextMuted);

    /// <summary>Default border (<c>ArcBorder</c>).</summary>
    public IBrush Border => ArcResources.Brush(ArcTokens.Border);

    /// <summary>Subtle border (<c>ArcBorderSubtle</c>).</summary>
    public IBrush BorderSubtle => ArcResources.Brush(ArcTokens.BorderSubtle);

    /// <summary>Strong border (<c>ArcBorderStrong</c>), used for quote rails.</summary>
    public IBrush BorderStrong => ArcResources.Brush(ArcTokens.BorderStrong);

    /// <summary>Surface brush (<c>ArcSurface</c>).</summary>
    public IBrush Surface => ArcResources.Brush(ArcTokens.Surface);

    /// <summary>Muted surface (<c>ArcSurfaceMuted</c>), also the code background.</summary>
    public IBrush SurfaceMuted => ArcResources.Brush(ArcTokens.SurfaceMuted);

    /// <summary>Accent brush (<c>ArcAccent</c>) for links and active states.</summary>
    public IBrush Accent => ArcResources.Brush(ArcTokens.Accent);

    /// <summary>Accent-tinted quiet fill (<c>ArcAccentSubtle</c>).</summary>
    public IBrush AccentSubtle => ArcResources.Brush(ArcTokens.AccentSubtle);

    /// <summary>Success brush (<c>ArcSuccess</c>).</summary>
    public IBrush Success => ArcResources.Brush(ArcTokens.Success);

    /// <summary>Warning brush (<c>ArcWarning</c>).</summary>
    public IBrush WarningBrush => ArcResources.Brush(ArcTokens.Warning);

    /// <summary>Danger brush (<c>ArcDanger</c>).</summary>
    public IBrush Danger => ArcResources.Brush(ArcTokens.Danger);

    /// <summary>Paragraph bottom spacing.</summary>
    public double BlockSpacing { get; init; } = Positive(ArcResources.Double(ArcTokens.Space3), 12);

    /// <summary>Extra top spacing before headings.</summary>
    public double HeadingTopSpacing { get; init; } = Positive(ArcResources.Double(ArcTokens.Space6), 24);

    /// <summary>Heading bottom spacing.</summary>
    public double HeadingBottomSpacing { get; init; } = Positive(ArcResources.Double(ArcTokens.Space2), 8);

    /// <summary>List indentation.</summary>
    public double ListIndent { get; init; } = Positive(ArcResources.Double(ArcTokens.Space5), 20);

    /// <summary>List item spacing.</summary>
    public double ListItemSpacing { get; init; } = Positive(ArcResources.Double(ArcTokens.Space1), 4);

    /// <summary>Quote indent and left border width.</summary>
    public double QuoteIndent { get; init; } = Positive(ArcResources.Double(ArcTokens.Space4), 16);

    /// <summary>Code block padding.</summary>
    public double CodePadding { get; init; } = Positive(ArcResources.Double(ArcTokens.Space4), 16);

    /// <summary>Code block bottom spacing.</summary>
    public double CodeBlockSpacing => BlockSpacing;

    /// <summary>Control radius (<c>ArcRadiusControl</c>).</summary>
    public CornerRadius ControlRadius { get; init; } = Radius(ArcTokens.RadiusControl, 18);

    /// <summary>Panel radius (<c>ArcRadiusPanel</c>) used by code blocks and tables.</summary>
    public CornerRadius PanelRadius { get; init; } = Radius(ArcTokens.RadiusPanel, 26);

    /// <summary>Surface radius (<c>ArcRadiusSurface</c>) used by images and cards.</summary>
    public CornerRadius SurfaceRadius { get; init; } = Radius(ArcTokens.RadiusSurface, 34);

    /// <summary>Code text colour.</summary>
    public IBrush CodeForeground => Foreground;

    /// <summary>Inline-code background (<c>ArcSurfaceMuted</c>: a quiet theme-aware fill with readable foreground).</summary>
    public IBrush InlineCodeBackground => ArcResources.Brush(ArcTokens.SurfaceMuted);

    private static double Positive(double value, double fallback) => value > 0 ? value : fallback;

    private static CornerRadius Radius(string key, double fallback)
    {
        var radius = ArcResources.Radius(key);
        return radius == default ? new CornerRadius(fallback) : radius;
    }
}
