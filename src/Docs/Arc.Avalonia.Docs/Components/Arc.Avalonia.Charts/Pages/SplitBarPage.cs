using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Split bar's completeness preview: the budget split with its legend, a two-way split, the budget bar
/// without its legend and the empty state. Declared state keys match Docs/split-bar.json so the audit can
/// compare the two.
/// </summary>
public sealed class SplitBarPage : ArcItemPage
{
    public override string ItemSlug => "split-bar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-budget", "variant-two-way", "state-no-legend", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("预算构成", "五个科目，图例给出百分比", Budget(showLegend: true)),
            Card("两段构成", "只分「回访」与「新访」两段", TwoWay())));

        panel.Children.Add(Row(
            Card("无图例", "ShowLegend=false，只留分割条与读数", Budget(showLegend: false)),
            Card("空数据", "Segments 为空时画出 No data", Empty())));

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

    private static Control Card(string title, string note, ArcSplitBar bar)
    {
        bar.Width = 340;
        bar.Height = 180;
        return DemoUi.Card(title, note, bar);
    }

    private static ArcSplitBar Budget(bool showLegend) => new()
    {
        Segments = ChartsData.Budget,
        TotalLabel = "Q4 budget",
        Unit = "$",
        ShowLegend = showLegend,
        ValueFormat = DollarsThousands
    };

    private static ArcSplitBar TwoWay() => new()
    {
        Segments = new[]
        {
            new ChartValue("Returning", 68),
            new ChartValue("New", 32)
        },
        TotalLabel = "Sessions",
        Unit = "visits"
    };

    private static ArcSplitBar Empty() => new()
    {
        Segments = Array.Empty<ChartValue>(),
        TotalLabel = "Q4 budget"
    };

    private static string DollarsThousands(double value)
        => "$" + value.ToString("N0", CultureInfo.InvariantCulture) + "k";
}
