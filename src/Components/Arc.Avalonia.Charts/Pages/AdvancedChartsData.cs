using System;
using System.Collections.Generic;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>Deterministic sample data for the two advanced chart pages.</summary>
internal static class AdvancedChartsData
{
    /// <summary>Cloud spend: three branches, two levels below them.</summary>
    public static ArcSunburstNode CloudSpend() => new(
        "All teams",
        new ArcSunburstNode(
            "Platform",
            new ArcSunburstNode(
                "Compute",
                new ArcSunburstNode("api", 18910),
                new ArcSunburstNode("batch", 8160)),
            new ArcSunburstNode("Storage", 8670)),
        new ArcSunburstNode(
            "Data",
            new ArcSunburstNode("Warehouse", 31020),
            new ArcSunburstNode("Pipelines", 9400)),
        new ArcSunburstNode(
            "Product",
            new ArcSunburstNode("Web", 12400),
            new ArcSunburstNode("Mobile", 6800)));

    public static IReadOnlyList<ArcSankeyNode> SignupNodes() => new[]
    {
        new ArcSankeyNode("organic", "Organic"),
        new ArcSankeyNode("paid", "Paid"),
        new ArcSankeyNode("activated", "Activated"),
        new ArcSankeyNode("inactive", "Not activated"),
        new ArcSankeyNode("free", "Free plan"),
        new ArcSankeyNode("pro", "Pro plan")
    };

    public static IReadOnlyList<ArcSankeyLink> SignupLinks() => new[]
    {
        new ArcSankeyLink("organic", "activated", 3380),
        new ArcSankeyLink("organic", "inactive", 1820),
        new ArcSankeyLink("paid", "activated", 1990),
        new ArcSankeyLink("paid", "inactive", 1910),
        new ArcSankeyLink("activated", "free", 3860),
        new ArcSankeyLink("activated", "pro", 1510)
    };

    public static IReadOnlyList<ArcFunnelStage> CheckoutStages() => new[]
    {
        new ArcFunnelStage("Visited store", 100000),
        new ArcFunnelStage("Added to cart", 16420),
        new ArcFunnelStage("Started checkout", 9840),
        new ArcFunnelStage("Purchased", 3590)
    };

    public static IReadOnlyList<string> OfficeAxes() => new[]
    {
        "Talent pool", "Affordability", "Transit", "Quality of life", "Tax climate", "Connectivity"
    };

    public static IReadOnlyList<ChartSeries> OfficeProfiles() => new[]
    {
        new ChartSeries("Lisbon", new double[] { 72, 84, 78, 81, 66, 74 }),
        new ChartSeries("Berlin", new double[] { 81, 68, 88, 74, 58, 82 }),
        new ChartSeries("Austin", new double[] { 68, 74, 62, 70, 86, 71 })
    };

    /// <summary>Twelve readings of p95 latency; the stream fills the rest of its window from the same baseline.</summary>
    public static IReadOnlyList<ChartSeries> LatencySeeds() => new[]
    {
        new ChartSeries("p95", new double[] { 182, 176, 191, 204, 188, 172, 165, 179, 212, 196, 184, 178 }),
        new ChartSeries("p50", new double[] { 92, 88, 95, 101, 94, 87, 84, 90, 104, 98, 93, 89 })
    };

    private static readonly string[] ProductNames =
    {
        "Oak chair", "Linen sofa", "Arc lamp", "Walnut desk", "Wool rug", "Glass table", "Steel shelf"
    };

    /// <summary>Eight periods of revenue where the lead changes hands twice.</summary>
    public static IReadOnlyList<ArcRaceFrame> ProductRace()
    {
        var values = new[,]
        {
            { 14000, 12000, 8000, 6000, 4000, 3000, 2000 },
            { 17000, 21000, 9000, 12000, 5000, 6000, 2400 },
            { 22000, 25000, 15000, 14000, 7000, 9500, 3000 },
            { 26000, 28000, 21000, 15500, 13000, 11000, 3600 },
            { 31000, 30500, 22500, 16000, 19000, 12200, 9000 },
            { 35000, 32000, 23000, 17500, 24500, 13000, 14200 },
            { 40000, 33500, 30000, 26000, 25000, 13500, 14800 },
            { 46000, 34000, 39000, 35000, 25500, 14000, 15200 }
        };

        var months = new[] { "January", "February", "March", "April", "May", "June", "July", "August" };
        var frames = new List<ArcRaceFrame>(months.Length);
        for (var period = 0; period < months.Length; period++)
        {
            var frame = new Dictionary<string, double>(StringComparer.Ordinal);
            for (var product = 0; product < ProductNames.Length; product++)
            {
                frame[ProductNames[product]] = values[period, product];
            }

            frames.Add(new ArcRaceFrame(months[period], frame));
        }

        return frames;
    }
}
