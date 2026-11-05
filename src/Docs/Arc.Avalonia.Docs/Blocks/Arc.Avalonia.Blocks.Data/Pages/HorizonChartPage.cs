using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Horizon chart: fourteen daily series folded into compact rows, with a synced crosshair, a sort by name,
/// peak or latest, a band count that folds every row together and a legend that explains the bands. Values are
/// seeded percentage deviations from each series' own baseline. An independent Avalonia implementation of
/// Arc's public horizon-chart block; no Pro source was used.
/// </summary>
public sealed class HorizonChartPage : ArcItemPage
{
    public override string ItemSlug => "horizon-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-horizon", "variant-folded-rows", "variant-bands", "variant-sort", "variant-legend"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Operations or growth dashboards comparing many markets, services or products against their own baseline over weeks.",
        "Monitoring views where the reader scans which rows broke from normal and when, then reads exact values on one day."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use small-multiples when there are only a few series and the reader needs axes and exact levels in each panel.",
        "Use ridgeline to compare distributions rather than values over time."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The chart is one focusable group: arrows move the crosshair, Shift moves a week, Home and End jump, Escape clears.",
        "The date and the highest and lowest rows are repeated in a polite live line."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Changing the band count folds every row on one shared value; the reference springs it, while this port repaints each row together.",
        "Reduced motion removes the fold animation and paints the new band count at once."
    };

    public override string? ImplementationNotes =>
        "Rows are drawn as four stacked band areas per side in one hue each rather than as separately folded layers with a shared transform, so a band count change repaints instead of animating a CSS-like fold. Values are signed percentages from a seeded baseline; fourteen rows over sixty days keep the chart legible at 1000 px.";

    private static readonly string[] Markets =
    {
        "North America", "United Kingdom", "Germany", "France", "Netherlands", "Spain",
        "Brazil", "Mexico", "Japan", "Singapore", "Australia", "India",
        "Canada", "South Africa"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var days = 60;
        var labels = Enumerable.Range(0, days)
            .Select(offset => new DateTime(2026, 9, 28).AddDays(offset).ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        var random = DataKit.Random(6120);
        var series = Markets.Select((market, index) =>
        {
            var values = new double[days];
            var level = (random() - 0.5) * 8;
            for (var day = 0; day < days; day++)
            {
                level = level * 0.94 + (random() - 0.48) * 9;
                var seasonal = Math.Sin((day + index * 3) / 7.0 * Math.PI * 2) * 5;
                values[day] = Math.Round(level + seasonal, 1);
            }

            return new HorizonSeries(market, values);
        }).ToList();

        var sort = "peak";
        var bands = 3;
        var stageHost = new ContentControl();
        var readout = DataKit.Caption("Focus the chart or hover a row to read one day across every market.");
        var status = DataKit.Status();
        ArcHorizonChart? chart = null;

        void Refresh()
        {
            if (chart is null)
            {
                chart = new ArcHorizonChart(series, labels);
                chart.DayChanged += (_, index) =>
                {
                    var top = series.OrderByDescending(item => item.Values[index]).First();
                    var bottom = series.OrderBy(item => item.Values[index]).First();
                    readout.Text = $"{labels[index]} · highest {top.Name} {DataKit.Signed(top.Values[index], "%", 1)} · lowest {bottom.Name} {DataKit.Signed(bottom.Values[index], "%", 1)}";
                };
                stageHost.Content = chart;
            }

            chart.Sort = sort;
            chart.Bands = bands;
            chart.InvalidateVisual();
        }

        var sortSelector = DataKit.Segmented(new[] { ("peak", "Peak"), ("latest", "Latest"), ("name", "Name") },
            sort, key =>
            {
                sort = key;
                Refresh();
                DataKit.Say(status, $"Rows sorted by {key}.");
            }, label: null);

        var bandSelector = DataKit.Segmented(new[] { ("1", "1 band"), ("2", "2 bands"), ("3", "3 bands"), ("4", "4 bands") },
            "3", key =>
            {
                bands = int.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
                Refresh();
                DataKit.Say(status, $"{bands} bands per side.");
            });

        Refresh();

        var header = DataKit.Header("Sessions against baseline", "14 markets · 60 days · deviation from each market's autumn average",
            DataKit.Row(10, sortSelector, bandSelector));

        var card = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Markets"), DataKit.Caption("above baseline uses series one, below uses series three")),
                stageHost,
                readout
            }
        });

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                Legend(DataKit.Brush(ArcTokens.Series1), "above baseline"),
                Legend(DataKit.Brush(ArcTokens.Series3), "below baseline"),
                DataKit.Caption("Darker bands are further from the baseline; the band switch folds every row at once.")
            }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, card, legend, status }
        }, 16);
    }

    private static Control Legend(global::Avalonia.Media.IBrush brush, string label) => DataKit.Row(6,
        new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(3),
            Background = brush,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        },
        DataKit.Caption(label));
}
