using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Profiles, live readings, and a ranking over time: the moving half of the advanced Arc charts.
/// </summary>
public sealed class AdvancedChartsPartTwoPage : ArcPage
{
    public override string Title => "Advanced charts, part two";

    public override string Group => "Advanced charts";

    public override string? Description =>
        "A profile comparison on one web, a stream that fills as readings arrive, and a ranking that overtakes itself over time. The stream and the race are paused for still frames and animate when they are live.";

    public override int Order => 404;

    public override Control Build(ArcDemoContext context)
    {
        var cards = Grid(
            Card(
                "Radar chart",
                "Three cities on six measures",
                new ArcRadarChart
                {
                    Axes = AdvancedChartsData.OfficeAxes(),
                    Series = AdvancedChartsData.OfficeProfiles(),
                    Height = 244,
                    MaxValue = 100,
                    ValueSuffix = " pts"
                },
                "Use it for the shape of two or three profiles. Hover a spoke to compare."),
            Card(
                "Realtime stream",
                "API latency, rolling 36 readings",
                new ArcRealtimeStream
                {
                    Series = AdvancedChartsData.LatencySeeds(),
                    Height = 244,
                    WindowSize = 36,
                    IsLive = true,
                    Interval = System.TimeSpan.FromMilliseconds(500),
                    ValueSuffix = " ms"
                },
                "Use it for one live measure. Space or the pill pauses the view."),
            Card(
                "Race bar chart",
                "Revenue by product, January to August",
                new ArcRaceBarChart
                {
                    Frames = AdvancedChartsData.ProductRace(),
                    Height = 244,
                    TopN = 6,
                    FrameIndex = 5,
                    IsPlaying = false,
                    ValueFormat = Dollars
                },
                "Use it to tell a ranking story. Play, pause, or scrub the timeline."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Profiles, live data and rankings",
                    "Three charts where the movement is part of the reading: a profile compared across measures, a reading that keeps arriving, and a lead that changes hands across periods.",
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

    private static string Dollars(double value)
        => "$" + ChartFormat.Compact(value);
}
