using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>Deterministic sample data used by both chart gallery pages.</summary>
internal static class ChartsData
{
    public static readonly string[] Weeks =
    {
        "Sep 1", "Sep 8", "Sep 15", "Sep 22", "Sep 29",
        "Oct 6", "Oct 13", "Oct 20", "Oct 27", "Nov 3",
        "Nov 10", "Nov 17"
    };

    public static readonly double[] ThisPeriod = { 42, 58, 51, 74, 68, 96, 88, 112, 104, 126, 119, 142 };
    public static readonly double[] PreviousPeriod = { 36, 44, 49, 55, 61, 66, 71, 78, 82, 90, 94, 101 };

    public static readonly double[] WeekdayMinutes = { 32, 48, 27, 54, 41, 62, 35 };
    public static readonly string[] WeekdayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static readonly ChartValue[] Traffic =
    {
        new("Search", 4210),
        new("Direct", 2380),
        new("Social", 1190),
        new("Email", 640)
    };

    public static readonly double[] Spark = { 12, 18, 15, 22, 30, 27, 34, 26, 38, 34, 41, 48 };

    public static readonly ChartValue[] PowerMix =
    {
        new("Wind + solar", 34),
        new("Hydro", 24),
        new("Gas", 18),
        new("Coal", 14),
        new("Other", 10)
    };

    public static readonly SlopeItem[] Channels =
    {
        new("Email", 3.1, 4.6),
        new("Search", 3.4, 3.6),
        new("Direct", 2.6, 3.1),
        new("Social", 1.9, 1.3),
        new("Referral", 1.4, 1.8)
    };

    public static readonly ChartValue[] RevenueMap =
    {
        new("North America", 18400),
        new("Europe", 12400),
        new("Asia Pacific", 8100),
        new("Latin America", 4200),
        new("Middle East", 2650),
        new("Africa", 1450)
    };

    public static readonly ChartValue[] Budget =
    {
        new("Payroll", 412),
        new("Cloud", 96.5),
        new("Marketing", 74.2),
        new("Software", 18.9),
        new("Other", 9.3)
    };

    public static readonly string[] StreamWeeks =
    {
        "W1", "W2", "W3", "W4", "W5", "W6",
        "W7", "W8", "W9", "W10", "W11", "W12"
    };

    public static readonly double[] Bugs = { 22, 30, 26, 38, 34, 48, 42, 52, 40, 46, 36, 32 };
    public static readonly double[] Billing = { 14, 18, 24, 20, 28, 26, 32, 30, 38, 34, 30, 24 };
    public static readonly double[] Onboarding = { 10, 14, 12, 18, 22, 20, 26, 24, 22, 28, 26, 30 };
    public static readonly double[] OtherTickets = { 6, 8, 7, 9, 8, 12, 10, 14, 12, 15, 11, 9 };

    public static IReadOnlyList<ChartSeries> Ridges()
    {
        return new[]
        {
            new ChartSeries("API", Distribution(180, 42, 0.3)),
            new ChartSeries("Web", Distribution(230, 58, 1.4)),
            new ChartSeries("Mobile", Distribution(286, 48, 2.6)),
            new ChartSeries("Batch", Distribution(338, 74, 3.4)),
            new ChartSeries("Edge", Distribution(410, 36, 4.7))
        };
    }

    public static int[] Activity()
    {
        var values = new int[16 * 7];
        for (var i = 0; i < values.Length; i++)
        {
            var weekday = i % 7;
            var value = (i * 17 + i * i * 5 + 11) % 23;
            if (weekday is 5 or 6)
            {
                value = (int)(value * 0.55);
            }
            else if (weekday is 1 or 3)
            {
                value += 8;
            }

            if (i % 19 == 0)
            {
                value = 0;
            }

            values[i] = value;
        }

        return values;
    }

    private static double[] Distribution(double mean, double spread, double phase)
    {
        return Enumerable.Range(0, 38)
            .Select(index =>
                mean
                + spread * Math.Sin(index * 1.71 + phase)
                + spread * 0.45 * Math.Cos(index * 0.67 + phase * 1.7)
                + spread * 0.18 * Math.Sin(index * 0.23 + phase))
            .ToArray();
    }
}
