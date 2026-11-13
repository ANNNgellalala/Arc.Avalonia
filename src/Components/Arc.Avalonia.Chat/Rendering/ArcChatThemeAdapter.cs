using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Theme;
using PuraMarkdown.Agent;
using PuraMarkdown.UI;
using PuraMarkdown.UI.Localization;
using PuraMarkdown.UI.Platform;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Maps Arc design tokens onto the PuraMarkdown render pipeline (PuraMarkdown.UI builds the theme/options;
/// PuraMarkdown.Agent adds the pura-agent fence components). Arc keeps ownership of the palette
/// (light/dark + accents); PuraMarkdown.UI owns the Markdown/Chat rendering.
/// </summary>
public static class ArcChatThemeAdapter
{
    /// <summary>Builds the render options used by <see cref="ArcChatView"/>.</summary>
    public static PuraMarkdownOptions CreateOptions() => CreateOptions(null);

    /// <summary>Builds the render options used by <see cref="ArcChatView"/> with an explicit link handler.</summary>
    /// <param name="linkHandler">
    /// Handler for message links. <see cref="ArcChatView"/> passes its own handler so clicks open
    /// http/https/mailto/tel in the platform launcher; when a host builds options itself it should pass its
    /// handler here (the upstream default, <see cref="NullLinkHandler"/>, opens nothing).
    /// </param>
    public static PuraMarkdownOptions CreateOptions(ILinkHandler? linkHandler)
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
            LinkHandler = linkHandler ?? NullLinkHandler.Instance,
        };
    }

    /// <summary>
    /// Copies <paramref name="source"/> with Arc's link handler installed, keeping every other render setting.
    /// Used when a host replaces <see cref="ArcChatView.Options"/> so message links do not silently stop
    /// working; <c>PuraMarkdownOptions</c> is immutable, hence the explicit copy.
    /// </summary>
    internal static PuraMarkdownOptions WithLinkHandler(PuraMarkdownOptions source, ILinkHandler linkHandler) => new()
    {
        Pipeline = source.Pipeline,
        Theme = source.Theme,
        DarkTheme = source.DarkTheme,
        FollowSystemTheme = source.FollowSystemTheme,
        InteractionEnabled = source.InteractionEnabled,
        UrlPolicy = source.UrlPolicy,
        ImageLoader = source.ImageLoader,
        LinkHandler = linkHandler,
        Clipboard = source.Clipboard,
        Limits = source.Limits,
        Localizer = source.Localizer,
        EnableTextSelection = source.EnableTextSelection,
        EnableBlockVirtualization = source.EnableBlockVirtualization,
    };

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

