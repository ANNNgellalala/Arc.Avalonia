using Avalonia.Interactivity;

namespace Arc.Avalonia.Markdown;

/// <summary>Raised when a Markdown link is clicked; set <see cref="Handled"/> to suppress the default action.</summary>
public class ArcMarkdownLinkClickedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcMarkdownLinkClickedEventArgs()
    {
    }

    /// <summary>Creates the args for a click.</summary>
    public ArcMarkdownLinkClickedEventArgs(string url, string text) => (Url, Text) = (url, text);

    /// <summary>Link target.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Visible link text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>When true the view does not open the link.</summary>
    public bool HandledByHost { get; set; }
}

/// <summary>Raised for recoverable renderer problems (unknown nodes, blocked content, truncation).</summary>
public class ArcMarkdownWarningEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcMarkdownWarningEventArgs()
    {
    }

    /// <summary>Creates the args for a warning.</summary>
    public ArcMarkdownWarningEventArgs(ArcMarkdownWarning warning) : this()
    {
        Code = warning.Code;
        Message = warning.Message;
        NodeType = warning.NodeType;
        Renderer = warning.Renderer;
    }

    /// <summary>Stable warning code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Node type that failed.</summary>
    public string? NodeType { get; init; }

    /// <summary>Renderer that failed, when known.</summary>
    public string? Renderer { get; init; }
}

/// <summary>Raised when an interactive agent component reports a choice or confirmation.</summary>
public class ArcMarkdownInteractionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcMarkdownInteractionEventArgs()
    {
    }

    /// <summary>Creates the args from an interaction record.</summary>
    public ArcMarkdownInteractionEventArgs(ArcMarkdownInteraction interaction) : this()
    {
        ComponentType = interaction.ComponentType;
        ComponentId = interaction.ComponentId;
        SelectedIds = interaction.SelectedIds;
        Confirmed = interaction.Confirmed;
        Value = interaction.Value;
    }

    /// <summary>Component type (<c>choice</c>, <c>confirm</c>, ...).</summary>
    public string ComponentType { get; init; } = string.Empty;

    /// <summary>Component id from the <c>pura-agent</c> payload.</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>Selected option ids.</summary>
    public IReadOnlyList<string> SelectedIds { get; init; } = Array.Empty<string>();

    /// <summary>Whether the interaction was confirmed (confirm component or a submitted choice).</summary>
    public bool Confirmed { get; init; }

    /// <summary>Free-form value (confirm labels, rating value, ...).</summary>
    public string? Value { get; init; }
}

/// <summary>A rendered top-level block, exposed for diagnostics and virtualization.</summary>
public sealed record ArcMarkdownBlockInfo(int Index, string NodeType, int Start, int End, int Line);
