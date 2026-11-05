using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Multi-select's completeness preview: the three field sizes, an empty trigger, a trigger with chips, the
/// +N overflow form, the error state and the disabled state. Declared keys match Docs/multi-select.json.
/// </summary>
public sealed class MultiSelectPage : ArcItemPage
{
    private static readonly ArcSelectOption[] Labels =
    {
        new("bug", "Bug"),
        new("feature", "Feature"),
        new("docs", "Docs"),
        new("design", "Design")
    };

    public override string ItemSlug => "multi-select";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-selected", "state-overflow", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcMultiSelect
            {
                Label = "Small",
                Options = Labels,
                Value = new[] { "bug", "feature" },
                MaxVisible = 2,
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcMultiSelect
            {
                Label = "Medium",
                Options = Labels,
                Value = new[] { "bug", "feature" },
                MaxVisible = 2,
                Size = ArcFieldSize.Md
            }),
            Field(new ArcMultiSelect
            {
                Label = "Large",
                Options = Labels,
                Value = new[] { "bug", "feature" },
                MaxVisible = 2,
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("已选与折叠",
            Field(new ArcMultiSelect
            {
                Label = "已选择",
                Options = Labels,
                Value = new[] { "bug", "docs" },
                MaxVisible = 2,
                Description = "标签顺序跟随 Options，而不是点击顺序。"
            }),
            Field(new ArcMultiSelect
            {
                Label = "折叠为 +N",
                Options = Labels,
                Value = new[] { "bug", "feature", "docs", "design" },
                MaxVisible = 2,
                Description = "只显示前两枚标签，其余折叠。"
            })));

        panel.Children.Add(Row("空态、错误与禁用",
            Field(new ArcMultiSelect
            {
                Label = "未选择",
                Placeholder = "选择标签",
                Options = Labels
            }),
            Field(new ArcMultiSelect
            {
                Label = "错误",
                Options = Labels,
                Value = new[] { "bug" },
                Error = "至少选择两个标签。"
            }),
            Field(new ArcMultiSelect
            {
                Label = "禁用",
                Options = Labels,
                Value = new[] { "design" },
                IsEnabled = false
            })));

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

    private static ArcMultiSelect Field(ArcMultiSelect select)
    {
        select.Width = 280;
        return select;
    }
}
