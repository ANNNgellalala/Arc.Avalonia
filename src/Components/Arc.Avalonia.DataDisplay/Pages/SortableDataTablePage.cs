using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Sortable data table's completeness preview: the base table, a table with rows already selected, a
/// read-only table with the selection model switched off, and the empty state. All four are on screen at
/// once, and the rows are the fixed project sample so the preview is deterministic.
/// </summary>
public sealed class SortableDataTablePage : ArcItemPage
{
    public override string ItemSlug => "sortable-data-table";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-basic", "variant-selected", "state-no-selection", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, Width = 800, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Section(
            "基础表格",
            "点击表头在升序 → 降序 → 关闭之间循环；Budget 列右对齐并使用等宽数字，Updated 列不参与排序。",
            BuildTable(selectable: true, selected: null, empty: false)));

        panel.Children.Add(Section(
            "预选行",
            "SelectedKeys 预设 p2 与 p5，页脚同步给出选中计数；Shift 点击可以连续选择。",
            BuildTable(selectable: true, selected: new[] { "p2", "p5" }, empty: false)));

        panel.Children.Add(Section(
            "只读、无选择器",
            "Selectable=false：去掉复选框列与页脚计数，只保留排序表头。",
            BuildTable(selectable: false, selected: null, empty: false)));

        panel.Children.Add(Section(
            "空态",
            "Rows 为空时保留表头并显示 EmptyMessage，用一句话说明下一步。",
            BuildTable(selectable: false, selected: null, empty: true)));

        return panel;
    }

    private static Control Section(string title, string note, Control table)
    {
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(DemoUi.Label(title));
        var caption = DemoUi.Caption(note);
        caption.TextWrapping = TextWrapping.Wrap;
        caption.MaxWidth = 800;
        panel.Children.Add(caption);
        panel.Children.Add(table);
        return panel;
    }

    private static Control BuildTable(bool selectable, IReadOnlyList<string>? selected, bool empty)
    {
        var table = new ArcSortableDataTable
        {
            Columns = Columns(),
            Rows = empty ? Array.Empty<object>() : Rows(),
            Caption = empty ? "Archived projects" : "Projects",
            Selectable = selectable,
            ItemName = new ArcTableItemName { One = "project", Other = "projects" },
            EmptyMessage = "No archived projects",
            DefaultSort = new ArcTableSort { Key = "name", Direction = ArcSortDirection.Asc }
        };

        if (selected is not null)
        {
            table.SelectedKeys = selected;
        }

        return table;
    }

    private static IEnumerable<ArcTableColumn> Columns() => new[]
    {
        new ArcTableColumn { Key = "name", Label = "Project" },
        new ArcTableColumn { Key = "owner", Label = "Owner", Width = 150, Sortable = false },
        new ArcTableColumn { Key = "status", Label = "Status", Width = 110, Sortable = false },
        new ArcTableColumn
        {
            Key = "budget",
            Label = "Budget",
            Width = 130,
            Numeric = true,
            Format = Money
        },
        new ArcTableColumn { Key = "updated", Label = "Updated", Width = 120, Sortable = false }
    };

    private static List<object> Rows() => new()
    {
        Row("p1", "Harbour", "Maya Chen", "Open", 42000, "2h ago"),
        Row("p2", "Atlas", "Leo Park", "Blocked", 18500, "4h ago"),
        Row("p3", "Harbour analytics", "Sara Ruiz", "Open", 86000, "Yesterday"),
        Row("p4", "Northwind", "Tom Hale", "Closed", 12800, "2d ago"),
        Row("p5", "Lattice", "Ines Ma", "Open", 54000, "3d ago"),
        Row("p6", "Orchard", "Leo Park", "Closed", 9700, "5d ago")
    };

    private static Dictionary<string, object?> Row(
        string id, string name, string owner, string status, double budget, string updated) => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["owner"] = owner,
        ["status"] = status,
        ["budget"] = budget,
        ["updated"] = updated
    };

    private static string Money(object? value) => value is null
        ? "—"
        : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("C0", CultureInfo.GetCultureInfo("en-US"));
}
