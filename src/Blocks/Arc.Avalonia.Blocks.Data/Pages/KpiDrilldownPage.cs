using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// KPI drilldown: four KPI cards that expand in place into a full detail chart with a period comparison and a
/// breakdown table, then fold back. Series and breakdowns are seeded sample data. An independent Avalonia
/// implementation of Arc's public kpi-drilldown block; no Pro source was used.
/// </summary>
public sealed class KpiDrilldownPage : ArcItemPage
{
    public override string ItemSlug => "kpi-drilldown";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-kpi", "variant-collapsed-cards", "variant-expanded-chart",
        "variant-comparison", "variant-breakdown-table"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A summary row where one metric deserves a closer look without leaving the page.",
        "Executive dashboards that need a comparison and a breakdown one click below the number."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use metric-explorer when the cards share one chart and people switch between metrics.",
        "Use metric-card for a static number with a sparkline."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each card is a button: Enter or Space expands it, Escape folds it back. The detail panel takes focus and reads its heading, comparison and table.",
        "The detail repeats the metric, both periods and every breakdown row as text, and the open card carries the accent outline as well as aria-expanded."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The detail panel replaces the row under the cards and folds back on one short fade; the reference grows the clicked card into the panel with a shared layout transition.",
        "Reduced motion swaps the panel instantly."
    };

    public override string? ImplementationNotes =>
        "The expansion is an in-place panel under the cards rather than a shared-element morph from the card, because Avalonia has no shared layout transition. The comparison is a dashed second series in ArcLineChart, the breakdown is hand-built rows (not a data grid), and everything is seeded for 60 days.";

    private sealed record DrillMetric(string Key, string Label, string Note, double[] Values, Func<double, string> Format,
        Func<double[], double> Aggregate, string[] Segments);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var days = 60;
        var metrics = new List<DrillMetric>
        {
            new("revenue", "Revenue", "Recognised this period", DataSamples.Daily(days, 8400, 48, 320, 220, 71, 0),
                value => DataKit.Money(value), values => values.Sum(), new[] { "New", "Expansion", "Renewal", "Usage", "Services" }),
            new("accounts", "Active accounts", "Weekly active workspaces", DataSamples.Daily(days, 1420, 4.2, 44, 22, 72, 0),
                value => DataKit.Number(value), values => values[^1], new[] { "Enterprise", "Scale", "Growth", "Starter", "Trial" }),
            new("churn", "Churn rate", "Logos lost this week", DataSamples.Daily(days, 2.1, -0.005, 0.6, 0.4, 73, 0),
                value => DataKit.Number(value, 1) + "%", values => values.Average(), new[] { "Involuntary", "Voluntary", "Downsell", "Non-renewal", "Unknown" }),
            new("nps", "NPS", "Rolling 30 day score", DataSamples.Daily(days, 41, 0.06, 2.4, 1.8, 74, 0),
                value => DataKit.Number(value), values => values.Average(), new[] { "Promoters", "Passives", "Detractors", "Surveyed", "Skipped" })
        };

        var open = "revenue";
        var range = 30;
        var cardsHost = new ContentControl();
        var detailHost = new ContentControl();
        var status = DataKit.Status();

        void Refresh()
        {
            var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*") };
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                var slice = metric.Values.Skip(metric.Values.Length - range).ToArray();
                var previous = metric.Values.Skip(Math.Max(0, metric.Values.Length - range * 2)).Take(range).ToArray();
                var total = metric.Aggregate(slice);
                var previousTotal = metric.Aggregate(previous);
                var change = previousTotal <= 0 ? 0 : (total - previousTotal) / previousTotal * 100;
                var selected = metric.Key == open;
                var card = Card(metric.Label, metric.Format(total), change, metric.Note, selected, () =>
                {
                    open = open == metric.Key ? string.Empty : metric.Key;
                    Refresh();
                    DataKit.Say(status, string.IsNullOrEmpty(open) ? "Detail folded back." : $"{metric.Label} opened.");
                });
                Grid.SetColumn(card, index * 2);
                cards.Children.Add(card);
            }

            cardsHost.Content = cards;

            var active = metrics.FirstOrDefault(item => item.Key == open);
            if (active is null)
            {
                detailHost.Content = DataKit.Tile(new TextBlock
                {
                    Text = "Select a KPI to expand its detail",
                    Classes = { "arc-body-sm", "arc-muted" },
                    HorizontalAlignment = HorizontalAlignment.Center
                }, 20);
                return;
            }

            var current = active.Values.Skip(active.Values.Length - range).ToArray();
            var baseline = active.Values.Skip(Math.Max(0, active.Values.Length - range * 2)).Take(range).ToArray();
            var labels = Enumerable.Range(1, range)
                .Select(offset => new DateTime(2026, 9, 28).AddDays(days - range + offset).ToString("MMM d",
                    System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var random = DataKit.Random(active.Key.Length * 97 + 5);
            var rows = new StackPanel { Spacing = 0 };
            var shares = new[] { 0.42, 0.25, 0.16, 0.11, 0.06 };
            for (var index = 0; index < active.Segments.Length; index++)
            {
                var currentValue = active.Aggregate(current) * shares[index];
                var change = (random() - 0.35) * 18;
                rows.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 12,
                    Height = 26,
                    Children =
                    {
                        DataKit.Text(active.Segments[index], "arc-body-sm"),
                        DataKit.AtColumn(DataKit.Text(active.Format(currentValue), "arc-body-sm arc-medium"), 1),
                        DataKit.AtColumn(DataKit.Delta(change, "%", 1), 2)
                    }
                });
            }

            var rangeSwitch = DataKit.Segmented(new[] { ("7", "7 days"), ("30", "30 days"), ("60", "60 days") },
                range.ToString(), key =>
                {
                    range = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
                    Refresh();
                });

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            header.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    DataKit.Row(8, DataKit.Label(active.Label + " detail"), DataKit.Pill(active.Note, ArcBadgeTone.Neutral)),
                    DataKit.Caption($"{active.Format(active.Aggregate(current))} this period · {active.Format(active.Aggregate(baseline))} previous period")
                }
            });
            var close = DataKit.Row(8, rangeSwitch, DataKit.IconBtn("x", "Fold detail back", () =>
            {
                open = string.Empty;
                Refresh();
                DataKit.Say(status, "Detail folded back.");
            }, 28));
            close.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(close, 1);
            header.Children.Add(close);

            var chart = new ArcLineChart
            {
                Series = new List<ChartSeries>
                {
                    new("This period", current),
                    new("Previous period", baseline, DataKit.Brush(ArcTokens.TextMuted), true)
                },
                Labels = labels,
                Height = 150,
                ShowArea = true,
                ShowGrid = true,
                ShowLegend = true,
                Smooth = true,
                ValueFormat = active.Format
            };

            detailHost.Content = DataKit.Card(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,16,300"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            header,
                            chart,
                            DataKit.Caption("Hover the chart or focus it and use the arrow keys for a dated value.")
                        }
                    },
                    DataKit.AtColumn(new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            DataKit.Label("Breakdown"),
                            DataKit.Caption("share of the period, change against the previous period"),
                            rows
                        }
                    }, 2)
                }
            });
        }

        Refresh();

        var headerBlock = DataKit.Header("Acme overview", "Four KPIs · click one to open its detail and comparison");

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { headerBlock, cardsHost, detailHost, status }
        }, 16);
    }

    private static Control Card(string label, string value, double change, string note, bool selected, Action onClick)
    {
        var border = new Border
        {
            Background = DataKit.Brush(selected ? ArcTokens.SurfaceMuted : ArcTokens.SurfaceRaised),
            BorderBrush = DataKit.Brush(selected ? ArcTokens.Accent : ArcTokens.Border),
            BorderThickness = new Thickness(selected ? 1.5 : 1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 10, 12, 10),
            Focusable = true,
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    DataKit.Row(6, DataKit.Caption(label), new ArcIcon
                    {
                        Icon = selected ? "chevron-up" : "chevron-down",
                        Size = 12,
                        Foreground = DataKit.Brush(ArcTokens.TextMuted)
                    }),
                    new TextBlock { Text = value, Classes = { "arc-subtitle" } },
                    DataKit.Delta(change, "%", 1),
                    DataKit.Caption(note)
                }
            }
        };
        AutomationProperties.SetName(border, $"{label}: {value}, {DataKit.Signed(change)} percent. Expands the detail.");
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
        return border;
    }
}
