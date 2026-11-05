using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Data grid's completeness preview: the editable grid with a pre-selected range, a compact read-only grid that
/// folds in a computed column plus the selection gutter, totals and zebra rows switched off, and the empty state.
/// Declared state keys match Docs/data-grid.json so the audit can compare the two. Rows and values are fixed,
/// so the preview is deterministic between runs.
/// </summary>
public sealed class DataGridPage : ArcItemPage
{
    private static readonly (string Id, string Project, string Owner, string Status, double Budget, double Progress, string Updated)[]
        Sample =
        {
            ("r1", "Atlas redesign", "Maya Chen", "In progress", 48250, 64, "Today"),
            ("r2", "Billing migration", "Leo Park", "Review", 18500, 82, "Today"),
            ("r3", "Checkout copy", "Sara Ruiz", "Planned", 86000, 35, "Yesterday"),
            ("r4", "Data export", "Tom Hale", "Blocked", 12800, 48, "Yesterday"),
            ("r5", "Design tokens", "Ines Ma", "In progress", 54000, 76, "2 days ago"),
            ("r6", "Mobile nav", "Noor Haddad", "Done", 9700, 100, "3 days ago")
        };

    public override string ItemSlug => "data-grid";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-basic", "variant-selected", "variant-computed",
        "state-readonly", "state-no-selection", "state-no-totals", "state-plain", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Section(
            "基础表格与预选行",
            "点表头循环排序，拖动表头右缘调整列宽，Shift 点击勾选连续区间；页脚给出行数与预算合计。",
            BasicGrid()));

        panel.Children.Add(Section(
            "计算列、只读、无选择器、无合计、无斑马纹",
            "Spent 列由 ValueGetter 计算，因此只读；这张表同时关闭了勾选列、页脚合计与斑马纹。",
            CompactGrid()));

        panel.Children.Add(Section(
            "空态",
            "Rows 为空时保留表头，并显示 EmptyMessage 与 EmptyDescription，可以把下一步的出口写清楚。",
            EmptyGrid()));

        return panel;
    }

    private static Control Section(string title, string note, Control grid)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Width = 780,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(DemoUi.Label(title));
        var caption = DemoUi.Caption(note);
        caption.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(caption);
        panel.Children.Add(grid);
        return panel;
    }

    private static ArcDataGrid BasicGrid() => new()
    {
        Columns = Columns(includeComputed: false),
        Rows = Rows(6),
        Label = "Q3 budget",
        DefaultSort = new ArcGridSort { Key = "budget", Descending = true },
        SelectedIds = new[] { "r2", "r5" },
        Zebra = true,
        Selectable = true,
        ShowTotals = true,
        MaxBodyHeight = 360
    };

    private static ArcDataGrid CompactGrid() => new()
    {
        Columns = Columns(includeComputed: true),
        Rows = Rows(5),
        Label = "Q3 budget, read only",
        Editable = false,
        Selectable = false,
        ShowTotals = false,
        Zebra = false,
        MaxBodyHeight = 240
    };

    private static ArcDataGrid EmptyGrid() => new()
    {
        Columns = Columns(includeComputed: false),
        Rows = new List<ArcGridRow>(),
        Label = "Empty budget",
        EmptyMessage = "No rows match",
        EmptyDescription = "Try a shorter search, or clear the status filter to see every project again.",
        Selectable = true,
        Zebra = true,
        MaxBodyHeight = 240
    };

    private static List<ArcGridColumn> Columns(bool includeComputed)
    {
        var columns = new List<ArcGridColumn>
        {
            new() { Key = "project", Header = "Project", Width = 0, MinWidth = 120 },
            new() { Key = "owner", Header = "Owner", Width = 120 },
            new() { Key = "status", Header = "Status", Width = 100 },
            new() { Key = "budget", Header = "Budget", Width = 110, Align = ArcGridAlign.Right, Format = Money },
            new()
            {
                Key = "progress",
                Header = "Progress",
                Width = 90,
                Align = ArcGridAlign.Right,
                ShowTotal = false,
                Format = Percent
            },
            new() { Key = "updated", Header = "Updated", Width = 0, MinWidth = 80 }
        };

        if (includeComputed)
        {
            columns.Insert(4, new ArcGridColumn
            {
                Key = "spent",
                Header = "Spent",
                Width = 120,
                Align = ArcGridAlign.Right,
                ShowTotal = false,
                Format = Money,
                ValueGetter = row => row["budget"] is double budget && row["progress"] is double progress
                    ? budget * progress / 100
                    : null
            });
        }

        return columns;
    }

    private static List<ArcGridRow> Rows(int count)
    {
        var rows = new List<ArcGridRow>();
        for (var index = 0; index < count && index < Sample.Length; index++)
        {
            var item = Sample[index];
            rows.Add(new ArcGridRow
            {
                Id = item.Id,
                Values =
                {
                    ["project"] = item.Project,
                    ["owner"] = item.Owner,
                    ["status"] = item.Status,
                    ["budget"] = item.Budget,
                    ["progress"] = item.Progress,
                    ["updated"] = item.Updated
                }
            });
        }

        return rows;
    }

    private static string Money(object? value) => value is null
        ? "—"
        : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("C0", CultureInfo.GetCultureInfo("en-US"));

    private static string Percent(object? value)
    {
        if (value is null)
        {
            return "—";
        }

        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return number.ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
