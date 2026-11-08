using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Registers the <c>pura-agent</c> fence component in an Arc Markdown pipeline. Hosts opt in with
/// <c>builder.UsePlugin(new ArcAgentMarkdownPlugin())</c> (or the <see cref="ArcAgentMarkdown.UseAgentComponents"/>
/// shorthand); without it the fence keeps rendering as a plain code block, which is the documented fallback.
/// Only the <c>pura-agent</c> info string is taken over; every other fence stays with the package defaults.
/// </summary>
public sealed class ArcAgentMarkdownPlugin : IArcMarkdownPlugin
{
    /// <summary>Plugin name used for build-time duplicate detection.</summary>
    public string Name => "arc.agent";

    /// <summary>Semantic version of the plugin surface.</summary>
    public string Version => "1.0.0";

    /// <summary>Component types this plugin can render, for host capability discovery.</summary>
    public IReadOnlyList<string> ComponentTypes { get; } = new[] { "choice", "confirm" };

    /// <summary>Registers the fence renderer. The 200 priority lets hosts override the component later.</summary>
    /// <param name="builder">Pipeline builder being configured.</param>
    public void Configure(ArcMarkdownPipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseFenceRenderer(new ArcAgentFenceRenderer(), priority: 200);
    }
}

/// <summary>Convenience entry points so hosts do not have to repeat the plugin wiring.</summary>
public static class ArcAgentMarkdown
{
    private static readonly Lazy<ArcMarkdownPipeline> DefaultPipelineHolder = new(CreatePipeline);

    /// <summary>CommonMark + GFM pipeline with the <c>pura-agent</c> fence renderer registered.</summary>
    public static ArcMarkdownPipeline DefaultPipeline => DefaultPipelineHolder.Value;

    /// <summary>
    /// Default options for agent message bodies: <see cref="DefaultPipeline"/>, selectable text and
    /// interaction enabled. Hosts may derive their own options from it.
    /// </summary>
    public static ArcMarkdownOptions DefaultOptions { get; } = new()
    {
        Pipeline = DefaultPipeline,
        EnableTextSelection = true,
        InteractionEnabled = true,
    };

    /// <summary>Adds the <c>pura-agent</c> component renderer to a pipeline builder.</summary>
    /// <param name="builder">Pipeline builder being configured.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static ArcMarkdownPipelineBuilder UseAgentComponents(this ArcMarkdownPipelineBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new ArcAgentMarkdownPlugin());
    }

    private static ArcMarkdownPipeline CreatePipeline() => new ArcMarkdownPipelineBuilder()
        .UseCommonMark()
        .UseGfm()
        .UsePlugin(new ArcAgentMarkdownPlugin())
        .Build();
}

/// <summary>
/// Takes over the <c>pura-agent</c> fence: parses the JSON payload and renders an Arc choice/confirm card, or
/// the matching loading/error/unsupported degradation card. Interaction state is restored from
/// <see cref="ArcMarkdownRenderContext.ComponentStates"/> and reported through
/// <see cref="ArcMarkdownRenderContext.InteractionSink"/> as <see cref="ArcMarkdownInteraction"/>.
/// </summary>
internal sealed class ArcAgentFenceRenderer : IArcMarkdownFenceRenderer
{
    private const string Language = "pura-agent";

    /// <summary>Diagnostic name used in warnings.</summary>
    public string Name => "arc.agent.fence";

    /// <inheritdoc />
    public bool CanRender(string language, string source, bool isClosed, ArcMarkdownRenderContext context)
        => string.Equals(language?.Trim(), Language, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Control Render(string language, string source, bool isClosed, ArcMarkdownRenderContext context)
    {
        try
        {
            if (!isClosed || (context.IsStreaming && context.IsTail))
            {
                return ArcAgentFallbackCards.Loading(context);
            }

            var result = ArcAgentJson.Parse(source);
            switch (result.Status)
            {
                case ArcAgentPayloadStatus.Success when result.Component is { } model:
                    return CreateComponent(model, context, result.Raw);

                case ArcAgentPayloadStatus.Unsupported:
                    return ArcAgentFallbackCards.Unsupported(context, result.Type ?? "(unknown)", result.Raw);

                default:
                    return BuildError(context, result);
            }
        }
        catch (Exception exception)
        {
            context.Warn(
                "ARC-AGENT-001",
                $"pura-agent 组件渲染失败：{exception.Message}",
                "pura-agent",
                Name);
            return ArcAgentFallbackCards.Error(
                context,
                context.Options.Localizer.Get(ArcMarkdownStringKeys.AgentComponentError),
                exception.Message,
                ArcAgentJson.Normalize(source));
        }
    }

    private static Control BuildError(ArcMarkdownRenderContext context, ArcAgentPayloadResult result)
    {
        var error = result.Error;
        var detail = error?.Path is { Length: > 0 } path
            ? $"{path}: {error.Message}"
            : error?.Message;
        context.Warn(
            "ARC-AGENT-002",
            $"pura-agent 载荷无效：{detail ?? "unknown error"}",
            "pura-agent",
            "arc.agent.fence");
        return ArcAgentFallbackCards.Error(
            context,
            context.Options.Localizer.Get(ArcMarkdownStringKeys.AgentComponentError),
            detail,
            result.Raw);
    }

    private static Control CreateComponent(
        ArcAgentComponentModel model,
        ArcMarkdownRenderContext context,
        string raw)
    {
        var componentId = string.IsNullOrWhiteSpace(model.Id) ? ArcAgentJson.CreateComponentId(raw) : model.Id!;
        var interactive = context.Options.InteractionEnabled && !context.IsStreaming;

        switch (model)
        {
            case ArcAgentChoiceModel choice:
            {
                var card = new ArcAgentChoiceCard
                {
                    Model = choice with { Id = componentId },
                    IsInteractive = interactive
                };
                card.Submitted += (_, args) => context.InteractionSink?.Invoke(new ArcMarkdownInteraction(
                    "choice",
                    args.ComponentId,
                    args.SelectedIds,
                    Confirmed: true)
                {
                    Raw = raw
                });
                RestoreState(card, context, componentId);
                return card;
            }

            case ArcAgentConfirmModel confirm:
            {
                var card = new ArcAgentConfirmCard
                {
                    Model = confirm with { Id = componentId },
                    IsInteractive = interactive
                };
                card.Decided += (_, args) => context.InteractionSink?.Invoke(new ArcMarkdownInteraction(
                    "confirm",
                    args.ComponentId,
                    Array.Empty<string>(),
                    args.Confirmed,
                    args.Value)
                {
                    Raw = raw
                });
                RestoreState(card, context, componentId);
                return card;
            }

            default:
                return ArcAgentFallbackCards.Unsupported(
                    context,
                    model.Type,
                    raw);
        }
    }

    private static void RestoreState(Control card, ArcMarkdownRenderContext context, string componentId)
    {
        if (context.ComponentStates is not { } states || !states.TryGetValue(componentId, out var state))
        {
            return;
        }

        switch (card)
        {
            case ArcAgentChoiceCard choice:
                choice.ApplyInteractionState(state);
                break;
            case ArcAgentConfirmCard confirm:
                confirm.ApplyInteractionState(state);
                break;
        }
    }
}
