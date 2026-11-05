using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Tag input's completeness preview: the three sizes, empty / filled / error / disabled states.
/// Declared state keys match Docs/tag-input.json.
/// </summary>
public sealed class TagInputPage : ArcItemPage
{
    public override string ItemSlug => "tag-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-filled", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcTagInput
            {
                Label = "Small",
                Placeholder = "添加标签",
                Value = new[] { "design" },
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcTagInput
            {
                Label = "Medium",
                Placeholder = "添加标签",
                Value = new[] { "design", "motion" }
            }),
            Field(new ArcTagInput
            {
                Label = "Large",
                Placeholder = "添加标签",
                Value = new[] { "design", "motion", "tokens" },
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("状态",
            Field(new ArcTagInput
            {
                Label = "空",
                Placeholder = "输入后按 Enter 添加",
                Description = "Enter 或逗号添加。"
            }),
            Field(new ArcTagInput
            {
                Label = "已填写",
                Value = new[] { "design", "motion", "tokens" },
                Placeholder = "添加标签",
                Description = "每枚标签都可单独删除。"
            }),
            Field(new ArcTagInput
            {
                Label = "错误",
                Value = new[] { "too-long-tag" },
                Placeholder = "添加标签",
                Error = "单个标签不能超过 12 个字符。"
            }),
            Field(new ArcTagInput
            {
                Label = "禁用",
                Value = new[] { "fixed" },
                Placeholder = "添加标签",
                IsEnabled = false
            })));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 14);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    private static T Field<T>(T control, double width = 252)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
