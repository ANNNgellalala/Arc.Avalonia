using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Switch's completeness preview: the off and on forms, the labelled and bare forms and the disabled state
/// (both on and off). Declared state keys match Docs/switch.json.
/// </summary>
public sealed class SwitchPage : ArcItemPage
{
    public override string ItemSlug => "switch";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-off", "variant-on", "variant-label", "variant-bare", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("开与关",
            new ArcSwitch { Label = "邮件通知", Checked = false },
            new ArcSwitch { Label = "产品更新", Checked = true }));

        panel.Children.Add(Row("标签形态",
            new ArcSwitch { Label = "自动保存", Checked = true },
            new ArcSwitch { Checked = true }));

        panel.Children.Add(Row("禁用",
            new ArcSwitch { Label = "安全提醒", Checked = true, IsEnabled = false },
            new ArcSwitch { Label = "每周摘要", Checked = false, IsEnabled = false }));

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
}
