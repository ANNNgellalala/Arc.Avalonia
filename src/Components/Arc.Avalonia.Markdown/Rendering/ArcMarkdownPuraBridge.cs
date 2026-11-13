using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Markdig.Syntax;
using PuraMarkdown.UI;
using PuraMarkdown.UI.Rendering;
using PuraContext = PuraMarkdown.UI.Rendering.RenderContext;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Per-view host state attached to the <see cref="PuraMarkdownOptions"/> instance. A PuraMarkdown
/// <see cref="PuraContext"/> only knows its PuraMarkdownOptions, so the bridge uses that instance as the
/// key back to the Arc view/options/pipeline that produced the render pass.
/// </summary>
internal sealed class ArcMarkdownHostState
{
    /// <summary>Arc options that produced the PuraMarkdown options.</summary>
    internal required ArcMarkdownOptions Options { get; init; }

    /// <summary>Arc pipeline (renderer registry, highlighter, Pura syntax pipeline).</summary>
    internal required ArcMarkdownPipeline Pipeline { get; init; }

    /// <summary>Arc view raising interaction/warning/link events back to the host; may be unset while disposed.</summary>
    internal ArcMarkdownView? View { get; set; }
}

/// <summary>
/// Bridge between the PuraMarkdown.UI render engine and Arc's renderer pipeline: it adapts Arc
/// <see cref="IArcMarkdownNodeRenderer"/> implementations into PuraMarkdown node renderers and builds the
/// Arc <see cref="ArcMarkdownRenderContext"/> for every PuraMarkdown render context, forwarding the event
/// sinks to the owning <see cref="ArcMarkdownView"/> so the public Arc events stay unchanged.
/// </summary>
/// <remarks>
/// All members are UI-thread only (renderers run on the UI thread). Both tables are weak-keyed, so
/// disposed views/options/render contexts are collected without explicit cleanup.
/// </remarks>
internal static class ArcMarkdownPuraBridge
{
    private static readonly ConditionalWeakTable<PuraMarkdownOptions, ArcMarkdownHostState> States = new();
    private static readonly ConditionalWeakTable<PuraContext, ArcMarkdownRenderContext> Contexts = new();

    /// <summary>Returns the host state registered for <paramref name="options"/>, if any.</summary>
    /// <param name="options">PuraMarkdown options created by <see cref="ArcMarkdownPuraOptions.Create"/>.</param>
    internal static ArcMarkdownHostState? GetState(PuraMarkdownOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return States.TryGetValue(options, out var state) ? state : null;
    }

