using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part one: the calendar grid and the date field. The calendar is always visible, so it is shown
/// inline; the date picker is shown closed, with the open panel described beside it.
/// </summary>
public sealed class PickersPage : ArcPage
{
    public override string Title => "Pickers";

    public override string Group => "Pickers";

    public override int Order => 500;

    public override string Description =>
        "Calendars, dates, times, color, files and ink. Every picker is a field that opens a floating panel; " +
        "the panels are repeated inline so both the closed and open states are visible.";

    public override Control Build(ArcDemoContext context)
    {
        var today = DateTime.Today;
        var month = new DateTime(today.Year, today.Month, 1);

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Calendar",
                    "Pick a date inline when the month should stay visible. Arrows move by day or week, PageUp and PageDown by month, Home and End to the week edges, Enter selects.",
                    BuildCalendars(today, month)),

                DemoUi.Section(
                    "Date picker",
                    "A date field in a form. Enter opens the calendar, Escape closes it and returns focus to the field, and Clear appears once a date is set.",
                    BuildDatePickers(today))
            }
        };
    }

    private static Control BuildCalendars(DateTime today, DateTime month)
    {
        var rangeStart = month.AddDays(2);
        var rangeEnd = month.AddDays(11);

        var range = new ArcCalendar
        {
            DisplayMonth = month,
            SelectedDate = rangeEnd,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            ShowToday = true
        };

        var constrained = new ArcCalendar
        {
            DisplayMonth = month.AddMonths(1),
            SelectedDate = today.AddDays(9),
            MinDate = today.AddDays(2),
            MaxDate = today.AddDays(45),
            DisabledDates = day => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(DemoUi.Row(range, constrained));
        panel.Children.Add(DemoUi.Caption(
            "Range ends carry the accent pill and the days between take the subtle tint. The second month blocks weekends and days past its min and max."));
        return DemoUi.Canvas(panel);
    }

    private static Control BuildDatePickers(DateTime today)
    {
        var filled = new ArcDatePicker
        {
            Width = 280,
            Label = "Start date",
            SelectedDate = today.AddDays(6),
            ShowToday = true,
            Description = "The day the subscription begins."
        };

        var empty = new ArcDatePicker
        {
            Width = 280,
            Label = "Due date",
            Placeholder = "Select a date",
            MinDate = today
        };

        var fields = DemoUi.Column(filled, empty);
        var note = DemoUi.Caption("The field opens the same calendar in a floating panel, dismissing on an outside click.");
        return new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { fields, note }
        };
    }
}
