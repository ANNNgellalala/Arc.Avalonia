using System;
using System.Globalization;
using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts;

/// <summary>A dense time series with an overview strip and a draggable window.</summary>
public sealed class ChartsBrushPage : ArcPage
{
    private static readonly DateTime FirstDay = new(2025, 10, 1);

    public override string Title => "Brush chart";

    public override string Group => "Charts";

    public override int Order => 402;

    public override string? Description =>
        "A dense daily series with an overview strip: drag the window to zoom, resize it by its handles, and read launches and incidents in place.";

    public override Control Build(ArcDemoContext context)
    {
        var chart = new ArcBrushChart
        {
            Series = DailyUsers(),
            Start = FirstDay,
            Height = 360,
            WindowStart = 0.55,
            WindowEnd = 1.0,
            ValueSuffix = " users",
            ValueFormat = value => value.ToString("N0", CultureInfo.InvariantCulture),
            Events = new[]
            {
                new ChartEvent(146, "v2 launch", ChartTone.Accent, "Offline mode and shared spaces"),
                new ChartEvent(181, "Pricing change", ChartTone.Warning),
                new ChartEvent(213, "Incident", ChartTone.Danger, "Queue backlog, 41 minutes")
            }
        };

        var card = new ArcChartCard
        {
            Title = "Daily active users",
            Subtitle = "Eight months of daily readings, with launches and incidents marked",
            Chart = chart,
            Caption =
                "Drag the window to pan and its edges to resize. Left and right arrow keys pan, Shift with the arrows resizes, and Home or End shows the whole series.",
            Width = 900
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Overview and detail",
                    "Use it for a year or more of daily data, where people need the whole history and a close look at any stretch of it.",
                    card)
            }
        };
    }

    private static double[] DailyUsers()
    {
        var values = new double[240];
        for (var index = 0; index < values.Length; index++)
        {
            var trend = 2380 + index * 7.4;
            var weekly = 250 * Math.Sin(index * 2 * Math.PI / 7 + 0.6);
            var monthly = 130 * Math.Sin(index * 2 * Math.PI / 30.4 + 1.2);
            var noise = 85 * Math.Sin(index * 1.7) + 48 * Math.Cos(index * 0.53);
            var spike = index % 37 == 0 ? 380 : 0;
            values[index] = Math.Round(trend + weekly + monthly + noise + spike);
        }

        return values;
    }
}
