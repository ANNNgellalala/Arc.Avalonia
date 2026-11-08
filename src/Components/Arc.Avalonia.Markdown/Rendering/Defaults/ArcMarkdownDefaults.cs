using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Built-in renderer set. Registered at priority 0 so hosts and plugins can override any node with a
/// higher-priority renderer; the returned fallback handles unknown nodes and never throws.
/// </summary>
internal static class ArcMarkdownDefaults
{
    /// <summary>Registers every built-in renderer and returns the fallback renderer.</summary>
    internal static IArcMarkdownNodeRenderer Register(
        List<(IArcMarkdownNodeRenderer Renderer, int Priority)> nodes,
        List<(IArcMarkdownFenceRenderer Renderer, int Priority)> fences)
    {
        // Built-ins sit in one priority band (0). Plugins default to 100, so a host renderer always wins
        // regardless of registration order; the fallback is pinned below everything.
        nodes.Add((new ThematicBreakRenderer(), 0));
        nodes.Add((new HeadingRenderer(), 0));
        nodes.Add((new ParagraphRenderer(), 0));
        nodes.Add((new QuoteRenderer(), 0));
        nodes.Add((new ListRenderer(), 0));
        nodes.Add((new TableRenderer(), 0));
        nodes.Add((new HtmlFallbackRenderer(), 0));
        nodes.Add((new CodeBlockRenderer(), 0));
        nodes.Add((new SkipRenderer(), 0));

        if (fences.Count > 0)
        {
            // Fences outrank the plain code renderer so a plugin can take over a language before the
            // generic code block sees it; a failing plugin still falls back to the code block.
            nodes.Add((
                new FenceDispatchRenderer(fences
                    .OrderByDescending(entry => entry.Priority)
                    .Select(entry => entry.Renderer)
                    .ToArray()),
                50));
        }

        var fallback = new FallbackRenderer();
        nodes.Add((fallback, int.MinValue));
        return fallback;
    }

    /// <summary>Control used when a node has no working renderer.</summary>
    internal static Control Unsupported(ArcMarkdownRenderContext context, string nodeType)
    {
        var style = context.Style;
        var text = context.CreateTextBlock();
        text.Text = context.Options.Localizer.Get(ArcMarkdownStringKeys.UnsupportedNode, nodeType);
        text.Foreground = style.TextMuted;
        text.FontSize = style.SmallSize;
        text.FontFamily = style.BodyFont;
        text.TextWrapping = TextWrapping.Wrap;
        text.Margin = new Thickness(0, 0, 0, style.BlockSpacing);
        return text;
    }

    /// <summary>Creates the shared text block shell (paragraphs, headings, list rows, table cells).</summary>
    internal static TextBlock CreateText(ArcMarkdownRenderContext context, bool wrap = true)
    {
        var style = context.Style;
        var text = context.CreateTextBlock();
        text.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        text.Foreground = style.Foreground;
        text.FontSize = style.BodySize;
        text.FontFamily = style.BodyFont;
        text.FontWeight = FontWeight.Normal;
        return text;
    }

    /// <summary>Removes the bottom margin of a block that is nested inside a list item, cell or quote.</summary>
    internal static void TrimBottomMargin(Control control)
    {
        var margin = control.Margin;
        if (margin.Bottom > 0)
        {
            control.Margin = new Thickness(margin.Left, margin.Top, margin.Right, 0);
        }
    }

    /// <summary>Extracts the raw source text of a leaf block, keeping one '\n' per Markdig line.</summary>
    internal static string GetLeafText(MarkdownObject node)
    {
        if (node is LeafBlock { Lines.Lines: { } lines })
        {
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(lines[i].ToString());
            }

            return builder.ToString().TrimEnd('\n');
        }

        return node.ToString() ?? string.Empty;
    }
}
