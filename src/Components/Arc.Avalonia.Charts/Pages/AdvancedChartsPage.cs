using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Hierarchy, flow, and drop-off: the advanced Arc charts drawn entirely with Avalonia's own rendering.
/// </summary>
public sealed class AdvancedChartsPage : ArcPage
{
    public override string Title => "Advanced charts";

    public override string Group => "Advanced charts";

    public override string? Description =>
        "Hierarchy, flow, and one sequence of steps: drill through a total, trace where a journey splits, and read the share kept at every stage. Each chart is drawn with Avalonia's own drawing context and the Arc series palette.";

    public override int Order => 403;

    public override Control Build(ArcDemoContext context)
    {
        var cards = Grid(
            Card(
                "Sunburst",
                "Cloud spend by team, September",
                new ArcSunburstChart
                {
                    Root = AdvancedChartsData.CloudSpend(),
                    CenterLabel = "Cloud spend",
                    Height = 244,
                    Depth = 3,
                    ValueFormat = DollarsK
                },
                "Use it to explore where a total comes from. Click an arc to drill in."),
            Card(
                "Sankey flow",
                "Signups to a plan, last 30 days",
                new ArcSankeyFlow
                {
                    Nodes = AdvancedChartsData.SignupNodes(),
                    Links = AdvancedChartsData.SignupLinks(),
                    Height = 244,
                    ValueSuffix = " people"
                },
                "Use it when a journey splits and recombines. Hover a node to trace it."),
            Card(
                "Funnel chart",
                "Checkout, last 7 days",
                new ArcFunnelChart
                {
                    Stages = AdvancedChartsData.CheckoutStages(),
                    Height = 244,
                    ValueSuffix = " sessions"
                },
                "Use it for ordered steps where each keeps part of the one before."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Hierarchy, flow and drop-off",
                    "Three shapes for data that is not a trend: a part-to-whole hierarchy you can drill into, a flow that splits and merges, and a sequence of steps where each keeps less than the one before.",
                    cards)
            }
        };
    }

    private static ArcChartCard Card(string title, string subtitle, Control chart, string caption)
    {
        return new ArcChartCard
        {
            Title = title,
            Subtitle = subtitle,
            Chart = chart,
            Caption = caption,
            Width = 516,
            Margin = new Thickness(0, 0, 14, 14)
        };
    }

    private static Control Grid(params Control[] cards)
    {
        var grid = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var card in cards)
        {
            grid.Children.Add(card);
        }

        return grid;
    }

    private static string DollarsK(double value)
        => "$" + (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
}
