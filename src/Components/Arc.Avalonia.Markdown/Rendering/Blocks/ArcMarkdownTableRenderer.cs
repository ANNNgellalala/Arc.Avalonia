using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Renders GFM tables: a muted header row, 1px hairlines between rows, per-column alignment, cell
/// blocks rendered recursively and a horizontal scroll host for wide tables. Empty tables degrade to
/// the localized empty-state copy instead of an empty frame.
/// </summary>
internal sealed class TableRenderer : IArcMarkdownNodeRenderer
{
    /// <summary>Upper bound for one cell measured inside the horizontally scrollable table.</summary>
    private const double MaxCellWidth = 420;

    public bool CanRender(MarkdownObject node, ArcMarkdownRenderContext context) => node is Table;

    public Control Render(MarkdownObject node, ArcMarkdownRenderContext context)
    {
        var table = (Table)node;
        var rows = table.OfType<TableRow>().ToList();
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        if (columnCount == 0)
        {
            var style = context.Style;
            return new TextBlock
            {
                Text = context.Options.Localizer.Get(ArcMarkdownStringKeys.EmptyDocument),
                Foreground = style.TextMuted,
                FontSize = style.SmallSize,
                FontFamily = style.BodyFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, style.BlockSpacing),
            };
        }

        var grid = new Grid();
        for (var column = 0; column < columnCount; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 88 });
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var row = rows[rowIndex];
            var cells = row.OfType<TableCell>().ToList();
            var column = 0;
            var isLastRow = rowIndex == rows.Count - 1;
            for (var cellIndex = 0; cellIndex < cells.Count && column < columnCount; cellIndex++)
            {
                var cell = cells[cellIndex];
                var columnSpan = Math.Clamp(cell.ColumnSpan, 1, columnCount - column);
                var control = CreateCell(row, cell, column, isLastRow, columnSpan, context);
                Grid.SetColumn(control, column);
                Grid.SetColumnSpan(control, columnSpan);
                Grid.SetRow(control, rowIndex);
                grid.Children.Add(control);
                column += columnSpan;
            }

            // Keep the row grid complete when a row has fewer cells than the header (malformed tables).
            while (column < columnCount)
            {
                var empty = CreateCell(row, null, column, isLastRow, 1, context);
                Grid.SetColumn(empty, column);
                Grid.SetRow(empty, rowIndex);
                grid.Children.Add(empty);
                column++;
            }
        }

        var frameStyle = context.Style;
        var frame = new Border
        {
            BorderBrush = frameStyle.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = frameStyle.PanelRadius,
            ClipToBounds = true,
            Child = grid,
        };

        return new ScrollViewer
        {
            // A wide table scrolls horizontally on its own; the gesture must not leak into the page scroller
            // and the scrollbar only appears while the table is touched.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsScrollChainingEnabled = false,
            AllowAutoHide = true,
            Margin = new Thickness(0, 0, 0, frameStyle.BlockSpacing),
            // The card must hug its columns when the table is narrower than the viewport; the fit host
            // keeps it left-aligned instead of letting ScrollContentPresenter stretch it to full width.
            Content = new ArcMarkdownTableFit { Child = frame },
        };
    }

    private static Control CreateCell(
        TableRow row,
        TableCell? cell,
        int column,
        bool isLastRow,
        int columnSpan,
        ArcMarkdownRenderContext context)
    {
        var style = context.Style;
        var panel = new StackPanel
        {
            Spacing = style.ListItemSpacing,
            VerticalAlignment = VerticalAlignment.Top,
        };

        if (cell is not null)
        {
            for (var i = 0; i < cell.Count; i++)
            {
                var block = context.RenderBlock(cell[i]);
                ArcMarkdownDefaults.TrimBottomMargin(block);
                panel.Children.Add(block);
            }
        }

        var alignment = cell is null ? null : GetAlignment(cell, column);
        if (alignment is not null)
        {
            ApplyAlignment(panel, alignment.Value);
        }

        if (row.IsHeader)
        {
            ApplyHeaderWeight(panel);
        }

        var border = new Border
        {
            BorderBrush = style.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, isLastRow ? 0 : 1),
            Padding = new Thickness(style.BlockSpacing, style.ListItemSpacing * 2),
            MaxWidth = MaxCellWidth * columnSpan,
            Child = panel,
        };
        if (row.IsHeader)
        {
            border.Background = style.SurfaceMuted;
        }

        return border;
    }

    private static TableColumnAlign? GetAlignment(TableCell cell, int column)
    {
        // GFM stores alignment on the table, not on the cell: cell -> row -> table.
        if (cell.Parent is not TableRow { Parent: Table table })
        {
            return null;
        }

        return column < table.ColumnDefinitions.Count
            ? table.ColumnDefinitions[column].Alignment
            : null;
    }

    private static void ApplyAlignment(Control control, TableColumnAlign alignment)
    {
        var textAlignment = alignment switch
        {
            TableColumnAlign.Center => TextAlignment.Center,
            TableColumnAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };

        switch (control)
        {
            case TextBlock text:
                text.TextAlignment = textAlignment;
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    ApplyAlignment(child, alignment);
                }

                break;
            case ContentControl { Content: Control content }:
                ApplyAlignment(content, alignment);
                break;
            case Border { Child: Control child }:
                ApplyAlignment(child, alignment);
                break;
        }
    }

    private static void ApplyHeaderWeight(Control control)
    {
        switch (control)
        {
            case TextBlock text:
                text.FontWeight = FontWeight.Medium;
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    ApplyHeaderWeight(child);
                }

                break;
            case ContentControl { Content: Control content }:
                ApplyHeaderWeight(content);
                break;
            case Border { Child: Control child }:
                ApplyHeaderWeight(child);
                break;
        }
    }
}

/// <summary>
/// Scroll host content for a GFM table card. <c>ScrollContentPresenter</c> arranges its child at
/// <c>max(content, viewport)</c> width, so a table narrower than the document would stretch its
/// rounded frame and muted header to the viewport, leaving a blank band to the right. This host
/// reports the card's natural width (columns + cell padding) and arranges the card at that width:
/// narrow tables hug their content, wide tables keep their full width and still scroll horizontally.
/// </summary>
internal sealed class ArcMarkdownTableFit : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
        {
            return default;
        }

        Child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        return Child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(
                0,
                0,
                Math.Min(child.DesiredSize.Width, finalSize.Width),
                child.DesiredSize.Height));
        }

        return finalSize;
    }
}
