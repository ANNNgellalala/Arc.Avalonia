using Avalonia.Controls;
using PuraMarkdown.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Renders an already parsed <see cref="PuraDocument"/> (for hosts that parse once and reuse the AST,
/// or that want to feed syntax diagnostics into their own toolbars).
/// </summary>
public class ArcMarkdownDocumentView : ContentControl
{
    private readonly ArcMarkdownView _inner = new();

    /// <summary>Creates the view.</summary>
    public ArcMarkdownDocumentView()
    {
        Content = _inner;
        _inner.LinkClicked += (_, args) => LinkClicked?.Invoke(this, args);
        _inner.Warning += (_, args) => Warning?.Invoke(this, args);
        _inner.InteractionOccurred += (_, args) => InteractionOccurred?.Invoke(this, args);
        _inner.Announced += (_, text) => Announced?.Invoke(this, text);
    }

    /// <summary>Syntax document to render; setting it re-renders without parsing.</summary>
    public PuraDocument? Document
    {
        get => _document;
        set
        {
            _document = value;
            if (value is not null)
            {
                _inner.RenderDocument(value);
            }
        }
    }

    /// <summary>Rendering options.</summary>
    public ArcMarkdownOptions? Options
    {
        get => _inner.Options;
        set => _inner.Options = value;
    }

    /// <summary>Link click (routed through the inner view).</summary>
    public event EventHandler<ArcMarkdownLinkClickedEventArgs>? LinkClicked;

    /// <summary>Renderer warning.</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs>? Warning;

    /// <summary>Agent-component interaction.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Accessibility announcement.</summary>
    public event EventHandler<string>? Announced;

    /// <summary>Outline of the rendered document.</summary>
    public PuraOutline? Outline => _inner.Outline;

    /// <summary>Plain text of the rendered document.</summary>
    public string GetPlainText() => _inner.GetPlainText();

    /// <summary>Re-renders the current document.</summary>
    public void Render() => _inner.Render();

    /// <summary>Scrolls a heading anchor into view.</summary>
    public bool ScrollToAnchor(string anchor) => _inner.ScrollToAnchor(anchor);

    private PuraDocument? _document;
}
