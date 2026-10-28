using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Selection toolbar: a checkbox-selectable project table with a floating bulk action bar that appears only
/// while rows are selected, counts them, and clears the selection. Archive and delete mutate the sample rows
/// and offer a single undo.
/// </summary>
public sealed class SelectionToolbarPage : ArcItemPage
{
    public override string ItemSlug => "selection-toolbar";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Bulk actions for checkbox-selectable tables and grids.",
        "Floating bars that appear only while items are selected."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use dropdown-menu for per-row actions rather than a batch bar."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The bar is a labelled toolbar, and the selected count is repeated in a polite live region so the change is announced.",
        "Tab moves through the actions and the clear button, Enter or Space activates them, and the clear button carries a Clear selection label."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The bar rises on a spring and leaves faster when the selection empties; the count rolls in the direction of the change.",
        "Reduced motion uses plain fades and no width spring."
    };

    public override string? ImplementationNotes =>
        "Rows are sample projects. Archive, change owner and delete act on the local list and can be undone once; nothing is persisted. Selection state comes from the sortable table, so the bar always reflects the real checkbox state.";

    private sealed record Project(string Id, string Name, string Owner, string Status, double Budget, string Updated);

    private static readonly Project[] Seed =
    {
        new("p1", "Harbour", "Maya Chen", "Open", 42000, "2h ago"),
        new("p2", "Atlas analytics", "Leo Park", "Blocked", 18500, "4h ago"),
        new("p3", "Harbour analytics", "Sara Ruiz", "Open", 86000, "Yesterday"),
        new("p4", "Northwind", "Tom Hale", "Closed", 12800, "2d ago"),
        new("p5", "Lattice", "Ines Ma", "Open", 54000, "3d ago")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var projects = Seed.ToList();
        var selected = new List<string> { "p2", "p3", "p4" };
        var lastRemoved = new List<(int Index, Project Project)>();

        var tableHost = new ContentControl();
        var barHost = new ContentControl();

        void SelectionChanged(IReadOnlyList<string> keys)
        {
            selected = keys.ToList();
            BuildBar();
            AppKit.Say(status, keys.Count == 0
                ? "Selection cleared."
                : $"{keys.Count} of {projects.Count} projects selected.");
        }

        void Remove(string verb)
        {
            lastRemoved = projects
                .Select((project, index) => (Index: index, Project: project))
                .Where(entry => selected.Contains(entry.Project.Id))
                .ToList();
            foreach (var entry in lastRemoved)
            {
                projects.Remove(entry.Project);
            }

            var count = lastRemoved.Count;
            selected = new List<string>();
            Build();
            AppKit.Say(status, $"{count} project{(count == 1 ? string.Empty : "s")} {verb}. Undo is available in the bar.");
        }

        void BuildTable()
        {
            var columns = new[]
            {
                new ArcTableColumn { Key = "name", Label = "Project" },
                new ArcTableColumn { Key = "owner", Label = "Owner", Width = 150, Sortable = false },
                new ArcTableColumn { Key = "status", Label = "Status", Width = 110, Sortable = false },
                new ArcTableColumn
                {
                    Key = "budget",
                    Label = "Budget",
                    Width = 120,
                    Numeric = true,
                    Format = value => "$" + Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.InvariantCulture)
                },
                new ArcTableColumn { Key = "updated", Label = "Updated", Width = 110, Sortable = false }
            };

            var rows = projects
                .Select(project => new Dictionary<string, object?>
                {
                    ["id"] = project.Id,
                    ["name"] = project.Name,
                    ["owner"] = project.Owner,
                    ["status"] = project.Status,
                    ["budget"] = project.Budget,
                    ["updated"] = project.Updated
                })
                .ToList();

            var table = new ArcSortableDataTable
            {
                Rows = rows,
                Columns = columns,
                Caption = "Projects",
                Selectable = true,
                SelectedKeys = selected.ToList(),
                DefaultSort = new ArcTableSort { Key = "name", Direction = ArcSortDirection.Asc },
                ItemName = new ArcTableItemName { One = "project", Other = "projects" }
            };
            table.SelectionChanged += (_, keys) => SelectionChanged(keys);

            tableHost.Content = table;
        }

        void BuildBar()
        {
            if (selected.Count == 0 && lastRemoved.Count == 0)
            {
                barHost.Content = null;
                return;
            }

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (selected.Count > 0)
            {
                content.Children.Add(AppKit.Row(7,
                    new TextBlock { Text = selected.Count.ToString(), Classes = { "arc-body-sm", "arc-medium" } },
                    new TextBlock
                    {
                        Text = selected.Count == 1 ? "project selected" : "projects selected",
                        Classes = { "arc-body-sm" },
                        VerticalAlignment = VerticalAlignment.Center
                    }));
                content.Children.Add(new Border { Width = 1, Height = 20, Background = AppKit.Brush(ArcTokens.Border) });
                content.Children.Add(AppKit.Btn("Archive", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Remove("archived"), icon: "archive", height: 30));
                content.Children.Add(AppKit.Btn("Change owner", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                {
                    foreach (var project in projects.Where(project => selected.Contains(project.Id)).ToList())
                    {
                        projects[projects.IndexOf(project)] = project with { Owner = "Maya Chen" };
                    }

                    var count = selected.Count;
                    Build();
                    AppKit.Say(status, $"Owner changed to Maya Chen for {count} projects.");
                }, icon: "user", height: 30));
                content.Children.Add(AppKit.Btn("Delete", ArcButtonVariant.Danger, ArcButtonSize.Sm, () => Remove("deleted"), icon: "trash-2", height: 30));
                content.Children.Add(AppKit.IconBtn("x", "Clear selection", () =>
                {
                    selected = new List<string>();
                    Build();
                    AppKit.Say(status, "Selection cleared.");
                }, 28));
            }
            else
            {
                content.Children.Add(AppKit.Row(7,
                    new ArcIcon { Icon = "check", Size = 14, Foreground = AppKit.Brush(ArcTokens.Success) },
                    new TextBlock { Text = "List updated", Classes = { "arc-body-sm" } }));
                content.Children.Add(new Border { Width = 1, Height = 20, Background = AppKit.Brush(ArcTokens.Border) });
                content.Children.Add(AppKit.Link("Undo", () =>
                {
                    foreach (var entry in lastRemoved.OrderBy(entry => entry.Index))
                    {
                        projects.Insert(Math.Min(entry.Index, projects.Count), entry.Project);
                    }

                    var restored = lastRemoved.Select(entry => entry.Project.Id).ToList();
                    selected = restored;
                    lastRemoved = new List<(int, Project)>();
                    Build();
                    AppKit.Say(status, $"{restored.Count} projects restored and selected.");
                }, trailingIcon: "undo-2"));
            }

            var bar = AppKit.Floating(content, 9, 14);
            bar.HorizontalAlignment = HorizontalAlignment.Center;
            bar.VerticalAlignment = VerticalAlignment.Bottom;
            bar.Margin = new Thickness(0, 0, 0, 14);
            barHost.Content = bar;
        }

        void Build()
        {
            BuildTable();
            BuildBar();
        }

        Build();

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = "Projects", Classes = { "arc-body", "arc-medium" } },
                        AppKit.Caption("Select rows to run batch actions. The bar appears only while something is selected.")
                    }
                },
                AppKit.Place(AppKit.Row(6,
                    AppKit.Pill($"{projects.Count} projects", ArcBadgeTone.Neutral),
                    AppKit.Btn("New project", ArcButtonVariant.Primary, ArcButtonSize.Sm, () => AppKit.Say(status, "Creation is simulated."), icon: "plus", height: 32)), 1)
            }
        };

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10,
            Children =
            {
                header,
                AppKit.Place(new Border { Child = tableHost }, 0, 1),
                AppKit.Place(new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        status,
                        new Border { Height = 4 },
                        barHost
                    }
                }, 0, 2)
            }
        };

        return AppKit.Screen(new Border { Padding = new Thickness(18, 14, 18, 12), Child = content }, 500);
    }
}
