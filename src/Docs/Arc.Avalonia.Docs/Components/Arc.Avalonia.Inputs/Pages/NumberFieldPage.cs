using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Number field's completeness preview: the three sizes, prefix and suffix variants, bounded and float-step
/// values, plus error and disabled states. Declared state keys match Docs/number-field.json.
/// </summary>
public sealed class NumberFieldPage : ArcItemPage
{
    public override string ItemSlug => "number-field";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "variant-prefix", "variant-suffix",
        "state-range", "state-float", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcNumberField
            {
                Label = "Small",
                Value = 5,
                Min = 1,
                Max = 500,
                Suffix = " seats",
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcNumberField
            {
                Label = "Medium",
                Value = 5,
                Min = 1,
                Max = 500,
                Suffix = " seats"
            }),
            Field(new ArcNumberField
            {
                Label = "Large",
                Value = 5,
                Min = 1,
                Max = 500,
                Suffix = " seats",
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("前后缀与边界",
            Field(new ArcNumberField
            {
                Label = "预算金额",
                Value = 1250,
                Min = 0,
                Max = 100000,
                Step = 10,
                Prefix = "$",
                Description = "方向键按 10 调整。"
            }),
            Field(new ArcNumberField
            {
                Label = "座位数",
                Value = 42,
                Min = 1,
                Max = 500,
                Suffix = " seats",
                Description = "长按步进按钮连续调整。"
            }),
            Field(new ArcNumberField
            {
                Label = "重试次数",
                Value = 5,
                Min = 1,
                Max = 5,
                Suffix = " 次",
                Description = "已到上限，增加按钮变灰。"
            })));

        panel.Children.Add(Row("小数与状态",
            Field(new ArcNumberField
            {
                Label = "不透明度",
                Value = 60,
                Min = 0,
                Max = 100,
                Step = 0.5,
                Suffix = "%",
                Description = "Step 0.5，保留一位小数。"
            }),
            Field(new ArcNumberField
            {
                Label = "折扣",
                Value = 120,
                Min = 0,
                Max = 100,
                Suffix = "%",
                Error = "折扣不能超过 100。"
            }),
            Field(new ArcNumberField
            {
                Label = "席位数",
                Value = 42,
                Min = 1,
                Max = 500,
                Suffix = " seats",
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

    private static T Field<T>(T control, double width = 216)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
