using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Button's completeness preview: every variant, all three sizes, the loading and disabled states and the
/// compact icon form. Declared state keys match Docs/button.json so the audit can compare the two.
/// </summary>
public sealed class ButtonPage : ArcItemPage
{
    public override string ItemSlug => "button";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-primary", "variant-secondary", "variant-ghost", "variant-danger",
        "size-sm", "size-md", "size-lg",
        "state-loading", "state-disabled", "state-icon"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("变体",
            Make("保存", ArcButtonVariant.Primary),
            Make("取消", ArcButtonVariant.Secondary),
            Make("稍后", ArcButtonVariant.Ghost),
            Make("删除", ArcButtonVariant.Danger)));

        panel.Children.Add(Row("尺寸",
            Sized(ArcButtonSize.Sm, "Small"),
            Sized(ArcButtonSize.Md, "Medium"),
            Sized(ArcButtonSize.Lg, "Large")));

        panel.Children.Add(Row("状态",
            new ArcButton { Content = "保存", Variant = ArcButtonVariant.Primary, Loading = true },
            new ArcButton { Content = "不可用", Variant = ArcButtonVariant.Secondary, IsEnabled = false },
            new ArcButton
            {
                Content = new ArcIcon { Icon = "plus", Size = 16 },
                Variant = ArcButtonVariant.Secondary,
                Compact = true
            }));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }

    private static ArcButton Sized(ArcButtonSize size, string label) => new()
    {
        Content = label,
        Variant = ArcButtonVariant.Secondary,
        Size = size
    };

    private static ArcButton Make(string label, ArcButtonVariant variant) => new()
    {
        Content = label,
        Variant = variant,
        Size = ArcButtonSize.Md
    };
}
