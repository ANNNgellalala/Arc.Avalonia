using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>
/// Deterministic sample data for the data blocks. Every series is generated from a fixed seed, so a
/// screenshot, a keyboard walk and a hover readout show the same numbers on every run and every machine.
/// </summary>
internal static class DataSamples
{
    private static readonly string[] MonthNames =
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

    private static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public static IReadOnlyList<string> DayNamesOfWeek => DayNames;

    /// <summary>A daily series with a gentle trend, a weekly rhythm and seeded noise.</summary>
    public static double[] Daily(int days, double start, double trendPerDay, double weekly, double noise,
        int seed = 7331, int weekendDip = 0)
    {
        var random = DataKit.Random(seed);
        var values = new double[days];
        var walk = 0.0;
        for (var day = 0; day < days; day++)
        {
            var weekday = day % 7;
            var wave = Math.Sin(day / 7.0 * Math.PI * 2 + seed % 5) * weekly * 0.5;
            walk = walk * 0.96 + (random() - 0.5) * noise * 0.4;
            var weekend = weekendDip > 0 && weekday >= 5 ? -weekendDip : 0;
            var level = start + trendPerDay * day + wave + walk + weekend;
            values[day] = Math.Max(0, Math.Round(level, 1));
        }

        return values;
    }

    /// <summary>A monthly series that compounds by <paramref name="growth"/> with seeded jitter.</summary>
    public static double[] Monthly(int months, double start, double growth, double noise, int seed = 4211)
    {
        var random = DataKit.Random(seed);
        var values = new double[months];
        var value = start;
        for (var month = 0; month < months; month++)
        {
            value *= 1 + growth + (random() - 0.5) * noise;
            values[month] = Math.Round(value);
        }

        return values;
    }

    /// <summary>Twelve three-letter month labels ending at <paramref name="endMonth"/>.</summary>
    public static List<string> Months(int count, int endMonth = 10, int endYear = 2026)
    {
        var labels = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = count - 1 - index;
            var month = endMonth - offset;
            var year = endYear;
            while (month < 0)
            {
                month += 12;
                year--;
            }

            labels.Add(MonthNames[month] + " " + (year % 100).ToString("00", CultureInfo.InvariantCulture));
        }

        return labels;
    }

    /// <summary>The last <paramref name="count"/> day numbers of a 30 day month, as hover labels.</summary>
    public static List<string> Days(int count, int startDay = 1) =>
        Enumerable.Range(startDay, count).Select(day => "Nov " + day).ToList();

    public static List<string> Numbers(int count) =>
        Enumerable.Range(1, count).Select(day => day.ToString(CultureInfo.InvariantCulture)).ToList();
}
