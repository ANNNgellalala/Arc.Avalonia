using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Default block renderers for the CommonMark/GFM contract nodes: paragraph, heading, quote, list
/// (including task lists), thematic break, HTML text fallback and the unknown-node fallback.
/// Every renderer resolves colours, type and spacing through <see cref="ArcMarkdownStyle"/> only.
/// </summary>
/// <remarks>
/// Nested blocks are rendered through <see cref="ArcMarkdownRenderContext.RenderBlock(MarkdownObject)"/>,
/// so host and plugin renderers with a higher priority already apply inside quotes, list items and
/// table cells. Renderers never throw: a failure is turned into a warning control by the context.
/// </remarks>
internal sealed class ParagraphRenderer : IArcMarkdownIncrementalRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is ParagraphBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var paragraph = (ParagraphBlock)node;
        var text = ArcMarkdownDefaults.CreateText(context);
        text.Margin = new Thickness(0, 0, 0, context.Style.BlockSpacing);
        context.RenderInlineChildren(paragraph.Inline, text.Inlines!);
        return text;
    }

    public bool CanUpdate(MarkdownObject oldNode, MarkdownObject newNode, ArcMarkdownRenderContext context)
        => oldNode is ParagraphBlock && newNode is ParagraphBlock;

    public void Update(Control existing, MarkdownObject newNode, ArcMarkdownRenderContext context)
    {
        if (existing is not TextBlock text || newNode is not ParagraphBlock paragraph)
        {
            return;
        }

        text.Inlines!.Clear();
        context.RenderInlineChildren(paragraph.Inline, text.Inlines!);
    }
}

/// <summary>Renders headings with the Arc display type ramp and a screen-reader heading level.</summary>
internal sealed class HeadingRenderer : IArcMarkdownIncrementalRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is HeadingBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var heading = (HeadingBlock)node;
        var level = Math.Clamp(heading.Level, 1, 6);
        var style = context.Style;
        var text = ArcMarkdownDefaults.CreateText(context);
        text.FontSize = style.HeadingSize(level);
        // Arc typography uses weight 400/500 only; 500 keeps headings distinct without faux bold.
        text.FontWeight = FontWeight.Medium;
        text.FontFamily = style.DisplayFont;
        text.Margin = new Thickness(
            0,
            level == 1 ? style.HeadingTopSpacing : style.HeadingTopSpacing * 0.6,
            0,
            style.HeadingBottomSpacing);
        context.RenderInlineChildren(heading.Inline, text.Inlines!);
        AutomationProperties.SetHeadingLevel(text, level);
        return text;
    }

    public bool CanUpdate(MarkdownObject oldNode, MarkdownObject newNode, ArcMarkdownRenderContext context)
        => oldNode is HeadingBlock oldHeading
           && newNode is HeadingBlock newHeading
           && oldHeading.Level == newHeading.Level;

    public void Update(Control existing, MarkdownObject newNode, ArcMarkdownRenderContext context)
    {
        if (existing is not TextBlock text || newNode is not HeadingBlock heading)
        {
            return;
        }

        text.Inlines!.Clear();
        context.RenderInlineChildren(heading.Inline, text.Inlines!);
    }
}

/// <summary>Renders a thematic break as a 1px hairline.</summary>
internal sealed class ThematicBreakRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is ThematicBreakBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var style = context.Style;
        return new Border
        {
            Height = 1,
            Background = style.Border,
            Margin = new Thickness(0, style.BlockSpacing, 0, style.BlockSpacing),
        };
    }
}

/// <summary>Renders a block quote as an Arc rail container with recursively rendered children.</summary>
internal sealed class QuoteRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is QuoteBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var quote = (QuoteBlock)node;
        var style = context.Style;
        var content = new StackPanel { Spacing = style.ListItemSpacing };
        var last = quote.Count - 1;
        for (var i = 0; i < quote.Count; i++)
        {
            var child = context.RenderBlock(quote[i]);
            if (i == last)
            {
                // The quote owns the bottom spacing; avoid a doubled gap after the last paragraph.
                ArcMarkdownDefaults.TrimBottomMargin(child);
            }

            content.Children.Add(child);
        }

        return new Border
        {
            BorderBrush = style.BorderStrong,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(style.QuoteIndent, 0, 0, 0),
            Margin = new Thickness(0, 0, 0, style.BlockSpacing),
            Child = content,
        };
    }
}

