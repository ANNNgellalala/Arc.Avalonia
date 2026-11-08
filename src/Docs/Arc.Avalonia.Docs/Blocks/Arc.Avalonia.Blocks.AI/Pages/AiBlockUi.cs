using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Shared Markdown body helper for the Blocks.AI pages. The design system has exactly one Markdown renderer
/// (<see cref="ArcMarkdownView"/>), so page bodies delegate to it instead of carrying a private subset
/// renderer; the max-width mapping keeps the column measure the pages were designed with.
/// </summary>
internal static class AiBlockUi
{
    internal static ArcMarkdownView Markdown(string markdown, double maxWidth = 0)
    {
        // The Markdown token snapshot is process-wide; refresh it for the palette active when this page is
        // built (pages are rebuilt on theme change) so Blocks.AI pages stay light/dark correct.
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        var view = new ArcMarkdownView
        {
            Markdown = markdown ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        if (maxWidth > 0)
        {
            view.MaxWidth = maxWidth;
        }

        return view;
    }
}
