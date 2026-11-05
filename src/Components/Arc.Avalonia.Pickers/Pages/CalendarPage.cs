using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Calendar's completeness preview: single selection with Today, a range band, min/max with disabled weekends
/// and an ISO week starting on Monday. All four are static, on the current month. Declared state keys match
/// Docs/calendar.json so the audit can compare the two.
/// </summary>
public sealed class CalendarPage : ArcItemPage
{
    public override string ItemSlug => "calendar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-single", "variant-range", "state-today", "state-constrained", "variant-monday"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var selected = Weekday(month, 9);

        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("单选", "选中日戴滑动的胶囊；ShowToday 让 Today 动作常驻头部。", new ArcCalendar
            {
                DisplayMonth = month,
                SelectedDate = selected,
                ShowToday = true
            }),
            Card("区间", "RangeStart 与 RangeEnd 之间的日子铺一层淡色。", new ArcCalendar
            {
                DisplayMonth = month,
                RangeStart = month.AddDays(2),
                RangeEnd = month.AddDays(11)
            })));

        panel.Children.Add(Row(
            Card("受限范围", "MinDate、MaxDate 与 DisabledDates 一起生效，周末不可选。", new ArcCalendar
            {
                DisplayMonth = month,
                SelectedDate = selected,
                MinDate = month.AddDays(1),
                MaxDate = month.AddDays(20),
                DisabledDates = day => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                ShowToday = true
            }),
            Card("周一起始", "WeekStartsOn=Monday，一周从周一开始，适合 ISO 周。", new ArcCalendar
            {
                DisplayMonth = month.AddMonths(1),
                SelectedDate = Weekday(month.AddMonths(1), 9),
                WeekStartsOn = DayOfWeek.Monday
            })));

        return panel;
    }

    private static DateTime Weekday(DateTime month, int day)
    {
        var date = month.AddDays(day);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 16);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, Control content) => DemoUi.Card(title, note, content);
}
