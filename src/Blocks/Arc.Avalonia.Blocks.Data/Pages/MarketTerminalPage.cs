using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Market terminal: a candlestick chart with volume and moving averages, a crosshair OHLC readout, a live
/// watchlist with sparklines and a market or limit order ticket. Prices are seeded sample data; submitting an
/// order only reports locally. An independent Avalonia implementation of Arc's public market-terminal block;
/// no Pro source was used.
/// </summary>
public sealed class MarketTerminalPage : ArcItemPage
{
    public override string ItemSlug => "market-terminal";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-terminal", "variant-candles", "variant-watchlist",
        "variant-order-ticket", "variant-limit-order"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A trading or treasury terminal where a candle chart, watchlist and order ticket share one surface.",
        "Showcase pages that need a dense, real-time-looking market view with a crosshair."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use line-chart for a single price series where candles add no information.",
        "Do not build a real order entry surface on sample data; connect a broker venue instead."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The chart is focusable: arrows step the crosshair, Home and End jump, and plus or minus zoom the window.",
        "The OHLC readout repeats every candle as text, and the ticket labels side, type, quantity and price."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The watchlist flashes on price changes in the reference; this port keeps prices static so a screenshot is deterministic and only the crosshair moves.",
        "Reduced motion removes the crosshair fade and never animates the quote."
    };

    public override string? ImplementationNotes =>
        "Candles, volume and averages are drawn by ArcCandleChart from 120 seeded bars per symbol. The watchlist does not flash, there is no streaming feed or pinch zoom, and the order ticket never reaches a venue.";

    private sealed record Symbol(string Key, string Name, double Start, double Drift, double Volatility, int Seed);

    private static readonly Symbol[] Symbols =
    {
        new("acme", "ACME", 184.20, 0.0009, 0.012, 1201),
        new("nova", "NOVA", 92.40, -0.0004, 0.018, 1202),
        new("heli", "HELI", 41.80, 0.0016, 0.022, 1203)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var candles = Symbols.ToDictionary(symbol => symbol.Key, symbol => BuildCandles(symbol));
        var symbolKey = "acme";
        var side = "buy";
        var orderType = "limit";
        var quantity = 25;
        var chartHost = new ContentControl();
        var quoteHost = new ContentControl();
        var watchHost = new ContentControl();
        var ticketHost = new ContentControl();
        var status = DataKit.Status();
        var chart = (ArcCandleChart?)null;
        var limitPrice = candles[symbolKey][^1].Close;

        Symbol Active() => Symbols.First(item => item.Key == symbolKey);

        void RefreshWatch()
        {
            var rows = new StackPanel { Spacing = 2 };
            foreach (var symbol in Symbols)
            {
                var bars = candles[symbol.Key];
                var last = bars[^1];
                var change = (last.Close - bars[0].Close) / bars[0].Close * 100;
                var selected = symbol.Key == symbolKey;
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,66,76"),
                    ColumnSpacing = 8,
                    Height = 32,
                    Background = selected ? DataKit.Brush(ArcTokens.SurfaceMuted) : null,
                    Children =
                    {
                        DataKit.Text(symbol.Name, "arc-body-sm arc-medium"),
                        DataKit.AtColumn(DataKit.Spark(bars.Select(bar => bar.Close).ToList(), 24,
                            change >= 0 ? ChartTone.Accent : ChartTone.Danger), 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.Money(last.Close, 2), "arc-body-sm arc-medium"), 2),
                        DataKit.AtColumn(DataKit.Delta(change, "%", 2), 3)
                    }
                };
                var button = new Border
                {
                    Child = row,
                    Padding = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Focusable = true
                };
                var key = symbol.Key;
                button.PointerPressed += (_, args) =>
                {
                    symbolKey = key;
                    limitPrice = candles[key][^1].Close;
                    RefreshChart();
                    RefreshWatch();
                    RefreshTicket();
                    args.Handled = true;
                };
                rows.Children.Add(button);
            }

            watchHost.Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    DataKit.Label("Watchlist"),
                    rows
                }
            };
        }

        void RefreshQuote(Candle? inspected = null)
        {
            var bars = candles[symbolKey];
            var candle = inspected ?? bars[^1];
            quoteHost.Content = DataKit.Row(14,
                DataKit.Text(DataKit.Money(candle.Close, 2), "arc-subtitle"),
                DataKit.Delta((candle.Close - bars[0].Close) / bars[0].Close * 100, "%", 2),
                DataKit.Caption($"O {DataKit.Money(candle.Open, 2)}"),
                DataKit.Caption($"H {DataKit.Money(candle.High, 2)}"),
                DataKit.Caption($"L {DataKit.Money(candle.Low, 2)}"),
                DataKit.Caption($"V {DataKit.Compact(candle.Volume)}"));
        }

        void RefreshChart()
        {
            chart = new ArcCandleChart(candles[symbolKey]) { Height = 280 };
            chart.Inspected += (_, candle) => RefreshQuote(candle);
            chartHost.Content = chart;
            RefreshQuote();
        }

        void RefreshTicket()
        {
            var bars = candles[symbolKey];
            var reference = orderType == "limit" ? limitPrice : bars[^1].Close;
            var notional = reference * quantity;

            var qtyText = new TextBlock
            {
                Text = quantity.ToString(CultureInfo.InvariantCulture),
                Classes = { "arc-body-sm", "arc-medium" },
                Width = 34,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var qtyRow = new Border
            {
                Background = DataKit.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(3),
                Child = DataKit.Row(4,
                    DataKit.IconBtn("minus", "Decrease quantity", () =>
                    {
                        quantity = Math.Max(1, quantity - 5);
                        RefreshTicket();
                    }, 26),
                    qtyText,
                    DataKit.IconBtn("plus", "Increase quantity", () =>
                    {
                        quantity += 5;
                        RefreshTicket();
                    }, 26))
            };

            var priceField = new Arc.Avalonia.Inputs.Controls.ArcInput
            {
                Value = reference.ToString("0.00", CultureInfo.InvariantCulture),
                IsEnabled = orderType == "limit"
            };

            ticketHost.Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    DataKit.Label("Order ticket"),
                    DataKit.Segmented(new[] { ("buy", "Buy"), ("sell", "Sell") }, side, key =>
                    {
                        side = key;
                        RefreshTicket();
                    }),
                    DataKit.Segmented(new[] { ("market", "Market"), ("limit", "Limit") }, orderType, key =>
                    {
                        orderType = key;
                        RefreshTicket();
                    }),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,10,*"),
                        Children =
                        {
                            new StackPanel { Spacing = 4, Children = { DataKit.Caption("Quantity"), qtyRow } },
                            DataKit.AtColumn(new StackPanel
                            {
                                Spacing = 4,
                                Children = { DataKit.Caption("Limit price"), priceField }
                            }, 2)
                        }
                    },
                    DataKit.Line("Estimated notional", DataKit.Money(notional, 2)),
                    DataKit.Btn(side == "buy" ? "Buy " + Active().Name : "Sell " + Active().Name,
                        ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                        {
                            DataKit.Say(status,
                                $"{side.ToUpperInvariant()} {quantity} {Active().Name} at {(orderType == "market" ? "market" : DataKit.Money(limitPrice, 2))} · notional {DataKit.Money(notional, 2)}. Sample order, nothing was sent.");
                        }, icon: side == "buy" ? "arrow-up-right" : "arrow-down-right")
                }
            };
        }

        var symbolSelector = DataKit.Segmented(Symbols.Select(symbol => (symbol.Key, symbol.Name)), symbolKey, key =>
        {
            symbolKey = key;
            limitPrice = candles[key][^1].Close;
            RefreshChart();
            RefreshWatch();
            RefreshTicket();
            DataKit.Say(status, $"Symbol set to {Active().Name}.");
        });

        RefreshChart();
        RefreshWatch();
        RefreshTicket();

        var header = DataKit.Header("Market terminal", $"{Active().Name} · 15 minute candles · session 09:30 to 15:30",
            DataKit.Row(10, symbolSelector, DataKit.Pill("Market open", ArcBadgeTone.Success, true)));

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 2,
                            Children = { DataKit.Label(Active().Name + " · ACME Cloud"), quoteHost }
                        },
                        DataKit.AtColumn(DataKit.Caption("+ / − zoom · arrows inspect"), 1)
                    }
                },
                chartHost,
                DataKit.Caption("Candles use series one for up bars and series four for down bars; the dashed lines are the 5 and 20 period averages.")
            }
        });

        var sideCard = DataKit.Card(new StackPanel
        {
            Spacing = 10,
            Children = { watchHost, DataKit.Divider(), ticketHost }
        }, width: 300);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,300"),
            Children = { chartCard, DataKit.AtColumn(sideCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, body, status }
        }, 16);
    }

    private static List<Candle> BuildCandles(Symbol symbol)
    {
        var random = DataKit.Random(symbol.Seed);
        var bars = new List<Candle>(120);
        var price = symbol.Start;
        var start = new DateTime(2026, 11, 26, 9, 30, 0);
        for (var index = 0; index < 120; index++)
        {
            var open = price;
            var move = symbol.Drift + (random() - 0.5) * symbol.Volatility;
            var close = Math.Max(1, open * (1 + move));
            var high = Math.Max(open, close) * (1 + random() * symbol.Volatility * 0.6);
            var low = Math.Min(open, close) * (1 - random() * symbol.Volatility * 0.6);
            var volume = Math.Round(180_000 + random() * 640_000);
            bars.Add(new Candle(
                start.AddMinutes(index * 3).ToString("HH:mm", CultureInfo.InvariantCulture),
                Math.Round(open, 2), Math.Round(high, 2), Math.Round(low, 2), Math.Round(close, 2), volume));
            price = close;
        }

        return bars;
    }
}
