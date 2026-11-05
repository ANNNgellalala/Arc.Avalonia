using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Maps a projected value (message, tool call, approval request, artifact...) to a view. Defaults are built in;
/// a host can register overrides for any type. This is a view registry, not a rendering layer: it only chooses
/// controls/DataTemplates and never draws anything.
/// </summary>
public interface IArcAgentItemViewRegistry
{
    /// <summary>Registers a strongly typed view factory (last registration wins).</summary>
    void Register<T>(Func<T, Control?> factory) where T : class;

    /// <summary>Registers a view factory for an arbitrary type.</summary>
    void Register(Type type, Func<object, Control?> factory);

    /// <summary>Resolves the best view for a value; null when no factory matches.</summary>
    Control? Resolve(object value);

    /// <summary>Creates an Avalonia template that delegates to <see cref="Resolve"/>.</summary>
    IDataTemplate CreateTemplate();
}

/// <summary>The default registry with built-in message/tool/approval/artifact views.</summary>
public sealed class ArcAgentItemViewRegistry : IArcAgentItemViewRegistry
{
    private readonly List<(Type Type, Func<object, Control?> Factory)> _factories = new();

    /// <summary>Creates a registry; pass false to start empty (tests often do).</summary>
    public ArcAgentItemViewRegistry(bool includeDefaults = true)
    {
        if (!includeDefaults)
        {
            return;
        }

        Register<ArcAgentMessage>(message => MessageView(message));
        Register<ArcToolCall>(call => new ArcAgentToolCallCard { Call = call, Margin = new Thickness(0, 2, 0, 2) });
        Register<ArcApprovalRequest>(request => ApprovalView(request));
        Register<ArcArtifact>(artifact => ArtifactView(artifact));
    }

    /// <inheritdoc />
    public void Register<T>(Func<T, Control?> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.RemoveAll(item => item.Type == typeof(T));
        _factories.Add((typeof(T), value => factory((T)value)));
    }

    /// <inheritdoc />
    public void Register(Type type, Func<object, Control?> factory)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);
        _factories.RemoveAll(item => item.Type == type);
        _factories.Add((type, factory));
    }

    /// <inheritdoc />
    public Control? Resolve(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (var index = _factories.Count - 1; index >= 0; index--)
        {
            var entry = _factories[index];
            if (entry.Type.IsInstanceOfType(value))
            {
                var control = entry.Factory(value);
                if (control is not null)
                {
                    return control;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public IDataTemplate CreateTemplate() =>
        new FuncDataTemplate<object>((value, _) => Resolve(value!) ?? Fallback(value), supportsRecycling: false);

    private static Control Fallback(object? value)
    {
        if (value is null)
        {
            return new TextBlock();
        }

        return ArcUi.Text(value.ToString() ?? string.Empty, "arc-body-sm");
    }

    private static Control MessageView(ArcAgentMessage message)
    {
        var stack = ArcUi.Stack(2);
        stack.Children.Add(ArcUi.Caption(message.Role switch
        {
            ArcAgentRole.User => "You",
            ArcAgentRole.Assistant => "Assistant",
            ArcAgentRole.Reasoning => "Thinking",
            ArcAgentRole.Tool => "Tool",
            ArcAgentRole.SubAgent => "Sub-agent",
            _ => "System"
        }));
        var text = ArcUi.Text(message.Text, "arc-body-sm", 680);
        text.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(text);
        return new Border
        {
            Background = message.IsStreaming ? ArcUi.Brush(ArcTokens.SurfaceMuted) : null,
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(10, 6, 10, 6),
            Child = stack
        };
    }

    private static Control ApprovalView(ArcApprovalRequest request)
    {
        var stack = ArcUi.Stack(4);
        stack.Children.Add(ArcUi.Strong($"{request.ToolName} needs approval"));
        stack.Children.Add(ArcUi.Body(request.Intent, 620));
        stack.Children.Add(ArcUi.Pill(request.Risk.ToString(), "shield-alert", accent: request.Risk == ArcRiskLevel.High));
        return ArcUi.Surface(stack, 12);
    }

    private static Control ArtifactView(ArcArtifact artifact)
    {
        var stack = ArcUi.Stack(2);
        stack.Children.Add(ArcUi.Strong(artifact.Title));
        if (!string.IsNullOrWhiteSpace(artifact.Subtitle))
        {
            stack.Children.Add(ArcUi.Caption(artifact.Subtitle));
        }

        if (!string.IsNullOrWhiteSpace(artifact.DisplayText))
        {
            stack.Children.Add(ArcUi.Mono(artifact.DisplayText));
        }

        var row = ArcUi.HStack(8);
        row.Children.Add(ArcUi.Icon("file-text", 15, ArcTokens.TextSecondary));
        row.Children.Add(stack);
        return ArcUi.Surface(row, 10);
    }
}

