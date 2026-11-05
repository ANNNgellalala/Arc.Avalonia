using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Morph select's completeness preview: the closed trigger with and without a value, the open surface, the
/// grouped list and the searchable form. Declared state keys match Docs/morph-select.json.
/// </summary>
public sealed class MorphSelectPage : ArcItemPage
{
    public override string ItemSlug => "morph-select";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-closed", "variant-placeholder", "variant-open", "variant-grouped", "variant-searchable"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("关闭态",
            Frame("已选择", 150, new ArcMorphSelect
            {
                Width = 200,
                Label = "时区",
                Value = "europe/zurich",
                Items = Zones()
            }),
            Frame("未选择", 150, new ArcMorphSelect
            {
                Width = 200,
                Label = "负责人",
                Placeholder = "Unassigned",
                Items = People()
            })));

        panel.Children.Add(Row("展开态",
            Frame("展开：高亮停在当前值", 360, new ArcMorphSelect
            {
                Width = 240,
                Label = "负责人",
                IsOpen = true,
                PanelWidth = 240,
                Value = "u2",
                Items = People()
            }),
            Frame("分组标题", 360, new ArcMorphSelect
            {
                Width = 240,
                Label = "区域",
                IsOpen = true,
                PanelWidth = 240,
                MaxListHeight = 280,
                Value = "america/new_york",
                Items = Zones()
            }),
            Frame("展开时换成搜索框", 360, new ArcMorphSelect
            {
                Width = 240,
                Label = "时区",
                IsOpen = true,
                Searchable = true,
                SearchPlaceholder = "搜索城市",
                PanelWidth = 240,
                Items = Zones()
            })));

        return panel;
    }

    private static List<ArcMorphSelectItem> Zones() => new()
    {
        new ArcMorphSelectGroup(
            "Europe",
            new ArcMorphSelectOption("europe/london", "London", "UTC+0", "united kingdom"),
            new ArcMorphSelectOption("europe/zurich", "Zurich", "UTC+1", "switzerland"),
            new ArcMorphSelectOption("europe/lisbon", "Lisbon", "UTC+0", "portugal")),
        new ArcMorphSelectGroup(
            "Americas",
            new ArcMorphSelectOption("america/new_york", "New York", "UTC−5", "united states eastern"),
            new ArcMorphSelectOption("america/chicago", "Chicago", "UTC−6", "central"),
            new ArcMorphSelectOption("america/los_angeles", "Los Angeles", "UTC−8", "pacific"))
    };

    private static List<ArcMorphSelectItem> People() => new()
    {
        new ArcMorphSelectOption("u1", "Maya Chen", "Design"),
        new ArcMorphSelectOption("u2", "Theo Park", "Engineering"),
        new ArcMorphSelectOption("u3", "Priya Raman", "Product")
    };

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 16, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }

    private static Control Frame(string caption, double height, Control content)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" } });
        stack.Children.Add(content);
        return new Border { Width = 240, Height = height, Child = stack };
    }
}
