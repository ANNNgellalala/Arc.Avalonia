using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Treemap's completeness preview: revenue by region with values, budget by account with its own format,
/// the regional map without inner values and the empty state. Declared state keys match Docs/treemap.json so
/// the audit can compare the two.
/// </summary>
public sealed class TreemapPage : ArcItemPage
{
    public override string ItemSlug => "treemap";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-regions", "variant-budget", "state-no-values", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("地区营收", "六个地区的营收占比，块内显示金额", Regions(showValues: true)),
            Card("预算分配", "五个预算科目的分配，单位换成千美元", Budget())));

        panel.Children.Add(Row(
            Card("只显示名称", "ShowValues=false，块内只留名称", Regions(showValues: false)),
            Card("空数据", "Items 为空时画出 No data", Empty())));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 0);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, ArcTreemap treemap)
    {
        treemap.Width = 340;
        treemap.Height = 190;
        return DemoUi.Card(title, note, treemap);
    }

    private static ArcTreemap Regions(bool showValues) => new()
    {
        Items = ChartsData.RevenueMap,
        ShowValues = showValues,
        ValueFormat = DollarsK
    };

    private static ArcTreemap Budget() => new()
    {
        Items = ChartsData.Budget,
        ValueFormat = DollarsThousands
    };

    private static ArcTreemap Empty() => new()
    {
        Items = Array.Empty<ChartValue>()
    };

    private static string DollarsK(double value)
        => "$" + (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k";

    private static string DollarsThousands(double value)
        => "$" + value.ToString("N0", CultureInfo.InvariantCulture) + "k";
}
