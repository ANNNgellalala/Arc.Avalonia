using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PuraMarkdown.Core.Security;
using PuraMarkdown.Core;

namespace Arc.Avalonia.Markdown;

/// <summary>Configured rendering environment shared by every <see cref="ArcMarkdownView"/> instance.</summary>
public sealed class ArcMarkdownOptions
{
    /// <summary>Parser, sanitizer and renderer registry. Defaults to CommonMark + GFM.</summary>
    public ArcMarkdownPipeline Pipeline { get; init; } = ArcMarkdownPipeline.Default;

    /// <summary>Image loader used for <c>![](url)</c>; defaults to an offline refusal.</summary>
    public IArcMarkdownImageLoader ImageLoader { get; init; } = ArcMarkdownPlatform.DefaultImageLoader;

    /// <summary>
    /// Opens links. The "open in browser" row of the link context menu delegates here when a host installs
    /// one (in-app navigation, deep links, allow-lists); with the default no-op handler the menu falls back to
    /// <see cref="ArcMarkdownPlatform.OpenInBrowserAsync"/> (system browser/mail client).
    /// </summary>
    public IArcMarkdownLinkHandler LinkHandler { get; init; } = ArcMarkdownPlatform.DefaultLinkHandler;

    /// <summary>Clipboard used by copy-code and copy-all actions.</summary>
    public IArcMarkdownClipboard Clipboard { get; init; } = ArcMarkdownPlatform.DefaultClipboard;

    /// <summary>Localized UI copy; Chinese by default to match the gallery.</summary>
    public IArcMarkdownLocalizer Localizer { get; init; } = ArcMarkdownLocalizer.Default;

    /// <summary>Size guards applied per parse. The pipeline sanitizer also runs with these.</summary>
    public PuraLimits Limits { get; init; } = new();

    /// <summary>URL policy for links and images.</summary>
    public IUrlPolicy UrlPolicy { get; init; } = DefaultUrlPolicy.Instance;

    /// <summary>Whether agent components may report interactions (read-only when false).</summary>
    public bool InteractionEnabled { get; init; } = true;

    /// <summary>Selectable text blocks instead of plain text blocks (selection + Ctrl+C).</summary>
    public bool EnableTextSelection { get; init; }

    /// <summary>Default options instance.</summary>
    public static ArcMarkdownOptions Default { get; } = new();
}

/// <summary>
/// Frozen Markdown environment: syntax pipeline, renderer registry and code highlighter.
/// Build once at startup with <see cref="ArcMarkdownPipelineBuilder"/> and reuse.
/// </summary>
public sealed class ArcMarkdownPipeline
{
    internal ArcMarkdownPipeline(
        PuraSyntaxPipeline syntax,
        ArcMarkdownRendererRegistry renderers,
        IArcMarkdownCodeHighlighter highlighter,
        IReadOnlyList<string> pluginNames)
    {
        Syntax = syntax;
        Renderers = renderers;
        Highlighter = highlighter;
        PluginNames = pluginNames;
    }

    /// <summary>PuraMarkdown syntax pipeline (Markdig + sanitizer + diagnostics + outline).</summary>
    public PuraSyntaxPipeline Syntax { get; }

    /// <summary>Renderer registry used for blocks, inlines and fences.</summary>
    public ArcMarkdownRendererRegistry Renderers { get; }

    /// <summary>Code highlighter used by the code block renderer.</summary>
    public IArcMarkdownCodeHighlighter Highlighter { get; }

    /// <summary>Names of registered plugins, in registration order.</summary>
    public IReadOnlyList<string> PluginNames { get; }

    /// <summary>The default CommonMark + GFM pipeline with every built-in Arc renderer.</summary>
    public static ArcMarkdownPipeline Default { get; } = new ArcMarkdownPipelineBuilder()
        .UseCommonMark()
        .UseGfm()
        .Build();
}

/// <summary>Renderer registry: priority-ordered candidates for a node, plus fence dispatch.</summary>
public sealed class ArcMarkdownRendererRegistry
{
    private readonly List<(IArcMarkdownNodeRenderer Renderer, int Priority)> _nodes;
    private readonly List<(IArcMarkdownFenceRenderer Renderer, int Priority)> _fences;
    private readonly IArcMarkdownNodeRenderer _fallback;

    internal ArcMarkdownRendererRegistry(
        IEnumerable<(IArcMarkdownNodeRenderer Renderer, int Priority)> nodes,
        IEnumerable<(IArcMarkdownFenceRenderer Renderer, int Priority)> fences,
        IArcMarkdownNodeRenderer fallback)
    {
        _fallback = fallback;
        _nodes = nodes.OrderByDescending(entry => entry.Priority).ToList();
        _fences = fences.OrderByDescending(entry => entry.Priority).ToList();
    }

    /// <summary>Renderers applicable to <paramref name="node"/>, highest priority first (capped).</summary>
    public IEnumerable<IArcMarkdownNodeRenderer> ResolveCandidates(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var emitted = 0;
        foreach (var entry in _nodes)
        {
            if (emitted >= 8)
            {
                yield break;
            }

            if (Safe(entry.Renderer, node, context))
            {
                emitted++;
                yield return entry.Renderer;
            }
        }

        yield return _fallback;
    }

