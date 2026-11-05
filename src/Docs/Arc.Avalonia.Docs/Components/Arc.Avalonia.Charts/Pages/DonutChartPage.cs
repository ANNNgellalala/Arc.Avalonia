using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Donut chart's completeness preview: the default ring with legend and centre total, a thin ring, a thick
/// ring, the ring without its legend and the empty state. Declared state keys match Docs/donut-chart.json
/// so the audit can compare the two.
/// </summary>
public sealed class DonutChartPage : ArcItemPage
{
    public override string ItemSlug => "donut-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-default", "variant-thin-ring", "variant-thick-ring", "state-no-legend", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("默认环形", "图例 + 中心合计，环宽 22", Ring(22, showLegend: true)),
            Card("细环", "RingThickness=12，中心数字更突出", Ring(12, showLegend: true))));

        panel.Children.Add(Row(
            Card("粗环", "RingThickness=34，颜色块更醒目", Ring(34, showLegend: true)),
            Card("无图例", "ShowLegend=false，只留圆环与中心合计", Ring(22, showLegend: false))));

        panel.Children.Add(Card("空数据", "Segments 为空时画出 No data", Empty()));

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

    private static Control Card(string title, string note, ArcDonutChart chart)
    {
        chart.Width = 340;
        chart.Height = 170;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcDonutChart Ring(double thickness, bool showLegend) => new()
    {
        Segments = ChartsData.Traffic,
        CenterLabel = "Total",
        Unit = "visits",
        RingThickness = thickness,
        ShowLegend = showLegend,
        ValueFormat = Number
    };

    private static ArcDonutChart Empty() => new()
    {
        Segments = Array.Empty<ChartValue>(),
        CenterLabel = "Total",
        Unit = "visits"
    };

    private static string Number(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
