using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Metric card's completeness preview: an up metric with a sparkline, a down metric and a flat metric.
/// Declared keys match Docs/metric-card.json.
/// </summary>
public sealed class MetricCardPage : ArcItemPage
{
    public override string ItemSlug => "metric-card";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "trend-up", "trend-down", "trend-flat", "variant-sparkline"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(new TextBlock { Text = "趋势与迷你图", Classes = { "arc-caption" } });

        var row = new WrapPanel();
        row.Children.Add(Card(new ArcMetricCard
        {
            Width = 260,
            Label = "Uptime",
            Value = 99.9,
            Suffix = "%",
            Context = "Last 30 days",
            Change = "+0.2%",
            Trend = ArcMetricTrend.Up,
            Sparkline = Sparkline()
        }));
        row.Children.Add(Card(new ArcMetricCard
        {
            Width = 260,
            Label = "Monthly revenue",
            Value = 48250,
            Context = "vs last month",
            Change = "-3.1%",
            Trend = ArcMetricTrend.Down
        }));
        row.Children.Add(Card(new ArcMetricCard
        {
            Width = 260,
            Label = "Active sessions",
            Value = 1284,
            Context = "Right now",
            Change = "0.0%",
            Trend = ArcMetricTrend.Flat
        }));
        panel.Children.Add(row);

        return panel;
    }

    private static Control Card(Control content)
    {
        content.Margin = new Thickness(0, 0, 16, 16);
        return content;
    }

    private static Control Sparkline()
    {
        var sparkline = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var values = new[] { 8.0, 12, 10, 15, 13, 18, 16, 22, 20, 25 };
        foreach (var value in values)
        {
            var bar = new Border
            {
                Width = 4,
                Height = value,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = DemoUi.Brush(ArcTokens.Series1),
                Opacity = 0.55
            };
            sparkline.Children.Add(bar);
        }

        return sparkline;
    }
}
