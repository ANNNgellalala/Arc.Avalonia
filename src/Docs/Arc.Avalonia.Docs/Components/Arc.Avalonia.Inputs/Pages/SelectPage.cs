using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Select's completeness preview: the three field sizes plus the empty, selected, error and disabled states.
/// The menu is not opened here so the preview stays an inline, screenshot-safe composition; opening is
/// exercised by the demo itself. Declared state keys match Docs/select.json.
/// </summary>
public sealed class SelectPage : ArcItemPage
{
    private static readonly ArcSelectOption[] Regions =
    {
        new ArcSelectOption("us", "United States"),
        new ArcSelectOption("eu", "Europe"),
        new ArcSelectOption("ap", "Asia Pacific", disabled: true)
    };

    public override string ItemSlug => "select";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-selected", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcSelect { Label = "Small", Options = Regions, Value = "eu", Size = ArcFieldSize.Sm }),
            Field(new ArcSelect { Label = "Medium", Options = Regions, Value = "eu", Size = ArcFieldSize.Md }),
            Field(new ArcSelect { Label = "Large", Options = Regions, Value = "eu", Size = ArcFieldSize.Lg })));

        panel.Children.Add(Row("空态与已选",
            Field(new ArcSelect
            {
                Label = "未选择",
                Placeholder = "请选择地区",
                Options = Regions
            }),
            Field(new ArcSelect
            {
                Label = "已选择",
                Options = Regions,
                Value = "eu",
                Description = "浮层里的高亮块会滑到选中行。"
            })));

        panel.Children.Add(Row("错误与禁用",
            Field(new ArcSelect
            {
                Label = "错误",
                Options = Regions,
                Value = "us",
                Error = "请选择一个地区。"
            }),
            Field(new ArcSelect
            {
                Label = "禁用",
                Options = Regions,
                Value = "us",
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

    private static ArcSelect Field(ArcSelect select)
    {
        select.Width = 240;
        return select;
    }
}
