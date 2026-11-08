using Avalonia;
using Avalonia.Controls;
using Markdig.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Renders indented and fenced code through <see cref="ArcMarkdownCodeBlock"/>. Fence info is parsed
/// into a language, oversized fences are truncated with a warning, and the unstable streaming tail
/// skips token colouring so a fast token stream does not re-colour on every frame.
/// </summary>
internal sealed class CodeBlockRenderer : IArcMarkdownIncrementalRenderer
{
    /// <summary>Maximum number of lines before <see cref="ArcMarkdownCodeBlock"/> collapses the block.</summary>
    private const int CollapseLineCount = 40;

    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is CodeBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var codeBlock = (CodeBlock)node;
        var style = context.Style;
        var fullSource = ArcMarkdownDefaults.GetLeafText(codeBlock).TrimEnd('\n');
        var source = Truncate(fullSource, codeBlock, context);
        var lineCount = CountLines(source);
        return new ArcMarkdownCodeBlock
        {
            Code = source,
            OriginalCode = fullSource,
            Language = GetLanguage(codeBlock),
            Highlighter = context.IsTail
                ? PlainCodeHighlighter.Instance
                : context.Options.Pipeline.Highlighter,
            Localizer = context.Options.Localizer,
            ShowLineNumbers = !context.IsTail && lineCount > 3,
            MaxLines = CollapseLineCount,
            Margin = new Thickness(0, 0, 0, style.CodeBlockSpacing),
        };
    }

    public bool CanUpdate(MarkdownObject oldNode, MarkdownObject newNode, ArcMarkdownRenderContext context)
        => oldNode is CodeBlock oldCode
           && newNode is CodeBlock newCode
           && string.Equals(GetLanguage(oldCode), GetLanguage(newCode), StringComparison.OrdinalIgnoreCase);

    public void Update(Control existing, MarkdownObject newNode, ArcMarkdownRenderContext context)
    {
        if (existing is not ArcMarkdownCodeBlock block || newNode is not CodeBlock codeBlock)
        {
            return;
        }

        var fullSource = ArcMarkdownDefaults.GetLeafText(codeBlock).TrimEnd('\n');
        var source = Truncate(fullSource, codeBlock, context);
        var highlighter = context.IsTail
            ? PlainCodeHighlighter.Instance
            : context.Options.Pipeline.Highlighter;
        var language = GetLanguage(codeBlock);
        var showLineNumbers = !context.IsTail && CountLines(source) > 3;

        // Each property change re-runs the control template, so only assign actual differences.
        if (!ReferenceEquals(block.Highlighter, highlighter))
        {
            block.Highlighter = highlighter;
        }

        if (!string.Equals(block.Language, language, StringComparison.Ordinal))
        {
            block.Language = language;
        }

        if (block.ShowLineNumbers != showLineNumbers)
        {
            block.ShowLineNumbers = showLineNumbers;
        }

        if (!string.Equals(block.Code, source, StringComparison.Ordinal))
        {
            block.Code = source;
        }

        if (!string.Equals(block.OriginalCode, fullSource, StringComparison.Ordinal))
        {
            block.OriginalCode = fullSource;
        }
    }

    /// <summary>Extracts the fence language from a fenced code block, or null for indented/plain code.</summary>
    internal static string? GetLanguage(CodeBlock codeBlock)
    {
        if (codeBlock is not FencedCodeBlock fenced || string.IsNullOrWhiteSpace(fenced.Info))
        {
            return null;
        }

        var info = fenced.Info.Trim();
        var space = info.IndexOf(' ');
        var language = space < 0 ? info : info[..space];
        return string.IsNullOrWhiteSpace(language) ? null : language;
    }

    private static string Truncate(string source, CodeBlock codeBlock, ArcMarkdownRenderContext context)
    {
        var maxFence = context.Options.Limits.MaxFenceChars;
        if (maxFence <= 0 || source.Length <= maxFence)
        {
            return source;
        }

        context.Warn(
            "ARC-MD-LIM-002",
            $"代码围栏超过 {maxFence} 字符，已截断显示",
            codeBlock.GetType().Name);
        return source[..maxFence] + "\n" + context.Options.Localizer.Get(ArcMarkdownStringKeys.CodeTruncated);
    }

    private static int CountLines(string source)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        var count = 1;
        foreach (var ch in source)
        {
            if (ch == '\n')
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Highlighter used on the streaming tail: never colours, so the control renders one plain run.</summary>
    private sealed class PlainCodeHighlighter : IArcMarkdownCodeHighlighter
    {
        internal static PlainCodeHighlighter Instance { get; } = new();

        public bool CanHighlight(string? language) => false;

        public IReadOnlyList<ArcMarkdownHighlightSpan> Highlight(string code, string? language)
            => Array.Empty<ArcMarkdownHighlightSpan>();
    }
}

/// <summary>
/// Routes fenced blocks to registered <see cref="IArcMarkdownFenceRenderer"/> plugins (agent
/// components, diagrams, math). A plugin that cannot render, returns null or throws lets the plain
/// code block render instead, so an unregistered or broken plugin never crashes the view.
/// </summary>
internal sealed class FenceDispatchRenderer : IArcMarkdownNodeRenderer
{
    private readonly IReadOnlyList<IArcMarkdownFenceRenderer> _renderers;

    public FenceDispatchRenderer(IReadOnlyList<IArcMarkdownFenceRenderer> renderers) => _renderers = renderers;

    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        if (node is not FencedCodeBlock fenced || _renderers.Count == 0)
        {
            return false;
        }

        var language = CodeBlockRenderer.GetLanguage(fenced);
        if (string.IsNullOrEmpty(language))
        {
            return false;
        }

        var source = ArcMarkdownDefaults.GetLeafText(fenced);
        var isClosed = fenced.ClosingFencedCharCount > 0;
        foreach (var renderer in _renderers)
        {
            if (SafeCanRender(renderer, language, source, isClosed, context))
            {
                return true;
            }
        }

        return false;
    }

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var fenced = (FencedCodeBlock)node;
        var language = CodeBlockRenderer.GetLanguage(fenced) ?? string.Empty;
        var source = ArcMarkdownDefaults.GetLeafText(fenced);
        var isClosed = fenced.ClosingFencedCharCount > 0;

        foreach (var renderer in _renderers)
        {
            if (!SafeCanRender(renderer, language, source, isClosed, context))
            {
                continue;
            }

            try
            {
                var control = renderer.Render(language, source, isClosed, context);
                if (control is not null)
                {
                    return control;
                }
            }
            catch (Exception ex)
            {
                context.Warn(
                    "ARC-MD-FENCE-001",
                    $"围栏渲染失败，已回退代码块：{ex.Message}",
                    nameof(FencedCodeBlock),
                    renderer.Name);
            }
        }

        return new CodeBlockRenderer().Render(node, context);
    }

    private static bool SafeCanRender(
        IArcMarkdownFenceRenderer renderer,
        string language,
        string source,
        bool isClosed,
        ArcMarkdownRenderContext context)
    {
        try
        {
            return renderer.CanRender(language, source, isClosed, context);
        }
        catch
        {
            return false;
        }
    }
}

