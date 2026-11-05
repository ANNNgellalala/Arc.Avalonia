using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Scenario page: a 10,000 node tree table. The table flattens the expanded tree into a row list and
/// realises only the rows inside the scroll viewport plus a six-row overscan, so the control tree stays
/// viewport-sized while the scrollbar still spans all 10,000 rows. Sorting, expansion, tri-state
/// selection and keyboard navigation keep their existing behaviour on the flattened rows.
/// Not a catalog item: a plain <see cref="ArcPage"/> so the 294-entry documentation audit is untouched.
/// </summary>
public sealed class TreeTableWindowPage : ArcPage
{
    private const int RootRows = 10_000;

    public override string Title => "Tree table · 10k windowed";

    public override string Group => "Data · scale";

    public override string? Description =>
        "The tree table flattens its expanded tree and creates row controls only for the viewport " +
        "(plus overscan). 10,000 nodes: the scrollbar covers every row, the visual tree only holds the " +
        "visible ones. Expand, sort, tri-state selection and arrow-key navigation behave as before.";

    public override int Order => 9202;

    public override Control Build(ArcDemoContext context)
    {
        var table = new ArcTreeTable
        {
            Width = 1_040,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "10,000 windowed rows",
            MaxBodyHeight = 560,
            Selectable = true,
            Columns = new List<ArcTreeTableColumn>
            {
                new() { Key = "name", Header = "Node", Width = 0, MinWidth = 240 },
                new() { Key = "owner", Header = "Owner", Width = 180, Sortable = true },
                new() { Key = "size", Header = "Size", Width = 110, Align = ArcTreeTableAlign.Right },
                new() { Key = "status", Header = "Status", Width = 140 }
            },
            Rows = CreateRows()
        };
        ArcUi.Name(
            table,
            "Tree table with 10,000 windowed rows",
            "Arrow keys move, Right and Left expand or collapse, Space selects, Enter activates. Only viewport rows are realised.");

        var stats = DemoUi.Caption(string.Empty);
        var last = string.Empty;
        table.LayoutUpdated += (_, _) =>
        {
            var text =
                $"{table.FlattenedRowCount.ToString("N0", CultureInfo.InvariantCulture)} flattened rows · " +
                $"{table.RealisedRowCount} realised row controls · " +
                $"window [{table.WindowFirstRow.ToString("N0", CultureInfo.InvariantCulture)}.." +
                $"{table.WindowLastRow.ToString("N0", CultureInfo.InvariantCulture)}] · " +
                $"{table.CachedRowCount} cached for reuse";
            if (text != last)
            {
                last = text;
                stats.Text = text;
            }
        };

        var root = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(DemoUi.Section(
            "10k nodes, viewport-sized row controls",
            "The flattened list is the source of truth for order, focus and selection; the window is a " +
            "rendering detail. Budget: ceil(viewport height / 44px) + 2 × 6 overscan + 1 row controls, " +
            "independent of the node count.",
            table));
        root.Children.Add(stats);
        root.Children.Add(DemoUi.Caption(
            "Regression checks: expand/collapse rebuilds the window, sorting rebuilds the flattened order, " +
            "tri-state selection stays normalised, and keyboard focus scrolls the target row into the window."));
        return root;
    }

    private static List<ArcTreeTableRow> CreateRows()
    {
        var rows = new List<ArcTreeTableRow>(RootRows);
        for (var index = 0; index < RootRows; index++)
        {
            var number = index + 1;
            var row = new ArcTreeTableRow
            {
                Id = $"node-{number}",
                Label = $"Node {number.ToString("N0", CultureInfo.InvariantCulture)}"
            };
            row["name"] = $"Node {number.ToString("N0", CultureInfo.InvariantCulture)}";
            row["owner"] = number % 3 == 0 ? "Platform" : number % 3 == 1 ? "Design" : "Growth";
            row["size"] = number % 97 == 0 ? 0d : number * 1.5;
            row["status"] = number % 11 == 0 ? "Needs review" : number % 5 == 0 ? "Blocked" : "Active";
            rows.Add(row);
        }

        return rows;
    }
}
