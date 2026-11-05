using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Streamgraph's completeness preview: four stacked layers, the same series reduced to two layers, the
/// four-layer graph without its legend and the empty state. Declared state keys match Docs/streamgraph.json
/// so the audit can compare the two.
/// </summary>
public sealed class StreamgraphPage : ArcItemPage
{
    public override string ItemSlug => "streamgraph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-four-layers", "variant-two-layers", "state-no-legend", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("四层流图", "四个类别堆叠，图例按层名列在顶部", FourLayers(showLegend: true)),
            Card("两层流图", "只保留两层时单层厚度读起来更直接", TwoLayers())));

        panel.Children.Add(Row(
            Card("无图例", "ShowLegend=false，只留流层与时间标签", FourLayers(showLegend: false)),
            Card("空数据", "Series 为空时画出 No data", Empty())));

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

    private static Control Card(string title, string note, ArcStreamgraph chart)
    {
        chart.Width = 340;
        chart.Height = 170;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcStreamgraph FourLayers(bool showLegend) => new()
    {
        Series = new[]
        {
            new ChartSeries("Bugs", ChartsData.Bugs),
            new ChartSeries("Billing", ChartsData.Billing),
            new ChartSeries("Onboarding", ChartsData.Onboarding),
            new ChartSeries("Other", ChartsData.OtherTickets)
        },
        Labels = ChartsData.StreamWeeks,
        ShowLegend = showLegend,
        ValueSuffix = " tickets"
    };

    private static ArcStreamgraph TwoLayers() => new()
    {
        Series = new[]
        {
            new ChartSeries("Bugs", ChartsData.Bugs),
            new ChartSeries("Billing", ChartsData.Billing)
        },
        Labels = ChartsData.StreamWeeks,
        ValueSuffix = " tickets"
    };

    private static ArcStreamgraph Empty() => new()
    {
        Series = Array.Empty<ChartSeries>(),
        Labels = Array.Empty<string>()
    };
}