    /// <summary>Fence renderers, highest priority first.</summary>
    public IReadOnlyList<IArcMarkdownFenceRenderer> Fences => _fences.Select(entry => entry.Renderer).ToArray();

    /// <summary>Fallback renderer for unsupported or unknown nodes.</summary>
    public IArcMarkdownNodeRenderer Fallback => _fallback;

    private static bool Safe(IArcMarkdownNodeRenderer renderer, MarkdownObject node, ArcMarkdownRenderContext context)
    {
        try
        {
            return renderer.CanRender(node, context);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Builds an immutable <see cref="ArcMarkdownPipeline"/>; use at startup only.</summary>
public sealed class ArcMarkdownPipelineBuilder
{
    private readonly PuraSyntaxBuilder _syntax = new();
    private readonly List<(IArcMarkdownNodeRenderer Renderer, int Priority)> _nodes = [];
    private readonly List<(IArcMarkdownFenceRenderer Renderer, int Priority)> _fences = [];
    private readonly List<IArcMarkdownPlugin> _plugins = [];
    private readonly HashSet<string> _pluginNames = new(StringComparer.OrdinalIgnoreCase);
    private IArcMarkdownCodeHighlighter _highlighter = ArcMarkdownHighlighter.Default;

    /// <summary>CommonMark baseline with precise source locations.</summary>
    public ArcMarkdownPipelineBuilder UseCommonMark()
    {
        _syntax.UseCommonMark();
        return this;
    }

    /// <summary>GFM tables, task lists, strikethrough and autolinks.</summary>
    public ArcMarkdownPipelineBuilder UseGfm()
    {
        _syntax.UseGfm();
        // GFM's autolinker only fires at a line start / after whitespace, so Chinese prose such as
        // 下载页：https://… keeps the URL as plain text. The extra parser accepts CJK punctuation
        // boundaries and emits the same AutolinkInline nodes; hosts no longer need to pre-process URLs.
        _syntax.UseBareUrlAutolinks();
        return this;
    }

    /// <summary>Adds a raw Markdig extension (advanced).</summary>
    public ArcMarkdownPipelineBuilder UseMarkdownExtension(IMarkdownExtension extension)
    {
        _syntax.UseMarkdownExtension(extension);
        return this;
    }

    /// <summary>Replaces the AST sanitizer.</summary>
    public ArcMarkdownPipelineBuilder UseSanitizer(IAstSanitizer sanitizer)
    {
        _syntax.UseSanitizer(sanitizer);
        return this;
    }

    /// <summary>Replaces the URL policy.</summary>
    public ArcMarkdownPipelineBuilder UseUrlPolicy(IUrlPolicy urlPolicy)
    {
        _syntax.UseUrlPolicy(urlPolicy);
        return this;
    }

    /// <summary>Configures default limits.</summary>
    public ArcMarkdownPipelineBuilder ConfigureLimits(Action<PuraLimits> configure)
    {
        _syntax.ConfigureLimits(configure);
        return this;
    }

    /// <summary>Registers a block/inline renderer.</summary>
    public ArcMarkdownPipelineBuilder UseRenderer(IArcMarkdownNodeRenderer renderer, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _nodes.Add((renderer, priority));
        return this;
    }

    /// <summary>Registers an inline renderer.</summary>
    public ArcMarkdownPipelineBuilder UseInlineRenderer(IArcMarkdownInlineRenderer renderer, int priority = 100)
        => UseRenderer(renderer, priority);

    /// <summary>Registers a fence renderer (agent components, diagrams, math).</summary>
    public ArcMarkdownPipelineBuilder UseFenceRenderer(IArcMarkdownFenceRenderer renderer, int priority = 100)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _fences.Add((renderer, priority));
        return this;
    }

    /// <summary>Replaces the code highlighter.</summary>
    public ArcMarkdownPipelineBuilder UseCodeHighlighter(IArcMarkdownCodeHighlighter highlighter)
    {
        ArgumentNullException.ThrowIfNull(highlighter);
        _highlighter = highlighter;
        return this;
    }

    /// <summary>Registers a plugin; duplicate names fail fast.</summary>
    public ArcMarkdownPipelineBuilder UsePlugin(IArcMarkdownPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrWhiteSpace(plugin.Name))
        {
            throw new InvalidOperationException($"插件 {plugin.GetType().Name} 的 Name 不能为空");
        }

        if (!_pluginNames.Add(plugin.Name))
        {
            throw new InvalidOperationException($"插件重复注册：{plugin.Name}");
        }

        _plugins.Add(plugin);
        plugin.Configure(this);
        return this;
    }

    /// <summary>Builds the immutable pipeline. Built-in renderers are added at priority 0.</summary>
    public ArcMarkdownPipeline Build()
    {
        var nodes = new List<(IArcMarkdownNodeRenderer, int)>(_nodes);
        var fences = new List<(IArcMarkdownFenceRenderer, int)>(_fences);
        var fallback = ArcMarkdownDefaults.Register(nodes, fences);
        return new ArcMarkdownPipeline(
            _syntax.Build(),
            new ArcMarkdownRendererRegistry(nodes, fences, fallback),
            _highlighter,
            _plugins.Select(plugin => plugin.Name).ToArray());
    }
}

