using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Small multiples: a trellis of twelve small line charts that reads as one chart, with a synced crosshair, a
/// shared or independent y scale, sorting and dimming for flat trends. A ghost of the overall total sits
/// behind every panel. Series are seeded sample data. An independent Avalonia implementation of Arc's public
/// small-multiples block; no Pro source was used.
/// </summary>
public sealed class SmallMultiplesPage : ArcItemPage
{
    public override string ItemSlug => "small-multiples";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-multiples", "variant-trellis", "variant-ghost-total", "variant-scale-shared", "variant-sort"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Eight to twelve similar series on one time axis where the shape matters more than exact levels.",
        "Operations or growth reviews that need to spot which panel breaks from its usual pattern."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use line-chart for one to four series that should be read precisely against a y axis.",
        "Use horizon-chart when there are more series than fit even as a trellis."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The trellis is one focusable group: arrows move the shared crosshair by a day, Home and End jump to the ends.",
        "The crosshair date and the hovered panel's values are repeated in a polite live line under the chart, so nothing depends on hover alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Sorting reorders panels by name, peak or latest; the reference slides rows with a FLIP transition, while this port paints the new order at once.",
        "Reduced motion removes the crosshair fade; the shared line appears in place."
    };

    public override string? ImplementationNotes =>
        "Panels are laid out by a fixed four-column grid rather than measured containers, so a resize keeps the same order. The ghost line is the sum of all series on its own scale, not a shared baseline; hover uses pointer distance to the nearest day and the tooltip lists the three largest panels only.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var days = 24;
        var labels = Enumerable.Range(1, days).Select(day => "Nov " + day).ToList();
        var names = new[]
        {
            "Organic", "Paid search", "Social", "Referral", "Email", "Direct",
            "Partners", "Affiliates", "Display", "Podcast", "Docs", "Community"
        };
        var series = names.Select((name, index) => new MultipleSeries(name,
            DataSamples.Daily(days, 120 + index * 34, 1.4 + index * 0.1, 14 + index * 2, 12 + index, 800 + index, 6)
                .Select(value => Math.Round(value)).ToArray())).ToList();

        var shared = true;
        var sort = "peak";
        var stageHost = new ContentControl();
        var readout = DataKit.Caption("Move the pointer across a panel, or use the arrow keys, to read one day across every series.");
        var status = DataKit.Status();
        ArcSmallMultiples? trellis = null;

        void Refresh()
        {
            if (trellis is null)
            {
                trellis = new ArcSmallMultiples(series, labels);
                trellis.DayChanged += (_, index) =>
                {
                    var top = series.OrderByDescending(item => item.Values[index]).First();
                    var bottom = series.OrderBy(item => item.Values[index]).First();
                    readout.Text = $"{labels[index]} · highest {top.Name} {DataKit.Number(top.Values[index])} · lowest {bottom.Name} {DataKit.Number(bottom.Values[index])}";
                };
                stageHost.Content = trellis;
            }

            trellis.SharedScale = shared;
            trellis.Sort = sort;
            trellis.InvalidateVisual();
        }

        var sortSelector = DataKit.Segmented(new[] { ("peak", "Peak"), ("latest", "Latest"), ("name", "Name") }, sort, key =>
        {
            sort = key;
            Refresh();
            DataKit.Say(status, $"Panels sorted by {key}.");
        });

        var scaleToggle = new ContentControl();
        scaleToggle.Content = DataKit.Toggle("Shared y scale", shared, value =>
        {
            shared = value;
            Refresh();
            DataKit.Say(status, value ? "Shared y scale: panels compare by height." : "Independent y scale: every panel keeps its own range.");
        });

        Refresh();

        var header = DataKit.Header("Sessions by channel", "24 days · 12 panels · one shared crosshair",
            DataKit.Row(10, sortSelector, scaleToggle));

        var card = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Trellis"), DataKit.Caption("ghost line is the total across channels")),
                stageHost,
                readout,
                DataKit.Caption("Panels with a flat trend are dimmed; the ghost line keeps the overall shape in view.")
            }
        });

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                DataKit.Row(6,
                    new Border { Width = 14, Height = 2, Background = DataKit.Brush(ArcTokens.Series1), VerticalAlignment = VerticalAlignment.Center },
                    DataKit.Caption("Channel")),
                DataKit.Row(6,
                    new Border { Width = 14, Height = 1, Background = DataKit.Brush(ArcTokens.TextMuted), VerticalAlignment = VerticalAlignment.Center },
                    DataKit.Caption("Total, dashed")),
                DataKit.Caption("A flat panel is one whose range is under 8% of its peak.")
            }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, card, legend, status }
        }, 16);
    }
}
