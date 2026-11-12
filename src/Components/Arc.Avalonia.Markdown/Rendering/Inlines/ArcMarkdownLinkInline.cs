using Avalonia.Controls.Documents;
using Avalonia.Media;

using MediaTextDecorations = Avalonia.Media.TextDecorations;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// A Markdown link rendered as <see cref="Span"/> text instead of an embedded control. Keeping the link in the
/// text run makes it share the body line box and baseline (no raised label, no inflated line height), wrap at
/// the same points as the surrounding prose, and participate in text selection/copy. Clicks are resolved by
/// <see cref="Controls.ArcMarkdownLinkInteraction"/> through a text hit test on the host text block.
/// </summary>
internal sealed class ArcMarkdownLinkInline : Span
{
    /// <param name="url">Raw link target as written in the document (may be a fragment such as <c>#anchor</c>).</param>
    /// <param name="displayText">Visible label text (also carried to <see cref="ArcMarkdownLinkClick.Text"/>).</param>
    /// <param name="title">Optional link title (surfaced as a tooltip).</param>
    /// <param name="accent">Accent brush for the label.</param>
    internal ArcMarkdownLinkInline(string url, string displayText, string? title, IBrush accent)
    {
        Url = url ?? string.Empty;
        DisplayText = displayText ?? string.Empty;
        Title = title;
        Foreground = accent;
        TextDecorations = MediaTextDecorations.Underline;
    }

    /// <summary>Raw link target.</summary>
    internal string Url { get; }

    /// <summary>Visible label text.</summary>
    internal string DisplayText { get; }

    /// <summary>Optional title from <c>[text](url "title")</c>.</summary>
    internal string? Title { get; }

    /// <summary>
    /// Number of text-layout positions the link's inline content consumes. <see cref="LineBreak"/> counts as
    /// <see cref="Environment.NewLine"/>: Avalonia's line-break run emits the platform newline sequence, and the
    /// text layout indexes that whole sequence on Windows (two positions) even though it is one inline.
    /// </summary>
    internal static int TextLength(InlineCollection? inlines)
    {
        if (inlines is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    count += run.Text?.Length ?? 0;
                    break;
                case LineBreak:
                    count += Environment.NewLine.Length;
                    break;
                case InlineUIContainer:
                    // The text layout advances one character for every embedded control.
                    count += 1;
                    break;
                case Span span:
                    count += TextLength(span.Inlines);
                    break;
                default:
                    count += 1;
                    break;
            }
        }

        return count;
    }
}
