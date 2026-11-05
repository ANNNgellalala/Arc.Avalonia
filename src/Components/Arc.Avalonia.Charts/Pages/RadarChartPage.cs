using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Radar chart's completeness preview: three profiles with a legend, a single profile alone, and two
/// profiles with the legend switched off. All three are static, and the profile values are fixed so the
/// web never changes between runs.
/// </summary>
public sealed class RadarChartPage : ArcItemPage
{
    public override string ItemSlug => "radar-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-three-profiles", "variant-single-profile", "state-no-legend"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("三个轮廓", "同一张网上比较三座城市，图例悬停把一条形状提到前面。",
                AdvancedChartsData.OfficeProfiles(), showLegend: true),
            Card("单个轮廓", "只有一条序列时，web 读作一张能力画像；MaxValue=100 固定轴的顶点。",
                new[] { new ChartSeries("Lisbon", new double[] { 72, 84, 78, 81, 66, 74 }) }, showLegend: true)));

        panel.Children.Add(Card("无图例", "ShowLegend=false：图例收起，全部空间留给 web 与轴标签。",
            new[]
            {
                new ChartSeries("Lisbon", new double[] { 72, 84, 78, 81, 66, 74 }),
                new ChartSeries("Berlin", new double[] { 81, 68, 88, 74, 58, 82 })
            }, showLegend: false));

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

    private static Control Card(string title, string note, IReadOnlyList<ChartSeries> series, bool showLegend)
    {
        var radar = new ArcRadarChart
        {
            Axes = AdvancedChartsData.OfficeAxes(),
            Series = series,
            MaxValue = 100,
            ShowLegend = showLegend,
            ValueSuffix = " pts",
            Width = 380,
            Height = 252
        };

        return DemoUi.Card(title, note, radar);
    }
}
