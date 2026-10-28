using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Usage forecast: a billing period spend chart with a forecast cone and a draggable budget line that dates
/// the crossing, plus alert rules and a forecast summary. All readings and rules are sample data; nothing is
/// persisted. An independent Avalonia implementation of Arc's public usage-forecast block; no Pro source
/// was used.
/// </summary>
public sealed class UsageForecastPage : ArcItemPage
{
    public override string ItemSlug => "usage-forecast";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Billing or usage pages that warn customers before they hit a budget.",
        "Metered products that show month to date usage with a forecast range."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use usage-meter for a simple progress bar against a limit.",
        "Use usage-pricing to estimate price from usage before buying."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The plot and the budget are one focusable slider: Up and Down move the budget a step, Page Up and Page Down move five, Home and End jump to the ends, and the arrow keys scrub a day.",
        "Alert rules are toggle buttons whose labels include their current state, and the forecast summary is a polite live region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The budget line follows drags directly and settles on a snap when released or moved by keyboard; the crossing marker and summary follow without overshoot.",
        "Reduced motion applies budget changes immediately with no spring."
    };

    public override string? ImplementationNotes =>
        "The forecast is a linear trend carried from the last seven measured days with a fixed confidence band, not a statistical model; the crossing is dated by interpolation on the cumulative projection. Spend is in dollars and the chart plots cumulative spend so the crossing has a real date. Alert rules only change local state.";

    private static readonly DateTime PeriodStart = new(2026, 11, 1);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        const int days = 30;
        const int measured = 18;
        var daily = DataSamples.Daily(days, 236, 7.4, 26, 38, 5150, 48);
        var projectedDaily = daily.ToArray();
        for (var day = measured; day < days; day++)
        {
            projectedDaily[day] = Math.Round(daily[measured - 1] + (day - measured + 1) * 8.6 +
                                             Math.Sin((day - measured) / 7.0 * Math.PI) * 22, 1);
        }

        var actual = new double[days];
        var forecast = new double[days];
        var low = new double[days];
        var high = new double[days];
        var actualSum = 0.0;
        var projectedSum = 0.0;
        for (var day = 0; day < days; day++)
        {
            if (day < measured)
            {
                actualSum += daily[day];
                actual[day] = Math.Round(actualSum);
                forecast[day] = double.NaN;
                low[day] = double.NaN;
                high[day] = double.NaN;
            }
            else
            {
                actual[day] = double.NaN;
            }
        }

        for (var day = measured - 1; day < days; day++)
        {
            if (day == measured - 1)
            {
                projectedSum = actualSum;
            }
            else
            {
                projectedSum += projectedDaily[day];
            }

            var spread = projectedSum * (0.02 + (day - measured + 1) * 0.006);
            forecast[day] = Math.Round(projectedSum);
            low[day] = Math.Round(projectedSum - spread);
            high[day] = Math.Round(projectedSum + spread);
        }

        var budget = 8000.0;
        var status = DataKit.Status();
        var statHost = new ContentControl();
        var summaryHost = new ContentControl();
        var meterHost = new ContentControl();
        var legendHost = new ContentControl();
        var alerts = new List<(string Label, bool On)> { ("Email at 80%", true), ("Email at 100%", true), ("Slack at 120%", false) };
        var alertHost = new StackPanel { Spacing = 6 };

        ArcForecastCone? chart = null;

        string CrossingLabel(double budgetValue, out double crossingValue)
        {
            for (var day = measured - 1; day < days; day++)
            {
                if (forecast[day] >= budgetValue)
                {
                    crossingValue = forecast[day];
                    return PeriodStart.AddDays(day).ToString("MMM d", CultureInfo.InvariantCulture);
                }
            }

            crossingValue = forecast[^1];
            return "after Nov 30";
        }

        void Refresh()
        {
            var crossing = CrossingLabel(budget, out var crossingValue);
            var projected = forecast[^1];
            var over = projected - budget;

            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note, ArcBadgeTone Tone)[]
            {
                ("Spend so far", DataKit.Money(actualSum), $"day {measured} of {days}", ArcBadgeTone.Neutral),
                ("Projected month end", DataKit.Money(projected), $"confidence ±{DataKit.Money(high[^1] - forecast[^1])}", ArcBadgeTone.Neutral),
                ("Budget", DataKit.Money(budget), over > 0 ? $"over by {DataKit.Money(over)}" : $"room for {DataKit.Money(-over)}", over > 0 ? ArcBadgeTone.Warning : ArcBadgeTone.Success),
                ("Crossing", crossing, over > 0 ? "budget reached" : "not reached", over > 0 ? ArcBadgeTone.Warning : ArcBadgeTone.Neutral)
            };
            for (var index = 0; index < tiles.Length; index++)
            {
                var tile = tiles[index];
                var card = DataKit.Tile(new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        DataKit.Caption(tile.Label),
                        DataKit.Text(tile.Value, "arc-subtitle"),
                        DataKit.Pill(tile.Note, tile.Tone, tile.Tone != ArcBadgeTone.Neutral)
                    }
                });
                Grid.SetColumn(card, index * 2);
                stats.Children.Add(card);
            }

            statHost.Content = stats;

            summaryHost.Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    DataKit.Text(over > 0
                        ? $"At this pace the period ends {DataKit.Money(over)} over budget on {crossing}."
                        : $"At this pace the period ends {DataKit.Money(-over)} under budget.", "arc-body-sm"),
                    DataKit.Caption($"Forecast from the last seven measured days · band {DataKit.Money(low[^1])} to {DataKit.Money(high[^1])}.")
                }
            };

            meterHost.Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    DataKit.Row(8,
                        DataKit.Caption("Allowance"),
                        DataKit.Caption($"{DataKit.Money(actualSum)} measured + {DataKit.Money(Math.Max(0, projected - actualSum))} projected of {DataKit.Money(budget)}")),
                    DataKit.Progress(Math.Min(projected, budget), budget,
                        over > 0 ? ArcProgressTone.Warning : ArcProgressTone.Accent)
                }
            };

            legendHost.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14,
                Children =
                {
                    Legend(DataKit.Brush(ArcTokens.Series1), "Measured"),
                    Legend(DataKit.Brush(ArcTokens.Series2), "Projected"),
                    Legend(DataKit.Brush(ArcTokens.Accent), "Budget")
                }
            };
        }

        static Control Legend(global::Avalonia.Media.IBrush brush, string label) => DataKit.Row(6,
            new Border { Width = 14, Height = 3, CornerRadius = new CornerRadius(2), Background = brush, VerticalAlignment = VerticalAlignment.Center },
            DataKit.Caption(label));

        void RefreshAlerts()
        {
            alertHost.Children.Clear();
            foreach (var alert in alerts)
            {
                var toggle = DataKit.Toggle(alert.Label, alert.On, value =>
                {
                    var index = alerts.FindIndex(item => item.Label == alert.Label);
                    alerts[index] = (alert.Label, value);
                    DataKit.Say(status, $"{alert.Label} {(value ? "on" : "off")}. Alert rules are local to this preview.");
                });
                alertHost.Children.Add(toggle);
            }
        }

        chart = new ArcForecastCone(actual, forecast, low, high, budget);
        chart.BudgetChanged += (sender, value) =>
        {
            budget = value;
            Refresh();
            DataKit.Say(status, $"Budget moved to {DataKit.Money(value)}; the crossing is {CrossingLabel(value, out var crossingForStatus)}.");
        };

        Refresh();
        RefreshAlerts();

        var header = DataKit.Header("Usage forecast",
            "November billing period · day 18 of 30 · Acme Inc.",
            DataKit.Row(8, DataKit.Pill("Metered plan", ArcBadgeTone.Neutral), DataKit.Meta("calendar", "Closes 30 Nov")));

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Cumulative spend"), DataKit.Caption("drag the budget line · ↑ ↓ adjust")),
                chart,
                legendHost,
                DataKit.Caption("Solid is measured, dashed is projected, the shaded cone is the confidence band."),
                DataKit.Divider(),
                meterHost
            }
        });

        var sideCard = DataKit.Card(new StackPanel
        {
            Spacing = 9,
            Children =
            {
                DataKit.Label("Forecast"),
                summaryHost,
                DataKit.Divider(),
                DataKit.Label("Alert rules"),
                alertHost,
                DataKit.Caption("Trend +4.2% a week, weekend spend 18% lower, band widens per projected day.")
            }
        }, width: 292);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,292"),
            Children = { chartCard, DataKit.AtColumn(sideCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, statHost, body, status }
        }, 16);
    }
}
