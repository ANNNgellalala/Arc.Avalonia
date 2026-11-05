using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Password strength's completeness preview: empty, too short, all four strength levels, revealed and the
/// external error state. Declared state keys match Docs/password-strength.json.
/// </summary>
public sealed class PasswordStrengthPage : ArcItemPage
{
    public override string ItemSlug => "password-strength";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-short", "state-weak", "state-fair",
        "state-good", "state-strong", "state-revealed", "state-error"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("强度等级",
            Field(new ArcPasswordStrength
            {
                Label = "空",
                Placeholder = "输入新密码",
                Description = "未输入时强度词隐藏。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "太短",
                Value = "maya",
                ShortLength = 8,
                Description = "低于 8 个字符，停在第一段。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "弱",
                Value = "mayachen",
                Description = "只满足长度这一条规则。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "一般",
                Value = "Mayachen",
                Description = "满足长度与大小写两条。"
            })));

        panel.Children.Add(Row("继续加强",
            Field(new ArcPasswordStrength
            {
                Label = "好",
                Value = "Maya2026",
                Description = "长度、大小写与数字三条。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "强",
                Value = "Maya-2026",
                Description = "四条规则全部满足。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "明文",
                Value = "Maya-2026",
                Revealed = true,
                Description = "眼睛图标切换为 eye-off。"
            }),
            Field(new ArcPasswordStrength
            {
                Label = "外部错误",
                Value = "Maya-2026",
                Error = "该密码已在此前泄露，请换一个。"
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

    private static T Field<T>(T control, double width = 262)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
