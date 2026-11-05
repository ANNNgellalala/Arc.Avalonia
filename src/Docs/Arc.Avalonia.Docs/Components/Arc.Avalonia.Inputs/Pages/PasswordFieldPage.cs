using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Password field's completeness preview: the three sizes, empty / masked / revealed / error / disabled
/// states. Declared state keys match Docs/password-field.json.
/// </summary>
public sealed class PasswordFieldPage : ArcItemPage
{
    public override string ItemSlug => "password-field";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-masked", "state-revealed", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcPasswordField
            {
                Label = "Small",
                Placeholder = "输入密码",
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcPasswordField
            {
                Label = "Medium",
                Placeholder = "输入密码"
            }),
            Field(new ArcPasswordField
            {
                Label = "Large",
                Placeholder = "输入密码",
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("状态",
            Field(new ArcPasswordField
            {
                Label = "默认",
                Placeholder = "输入密码",
                Description = "至少 8 个字符。"
            }),
            Field(new ArcPasswordField
            {
                Label = "遮罩",
                Value = "correct horse",
                Description = "圆点遮罩，内容不可读。"
            }),
            Field(new ArcPasswordField
            {
                Label = "明文",
                Value = "correct horse",
                Revealed = true,
                Description = "眼睛图标切换为 eye-off。"
            }),
            Field(new ArcPasswordField
            {
                Label = "错误",
                Value = "correct hors",
                Error = "两次输入不一致。"
            }),
            Field(new ArcPasswordField
            {
                Label = "禁用",
                Value = "由管理员设置",
                IsEnabled = false,
                Description = "禁用时仍以遮罩展示。"
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

    private static T Field<T>(T control, double width = 236)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