/// <summary>
/// Renders ordered, unordered and task lists. All items share one grid so the marker column stays
/// aligned across the list; nested lists render inside the item content column.
/// </summary>
internal sealed class ListRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is ListBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var list = (ListBlock)node;
        var style = context.Style;
        var items = list.OfType<ListItemBlock>().ToList();
        if (items.Count == 0)
        {
            return new Panel { IsVisible = false };
        }

        var grid = new Grid { Margin = new Thickness(0, 0, 0, style.BlockSpacing) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var number = 1;
        if (list.IsOrdered && !int.TryParse(list.OrderedStart, out number))
        {
            number = 1;
        }

        for (var row = 0; row < items.Count; row++)
        {
            var item = items[row];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var task = FindTaskList(item);
            var marker = task is null
                ? CreateMarker(list.IsOrdered, number, context)
                : CreateTaskMarker(task, context);
            Grid.SetRow(marker, row);
            Grid.SetColumn(marker, 0);
            grid.Children.Add(marker);

            var content = new StackPanel
            {
                Spacing = style.ListItemSpacing,
                VerticalAlignment = VerticalAlignment.Top,
            };
            for (var childIndex = 0; childIndex < item.Count; childIndex++)
            {
                var child = item[childIndex];
                if (task is not null && ReferenceEquals(child, task.Paragraph))
                {
                    var text = ArcMarkdownDefaults.CreateText(context);
                    text.Margin = new Thickness(0);
                    // The checkbox already occupies the marker column, so the TaskList inline is skipped.
                    ArcMarkdownDefaultInlineRenderer.RenderChildren(
                        task.Paragraph.Inline,
                        text.Inlines!,
                        context,
                        skipFirst: true);
                    content.Children.Add(text);
                    continue;
                }

                var control = context.RenderBlock(child);
                ArcMarkdownDefaults.TrimBottomMargin(control);
                content.Children.Add(control);
            }

            Grid.SetRow(content, row);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);

            if (list.IsOrdered)
            {
                number++;
            }
        }

        return grid;
    }

    private static Control CreateMarker(bool ordered, int number, ArcMarkdownRenderContext context)
    {
        var style = context.Style;
        return new TextBlock
        {
            Text = ordered ? $"{number}." : "•",
            Foreground = style.TextMuted,
            FontSize = style.BodySize,
            FontFamily = style.BodyFont,
            TextAlignment = ordered ? TextAlignment.Right : TextAlignment.Left,
            MinWidth = ordered ? 24 : 16,
            Margin = new Thickness(0, 0, style.ListItemSpacing * 2, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    private static Control CreateTaskMarker(TaskListItem task, ArcMarkdownRenderContext context)
    {
        var style = context.Style;
        var checkbox = new CheckBox
        {
            IsChecked = task.List.Checked,
            IsEnabled = false,
            IsHitTestVisible = false,
            Focusable = false,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 1, style.ListItemSpacing * 2, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var label = ArcMarkdownDefaultInlineRenderer.GetInlineText(task.Paragraph.Inline).Trim();
        if (!string.IsNullOrEmpty(label))
        {
            AutomationProperties.SetName(checkbox, label);
        }

        return checkbox;
    }

    private static TaskListItem? FindTaskList(ListItemBlock item)
    {
        for (var i = 0; i < item.Count; i++)
        {
            if (item[i] is ParagraphBlock { Inline.FirstChild: TaskList taskList } paragraph)
            {
                return new TaskListItem(paragraph, taskList);
            }
        }

        return null;
    }

    private sealed record TaskListItem(ParagraphBlock Paragraph, TaskList List);
}

/// <summary>HTML is disabled at parse time; any surviving HTML block is shown as plain source text.</summary>
internal sealed class HtmlFallbackRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is HtmlBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var text = ArcMarkdownDefaults.GetLeafText(node);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Panel { IsVisible = false };
        }

        var style = context.Style;
        var textBlock = context.CreateTextBlock();
        textBlock.Text = text;
        textBlock.FontFamily = style.MonoFont;
        textBlock.FontSize = style.SmallSize;
        textBlock.Foreground = style.TextMuted;
        textBlock.TextWrapping = TextWrapping.Wrap;
        textBlock.Margin = new Thickness(0, 0, 0, style.BlockSpacing);
        return textBlock;
    }
}

/// <summary>
/// Parsed-only nodes that must not produce UI: link reference definitions, blank lines and the empty
/// placeholder blocks Markdig can emit. Rendering them as nothing keeps the document free of gaps.
/// </summary>
internal sealed class SkipRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context)
        => node is LinkReferenceDefinitionGroup
           or LinkReferenceDefinition
           or BlankLineBlock
           or EmptyBlock;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
        => new Panel { IsVisible = false };
}

/// <summary>
/// Last-resort fallback. Unknown containers keep their children, unknown inlines/leaves keep their
/// text, and every fallback reports a warning instead of throwing or dropping content.
/// </summary>
internal sealed class FallbackRenderer : IArcMarkdownNodeRenderer
{
    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => true;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var nodeType = node.GetType().Name;
        switch (node)
        {
            case ContainerBlock container:
                context.Warn("ARC-MD-NODE-001", $"未知块节点 {nodeType}，已按子块降级渲染", nodeType);
                var panel = new StackPanel { Spacing = 0 };
                for (var i = 0; i < container.Count; i++)
                {
                    panel.Children.Add(context.RenderBlock(container[i]));
                }

                return panel;

            case LeafBlock { Inline: not null } leaf:
                context.Warn("ARC-MD-NODE-001", $"未知块节点 {nodeType}，已按行内文本降级渲染", nodeType);
                var text = ArcMarkdownDefaults.CreateText(context);
                text.Margin = new Thickness(0, 0, 0, context.Style.BlockSpacing);
                context.RenderInlineChildren(leaf.Inline, text.Inlines!);
                return text;

            case LeafBlock:
                context.Warn("ARC-MD-NODE-001", $"未知块节点 {nodeType}，已按纯文本降级渲染", nodeType);
                var style = context.Style;
                var plain = context.CreateTextBlock();
                plain.Text = ArcMarkdownDefaults.GetLeafText(node);
                plain.FontFamily = style.MonoFont;
                plain.FontSize = style.SmallSize;
                plain.Foreground = style.TextMuted;
                plain.TextWrapping = TextWrapping.Wrap;
                plain.Margin = new Thickness(0, 0, 0, style.BlockSpacing);
                return plain;

            default:
                context.Warn("ARC-MD-NODE-001", $"未知节点 {nodeType}，已降级为提示控件", nodeType);
                return ArcMarkdownDefaults.Unsupported(context, nodeType);
        }
    }
}
