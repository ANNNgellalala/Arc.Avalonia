using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>Trend, comparison, and meter charts at card size.</summary>
public sealed class ChartsPage : ArcPage
{
    public override string Title => "Charts";

    public override string Group => "Charts";

    public override string? Description =>
        "Custom drawn data visualisations on the Arc series palette: quiet axes, thin strokes, tabular figures, and readable hover states.";

    public override int Order => 400;

    public override Control Build(ArcDemoContext context)
    {
        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Common charts",
                    "Lines for change over time, bars for discrete comparison, donuts for one whole, sparklines for a compact read, a gauge for one value, and a two-column slope.",
                    Grid(
                        Card(
                            "Line chart",
                            "Weekly signups, this period vs previous",
                            new ArcLineChart
                            {
                                Series = new[]
                                {
                                    new ChartSeries("This period", ChartsData.ThisPeriod),
                                    new ChartSeries("Previous", ChartsData.PreviousPeriod, Dashed: true)
                                },
                                Labels = ChartsData.Weeks,
                                Height = 220,
                                ValueSuffix = " signups"
                            },
                            "Hover the plot for a crosshair. Click a legend item to hide a line."),
                        Card(
                            "Bar chart",
                            "Active minutes by weekday",
                            new ArcBarChart
                            {
                                Values = ChartsData.WeekdayMinutes,
                                Labels = ChartsData.WeekdayLabels,
                                Height = 220,
                                ValueSuffix = " min"
                            },
                            "The average is a reference, not a target. Hover a bar for its value."),
                        Card(
                            "Donut chart",
                            "Visits by source",
                            new ArcDonutChart
                            {
                                Segments = ChartsData.Traffic,
                                Height = 220,
                                Unit = "visits",
                                ValueFormat = Number
                            },
                            "Hover a segment or legend row. The centre follows the active part."),
                        Card(
                            "Sparkline",
                            "Signups over the last twelve weeks",
                            new ArcSparkline
                            {
                                Values = ChartsData.Spark,
                                Labels = ChartsData.Weeks,
                                Height = 220,
                                Value = "48",
                                Change = "+26%",
                                Tone = ChartTone.Success
                            },
                            "A last-point dot anchors the trend. Hover to read earlier points."),
                        Card(
                            "Gauge",
                            "Storage used",
                            new ArcGauge
                            {
                                Value = 72,
                                Minimum = 0,
                                Maximum = 100,
                                Label = "Storage used",
                                Detail = "360 of 500 GB",
                                Target = 85,
                                ValueSuffix = "%",
                                Height = 220,
                                Thresholds = new[]
                                {
                                    new GaugeThreshold(0, ChartTone.Success, "Healthy"),
                                    new GaugeThreshold(70, ChartTone.Warning, "Filling up"),
                                    new GaugeThreshold(90, ChartTone.Danger, "Critical")
                                }
                            },
                            "Thresholds name the band in text, so the tone is never the only key."),
                        Card(
                            "Slope chart",
                            "Conversion by channel, Q3 to Q4",
                            new ArcSlopeChart
                            {
                                Items = ChartsData.Channels,
                                StartLabel = "Q3",
                                EndLabel = "Q4",
                                HighlightKey = "Email",
                                Height = 220,
                                ValueFormat = Percent
                            },
                            "Names sit on the left, end values and ranks on the right.")))
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

    private static string Number(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
