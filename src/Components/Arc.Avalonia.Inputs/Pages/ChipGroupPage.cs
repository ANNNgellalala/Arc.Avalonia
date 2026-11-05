using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Chip group's completeness preview: multi-select, the clearable single mode, the +N collapsed form and the
/// no-filter state. Declared state keys match Docs/chip-group.json.
/// </summary>
public sealed class ChipGroupPage : ArcItemPage
{
    private static readonly ArcChipOption[] Topics =
    {
        new("design", "Design"),
        new("motion", "Motion"),
        new("code", "Code"),
        new("research", "Research"),
        new("writing", "Writing")
    };

    private static readonly ArcChipOption[] Statuses =
    {
        new("open", "Open"),
        new("closed", "Closed"),
        new("archived", "Archived")
    };

    public override string ItemSlug => "chip-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-multi", "variant-single", "variant-collapsed", "variant-none"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("多选",
            new ArcChipGroup
            {
                Label = "Topics",
                Multiple = true,
                MaxVisible = 5,
                Value = new[] { "design", "motion" },
                Options = Topics
            }));

        panel.Children.Add(Row("单选，再点一次清空",
            new ArcChipGroup
            {
                Label = "Status",
                Multiple = false,
                Value = new[] { "open" },
                Options = Statuses
            }));

        panel.Children.Add(Row("超出 MaxVisible 折叠",
            new ArcChipGroup
            {
                Label = "Topics",
                Multiple = true,
                MaxVisible = 3,
                Value = new[] { "design", "writing" },
                Options = Topics
            }));

        panel.Children.Add(Row("没有任何筛选",
            new ArcChipGroup
            {
                Label = "Topics",
                Multiple = true,
                MaxVisible = 5,
                Options = Topics
            }));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 12, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
