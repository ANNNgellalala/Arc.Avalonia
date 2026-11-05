using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Filter toolbar's completeness preview: applied chips with a count, a toolbar with no filters at all, and
/// a toolbar with the search box switched off. All three are static, so the chips, count and menus read in a
/// still; the fields and sort options are fixed.
/// </summary>
public sealed class FilterToolbarPage : ArcItemPage
{
    public override string ItemSlug => "filter-toolbar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-filters", "variant-empty", "state-no-search"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, Width = 880, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Section(
            "已应用的筛选",
            "搜索词、两个可移除 chip、Add filter 与 Sort 菜单、结果计数都在一行，放不下时自动换行。",
            Toolbar(search: true, chips: true, count: 128)));

        panel.Children.Add(Section(
            "没有筛选条件",
            "Filters 为空、ResultCount 省略时只留搜索与添加筛选的入口，用作工具栏初始态。",
            Toolbar(search: true, chips: false, count: null)));

        panel.Children.Add(Section(
            "无搜索框",
            "ShowSearch=false：搜索框收起，chip、Add filter、Sort 与计数仍在。",
            Toolbar(search: false, chips: true, count: 42)));

        return panel;
    }

    private static Control Section(string title, string note, Control toolbar)
    {
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(DemoUi.Label(title));
        var caption = DemoUi.Caption(note);
        caption.TextWrapping = TextWrapping.Wrap;
        caption.MaxWidth = 880;
        panel.Children.Add(caption);
        panel.Children.Add(toolbar);
        return panel;
    }

    private static Control Toolbar(bool search, bool chips, int? count)
    {
        var toolbar = new ArcFilterToolbar
        {
            Width = 860,
            Label = "Project filters",
            ShowSearch = search,
            SearchText = search ? "design" : string.Empty,
            SearchPlaceholder = "Search projects",
            ResultCount = count,
            ResultLabel = "projects",
            SortKey = "updated",
            Fields = Fields(),
            SortOptions = new[]
            {
                new ArcSortOption { Key = "updated", Label = "Last updated" },
                new ArcSortOption { Key = "name", Label = "Name" },
                new ArcSortOption { Key = "budget", Label = "Budget" }
            }
        };

        if (chips)
        {
            toolbar.Filters = new[]
            {
                new ArcFilterChip { Id = "status", Label = "Status", Value = "Open" },
                new ArcFilterChip { Id = "owner", Label = "Owner", Value = "Maya" }
            };
        }
        else
        {
            toolbar.Filters = new List<ArcFilterChip>();
        }

        return toolbar;
    }

    private static IEnumerable<ArcFilterField> Fields() => new[]
    {
        new ArcFilterField
        {
            Id = "status",
            Label = "Status",
            Options = new List<ArcFilterOption>
            {
                new() { Value = "Open" },
                new() { Value = "Closed" },
                new() { Value = "Blocked" }
            }
        },
        new ArcFilterField
        {
            Id = "owner",
            Label = "Owner",
            Options = new List<ArcFilterOption>
            {
                new() { Value = "Maya" },
                new() { Value = "Leo" },
                new() { Value = "Sara" },
                new() { Value = "Tom" }
            }
        },
        new ArcFilterField
        {
            Id = "priority",
            Label = "Priority",
            Options = new List<ArcFilterOption>
            {
                new() { Value = "High", Label = "High priority" },
                new() { Value = "Medium", Label = "Medium priority" },
                new() { Value = "Low", Label = "Low priority" }
            }
        }
    };
}
