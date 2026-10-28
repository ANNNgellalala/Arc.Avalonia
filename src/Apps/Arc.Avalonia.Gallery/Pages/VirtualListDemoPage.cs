using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>
/// The virtualising list, shown with ten thousand rows. The list is a plain UI container: the page supplies
/// the data, the counts and the scroll controls, and the control only reports what is visible.
/// </summary>
public sealed class VirtualListDemoPage : ArcPage
{
    private const int InitialRows = 10000;

    public override string Title => "Virtualized list";

    public override string Group => "P0 · Foundation";

    public override string? Description =>
        "A ListBox primitive that always virtualises its rows, with stick-to-bottom, jump to end and jump to " +
        "index. The demo holds 10,000 rows; only the rows in the viewport are measured and drawn.";

    public override int Order => 20021;

    public override Control Build(ArcDemoContext context)
    {
        var rows = new ObservableCollection<VirtualRow>(CreateRows(1, InitialRows));

        var list = new ArcVirtualList
        {
            Width = 780,
            Height = 470,
            HorizontalAlignment = HorizontalAlignment.Left,
            StickToBottom = false,
            ItemsSource = rows,
            ItemTemplate = new FuncDataTemplate<VirtualRow>((row, _) => BuildRow(row!), true),
            AutoScrollToSelectedItem = false
        };
        ArcUi.Name(
            list,
            "Virtualized list with 10,000 rows",
            "Use the scroll bar or the page buttons to move. Only the visible rows exist.");

        var count = ArcUi.Caption(string.Empty);
        var selection = ArcUi.Caption("No row selected");
        var scrollHost = new Border { VerticalAlignment = VerticalAlignment.Center };

        void UpdateCount() => count.Text = $"{rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows";
        void UpdateScrollState()
        {
            var (word, tone) = list.ScrollState switch
            {
                ArcVirtualListScrollState.AtTop => ("At top", ArcBadgeTone.Neutral),
                ArcVirtualListScrollState.AtBottom => ("At bottom", ArcBadgeTone.Success),
                _ => ("In middle", ArcBadgeTone.Accent)
            };
            scrollHost.Child = ArcUi.Badge(word, tone);
        }

        UpdateCount();
        UpdateScrollState();
        rows.CollectionChanged += OnRowsChanged;
        list.SelectionChanged += (_, _) =>
        {
            selection.Text = list.SelectedItem is VirtualRow row
                ? $"Row {row.Index.ToString("N0", CultureInfo.InvariantCulture)} selected \u00b7 {row.Title}"
                : "No row selected";
        };
        list.ScrollStateChanged += (_, _) => UpdateScrollState();

        void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCount();

        var stick = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = "Stick to bottom: off"
        };
        ArcUi.Name(stick, "Toggle stick to bottom", "Follow newly appended rows, like a live transcript.");
        stick.Click += (_, _) =>
        {
            list.StickToBottom = !list.StickToBottom;
            stick.Content = list.StickToBottom ? "Stick to bottom: on" : "Stick to bottom: off";
        };

        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(ActionButton("Back to top", "Scroll to the first row", () => list.ScrollToIndex(0)));
        toolbar.Children.Add(ActionButton("Jump to 5,000", "Scroll to row 5,000", () => list.ScrollToIndex(4999)));
        toolbar.Children.Add(ActionButton("Scroll to end", "Scroll to the last row", list.ScrollToEnd));
        toolbar.Children.Add(ActionButton("Append 100 rows", "Append one hundred rows to the list", () =>
        {
            var start = rows.Count + 1;
            foreach (var row in CreateRows(start, 100))
            {
                rows.Add(row);
            }
        }));
        toolbar.Children.Add(stick);
        stick.Margin = new Thickness(0, 0, 6, 6);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 12 };
        footer.Children.Add(count);
        Grid.SetColumn(selection, 1);
        footer.Children.Add(selection);
        Grid.SetColumn(scrollHost, 3);
        footer.Children.Add(scrollHost);

        var live = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { toolbar, list, footer }
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Virtualized list",
                    "The list owns no data: it virtualises whatever ItemsSource the host passes, follows " +
                    "appends only while StickToBottom is on, and reports its scroll bucket. Selection is " +
                    "preserved because rows are recycled, not rebuilt.",
                    live),

                DemoUi.Section(
                    "What stays with the host",
                    "Loading, paging, search and persistence are host concerns. The control shows the rows it " +
                    "is given and raises AtBottom through ScrollStateChanged so a jump-to-latest affordance " +
                    "can hide itself.",
                    new ArcCard
                    {
                        Padding = new Thickness(16),
                        Content = ArcUi.Stack(
                            6,
                            ArcUi.Label("API"),
                            ArcUi.Body(
                                "StickToBottom follows collection changes; ScrollToEnd, ScrollToIndex and " +
                                "ScrollState (AtTop / InMiddle / AtBottom) are the whole data-free surface.",
                                700))
                    })
            }
        };
    }

    private static ArcButton ActionButton(string label, string name, Action action)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = label,
            Margin = new Thickness(0, 0, 6, 6)
        };
        ArcUi.Name(button, name);
        button.Click += (_, _) => action();
        return button;
    }

    private static Control BuildRow(VirtualRow row)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("60,*,Auto"),
            Height = 38,
            VerticalAlignment = VerticalAlignment.Center
        };

        var index = new TextBlock
        {
            Text = row.Index.ToString("N0", CultureInfo.InvariantCulture),
            Classes = { "arc-caption", "arc-mono" },
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(index);

        var title = new TextBlock
        {
            Text = row.Title,
            Classes = { "arc-body-sm" },
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);
        title.Margin = new Thickness(8, 0, 12, 0);
        grid.Children.Add(title);

        var badge = ArcUi.Badge(row.Kind, row.Tone);
        badge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        return new Border { Padding = new Thickness(12, 0), Child = grid };
    }

    private static IEnumerable<VirtualRow> CreateRows(int start, int count)
    {
        for (var offset = 0; offset < count; offset++)
        {
            var index = start + offset;
            var (kind, tone) = (index % 4) switch
            {
                0 => ("Event", ArcBadgeTone.Accent),
                1 => ("Reminder", ArcBadgeTone.Warning),
                2 => ("Task", ArcBadgeTone.Neutral),
                _ => ("Medication", ArcBadgeTone.Success)
            };

            yield return new VirtualRow(
                index,
                (index % 3) switch
                {
                    0 => $"Morning briefing item {index:N0}: review the day and approve the plan",
                    1 => $"Captured note {index:N0}: follow up with the clinic about the appointment",
                    _ => $"Routine run {index:N0}: weekly review finished, transcript archived"
                },
                kind,
                tone);
        }
    }

    private sealed record VirtualRow(int Index, string Title, string Kind, ArcBadgeTone Tone);
}
