using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Tree table's completeness preview: an expanded hierarchy with a mixed selection and a lazily loaded row,
/// the same hierarchy collapsed to its roots, and an expanded table with the summary footer switched off.
/// Every declared state is on screen at once; the hierarchy is a fixed sample.
/// </summary>
public sealed class TreeTableItemPage : ArcItemPage
{
    public override string ItemSlug => "tree-table";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-expanded", "variant-collapsed", "state-selected", "state-no-summary", "variant-lazy"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, Width = 820, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Section(
            "展开层级、三态选择与按需加载",
            "Product 只有一个子行被选中，因此显示为混合态；Cloud archive 带子行标记但数据按需加载，第一次展开时才请求。",
            BuildTable(expanded: true, selected: true, summary: true)));

        panel.Children.Add(Section(
            "全部收起",
            "DefaultExpandedIds 为空时只显示根行；右箭头或点击可逐层展开。",
            BuildTable(expanded: false, selected: false, summary: true)));

        panel.Children.Add(Section(
            "无页脚汇总",
            "ShowSummary=false：收起行数与选中数汇总，只留表格本身。",
            BuildTable(expanded: true, selected: false, summary: false)));

        return panel;
    }

    private static Control Section(string title, string note, Control table)
    {
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(DemoUi.Label(title));
        var caption = DemoUi.Caption(note);
        caption.TextWrapping = TextWrapping.Wrap;
        caption.MaxWidth = 820;
        panel.Children.Add(caption);
        panel.Children.Add(table);
        return panel;
    }

    private static Control BuildTable(bool expanded, bool selected, bool summary)
    {
        var table = new ArcTreeTable
        {
            Columns = new List<ArcTreeTableColumn>
            {
                new() { Key = "project", Header = "Project", Width = 0, MinWidth = 240 },
                new() { Key = "owner", Header = "Owner", Width = 150, Sortable = false },
                new() { Key = "status", Header = "Status", Width = 120, Sortable = false },
                new() { Key = "budget", Header = "Budget", Width = 140, Align = ArcTreeTableAlign.Right, Format = Money }
            },
            Rows = BuildRows(),
            Label = "Q3 project budgets",
            Selectable = selected,
            DefaultExpandedIds = expanded ? new[] { "product", "atlas", "marketing", "brand" } : Array.Empty<string>(),
            DefaultSelectedIds = selected ? new[] { "atlas" } : Array.Empty<string>(),
            DefaultSort = new ArcTreeTableSort { Key = "budget", Descending = true },
            LoadChildren = LoadArchiveAsync,
            MaxBodyHeight = 0,
            ShowSummary = summary
        };

        return table;
    }

    private static Task<IReadOnlyList<ArcTreeTableRow>> LoadArchiveAsync(ArcTreeTableRow row) =>
        Task.FromResult<IReadOnlyList<ArcTreeTableRow>>(Array.Empty<ArcTreeTableRow>());

    private static List<ArcTreeTableRow> BuildRows() => new()
    {
        Row("product", "Product", "Maya Chen", "Live", 66750,
            Row("atlas", "Atlas redesign", "Maya Chen", "In progress", 48250,
                Row("research", "Research", "Ines Ma", "Done", 18000),
                Row("design-system", "Design system", "Sara Ruiz", "In progress", 30250)),
            Row("billing", "Billing migration", "Leo Park", "Review", 18500)),
        Row("marketing", "Marketing", "Leo Park", "Live", 63400,
            Row("brand", "Brand refresh", "Ines Ma", "In progress", 41000,
                Row("naming", "Naming", "Ada Lin", "Review", 16000),
                Row("illustration", "Illustration", "Noor Haddad", "In progress", 25000)),
            Row("launch", "Launch site", "Tom Hale", "Planned", 22400)),
        new ArcTreeTableRow
        {
            Id = "cloud-archive",
            Label = "Cloud archive",
            HasChildren = true,
            ["project"] = "Cloud archive",
            ["owner"] = "—",
            ["status"] = "On demand",
            ["budget"] = null
        }
    };

    private static ArcTreeTableRow Row(
        string id, string project, string owner, string status, double budget, params ArcTreeTableRow[] children)
    {
        var row = new ArcTreeTableRow { Id = id, Label = project };
        row["project"] = project;
        row["owner"] = owner;
        row["status"] = status;
        row["budget"] = budget;
        row.Children.AddRange(children);
        return row;
    }

    private static string Money(object? value)
    {
        if (value is null)
        {
            return "—";
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    }
}
