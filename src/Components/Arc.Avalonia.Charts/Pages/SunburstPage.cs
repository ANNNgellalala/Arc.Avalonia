using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Sunburst's completeness preview: three rings, two rings, labels switched off and the legend switched off.
/// Every declared state is visible as its own static chart, so the preview needs no interaction. The tree is
/// the shared deterministic cloud-spend sample from <see cref="AdvancedChartsData"/>.
/// </summary>
public sealed class SunburstPage : ArcItemPage
{
    public override string ItemSlug => "sunburst";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-depth-3", "variant-depth-2", "state-no-labels", "state-no-legend"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("三层环图", "Depth=3：三圈逐层下钻，中心读数跟着指针与焦点。", 3, labels: true, legend: true),
            Card("两层环图", "Depth=2：只画两圈，更浅的分支填满半径。", 2, labels: true, legend: true)));

        panel.Children.Add(Row(
            Card("无环上标签", "ShowLabels=false：环上标签收起，靠图例与中心读数识别。", 3, labels: false, legend: true),
            Card("无图例", "ShowLegend=false：右侧列表收起，全部宽度留给环。", 3, labels: true, legend: false)));

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

    private static Control Card(string title, string note, int depth, bool labels, bool legend)
    {
        var chart = new ArcSunburstChart
        {
            Root = AdvancedChartsData.CloudSpend(),
            CenterLabel = "Cloud spend",
            Depth = depth,
            ShowLabels = labels,
            ShowLegend = legend,
            ValueFormat = DollarsK,
            Width = 380,
            Height = 236
        };

        return DemoUi.Card(title, note, chart);
    }

    private static string DollarsK(double value) =>
        "$" + (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
}
