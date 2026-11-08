using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PuraMarkdown.Security;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Read-only services handed to every renderer: options, registry, sanitizer result, dispatcher,
/// event sinks and the recursive render helpers. One instance is created per render pass.
/// </summary>
public sealed class ArcMarkdownRenderContext
{
    /// <summary>Rendering options.</summary>
    public required ArcMarkdownOptions Options { get; init; }

    /// <summary>Renderer registry.</summary>
    public required ArcMarkdownRendererRegistry Renderers { get; init; }

    /// <summary>AST sanitizer result for the document being rendered.</summary>
    public required SanitizeResult Sanitized { get; init; }

    /// <summary>UI dispatcher (image loads post results back through it).</summary>
    public required IDispatcher Dispatcher { get; init; }

    /// <summary>True while a streaming session is appending.</summary>
    public bool IsStreaming { get; init; }

    /// <summary>True for the unstable tail window (avoid expensive highlighting/diagrams).</summary>
    public bool IsTail { get; init; }

    /// <summary>Persisted interaction state, by component id, restored after re-render/virtualization.</summary>
    public IReadOnlyDictionary<string, ArcMarkdownInteraction>? ComponentStates { get; init; }

    /// <summary>Receives agent-component interactions.</summary>
    public Action<ArcMarkdownInteraction>? InteractionSink { get; init; }

    /// <summary>Receives renderer warnings.</summary>
    public Action<ArcMarkdownWarning>? WarningSink { get; init; }

    /// <summary>Receives link clicks (host may set <see cref="ArcMarkdownLinkClick.Handled"/>).</summary>
    public Action<ArcMarkdownLinkClick>? LinkClickSink { get; init; }

    /// <summary>Receives image-loading notifications.</summary>
    public Action<string>? ImageLoadingSink { get; init; }

    /// <summary>Receives image-load results.</summary>
    public Action<string, ArcMarkdownImageResult>? ImageLoadedSink { get; init; }

    /// <summary>Resolved Arc tokens used by built-in renderers.</summary>
    public ArcMarkdownStyle Style { get; } = ArcMarkdownStyle.Current;

    /// <summary>Renders a block through the registry, degrading a failing renderer to a warning control.</summary>
    public Control RenderBlock(MarkdownObject node)
    {
        foreach (var renderer in Renderers.ResolveCandidates(node, this))
        {
            try
            {
                return renderer.Render(node, this);
            }
            catch (Exception ex)
            {
                Warn("ARC-MD-RENDER-010", $"渲染器 {renderer.GetType().Name} 渲染失败：{ex.Message}", node.GetType().Name, renderer.GetType().Name);
            }
        }

        return ArcMarkdownDefaults.Unsupported(this, node.GetType().Name);
    }

    /// <summary>Renders all children of a container block.</summary>
    public IReadOnlyList<Control> RenderChildren(ContainerBlock container)
    {
        var controls = new List<Control>(container.Count);
        foreach (var child in container)
        {
            controls.Add(RenderBlock(child));
        }

        return controls;
    }

    /// <summary>Renders Markdig inlines into an inline collection.</summary>
    public void RenderInlineChildren(ContainerInline? container, InlineCollection target)
    {
        if (container is null)
        {
            return;
        }

        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            ArcMarkdownDefaultInlineRenderer.RenderOne(inline, target, this);
        }
    }

    /// <summary>Reports a renderer warning.</summary>
    public void Warn(string code, string message, string? nodeType = null, string? renderer = null)
        => WarningSink?.Invoke(new ArcMarkdownWarning(code, message, nodeType, renderer));

    /// <summary>Creates a text block honouring the selection option.</summary>
    public TextBlock CreateTextBlock()
        => Options.EnableTextSelection ? new SelectableTextBlock() : new TextBlock();
}

/// <summary>A renderer warning surfaced to the host.</summary>
public sealed record ArcMarkdownWarning(string Code, string Message, string? NodeType = null, string? Renderer = null);

/// <summary>A link click; set <see cref="Handled"/> to stop the default handler.</summary>
public sealed class ArcMarkdownLinkClick(string url, string text)
{
    /// <summary>Link target.</summary>
    public string Url { get; } = url;

    /// <summary>Visible link text.</summary>
    public string Text { get; } = text;

    /// <summary>When true the view raises the event but does not open the link.</summary>
    public bool Handled { get; set; }
}

/// <summary>One agent-component interaction, persistable by <see cref="ArcMarkdownView.InteractionStates"/>.</summary>
public sealed record ArcMarkdownInteraction(
    string ComponentType,
    string ComponentId,
    IReadOnlyList<string> SelectedIds,
    bool Confirmed,
    string? Value = null)
{
    /// <summary>Raw JSON payload (component specific).</summary>
    public string? Raw { get; init; }
}
