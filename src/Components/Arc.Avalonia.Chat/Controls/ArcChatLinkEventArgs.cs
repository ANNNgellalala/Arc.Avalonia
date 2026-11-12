using Avalonia.Interactivity;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Raised when a link inside a rendered chat message is activated. The upstream
/// <c>PuraMarkdown.Agent</c> renderer owns the <c>HyperlinkButton</c> but does not surface a chat-level
/// link event, so Arc raises this one first: set <see cref="HandledByHost"/> to take over (in-app
/// navigation, telemetry, clipboard, ...) and keep Arc from opening the link.
/// </summary>
public class ArcChatLinkClickedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcChatLinkClickedEventArgs()
    {
    }

    /// <summary>Creates the args for a routed event.</summary>
    public ArcChatLinkClickedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Creates the args for one click.</summary>
    public ArcChatLinkClickedEventArgs(RoutedEvent routedEvent, string url) : base(routedEvent) => Url = url;

    /// <summary>Raw link target (scheme included).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>When true the view does not open the link.</summary>
    public bool HandledByHost { get; set; }
}
