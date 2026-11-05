using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Textarea's completeness preview: the three sizes, the default / filled / counter / error / disabled
/// states and the auto-grow behaviour. Declared state keys match Docs/textarea.json.
/// </summary>
public sealed class TextareaPage : ArcItemPage
{
    public override string ItemSlug => "textarea";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-default", "state-filled", "state-counter", "state-autogrow", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcTextarea
            {
                Label = "Small",
                Placeholder = "36 px 基线",
                Rows = 3,
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcTextarea
            {
                Label = "Medium",
                Placeholder = "默认尺寸",
                Rows = 3
            }),
            Field(new ArcTextarea
            {
                Label = "Large",
                Placeholder = "50 px 基线",
                Rows = 3,
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("状态",
            Field(new ArcTextarea
            {
                Label = "默认",
                Placeholder = "补充说明，例如复现步骤。",
                Rows = 3,
                Description = "帮助文案显示在字段下方。"
            }),
            Field(new ArcTextarea
            {
                Label = "已填写",
                Value = "修复了导入流程，\n并整理了空状态。",
                Rows = 3,
                Description = "换行按原样保留。"
            }),
            Field(new ArcTextarea
            {
                Label = "字数限制",
                Value = "修复了导入流程，并整理了空状态。",
                Rows = 3,
                ShowCounter = true,
                MaxLength = 280,
                Description = "超出上限后无法继续输入。"
            }),
            Field(new ArcTextarea
            {
                Label = "错误",
                Value = "太短",
                Rows = 3,
                Error = "至少写 10 个字符。"
            }),
            Field(new ArcTextarea
            {
                Label = "禁用",
                Value = "由管理员统一设置",
                Rows = 3,
                IsEnabled = false
            })));

        panel.Children.Add(Row("自动增长",
            Field(new ArcTextarea
            {
                Label = "随内容长高",
                Rows = 2,
                AutoGrow = true,
                Value = "修复了导入流程，\n并整理了空状态。\n补充了键盘操作说明。",
                Description = "输入区随内容长高，不出现内部滚动条。"
            }, 320)));

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

    private static T Field<T>(T control, double width = 236)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
