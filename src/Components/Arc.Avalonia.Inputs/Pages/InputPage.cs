using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Input's completeness preview: the three field sizes, the default / filled / error / disabled states and the
/// leading and trailing icon affordances. Declared state keys match Docs/input.json so the audit can compare
/// the two lists.
/// </summary>
public sealed class InputPage : ArcItemPage
{
    public override string ItemSlug => "input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-default", "state-filled", "state-error", "state-disabled",
        "variant-leading-icon", "variant-trailing-icon"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcInput { Label = "Small", Placeholder = "you@acme.com", Size = ArcFieldSize.Sm }),
            Field(new ArcInput { Label = "Medium", Placeholder = "you@acme.com", Size = ArcFieldSize.Md }),
            Field(new ArcInput { Label = "Large", Placeholder = "you@acme.com", Size = ArcFieldSize.Lg })));

        panel.Children.Add(Row("状态",
            Field(new ArcInput
            {
                Label = "默认",
                Placeholder = "you@acme.com",
                Description = "帮助文案显示在字段下方。"
            }),
            Field(new ArcInput
            {
                Label = "已填写",
                Value = "maya@acme.com",
                Description = "有值时右侧出现清除按钮。"
            }),
            Field(new ArcInput
            {
                Label = "错误",
                Value = "maya@acme",
                Error = "请输入有效的邮箱地址。"
            })));

        panel.Children.Add(Row("禁用与图标",
            Field(new ArcInput
            {
                Label = "禁用",
                Value = "由管理员设置",
                IsEnabled = false
            }),
            Field(new ArcInput
            {
                Label = "前置图标",
                Value = "Northwind",
                LeadingIcon = "briefcase"
            }),
            Field(new ArcInput
            {
                Label = "后置图标",
                Value = "42",
                TrailingIcon = "users",
                Description = "最多 500 个席位。"
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

    private static ArcInput Field(ArcInput input)
    {
        input.Width = 240;
        return input;
    }
}
