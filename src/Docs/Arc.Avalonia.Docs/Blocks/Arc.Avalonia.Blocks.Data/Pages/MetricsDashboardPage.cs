using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Metrics dashboard: four KPI tabs, one inspectable line chart with an optional previous-period
/// comparison, and top pages and sources for the selected range. All figures are seeded sample data; the
/// Export action only reports locally. An independent Avalonia implementation of Arc's public
/// metrics-dashboard block; no Pro source was used.
/// </summary>
public sealed class MetricsDashboardPage : ArcItemPage
{
    public override string ItemSlug => "metrics-dashboard";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-dashboard", "variant-kpi-tabs", "variant-compare", "variant-split-bar", "variant-top-lists"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A small, explorable KPI overview with a range toggle and one chart.",
        "Product or marketing dashboards with four headline metrics, a 7, 30 or 90 day range, and top pages and sources."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use metric-explorer or revenue-explorer for larger or drill-down data."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab reaches the range control, Export, the KPI tabs, Compare and the chart; the chart inspects points with the arrow keys, Home and End.",
        "KPI tiles read as tabs with aria-selected, and deltas say up or down rather than relying on colour."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "KPI values count to their new amounts with tabular figures and the KPI underline slides on a spring; reduced motion swaps values and views immediately."
    };

    public override string? ImplementationNotes =>
        "Every series is seeded in the page (180 days, so the 90 day range has a comparison period). The chart is ArcLineChart, so the previous period is a dashed series rather than a shaded band; Export only shows a local confirmation.";

    private enum MetricKind
    {
        Visitors,
        Signups,
        Revenue,
        Churn
    }

    private sealed record Metric(string Label, MetricKind Kind, Func<double, string> Format, double[] Series);

    private static readonly (string Key, string Label, int Days)[] Ranges =
    {
        ("7", "7 days", 7),
        ("30", "30 days", 30),
        ("90", "90 days", 90)
    };

    private static string[] DateLabels(int days)
    {
        var start = new DateTime(2026, 5, 29);
        return Enumerable.Range(0, days)
            .Select(offset => start.AddDays(offset).ToString("MMM d", CultureInfo.InvariantCulture))
            .ToArray();
    }

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var visitors = DataSamples.Daily(180, 2050, 4.2, 190, 130, 11, 160);
        var signups = visitors.Select((value, index) =>
            Math.Round(value * 0.031 + Math.Sin(index / 5.0) * 4 + (index % 7 == 3 ? 6 : 0), 0)).ToArray();
        var revenue = visitors.Select((value, index) =>
            Math.Round(value * 0.42 + 120 + Math.Sin(index / 9.0) * 30, 0)).ToArray();
        var churn = visitors.Select((value, index) =>
            Math.Round(2 + Math.Abs(Math.Sin(index / 6.0)) * 5 + (index % 9 == 0 ? 3 : 0), 0)).ToArray();

        var metrics = new List<Metric>
        {
            new("Visitors", MetricKind.Visitors, value => DataKit.Number(value), visitors),
            new("Signups", MetricKind.Signups, value => DataKit.Number(value), signups),
            new("Revenue", MetricKind.Revenue, value => DataKit.Money(value), revenue),
            new("Churn rate", MetricKind.Churn, value => DataKit.Number(value), churn)
        };

        var rangeDays = 30;
        var metric = MetricKind.Visitors;
        var compare = true;

        var rangeHost = new ContentControl();
        var tilesHost = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*") };
        var chartHost = new ContentControl();
        var sourcesHost = new ContentControl();
        var pagesHost = new StackPanel { Spacing = 0 };
        var comparisonHost = new ContentControl();
        var status = DataKit.Status();
        var rangeNote = DataKit.Caption(string.Empty);

        void Rebuild()
        {
            var current = metrics.First(item => item.Kind == metric);
            var values = current.Series;
            var window = values.Skip(values.Length - rangeDays).ToArray();
            var previous = values.Skip(Math.Max(0, values.Length - rangeDays * 2)).Take(rangeDays).ToArray();

            // KPI tiles.
            tilesHost.Children.Clear();
            for (var index = 0; index < metrics.Count; index++)
            {
                var item = metrics[index];
                var slice = item.Series.Skip(item.Series.Length - rangeDays).ToArray();
                var previousSlice = item.Series.Skip(Math.Max(0, item.Series.Length - rangeDays * 2))
                    .Take(rangeDays).ToArray();
                var visitorsSlice = visitors.Skip(visitors.Length - rangeDays).Sum();
                var previousVisitors = visitors.Skip(Math.Max(0, visitors.Length - rangeDays * 2))
                    .Take(rangeDays).Sum();
                double change;
                string value;
                if (item.Kind == MetricKind.Churn)
                {
                    var rate = slice.Sum() / Math.Max(1, visitorsSlice);
                    var previousRate = previousSlice.Sum() / Math.Max(1, previousVisitors);
                    change = previousRate <= 0 ? 0 : (rate - previousRate) / previousRate * 100;
                    value = DataKit.Percent(rate, 1);
                }
                else
                {
                    var total = slice.Sum();
                    var previousTotal = previousSlice.Sum();
                    change = previousTotal <= 0 ? 0 : (total - previousTotal) / previousTotal * 100;
                    value = item.Format(total);
                }

                var tile = KpiTile(item.Label, value, change, item.Kind == metric, () =>
                {
                    metric = item.Kind;
                    Rebuild();
                });
                Grid.SetColumn(tile, index * 2);
                tilesHost.Children.Add(tile);
            }

            // The inspectable chart: current window, plus the previous period when Compare is on.
            var series = new List<ChartSeries> { new("This period", window) };
            if (compare)
            {
                series.Add(new ChartSeries("Previous", previous, DataKit.Brush(ArcTokens.TextMuted), true));
            }

            var chart = new ArcLineChart
            {
                Series = series,
                Labels = window.Select((_, index) => DateLabels(180)[values.Length - rangeDays + index]).ToList(),
                Height = 182,
                ShowArea = true,
                ShowGrid = true,
                ShowLegend = compare,
                Smooth = true,
                ValueFormat = current.Kind == MetricKind.Revenue ? value => DataKit.Money(value) : value => DataKit.Number(value)
            };
            chartHost.Content = chart;

            comparisonHost.Content = DataKit.Toggle("Compare", compare, checkedValue =>
            {
                compare = checkedValue;
                Rebuild();
            });

            rangeNote.Text = $"{(current.Kind == MetricKind.Churn ? "Churned accounts as a share of visitors" : "Totals")} for the last {rangeDays} days, compared with the {rangeDays} days before";

            // Sources: a composition bar plus ranked rows that follow the range.
            var visitTotal = Math.Round(window.Sum() * 0.06);
            var sourceValues = new[]
            {
                new ChartValue("Direct", Math.Round(visitTotal * 0.42), DataKit.Brush(ArcTokens.Series1)),
                new ChartValue("Search", Math.Round(visitTotal * 0.31), DataKit.Brush(ArcTokens.Series2)),
                new ChartValue("Social", Math.Round(visitTotal * 0.17), DataKit.Brush(ArcTokens.Series3)),
                new ChartValue("Referral", Math.Round(visitTotal * 0.10), DataKit.Brush(ArcTokens.Series4))
            };
            var sourceTotal = sourceValues.Sum(item => item.Value);
            var sourceRows = new StackPanel { Spacing = 7 };
            foreach (var source in sourceValues)
            {
                var share = source.Value / sourceTotal;
                sourceRows.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        DataKit.Dot(source.Brush ?? DataKit.Brush(ArcTokens.Series1)),
                        DataKit.AtColumn(DataKit.Text(source.Name, "arc-body-sm"), 1),
                        DataKit.AtColumn(DataKit.Caption(DataKit.Percent(share) + " · " + DataKit.Number(source.Value)), 2)
                    }
                });
            }

            sourcesHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new ArcSplitBar
                    {
                        Segments = sourceValues,
                        Height = 92,
                        ShowLegend = false,
                        TotalLabel = "Traffic",
                        Unit = "visits"
                    },
                    sourceRows
                }
            };

            // Top pages: path, a quiet share bar, views and change.
            var pageRows = new (string Path, double Share, double Change)[]
            {
                ("/pricing", 0.24, 8.4),
                ("/docs/quickstart", 0.19, 4.1),
                ("/changelog", 0.15, 12.6),
                ("/blog/design-tokens", 0.12, -3.2),
                ("/customers", 0.09, 1.8)
            };
            pagesHost.Children.Clear();
            foreach (var row in pageRows)
            {
                var views = Math.Round(visitTotal * 0.32 * row.Share * 4);
                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,96,Auto,Auto"),
                    ColumnSpacing = 10,
                    Height = 22,
                    Children =
                    {
                        DataKit.Text(row.Path, "arc-body-sm"),
                        DataKit.AtColumn(DataKit.Progress(row.Share, 0.3), 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.Number(views), "arc-body-sm arc-medium"), 2),
                        DataKit.AtColumn(DataKit.Delta(row.Change, "%", 1), 3)
                    }
                };
                pagesHost.Children.Add(grid);
            }
        }

        var rangeSelector = DataKit.Segmented(Ranges.Select(range => (range.Key, range.Label)), "30", key =>
        {
            rangeDays = int.Parse(key, CultureInfo.InvariantCulture);
            Rebuild();
        });
        rangeHost.Content = rangeSelector;

        var export = DataKit.Btn("Export", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
            DataKit.Say(status, $"Exported {rangeDays} days of {metrics.First(item => item.Kind == metric).Label.ToLowerInvariant()} as CSV. Sample data only."),
            icon: "download");

        Rebuild();

        var headerRight = DataKit.Row(10, rangeHost, comparisonHost, export);
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        headerRight.VerticalAlignment = VerticalAlignment.Top;

        var header = DataKit.Header("Acme analytics", "1 Jun – 26 Nov 2026 · all channels · last synced 4 minutes ago", headerRight);

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8,
                    DataKit.Text(metrics.First(item => item.Kind == metric).Label + " over time", "arc-label"),
                    DataKit.Caption("· hover or focus the chart for a value")),
                chartHost
            }
        });

        var sourcesCard = DataKit.Card(new StackPanel
        {
            Spacing = 10,
            Children = { DataKit.Label("Top sources"), sourcesHost }
        }, width: 286);

        var middle = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,286"),
            Children = { chartCard, DataKit.AtColumn(sourcesCard, 2) }
        };

        var pagesCard = DataKit.Card(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Top pages"), DataKit.Caption("views · change against the previous period")),
                new Border { Height = 4 },
                pagesHost
            }
        });

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 8,
            Children = { header, tilesHost, middle, pagesCard, status }
        }, 16);
    }

    /// <summary>A KPI tab: label, figure, change, and an accent underline for the charted metric.</summary>
    private static Control KpiTile(string label, string value, double change, bool selected, Action onClick)
    {
        var content = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                DataKit.Caption(label),
                new TextBlock { Text = value, Classes = { "arc-subtitle" } },
                DataKit.Delta(change, "%", 1)
            }
        };

        var underline = new Border
        {
            Height = 2,
            Margin = new Thickness(0, 8, 0, 0),
            Background = selected ? DataKit.Brush(ArcTokens.Accent) : null,
            CornerRadius = new CornerRadius(1)
        };

        var border = new Border
        {
            Background = selected ? DataKit.Brush(ArcTokens.SurfaceMuted) : DataKit.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = selected ? DataKit.Brush(ArcTokens.BorderStrong) : DataKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 8, 12, 6),
            Focusable = true,
            Child = new StackPanel { Spacing = 0, Children = { content, underline } }
        };
        AutomationProperties.SetName(border, $"{label}: {value}, {DataKit.Signed(change)} percent. Opens the chart.");
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
        };
        border.PointerEntered += (_, _) => border.Background = DataKit.Brush(ArcTokens.SurfaceMuted);
        border.PointerExited += (_, _) =>
            border.Background = selected ? DataKit.Brush(ArcTokens.SurfaceMuted) : DataKit.Brush(ArcTokens.SurfaceRaised);
        return border;
    }
}
