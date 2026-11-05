using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Scenario page: the reusable <see cref="ArcVirtualList"/> primitive hosting 10,000 rows. It is the
/// list base for large data views (the same primitive the chat/transcript style surfaces reuse): the
/// page owns the data and the item template, the list realises only the rows inside the viewport.
/// Not a catalog item: a plain <see cref="ArcPage"/> so the 294-entry documentation audit is untouched.
/// </summary>
public sealed class BigListPage : ArcPage
{
    private const int InitialRows = 10_000;

    public override string Title => "Virtualised list · 10k rows";

    public override string Group => "Data · scale";

    public override string? Description =>
        "ArcVirtualList is a ListBox primitive whose item panel is always a VirtualizingStackPanel: " +
        "only the rows inside the viewport are measured and drawn. The page owns the data, the item " +
        "template and the selection; the list owns the scroll window.";

    public override int Order => 9201;

    public override Control Build(ArcDemoContext context)
    {
        var rows = new ObservableCollection<BigListRow>(CreateRows(1, InitialRows));
        var list = new ArcVirtualList
        {
            Width = 880,
            Height = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = rows,
            ItemTemplate = new FuncDataTemplate<BigListRow>((row, _) => BuildRow(row!), supportsRecycling: true),
            AutoScrollToSelectedItem = false
        };
        ArcUi.Name(
            list,
            "Virtualised list with 10,000 rows",
            "Use the scroll bar, the keyboard or the page buttons. Only the visible rows exist as controls.");

        var status = DemoUi.Caption($"{rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows · no selection");
        list.SelectionChanged += (_, _) =>
        {
            status.Text = list.SelectedItem is BigListRow row
                ? $"Row {row.Index.ToString("N0", CultureInfo.InvariantCulture)} selected · {row.Title}"
                : $"{rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows · no selection";
        };

        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(ScenarioUi.Button("Back to top", "Scroll to the first row", () => list.ScrollToIndex(0)));
        toolbar.Children.Add(ScenarioUi.Button("Jump to 5,000", "Scroll to the middle", () => list.ScrollToIndex(4_999)));
        toolbar.Children.Add(ScenarioUi.Button("Scroll to end", "Scroll to the last row", list.ScrollToEnd));

        var root = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(DemoUi.Section(
            "10k rows, viewport-sized control tree",
            "The list is the reusable data primitive: ItemsSource + DataTemplate + selection + keyboard, " +
            "and the scroll window is handled by Avalonia's VirtualizingStackPanel. Chat transcripts, logs " +
            "and inboxes reuse the same primitive with StickToBottom.",
            list));
        root.Children.Add(toolbar);
        root.Children.Add(status);
        return root;
    }

    private static Control BuildRow(BigListRow row)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("72,*,160,90"),
            Height = 40
        };
        grid.Children.Add(Cell(row.Index.ToString("N0", CultureInfo.InvariantCulture), HorizontalAlignment.Right));
        var title = Cell(row.Title, HorizontalAlignment.Left);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);
        var owner = Cell(row.Owner, HorizontalAlignment.Left);
        Grid.SetColumn(owner, 2);
        grid.Children.Add(owner);
        var state = Cell(row.Status, HorizontalAlignment.Left);
        Grid.SetColumn(state, 3);
        grid.Children.Add(state);
        return grid;
    }

    private static TextBlock Cell(string text, HorizontalAlignment alignment) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = alignment,
        Margin = new Thickness(12, 0),
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private static System.Collections.Generic.IEnumerable<BigListRow> CreateRows(int start, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var number = start + index;
            yield return new BigListRow(
                number,
                $"Row {number.ToString("N0", CultureInfo.InvariantCulture)}",
                number % 3 == 0 ? "Platform" : number % 3 == 1 ? "Design" : "Growth",
                number % 5 == 0 ? "Blocked" : "Active");
        }
    }
}

/// <summary>One row of the 10k list scenario. Public so the DataTemplate can bind by property name.</summary>
public sealed record BigListRow(int Index, string Title, string Owner, string Status);

/// <summary>Small shared helpers for the scenario pages in this folder.</summary>
internal static class ScenarioUi
{
    internal static Control Button(string text, string help, Action action)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = text,
            Margin = new Thickness(0, 0, 6, 6)
        };
        ArcUi.Name(button, text, help);
        button.Click += (_, _) => action();
        return button;
    }
}
