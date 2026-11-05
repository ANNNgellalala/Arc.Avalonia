using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Checkbox's completeness preview: the three values (unchecked, checked, indeterminate), the label-only and
/// label-plus-description forms and the disabled state. Declared keys match Docs/checkbox.json.
/// </summary>
public sealed class CheckboxPage : ArcItemPage
{
    public override string ItemSlug => "checkbox";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-unchecked", "state-checked", "state-indeterminate",
        "variant-label", "variant-description", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("三态",
            Column(
                new ArcCheckbox
                {
                    Checked = false,
                    Label = "Weekly digest",
                    Description = "每周五发送。"
                },
                new ArcCheckbox
                {
                    Checked = true,
                    Label = "Email notifications",
                    Description = "每天 9:00 发送摘要。"
                },
                new ArcCheckbox
                {
                    Checked = null,
                    Label = "Select all projects",
                    Description = "部分项目已勾选。"
                })));

        panel.Children.Add(Row("标签形态",
            Column(
                new ArcCheckbox { Checked = true, Label = "只有标签" },
                new ArcCheckbox
                {
                    Checked = true,
                    Label = "标签加说明",
                    Description = "说明文字同样是点击区域的一部分。"
                })));

        panel.Children.Add(Row("禁用",
            Column(
                new ArcCheckbox
                {
                    Checked = true,
                    IsEnabled = false,
                    Label = "Security alerts",
                    Description = "管理员固定开启。"
                },
                new ArcCheckbox
                {
                    Checked = null,
                    IsEnabled = false,
                    Label = "Some projects",
                    Description = "禁用状态下仍显示中间态。"
                })));

        return panel;
    }

    private static Control Column(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 6, Width = 300 };
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return stack;
    }

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
}
