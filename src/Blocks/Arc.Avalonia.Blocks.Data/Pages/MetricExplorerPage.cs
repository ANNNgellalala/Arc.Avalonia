using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Metric explorer: four KPI cards that open into one scrubbable chart, with metric tabs and a range switch.
/// The open card is a custom surface beside the chart rather than a shared-layout morph. All series are
/// seeded sample data. An independent Avalonia implementation of Arc's public metric-explorer block; no Pro
/// source was used.
/// </summary>
public sealed class MetricExplorerPage : ArcItemPage
{
    public override string ItemSlug => "metric-explorer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The top of a SaaS analytics or finance page with KPI cards that open into a chart.",
        "Dashboards where people move between a few metrics and date ranges."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use stat-card or metric-card for static KPI tiles.",
        "Use metrics-dashboard for a full dashboard layout."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each card is a button whose label reads the value, change and range; Enter or Space opens it and Escape closes the chart back to the cards.",
        "The open chart uses a tablist for the metrics, a range switch, and a slider-style scrubber that reads the dated value."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A card's surface grows into the chart panel and the line is replaced when the metric or range changes; reduced motion swaps both instantly."
    };

    public override string? ImplementationNotes =>
        "The open state is an in-place panel under the cards rather than a shared-layout morph from the clicked card, because Avalonia has no shared element transition. The chart is ArcLineChart (hover and keyboard inspection built in) and the data is seeded for 90 days.";

    private sealed record Metric(string Key, string Label, double[] Values, Func<double, string> Format, string Note, bool Level = false);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var days = 90;
        var metrics = new List<Metric>
        {
            new("visitors", "Visitors", DataSamples.Daily(days, 2380, 8.5, 210, 150, 21, 170),
                value => DataKit.Number(value), "unique sessions"),
            new("signups", "Signups", DataSamples.Daily(days, 74, 0.55, 12, 9, 22, 6),
                value => DataKit.Number(value), "trial starts"),
            new("mrr", "MRR", DataSamples.Daily(days, 7300, 42, 260, 180, 23, 0),
                value => DataKit.Money(value), "monthly recurring", Level: true),
            new("churn", "Churn", DataSamples.Daily(days, 5.2, -0.005, 2.4, 1.6, 24, 0),
                value => DataKit.Number(value), "accounts lost")
        };

        var rangeDays = 30;
        var open = true;
        var activeKey = "mrr";

        var cardsHost = new ContentControl();
        var detailHost = new ContentControl();
        var status = DataKit.Status();

        void Rebuild()
        {
            var active = metrics.First(item => item.Key == activeKey);
            var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*") };
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                var slice = metric.Values.Skip(metric.Values.Length - rangeDays).ToArray();
                var previous = metric.Values.Skip(Math.Max(0, metric.Values.Length - rangeDays * 2)).Take(rangeDays).ToArray();
                var total = metric.Level ? slice[^1] : slice.Sum();
                var previousTotal = metric.Level ? previous[^1] : previous.Sum();
                var change = previousTotal <= 0 ? 0 : (total - previousTotal) / previousTotal * 100;
                var selected = open && metric.Key == activeKey;
                var card = Card(metric, total, change, slice, selected, () =>
                {
                    activeKey = metric.Key;
                    open = true;
                    Rebuild();
                    DataKit.Say(status, $"{metric.Label} opened for the last {rangeDays} days.");
                });
                Grid.SetColumn(card, index * 2);
                cards.Children.Add(card);
            }

            cardsHost.Content = cards;

            if (!open)
            {
                detailHost.Content = DataKit.Tile(new TextBlock
                {
                    Text = "Select a card to open its chart",
                    Classes = { "arc-body-sm", "arc-muted" },
                    HorizontalAlignment = HorizontalAlignment.Center
                }, 24);
                return;
            }

            var window = active.Values.Skip(active.Values.Length - rangeDays).ToArray();
            var labels = Enumerable.Range(1, window.Length)
                .Select(offset => new DateTime(2026, 9, 1).AddDays(days - window.Length + offset - 1)
                    .ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var chart = new ArcLineChart
            {
                Series = new List<ChartSeries> { new(active.Label, window) },
                Labels = labels,
                Height = 158,
                ShowArea = true,
                ShowGrid = true,
                ShowLegend = false,
                Smooth = true,
                ValueFormat = active.Format
            };

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var metric in metrics)
            {
                var button = DataKit.Chip(metric.Label, metric.Key == activeKey, () =>
                {
                    activeKey = metric.Key;
                    Rebuild();
                });
                tabs.Children.Add(button);
            }

            var range = DataKit.Segmented(new[] { ("7", "7 days"), ("30", "30 days"), ("90", "90 days") },
                rangeDays.ToString(), key =>
                {
                    rangeDays = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
                    Rebuild();
                });

            var close = DataKit.IconBtn("x", "Close chart", () =>
            {
                open = false;
                Rebuild();
                DataKit.Say(status, "Chart closed. Cards are back.");
            }, 28);

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            header.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    DataKit.Row(8, DataKit.Label("Charted metric"), DataKit.Caption("· hover or focus the chart")),
                    tabs
                }
            });
            var right = DataKit.Row(8, range, close);
            right.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(right, 1);
            header.Children.Add(right);

            detailHost.Content = DataKit.Card(new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    header,
                    chart,
                    DataKit.Caption($"{active.Label} · {active.Note} · {labels[0]} to {labels[^1]}")
                }
            });
        }

        Rebuild();

        var headerBlock = DataKit.Header("Revenue analytics",
            "Four metrics from the warehouse · sweep the cards or the range",
            DataKit.Meta("refresh-cw", "Updated 4 minutes ago"));

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { headerBlock, cardsHost, detailHost, status }
        }, 16);
    }

    /// <summary>A KPI card: caption, value, change, sparkline; the open card carries the accent outline.</summary>
    private static Control Card(Metric metric, double total, double change, double[] slice, bool selected, Action onClick)
    {
        var body = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                DataKit.Caption(metric.Label),
                new TextBlock { Text = metric.Format(total), Classes = { "arc-subtitle" } },
                DataKit.Delta(change, "%", 1),
                DataKit.Spark(slice, 30)
            }
        };

        var border = new Border
        {
            Background = DataKit.Brush(selected ? ArcTokens.SurfaceMuted : ArcTokens.SurfaceRaised),
            BorderBrush = DataKit.Brush(selected ? ArcTokens.Accent : ArcTokens.Border),
            BorderThickness = new Thickness(selected ? 1.5 : 1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 10, 12, 8),
            Focusable = true,
            Child = body
        };
        AutomationProperties.SetName(border, $"{metric.Label}: {metric.Format(total)}, {DataKit.Signed(change)} percent. Opens the chart.");
        border.PointerPressed += (_, args) =>
        {
            onClick();
            args.Handled = true;
        };
        border.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Enter or Key.Space)
            {
                onClick();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                onClick();
                args.Handled = true;
            }
        };
        border.PointerEntered += (_, _) => border.BorderBrush = DataKit.Brush(ArcTokens.BorderStrong);
        border.PointerExited += (_, _) =>
            border.BorderBrush = DataKit.Brush(selected ? ArcTokens.Accent : ArcTokens.Border);
        return border;
    }
}
