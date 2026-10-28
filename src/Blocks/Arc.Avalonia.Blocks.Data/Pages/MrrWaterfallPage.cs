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
/// MRR waterfall: a bridge of floating bars from starting to ending MRR, a period switch that rebuilds the
/// bridge, and a panel that lists the accounts behind the selected movement. All movements and accounts are
/// seeded sample data. An independent Avalonia implementation of Arc's public mrr-waterfall block; no Pro
/// source was used.
/// </summary>
public sealed class MrrWaterfallPage : ArcItemPage
{
    public override string ItemSlug => "mrr-waterfall";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Revenue or board reporting that explains how MRR moved from start to end of a period.",
        "Finance views where each movement should list the accounts behind it."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use revenue-explorer for MRR over many months.",
        "Use bar-chart for plain comparisons that do not build from a start value."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The bridge is a radio group: Tab enters it, the arrow keys move the selected bar (wrapping at the ends), and Home and End select the first or last movement.",
        "Each bar reads its movement, amount and account count, and the accounts panel is labelled by the selected movement."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Bars redraw to new levels when the period changes, the selected bar's value rises above it, and the accounts list is replaced in place.",
        "Reduced motion swaps the figures without a rise; nothing overshoots because the values are static."
    };

    public override string? ImplementationNotes =>
        "The bridge is drawn by a custom control (ArcWaterfallBars) because Arc's waterfall is a bespoke block; levels are truncated to the movement band with labelled gridlines, so a reader can see the axis does not start at zero. Account lists are generated deterministically from the movement total and are not virtualised.";

    private sealed record Period(string Key, string Label, string Caption, double Start, (string Label, double Delta)[] Moves);

    private static readonly Period[] Periods =
    {
        new("oct", "October", "October 2026",
            186_400,
            new[]
            {
                ("New", 18_600.0), ("Expansion", 9_200.0), ("Reactivation", 2_400.0),
                ("Contraction", -4_100.0), ("Churn", -7_900.0)
            }),
        new("nov", "November", "November 2026",
            204_600,
            new[]
            {
                ("New", 21_200.0), ("Expansion", 7_400.0), ("Reactivation", 1_800.0),
                ("Contraction", -5_600.0), ("Churn", -9_300.0)
            }),
        new("q3", "Q3", "Q3 2026 (Jul – Sep)",
            168_200,
            new[]
            {
                ("New", 46_400.0), ("Expansion", 22_800.0), ("Reactivation", 5_200.0),
                ("Contraction", -11_700.0), ("Churn", -20_300.0)
            })
    };

    private static readonly string[] AccountNames =
    {
        "Northwind Labs", "Kestrel Health", "Bramble Studio", "Halcyon Freight",
        "Meridian Works", "Okapi Retail", "Pinewood AI", "Quartz Bank",
        "Sable & Co", "Tidewater Group"
    };

    private static readonly string[] PlanNames = { "Scale", "Growth", "Enterprise" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var periodKey = "oct";
        var selected = 1;
        var bridgeHost = new ContentControl();
        var accountsHost = new ContentControl();
        var statHost = new ContentControl();
        var caption = DataKit.Caption(string.Empty);
        var status = DataKit.Status();
        ArcWaterfallBars? bridge = null;

        Period Current() => Periods.First(item => item.Key == periodKey);

        IReadOnlyList<WaterfallBar> BuildBars(Period period)
        {
            var bars = new List<WaterfallBar>
            {
                new("Start", period.Start, "Opening MRR", total: true)
            };
            foreach (var move in period.Moves)
            {
                var count = Math.Max(2, (int)Math.Round(Math.Abs(move.Delta) / 2600));
                bars.Add(new WaterfallBar(move.Label, move.Delta, $"{count} accounts"));
            }

            var end = period.Start + period.Moves.Sum(move => move.Delta);
            bars.Add(new WaterfallBar("End", end, "Closing MRR", total: true));
            return bars;
        }

        List<(string Name, string Detail, double Amount)> AccountsFor(Period period, int index)
        {
            // Index 0 and the last index are totals; the moves sit between them.
            var result = new List<(string, string, double)>();
            if (index <= 0 || index > period.Moves.Length)
            {
                var total = index <= 0 ? period.Start : period.Start + period.Moves.Sum(move => move.Delta);
                return new List<(string, string, double)>
                {
                    ("Book total", $"{period.Moves.Length} movements", total)
                };
            }

            var move = period.Moves[index - 1];
            var shares = new[] { 0.38, 0.27, 0.21, 0.14 };
            var count = Math.Max(2, (int)Math.Round(Math.Abs(move.Delta) / 2600));
            for (var i = 0; i < shares.Length && i < count; i++)
            {
                var name = AccountNames[(index * 2 + i) % AccountNames.Length];
                var plan = PlanNames[(index + i) % PlanNames.Length];
                var seats = 12 + ((index * 7 + i * 5) % 60);
                result.Add((name, $"{plan} · {seats} seats", Math.Round(move.Delta * shares[i] / 100) * 100));
            }

            return result;
        }

        void RefreshAccounts()
        {
            var period = Current();
            var index = bridge?.Selected ?? selected;
            var accounts = AccountsFor(period, index);

            var rows = new StackPanel { Spacing = 0 };
            foreach (var account in accounts)
            {
                rows.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 10,
                    Height = 34,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 0,
                            Children =
                            {
                                DataKit.Text(account.Name, "arc-body-sm arc-medium"),
                                DataKit.Caption(account.Detail)
                            }
                        },
                        DataKit.AtColumn(DataKit.Text(DataKit.Signed(account.Amount), "arc-body-sm arc-medium"), 1)
                    }
                });
            }

            var title = index <= 0
                ? "Opening MRR"
                : index > period.Moves.Length ? "Closing MRR" : period.Moves[index - 1].Label + " MRR";
            var detail = index <= 0 || index > period.Moves.Length
                ? period.Caption + " · book total"
                : $"{period.Caption} · {accounts.Count} of " +
                  Math.Max(2, (int)Math.Round(Math.Abs(period.Moves[index - 1].Delta) / 2600)) + " accounts shown";

            accountsHost.Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    DataKit.Row(8, DataKit.Label(title), DataKit.Caption("· " + period.Label)),
                    DataKit.Caption(detail),
                    new Border { Height = 4 },
                    rows,
                    DataKit.Link("View all accounts", () =>
                        DataKit.Say(status, "The reference opens the full account list; this specimen shows four.")),
                    DataKit.Divider(),
                    DataKit.Caption("Amounts are monthly recurring revenue in USD. Select a bar to list its accounts.")
                }
            };
        }

        void RefreshStats()
        {
            var period = Current();
            var end = period.Start + period.Moves.Sum(move => move.Delta);
            var churn = period.Moves.Where(move => move.Label == "Churn").Sum(move => move.Delta);
            var contraction = period.Moves.Where(move => move.Label == "Contraction").Sum(move => move.Delta);
            var gross = period.Moves.Where(move => move.Delta > 0).Sum(move => move.Delta);
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note)[]
            {
                ("Starting MRR", DataKit.Money(period.Start), period.Caption),
                ("Gross new", DataKit.Signed(gross), "new, expansion, reactivation"),
                ("Contraction + churn", DataKit.Signed(contraction + churn), $"{DataKit.Percent(Math.Abs(contraction + churn) / period.Start, 1)} of starting MRR"),
                ("Ending MRR", DataKit.Money(end), $"net {DataKit.Signed(end - period.Start)}")
            };
            for (var index = 0; index < tiles.Length; index++)
            {
                var tile = tiles[index];
                var card = DataKit.Tile(new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        DataKit.Caption(tile.Label),
                        DataKit.Text(tile.Value, "arc-subtitle"),
                        DataKit.Caption(tile.Note)
                    }
                });
                Grid.SetColumn(card, index * 2);
                stats.Children.Add(card);
            }

            statHost.Content = stats;
        }

        void Refresh()
        {
            var period = Current();
            var bars = BuildBars(period);
            selected = Math.Min(selected, bars.Count - 1);
            bridge = new ArcWaterfallBars(bars) { Selected = selected };
            bridge.SelectionChanged += (_, index) =>
            {
                selected = index;
                RefreshAccounts();
                DataKit.Say(status, $"Selected {bars[index].Label}: {(bars[index].Total ? DataKit.Money(bars[index].Delta) : DataKit.Signed(bars[index].Delta))}.");
            };
            bridgeHost.Content = bridge;
            caption.Text = period.Caption + " · account movements";
            RefreshStats();
            RefreshAccounts();
        }

        var periodSelector = DataKit.Segmented(Periods.Select(item => (item.Key, item.Label)), periodKey, key =>
        {
            periodKey = key;
            Refresh();
            DataKit.Say(status, $"Bridge rebuilt for {Periods.First(item => item.Key == key).Caption}.");
        });

        Refresh();

        var header = DataKit.Header("MRR bridge", "How recurring revenue moved from start to end of the period", periodSelector);

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Bridge"), caption),
                bridgeHost,
                DataKit.Row(14,
                    Legend(DataKit.Brush(ArcTokens.Success), "New and expansion"),
                    Legend(DataKit.Brush(ArcTokens.Danger), "Contraction and churn"),
                    Legend(Ink.Alpha(DataKit.Brush(ArcTokens.Foreground), 0.82), "Period total"))
            }
        });

        var sideCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("Accounts"), accountsHost }
        }, width: 300);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,300"),
            Children = { chartCard, DataKit.AtColumn(sideCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, statHost, body, status }
        }, 16);
    }

    private static Control Legend(global::Avalonia.Media.IBrush brush, string label) => DataKit.Row(6,
        new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(4), Background = brush, VerticalAlignment = VerticalAlignment.Center },
        DataKit.Caption(label));
}
