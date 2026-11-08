using Avalonia.Controls;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Obsolete compatibility alias for the old Content chat control. Use <see cref="ArcChatView"/> for the
/// bubbles and <see cref="ArcChatComposer"/> for the input; both are in this package and render Markdown.
/// </summary>
[Obsolete("ArcChatThread moved to Arc.Avalonia.Chat. Use ArcChatView (bubbles + Markdown) and ArcChatComposer instead.")]
public class ArcChatThread : ArcChatView
{
    /// <summary>Uses the <see cref="ArcChatView"/> control theme so the alias looks identical.</summary>
    protected override Type StyleKeyOverride => typeof(ArcChatView);
}
