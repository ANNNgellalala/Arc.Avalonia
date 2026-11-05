using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Waffle chart's completeness preview: a five-group mix, a balanced four-way split, the mix without its
/// legend and the empty state. Declared state keys match Docs/waffle-chart.json so the audit can compare
/// the two.
/// </summary>
public sealed class WaffleChartPage : ArcItemPage
{
    public override string ItemSlug => "waffle-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-mix", "variant-balanced", "state-no-legend", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("五组构成", "电力结构，图例给出各组百分比", Mix(showLegend: true)),
            Card("四等分", "四组各占 25%，格子均分", Balanced())));

        panel.Children.Add(Row(
            Card("无图例", "ShowLegend=false，只留格子与组色", Mix(showLegend: false)),
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

    private static Control Card(string title, string note, ArcWaffleChart chart)
    {
        chart.Width = 340;
        chart.Height = 180;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcWaffleChart Mix(bool showLegend) => new()
    {
        Segments = ChartsData.PowerMix,
        Unit = "TWh",
        ShowLegend = showLegend
    };

    private static ArcWaffleChart Balanced() => new()
    {
        Segments = new[]
        {
            new ChartValue("North", 25),
            new ChartValue("South", 25),
            new ChartValue("East", 25),
            new ChartValue("West", 25)
        },
        Unit = "seats"
    };

    private static ArcWaffleChart Empty() => new()
    {
        Segments = Array.Empty<ChartValue>()
    };
}