    /// <summary>Registers (or replaces) the host state for <paramref name="options"/>.</summary>
    /// <param name="options">PuraMarkdown options instance handed to the engine.</param>
    /// <param name="state">Arc host state.</param>
    internal static void Register(PuraMarkdownOptions options, ArcMarkdownHostState state)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);

        States.Remove(options);
        States.Add(options, state);
    }

    /// <summary>Gets (and caches) the Arc render context for a PuraMarkdown render context.</summary>
    /// <param name="context">PuraMarkdown render context for the current render pass.</param>
    internal static ArcMarkdownRenderContext ContextFor(PuraContext context) => ContextFor(context, null, null);

    /// <summary>
    /// Gets (and caches) the Arc render context for a PuraMarkdown render context, with a pipeline/registry
    /// fallback for callers rendered without a registered host state (tests, standalone renderers).
    /// </summary>
    /// <param name="context">PuraMarkdown render context for the current render pass.</param>
    /// <param name="pipeline">Arc pipeline owning <paramref name="context"/>'s renderers, when known.</param>
    /// <param name="renderers">Arc renderer registry to fall back to when no host state is registered.</param>
    internal static ArcMarkdownRenderContext ContextFor(
        PuraContext context,
        ArcMarkdownPipeline? pipeline,
        ArcMarkdownRendererRegistry? renderers)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Contexts.GetValue(context, key => BuildContext(key, pipeline, renderers));
    }

    private static ArcMarkdownRenderContext BuildContext(
        PuraContext context,
        ArcMarkdownPipeline? pipeline,
        ArcMarkdownRendererRegistry? renderers)
    {
        var state = GetState(context.Options);
        if (state is not null)
        {
            var view = state.View;
            return new ArcMarkdownRenderContext
            {
                Options = state.Options,
                Renderers = state.Pipeline.Renderers,
                Sanitized = context.Sanitized,
                Dispatcher = context.Dispatcher,
                IsStreaming = context.IsStreaming,
                IsTail = context.IsTail,
                ComponentStates = view?.InteractionStatesSnapshot,
                InteractionSink = view is null ? null : interaction => view.RaiseArcInteraction(interaction),
                WarningSink = view is null ? null : warning => view.RaiseArcWarning(warning),
                LinkClickSink = view is null ? null : click => view.HandleArcLinkClick(click),
                ImageLoadingSink = null,
                ImageLoadedSink = null,
            };
        }

        // No host state (renderer used outside ArcMarkdownView): keep rendering with Arc defaults and no
        // view sinks instead of failing the whole document.
        var fallbackPipeline = pipeline ?? ArcMarkdownPipeline.Default;
        return new ArcMarkdownRenderContext
        {
            Options = new ArcMarkdownOptions { Pipeline = fallbackPipeline },
            Renderers = renderers ?? fallbackPipeline.Renderers,
            Sanitized = context.Sanitized,
            Dispatcher = context.Dispatcher,
            IsStreaming = context.IsStreaming,
            IsTail = context.IsTail,
        };
    }
}

/// <summary>
/// Adapts one Arc node renderer into the PuraMarkdown renderer contract: <see cref="CanRender"/> delegates
/// to the Arc renderer with an Arc render context (failures mean "cannot render", so the PuraMarkdown
/// fallback chain continues), <see cref="Render"/> renders the node with Arc controls/tokens.
/// </summary>
internal sealed class ArcNodeRendererAdapter : IPuraNodeRenderer
{
    private readonly ArcMarkdownRendererRegistry _renderers;
    private readonly IArcMarkdownNodeRenderer _inner;
    private readonly ArcMarkdownPipeline _pipeline;

    /// <summary>Creates the adapter.</summary>
    /// <param name="renderers">Arc renderer registry owning <paramref name="inner"/>.</param>
    /// <param name="inner">Arc renderer to delegate to.</param>
    /// <param name="pipeline">Arc pipeline that produced the renderer (fallback context source).</param>
    internal ArcNodeRendererAdapter(
        ArcMarkdownRendererRegistry renderers,
        IArcMarkdownNodeRenderer inner,
        ArcMarkdownPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pipeline);

        _renderers = renderers;
        _inner = inner;
        _pipeline = pipeline;
    }

    /// <summary>Whether the Arc renderer handles <paramref name="node"/>; never throws.</summary>
    /// <param name="node">Markdig node.</param>
    /// <param name="context">PuraMarkdown render context.</param>
    public bool CanRender(MarkdownObject node, PuraContext context)
    {
        try
        {
            return _inner.CanRender(node, ArcMarkdownPuraBridge.ContextFor(context, _pipeline, _renderers));
        }
        catch (Exception ex)
        {
            // Match ArcMarkdownRendererRegistry.Safe: a broken CanRender must not fail the render pass.
            ArcMarkdownPuraBridge.ContextFor(context, _pipeline, _renderers)
                .Warn("ARC-MD-RENDER-000", $"渲染器 {_inner.GetType().Name} 的 CanRender 抛异常：{ex.Message}", node.GetType().Name, _inner.GetType().Name);
            return false;
        }
    }

    /// <summary>Renders <paramref name="node"/> through the Arc renderer.</summary>
    /// <param name="node">Markdig node.</param>
    /// <param name="context">PuraMarkdown render context.</param>
    public Control Render(MarkdownObject node, PuraContext context)
        => _inner.Render(node, ArcMarkdownPuraBridge.ContextFor(context, _pipeline, _renderers));
}
