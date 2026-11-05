using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Search field's completeness preview: three sizes, empty / filled / disabled states and the shortcut hint.
/// Declared state keys match Docs/search-field.json.
/// </summary>
public sealed class SearchFieldPage : ArcItemPage
{
    public override string ItemSlug => "search-field";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-filled", "variant-shortcut", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcSearchField
            {
                Label = "Small",
                Placeholder = "搜索成员",
                Size = ArcFieldSize.Sm
            }),
            Field(new ArcSearchField
            {
                Label = "Medium",
                Placeholder = "搜索成员"
            }),
            Field(new ArcSearchField
            {
                Label = "Large",
                Placeholder = "搜索成员",
                Size = ArcFieldSize.Lg
            })));

        panel.Children.Add(Row("状态",
            Field(new ArcSearchField
            {
                Label = "空",
                Placeholder = "姓名或邮箱",
                Description = "清除槽位预留但不显示按钮。"
            }),
            Field(new ArcSearchField
            {
                Label = "有查询",
                Value = "INV-2041",
                Description = "出现清除按钮，宽度不跳动。"
            }),
            Field(new ArcSearchField
            {
                Label = "快捷键提示",
                Placeholder = "搜索命令",
                ShortcutHint = "⌘K",
                Description = "有输入后提示让位给清除按钮。"
            }),
            Field(new ArcSearchField
            {
                Label = "禁用",
                Value = "由管理员锁定",
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

    private static T Field<T>(T control, double width = 256)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
