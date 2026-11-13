using Avalonia;
using Avalonia.Media;
using PuraMarkdown.UI;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Maps Arc design-system tokens (<see cref="ArcMarkdownStyle"/>) onto the PuraMarkdown skin contract
/// (<see cref="PuraTheme"/>). Only used by renderers that fall through to the PuraMarkdown defaults
/// (Arc renders almost every node itself); keeping the mapping complete means a Pura-rendered fallback
/// still looks like Arc in light/dark and every accent.
/// </summary>
internal static class ArcMarkdownPuraTheme
{
    // Fallbacks are used only when Arc hands us a non-solid brush (gradients are legal <see cref="IBrush"/>s).
    // Named colors only (token-only gate R6: no hex/rgb/FromArgb in new files).
    private static readonly Color TextFallback = Colors.Black;
    private static readonly Color SurfaceFallback = Colors.White;
    private static readonly Color SurfaceMutedFallback = Colors.WhiteSmoke;
    private static readonly Color BorderFallback = Colors.LightGray;
    private static readonly Color AccentFallback = Colors.RoyalBlue;
    private static readonly Color MutedFallback = Colors.Gray;
    private static readonly Color DangerFallback = Colors.IndianRed;

    /// <summary>Builds the PuraMarkdown theme for the current Arc token snapshot.</summary>
    /// <param name="style">Arc token snapshot.</param>
    /// <returns>A PuraMarkdown theme mirroring the Arc tokens.</returns>
    internal static PuraTheme Build(ArcMarkdownStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var text = Solid(style.Foreground, TextFallback);
        var surface = Solid(style.Surface, SurfaceFallback);
        var border = Solid(style.Border, BorderFallback);
        var accent = Solid(style.Accent, AccentFallback);
        var muted = Solid(style.TextMuted, MutedFallback);
        var code = Solid(style.SurfaceMuted, SurfaceMutedFallback);
        var strong = Solid(style.BorderStrong, border);
        var danger = Solid(style.Danger, DangerFallback);
        var success = Solid(style.Success, accent);
        var warning = Solid(style.WarningBrush, danger);

        var colors = new PuraColors(
            Text: text,
            Background: surface,
            Border: border,
            Accent: accent,
            Muted: muted,
            CodeBackground: code,
            CodeBorder: border,
            Danger: danger) with
        {
            Link = accent,
            LinkHover = accent,
            SelectionBackground = accent,
            CodeHeaderBackground = code,
            CodeHeaderForeground = muted,
            TableHeaderBackground = code,
            TableBorder = border,
            QuoteBar = strong,
            QuoteBackground = Colors.Transparent,
            AlertNoteBackground = code,
            AlertNoteBorder = accent,
            AlertTipBackground = code,
            AlertTipBorder = accent,
            AlertImportantBackground = code,
            AlertImportantBorder = accent,
            AlertWarningBackground = code,
            AlertWarningBorder = danger,
            AlertCautionBackground = code,
            AlertCautionBorder = danger,
            Divider = border,
            TextMuted = muted,
        };

        var typography = new PuraTypography(
            BodySize: style.BodySize,
            Heading1Size: style.HeadingSize(1),
            Heading2Size: style.HeadingSize(2),
            Heading3Size: style.HeadingSize(3),
            Heading4Size: style.HeadingSize(4),
            Heading5Size: style.HeadingSize(5),
            Heading6Size: style.HeadingSize(6),
            BodyFont: style.BodyFont,
            MonoFont: style.MonoFont);

        var spacing = new PuraSpacing(
            BlockSpacing: style.BlockSpacing,
            ListIndent: style.ListIndent,
            CodePadding: style.CodePadding,
            CodeBlockSpacing: style.CodeBlockSpacing,
            QuoteIndent: style.QuoteIndent);

        var radii = new PuraRadii(
            CodeBlock: Round(style.PanelRadius),
            Button: Round(style.ControlRadius),
            Border: Round(style.SurfaceRadius));

        return new PuraTheme
        {
            Name = "arc",
            Colors = colors,
            Typography = typography,
            Spacing = spacing,
            Radii = radii,
            CodeTheme = new PuraCodeTheme(
                PlainText: text,
                Comment: muted,
                Keyword: accent,
                String: success,
                Number: warning),
        };
    }

    private static Color Solid(IBrush? brush, Color fallback)
        => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private static double Round(CornerRadius radius) => Math.Max(0, Math.Round(radius.TopLeft));
}
