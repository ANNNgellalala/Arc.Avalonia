using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Money input's completeness preview: USD / EUR / JPY formatting, quick-add chips, empty / filled / error /
/// disabled states. Declared state keys match Docs/money-input.json.
/// </summary>
public sealed class MoneyInputPage : ArcItemPage
{
    public override string ItemSlug => "money-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-usd", "variant-eur", "variant-jpy", "variant-quick-add",
        "state-empty", "state-filled", "state-error", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("币种",
            Field(new ArcMoneyInput
            {
                Label = "USD",
                Currency = "USD",
                Value = 125000,
                Description = "125000 表示 $1,250.00。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "EUR",
                Currency = "EUR",
                Value = 9900,
                Description = "欧元符号与两位小数。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "JPY",
                Currency = "JPY",
                Value = 4800,
                Description = "日元不保留小数位。"
            })));

        panel.Children.Add(Row("状态与快捷金额",
            Field(new ArcMoneyInput
            {
                Label = "空值",
                Currency = "USD",
                Description = "占位显示 $0.00。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "已填写",
                Currency = "USD",
                Value = 2500,
                Description = "键入时实时千分位分组。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "错误",
                Currency = "USD",
                Value = 100,
                Min = 1000,
                Error = "至少充值 $10.00。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "快捷金额",
                Currency = "CHF",
                Value = 12345678,
                QuickAdd = new[] { 100, 500 },
                Description = "下方加量按主单位。"
            }),
            Field(new ArcMoneyInput
            {
                Label = "禁用",
                Currency = "GBP",
                Value = 4999,
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
            child.Margin = new Thickness(0, 0, 10, 16);
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
