using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Markdig.Syntax;
using PuraMarkdown.Security;
using PuraMarkdown.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Renders one Markdig AST node into an Avalonia control. Implementations are called on the UI thread
/// only; <see cref="CanRender"/> must be cheap, deterministic and free of side effects.
/// </summary>
public interface IArcMarkdownNodeRenderer
{
    /// <summary>Whether this renderer handles the node.</summary>
    bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context);

    /// <summary>Renders the node. Throwing degrades that block to a warning instead of failing the view.</summary>
    Control Render(MarkdownObject node, ArcMarkdownRenderContext context);
}

/// <summary>
/// Node renderer that appends into an <see cref="InlineCollection"/> instead of producing a control.
/// Used for emphasis, inline code, links, images and other CommonMark inlines.
/// </summary>
public interface IArcMarkdownInlineRenderer : IArcMarkdownNodeRenderer
{
    /// <summary>Appends the inline (and its children) to <paramref name="target"/>.</summary>
    void RenderInline(MarkdownObject node, InlineCollection target, ArcMarkdownRenderContext context);
}

/// <summary>
/// Optional incremental renderer: when the block type is unchanged but its text grew (the common
/// streaming case), the existing control can be updated in place instead of rebuilt.
/// </summary>
public interface IArcMarkdownIncrementalRenderer : IArcMarkdownNodeRenderer
{
    /// <summary>Whether the existing control can be updated for the new node.</summary>
    bool CanUpdate(MarkdownObject oldNode, MarkdownObject newNode, ArcMarkdownRenderContext context);

    /// <summary>Updates an existing control in place.</summary>
    void Update(Control existing, MarkdownObject newNode, ArcMarkdownRenderContext context);
}

/// <summary>
/// Takes over a fenced code block by language (agent components, diagrams, math, custom blocks). A
/// renderer that throws or returns null lets the default code block render instead.
/// </summary>
public interface IArcMarkdownFenceRenderer
{
    /// <summary>Unique diagnostic name.</summary>
    string Name { get; }

    /// <summary>Whether the fence with this language and body should be rendered as a component.</summary>
    bool CanRender(string language, string source, bool isClosed, ArcMarkdownRenderContext context);

    /// <summary>Renders the fence.</summary>
    Control Render(string language, string source, bool isClosed, ArcMarkdownRenderContext context);
}

/// <summary>Token kind produced by <see cref="IArcMarkdownCodeHighlighter"/>; mapped to semantic tokens.</summary>
public enum ArcMarkdownTokenKind
{
    Plain,
    Comment,
    Keyword,
    String,
    Number,
    Type,
    Function,
    Variable,
    Operator,
    Punctuation,
    Tag,
    Attribute,
    Addition,
    Deletion,
}

/// <summary>A highlighted range inside a code block.</summary>
public readonly record struct ArcMarkdownHighlightSpan(int Start, int Length, ArcMarkdownTokenKind Kind = ArcMarkdownTokenKind.Plain);

/// <summary>Pluggable, language-aware code colouring. The built-in implementation is a small tokenizer.</summary>
public interface IArcMarkdownCodeHighlighter
{
    /// <summary>Whether the language can be coloured; unknown languages fall back to plain text.</summary>
    bool CanHighlight(string? language);

    /// <summary>Returns the highlighted ranges, ordered by start offset.</summary>
    IReadOnlyList<ArcMarkdownHighlightSpan> Highlight(string code, string? language);
}

/// <summary>A Markdown rendering plugin: registers renderers, fence handlers and highlighters at build time.</summary>
public interface IArcMarkdownPlugin
{
    /// <summary>Unique plugin name (build-time duplicate detection).</summary>
    string Name { get; }

    /// <summary>Semantic version.</summary>
    string Version { get; }

    /// <summary>Registers capabilities.</summary>
    void Configure(ArcMarkdownPipelineBuilder builder);
}

/// <summary>Loads images referenced by Markdown. The default implementation refuses network access.</summary>
public interface IArcMarkdownImageLoader
{
    /// <summary>Loads one image.</summary>
    Task<ArcMarkdownImageResult> LoadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Result of an image load: a bitmap, a deliberate refusal, or a failure reason.</summary>
public sealed record ArcMarkdownImageResult(bool Success, Bitmap? Image, string? Reason)
{
    /// <summary>Successful result carrying a bitmap.</summary>
    public static ArcMarkdownImageResult Loaded(Bitmap image) => new(true, image, null);

    /// <summary>Refused by policy (offline default, scheme not allowed, ...).</summary>
    public static ArcMarkdownImageResult Refused(string reason) => new(false, null, reason);

    /// <summary>Load failure (download/decoder error).</summary>
    public static ArcMarkdownImageResult Failed(string reason) => new(false, null, reason);
}

/// <summary>Opens links from Markdown. Hosts install a real implementation; the default does nothing.</summary>
public interface IArcMarkdownLinkHandler
{
    /// <summary>Opens <paramref name="url"/>.</summary>
    Task OpenAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Clipboard access for copy actions. The default no-ops when no top level is available.</summary>
public interface IArcMarkdownClipboard
{
    /// <summary>Writes plain text.</summary>
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>Localized UI copy (copy code, image placeholder, unsupported node, ...).</summary>
public interface IArcMarkdownLocalizer
{
    /// <summary>Returns the copy for <paramref name="key"/>, formatting optional arguments.</summary>
    string Get(string key, params object?[] arguments);
}

/// <summary>Stable localization keys used by the Markdown controls.</summary>
public static class ArcMarkdownStringKeys
{
    public const string Copy = "markdown.copy";
    public const string Copied = "markdown.copied";
    public const string CopyFailed = "markdown.copyFailed";
    public const string CopyCode = "markdown.copyCode";
    public const string CodeLanguageFallback = "markdown.codeLanguage";
    public const string CodeTruncated = "markdown.codeTruncated";
    public const string Image = "markdown.image";
    public const string ImageLoading = "markdown.imageLoading";
    public const string ImageBlocked = "markdown.imageBlocked";
    public const string ImageLoadFailed = "markdown.imageLoadFailed";
    public const string UnsupportedNode = "markdown.unsupportedNode";
    public const string EmptyDocument = "markdown.empty";
    public const string AgentComponentError = "markdown.agentComponentError";
    public const string AgentUnsupported = "markdown.agentUnsupported";
    public const string AgentChoice = "markdown.agentChoice";
    public const string AgentConfirm = "markdown.agentConfirm";
    public const string Submit = "markdown.submit";
    public const string Cancel = "markdown.cancel";
    public const string Confirm = "markdown.confirm";
}
