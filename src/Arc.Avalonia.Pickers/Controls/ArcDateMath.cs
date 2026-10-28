using System;
using System.Globalization;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// Shared date maths and en-US formatting for the pickers. Arc fixes the calendar locale to en-US by
/// default, so the port keeps one deterministic culture instead of inheriting the host locale.
/// </summary>
internal static class ArcDateMath
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static DateTime Day(DateTime value) => value.Date;

    public static DateTime StartOfMonth(DateTime value) => new(value.Year, value.Month, 1);

    public static DateTime StartOfWeek(DateTime value, DayOfWeek firstDayOfWeek)
    {
        var diff = ((int)value.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return value.Date.AddDays(-diff);
    }

    public static bool SameDay(DateTime? left, DateTime? right) =>
        left.HasValue && right.HasValue && left.Value.Date == right.Value.Date;

    public static DateTime AddMonthsClamped(DateTime value, int months)
    {
        var target = StartOfMonth(value).AddMonths(months);
        var day = Math.Min(value.Day, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTime(target.Year, target.Month, day);
    }

    public static string MonthTitle(DateTime value) => value.ToString("MMMM yyyy", Culture);

    public static string MonthShort(DateTime value) => value.ToString("MMM", Culture);

    public static string WeekdayShort(DayOfWeek day) => Culture.DateTimeFormat.ShortestDayNames[(int)day];

    public static string FieldDate(DateTime value, string format)
    {
        try
        {
            return value.ToString(format, Culture);
        }
        catch (FormatException)
        {
            return value.ToString("MMM d, yyyy", Culture);
        }
    }

    public static string ShortDate(DateTime value) => value.ToString("MMM d, yyyy", Culture);

    public static string DayAndMonth(DateTime value) => value.ToString("MMM d", Culture);

    /// <summary>Formats an inclusive range the way Arc's trigger does, collapsing repeated parts.</summary>
    public static string Range(DateTime start, DateTime end)
    {
        if (start.Year == end.Year && start.Month == end.Month)
        {
            return $"{DayAndMonth(start)} \u2013 {end.Day}, {end.Year}";
        }

        if (start.Year == end.Year)
        {
            return $"{DayAndMonth(start)} \u2013 {DayAndMonth(end)}, {end.Year}";
        }

        return $"{ShortDate(start)} \u2013 {ShortDate(end)}";
    }

    /// <summary>The first of the month plus up to six weeks, the grid Arc renders for a month.</summary>
    public static (DateTime FirstVisible, int Weeks) MonthGrid(DateTime month, DayOfWeek firstDayOfWeek)
    {
        var first = StartOfWeek(StartOfMonth(month), firstDayOfWeek);
        var lead = (int)(StartOfMonth(month) - first).TotalDays;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        var weeks = (int)Math.Ceiling((lead + days) / 7.0);
        return (first, Math.Clamp(weeks, 4, 6));
    }
}
