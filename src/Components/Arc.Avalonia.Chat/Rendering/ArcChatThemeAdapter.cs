using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Theme;
using PuraMarkdown.Agent;
using PuraMarkdown.Agent.Localization;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Maps Arc design tokens onto the PuraMarkdown.Agent theme/pipeline. Arc keeps ownership of the palette
/// (light/dark + accents); PuraMarkdown.Agent owns the Markdown/Chat/Agent rendering.
/// </summary>
public static class ArcChatThemeAdapter
{
    /// <summary>Builds the render options used by <see cref="ArcChatView"/>.</summary>
    public static PuraMarkdownOptions CreateOptions()
    {
        var light = BuildTheme(ThemeVariant.Light, "arc-light");
        var dark = BuildTheme(ThemeVariant.Dark, "arc-dark");
        return new PuraMarkdownOptions
        {
            Pipeline = BuildPipeline(),
            Theme = light,
            DarkTheme = dark,
            FollowSystemTheme = true,
            EnableTextSelection = true,
            Localizer = ChinesePuraLocalizer.Instance,
        };
    }

    /// <summary>CommonMark + GFM + the built-in pura-agent components (choice/confirm/tool-call).</summary>
    public static PuraPipeline BuildPipeline() =>
        new PuraPipelineBuilder()
            .UseCommonMark()
            .UseGfm()
            .UseAgentComponents()
            .Build();

    private static PuraTheme BuildTheme(ThemeVariant variant, string name) => new()
    {
        Name = name,
        Colors = new PuraColors(
            Text: Color(ArcTokens.Foreground, variant),
            Background: Color(ArcTokens.Background, variant),
            Border: Color(ArcTokens.Border, variant),
            Accent: Color(ArcTokens.Accent, variant),
            Muted: Color(ArcTokens.TextSecondary, variant),
            CodeBackground: Color(ArcTokens.SurfaceMuted, variant),
            CodeBorder: Color(ArcTokens.Border, variant),
            Danger: Color(ArcTokens.Danger, variant)),
        Typography = new PuraTypography(
            BodySize: PuraTypography.Default.BodySize,
            Heading1Size: PuraTypography.Default.Heading1Size,
            Heading2Size: PuraTypography.Default.Heading2Size,
            Heading3Size: PuraTypography.Default.Heading3Size,
            Heading4Size: PuraTypography.Default.Heading4Size,
            Heading5Size: PuraTypography.Default.Heading5Size,
            Heading6Size: PuraTypography.Default.Heading6Size,
            BodyFont: ArcResources.Font(ArcTokens.FontBody, variant),
            MonoFont: new FontFamily("Consolas, Cascadia Mono, DejaVu Sans Mono, monospace")),
        Spacing = PuraSpacing.Default,
        CodeTheme = new PuraCodeTheme(
            PlainText: Color(ArcTokens.Foreground, variant),
            Comment: Color(ArcTokens.TextSecondary, variant),
            Keyword: Color(ArcTokens.Accent, variant),
            String: Color(ArcTokens.Success, variant),
            Number: Color(ArcTokens.TextSecondary, variant)),
    };

    private static Color Color(string token, ThemeVariant variant) => ArcResources.Color(token, variant);
}

