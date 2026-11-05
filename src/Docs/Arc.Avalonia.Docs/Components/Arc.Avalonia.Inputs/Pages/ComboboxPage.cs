using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Combobox's completeness preview: the three field sizes plus the empty, selected, error and disabled
/// states. Declared state keys match Docs/combobox.json so the audit can compare the two lists.
/// </summary>
public sealed class ComboboxPage : ArcItemPage
{
    private static readonly ArcComboboxOption[] TimeZones =
    {
        new("utc", "UTC"),
        new("cet", "Central European", keywords: new[] { "zurich", "berlin" }),
        new("pst", "Pacific", keywords: new[] { "san francisco" }),
        new("jst", "Japan", keywords: new[] { "tokyo" })
    };

    public override string ItemSlug => "combobox";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-empty", "state-selected", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcCombobox { Label = "Small", Options = TimeZones, Value = "cet", Size = ArcFieldSize.Sm }),
            Field(new ArcCombobox { Label = "Medium", Options = TimeZones, Value = "cet", Size = ArcFieldSize.Md }),
            Field(new ArcCombobox { Label = "Large", Options = TimeZones, Value = "cet", Size = ArcFieldSize.Lg })));

        panel.Children.Add(Row("空态与已选",
            Field(new ArcCombobox
            {
                Label = "未选择",
                Placeholder = "搜索或选择…",
                Options = TimeZones,
                Description = "键入即过滤，焦点留在字段里。"
            }),
            Field(new ArcCombobox
            {
                Label = "已选择",
                Options = TimeZones,
                Value = "pst",
                Description = "右侧清除按钮可丢弃当前值。"
            })));

        panel.Children.Add(Row("错误与禁用",
            Field(new ArcCombobox
            {
                Label = "错误",
                Options = TimeZones,
                Value = "utc",
                Error = "请选择一个有效时区。"
            }),
            Field(new ArcCombobox
            {
                Label = "禁用",
                Options = TimeZones,
                Value = "pst",
                IsEnabled = false
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

    private static ArcCombobox Field(ArcCombobox combobox)
    {
        combobox.Width = 260;
        return combobox;
    }
}
