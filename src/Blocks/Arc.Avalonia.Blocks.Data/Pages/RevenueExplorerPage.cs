using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Revenue explorer: twenty-four months of MRR with an overview brush that zooms the range, a previous-period
/// comparison, composition toggles and totals that follow the window. All figures are seeded sample data.
/// An independent Avalonia implementation of Arc's public revenue-explorer block; no Pro source was used.
/// </summary>
public sealed class RevenueExplorerPage : ArcItemPage
{
    public override string ItemSlug => "revenue-explorer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Monthly MRR or revenue broken into new, expansion and churn on an analytics page.",
        "Charts where people need to zoom a date range with an overview brush and compare against a prior period."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "On the brush, Arrow keys pan a month, Shift with the arrows resizes the window, and Home and End jump to the timeline edges; the comparison toggle is a switch.",
        "The summarised range is repeated in words above the chart, so the brush position is never the only signal."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Paths and axis labels recompute as the brush moves; totals step to the new range without overshooting. The reference glides paths on springs, which is noted below.",
        "Reduced motion is honoured by the status and count updates, which swap instantly."
    };

    public override string? ImplementationNotes =>
        "The window is sliced in place rather than animated between ranges, and the chart is ArcLineChart (so the comparison is a dashed second series rather than a shaded band). The overview strip is a hand drawn brush control; both are deterministic, seeded and reduced-motion safe.";

    private sealed record Component(string Name, double[] Values, int SeriesIndex, bool Enabled);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var months = DataSamples.Months(24, 10, 2026);
        var mrr = DataSamples.Monthly(24, 148_000, 0.019, 0.012, 4211);
        var newMrr = mrr.Select(value => Math.Round(value * 0.041)).ToArray();
        var expansion = mrr.Select(value => Math.Round(value * 0.021)).ToArray();
        var churn = mrr.Select(value => Math.Round(value * 0.014)).ToArray();

        var components = new List<Component>
        {
            new("New", newMrr, 0, true),
            new("Expansion", expansion, 1, true),
            new("Churn", churn, 2, true)
        };

        var compare = true;
        var start = 0.0;
        var end = 1.0;

        var chartHost = new ContentControl();
        var summaryHost = new ContentControl();
        var arrangementHost = new ContentControl();
        var status = DataKit.Status();
        var caption = DataKit.Caption(string.Empty);

        start = 0.25;
        var brush = new ArcOverviewBrush(mrr, start, end);
        var chartHostHost = new ContentControl { Content = brush };

        void Refresh()
        {
            var count = mrr.Length;
            var first = (int)Math.Floor(start * (count - 1));
            var last = (int)Math.Ceiling(end * (count - 1));
            if (last - first < 3)
            {
                last = Math.Min(count - 1, first + 3);
            }

            var window = mrr.Skip(first).Take(last - first + 1).ToArray();
            var labels = months.Skip(first).Take(window.Length).ToList();
            var previous = mrr.Skip(Math.Max(0, first - window.Length)).Take(window.Length).ToArray();

            var series = new List<ChartSeries> { new("MRR", window) };
            if (compare && previous.Length > 1)
            {
                series.Add(new ChartSeries("Previous period", previous, DataKit.Brush(ArcTokens.TextMuted), true));
            }

            chartHost.Content = new ArcLineChart
            {
                Series = series,
                Labels = labels,
                Height = 206,
                ShowArea = true,
                ShowLegend = compare,
                ShowGrid = true,
                Smooth = true,
                ValueFormat = value => DataKit.Money(value)
            };

            var enabled = components.Where(component => component.Enabled).ToList();
            var newTotal = enabled.Where(item => item.Name == "New").Sum(item => item.Values.Skip(first).Take(window.Length).Sum());
            var expansionTotal = enabled.Where(item => item.Name == "Expansion").Sum(item => item.Values.Skip(first).Take(window.Length).Sum());
            var churnTotal = enabled.Where(item => item.Name == "Churn").Sum(item => item.Values.Skip(first).Take(window.Length).Sum());
            var net = newTotal + expansionTotal - churnTotal;
            var latest = window[^1];
            var growth = (window[^1] - window[0]) / Math.Max(1, window[0]);

            caption.Text = $"{labels[0]} to {labels[^1]} · {window.Length} months";

            summaryHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    DataKit.Stat("Closing MRR", DataKit.Money(latest, 0),
                        DataKit.Signed(growth * 100, "%") + " over the range"),
                    DataKit.Divider(),
                    DataKit.Stat("Net new MRR", DataKit.Money(net),
                        $"new {DataKit.MoneyCompact(newTotal)} + expansion {DataKit.MoneyCompact(expansionTotal)} − churn {DataKit.MoneyCompact(churnTotal)}"),
                    new Border { Height = 2 },
                    DataKit.Caption("Gross MRR movement"),
                    CompositionLegend(enabled, first, window.Length)
                }
            };
        }

        Control CompositionLegend(List<Component> enabled, int first, int length)
        {
            var panel = new StackPanel { Spacing = 6 };
            foreach (var component in components)
            {
                var total = component.Values.Skip(first).Take(length).Sum();
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 8,
                    Opacity = component.Enabled ? 1 : 0.45,
                    Children =
                    {
                        DataKit.Dot(DataKit.Series(component.SeriesIndex)),
                        DataKit.AtColumn(DataKit.Text(component.Name, "arc-body-sm"), 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.MoneyCompact(total), "arc-body-sm arc-medium"), 2)
                    }
                };
                panel.Children.Add(row);
            }

            return panel;
        }

        void RefreshArrange()
        {
            var first = (int)Math.Floor(start * (mrr.Length - 1));
            var last = (int)Math.Ceiling(end * (mrr.Length - 1));
            var length = Math.Max(1, last - first + 1);
            var host = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            foreach (var component in components)
            {
                var count = component.Values.Skip(first).Take(length).Sum();
                var chip = DataKit.Chip(component.Name, component.Enabled, () =>
                {
                    var enabledCount = components.Count(item => item.Enabled);
                    if (component.Enabled && enabledCount <= 1)
                    {
                        DataKit.Say(status, "Keep at least one component visible.");
                        return;
                    }

                    var index = components.IndexOf(component);
                    components[index] = component with { Enabled = !component.Enabled };
                    RefreshArrange();
                    Refresh();
                    DataKit.Say(status, $"{component.Name} {(component.Enabled ? "hidden" : "shown")} in the composition.");
                }, DataKit.MoneyCompact(count));
                host.Children.Add(chip);
            }

            arrangementHost.Content = host;
        }

        brush.WindowChanged += (_, window) =>
        {
            start = window.Start;
            end = window.End;
            Refresh();
            RefreshArrange();
        };

        var compareToggle = DataKit.Toggle("Compare to previous", compare, value =>
        {
            compare = value;
            Refresh();
        });

        Refresh();
        RefreshArrange();

        var header = DataKit.Header(
            "Revenue explorer",
            "Monthly recurring revenue through Nov 2026",
            compareToggle);

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Recurring revenue"), arrangementHost),
                chartHost,
                chartHostHost,
                DataKit.Row(8,
                    caption,
                    DataKit.Caption("· drag the strip to zoom, edges resize, arrows pan"),
                    DataKit.Link("Reset", () =>
                    {
                        start = 0;
                        end = 1;
                        brush.SetWindow(0, 1);
                        DataKit.Say(status, "Range reset to all 24 months.");
                    }))
            }
        });

        var summaryCard = DataKit.Card(new StackPanel
        {
            Spacing = 10,
            Children = { DataKit.Label("Range summary"), summaryHost }
        }, width: 292);

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,292"),
            Children = { chartCard, DataKit.AtColumn(summaryCard, 2) }
        };

        var movementRows = new StackPanel { Spacing = 0 };
        var accounts = new (string Name, string Detail, double Amount)[]
        {
            ("Northwind Labs", "Expansion · 42 seats", 8_400),
            ("Kestrel Health", "New · annual", 6_200),
            ("Bramble Studio", "Expansion · usage", 3_950),
            ("Halcyon Freight", "Churn · downsell", -2_100)
        };
        foreach (var account in accounts)
        {
            movementRows.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10,
                Height = 28,
                Children =
                {
                    DataKit.Pill(account.Amount >= 0 ? "Up" : "Down", account.Amount >= 0 ? ArcBadgeTone.Success : ArcBadgeTone.Danger, true),
                    DataKit.AtColumn(new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            DataKit.Text(account.Name, "arc-body-sm"),
                            DataKit.Caption(account.Detail)
                        }
                    }, 1),
                    DataKit.AtColumn(DataKit.Text(DataKit.Signed(account.Amount), "arc-body-sm arc-medium"), 2)
                }
            });
        }

        var movementCard = DataKit.Card(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Largest movements"), DataKit.Caption("this range")),
                movementRows
            }
        });

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, top, movementCard, status }
        }, 16);
    }
}
