using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts;

/// <summary>Unit, hierarchy, rhythm, density, flow, and share demos.</summary>
public sealed class ChartsMorePage : ArcPage
{
    public override string Title => "Charts, part two";

    public override string Group => "Charts";

    public override string? Description =>
        "The second set of Arc data visualisations: counting units, hierarchy, daily rhythm, density, flow, and one hundred percent.";

    public override int Order => 401;

    public override Control Build(ArcDemoContext context)
    {
        var rows = Grid(
            Card(
                "Waffle chart",
                "Electricity generation mix",
                new ArcWaffleChart
                {
                    Segments = ChartsData.PowerMix,
                    Height = 220,
                    Unit = "TWh"
                },
                "Each square is one percent. Hover a group to isolate its cells."),
            Card(
                "Treemap",
                "Revenue by region",
                new ArcTreemap
                {
                    Items = ChartsData.RevenueMap,
                    Height = 220,
                    ValueFormat = DollarsK
                },
                "Tiles use area for size and series tints for identity. Hover for share."),
            Card(
                "Activity heatmap",
                "Contributions, last sixteen weeks",
                new ArcChartActivityHeatmap
                {
                    Values = ChartsData.Activity(),
                    StartDate = new DateTime(2025, 12, 29),
                    Unit = "contributions",
                    Height = 220
                },
                "Five tint steps from the accent. Hover a day for its date and count."),
            Card(
                "Animated counter",
                "Monthly recurring revenue",
                new ArcChartAnimatedCounter
                {
                    Value = 1284,
                    Prefix = "$",
                    Label = "Monthly recurring revenue",
                    Height = 220
                },
                "Tabular figures keep columns steady. Travel is skipped under reduced motion."),
            Card(
                "Ridgeline",
                "API latency by surface",
                new ArcRidgeline
                {
                    Series = ChartsData.Ridges(),
                    Unit = " ms",
                    HighlightIndex = 0,
                    Height = 220
                },
                "One highlight colour, overlapping densities. Hover for median and middle half."),
            Card(
                "Streamgraph",
                "Support tickets by topic",
                new ArcStreamgraph
                {
                    Series = new[]
                    {
                        new ChartSeries("Bugs", ChartsData.Bugs),
                        new ChartSeries("Billing", ChartsData.Billing),
                        new ChartSeries("Onboarding", ChartsData.Onboarding),
                        new ChartSeries("Other", ChartsData.OtherTickets)
                    },
                    Labels = ChartsData.StreamWeeks,
                    ValueSuffix = " tickets",
                    Height = 220
                },
                "Hover a layer to isolate it and read the week and its share."));

        var split = Card(
            "Split bar",
            "Q4 budget, $611k",
            new ArcSplitBar
            {
                Segments = ChartsData.Budget,
                TotalLabel = "Q4 budget",
                Height = 150,
                ValueFormat = DollarsThousands
            },
            "Hover a segment or legend row for its exact share.");

        var content = new StackPanel();
        content.Children.Add(rows);
        content.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = split
        });

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "More charts",
                    "A unit grid, a squarified treemap, a year-like calendar, a changing number, overlapping densities, layered streams, and a split bar.",
                    content)
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
            Width = 340,
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
        => "$" + (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "k";

    private static string DollarsThousands(double value)
        => "$" + value.ToString("N0", CultureInfo.InvariantCulture) + "k";
}
