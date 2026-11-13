using PuraMarkdown.UI;
using PuraMarkdown.UI.Engine;
using PuraMarkdown.UI.Localization;
using PuraMarkdown.UI.Platform;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Builds the <see cref="PuraMarkdownOptions"/> that lets <see cref="PuraMarkdownView"/> run Arc's own
/// renderer pipeline: theme tokens come from <see cref="ArcMarkdownStyle"/>, platform services are
/// forwarded to the Arc interfaces hosts already install, and the view/pipeline pair is registered with
/// <see cref="ArcMarkdownPuraBridge"/> so Arc renderers see an <see cref="ArcMarkdownRenderContext"/>.
/// </summary>
internal static class ArcMarkdownPuraOptions
{
    /// <summary>Creates and registers the PuraMarkdown options for one Arc view.</summary>
    /// <param name="arc">Arc options of the owning view.</param>
    /// <param name="pipeline">Arc pipeline of the owning view.</param>
    /// <param name="state">Host state linking the resulting options back to the Arc view.</param>
    /// <returns>Options for <see cref="PuraMarkdownView.Options"/>.</returns>
    internal static PuraMarkdownOptions Create(
        ArcMarkdownOptions arc,
        ArcMarkdownPipeline pipeline,
        ArcMarkdownHostState state)
    {
        ArgumentNullException.ThrowIfNull(arc);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(state);

        var puraPipeline = pipeline.PuraPipeline;
        if (puraPipeline is null)
        {
            throw new InvalidOperationException(
                "ArcMarkdownPipeline.PuraPipeline 尚未初始化；请通过 ArcMarkdownPipelineBuilder.Build() 构建管道。");
        }

        var options = new PuraMarkdownOptions
        {
            Pipeline = puraPipeline,
            Theme = ArcMarkdownPuraTheme.Build(ArcMarkdownStyle.Current),
            Clipboard = new ArcMarkdownPuraClipboardAdapter(arc.Clipboard ?? ArcMarkdownPlatform.DefaultClipboard),
            LinkHandler = CreateLinkHandler(arc.LinkHandler),
            // Arc renders images itself through its own renderers (ArcMarkdownOptions.ImageLoader); the
            // PuraMarkdown image path is only a fallback, so keep it offline-safe.
            ImageLoader = NullImageLoader.Instance,
            Localizer = new ArcMarkdownPuraLocalizerAdapter(arc.Localizer ?? ArcMarkdownLocalizer.Default),
            Limits = arc.Limits,
            UrlPolicy = arc.UrlPolicy,
            InteractionEnabled = arc.InteractionEnabled,
            EnableTextSelection = arc.EnableTextSelection,
            // ArcMarkdownView keeps its own outer ScrollViewer/template; nested Pura virtualization would
            // wrap the document in a second scroll viewer.
            BlockVirtualization = BlockVirtualizationMode.Never,
            RenderOptions = new PuraRenderOptions { Mode = PuraRenderMode.Auto },
        };

        ArcMarkdownPuraBridge.Register(options, state);
        return options;
    }

    private static ILinkHandler CreateLinkHandler(IArcMarkdownLinkHandler? linkHandler)
    {
        // The Arc default is a no-op whose real behaviour (open in browser / copy) lives in the link
        // context menu; in that case keep the PuraMarkdown no-op default instead of wrapping another no-op.
        if (linkHandler is null || ReferenceEquals(linkHandler, ArcMarkdownPlatform.DefaultLinkHandler))
        {
            return NullLinkHandler.Instance;
        }

        return new ArcMarkdownPuraLinkHandlerAdapter(linkHandler);
    }
}

/// <summary>Forwards <see cref="IPuraClipboard"/> copy actions to the Arc clipboard.</summary>
internal sealed class ArcMarkdownPuraClipboardAdapter(IArcMarkdownClipboard inner) : IPuraClipboard
{
    /// <inheritdoc />
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        => inner.SetTextAsync(text, cancellationToken);
}

/// <summary>Forwards <see cref="ILinkHandler"/> link opens to the Arc link handler.</summary>
internal sealed class ArcMarkdownPuraLinkHandlerAdapter(IArcMarkdownLinkHandler inner) : ILinkHandler
{
    /// <inheritdoc />
    public Task OpenAsync(string url, CancellationToken cancellationToken = default)
        => inner.OpenAsync(url, cancellationToken);
}

/// <summary>Forwards <see cref="IPuraLocalizer"/> lookups to the Arc localizer (keys pass through).</summary>
internal sealed class ArcMarkdownPuraLocalizerAdapter(IArcMarkdownLocalizer inner) : IPuraLocalizer
{
    /// <inheritdoc />
    public string Get(string key, params object?[] args)
        => args.Length == 0 ? inner.Get(key) : inner.Get(key, args);
}
