using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Date picker's completeness preview: the empty field, a field with a selected date and its clear button, a
/// field limited by min/max and disabled days, and the open panel shown inline the way the picker page does it.
/// Declared state keys match Docs/date-picker.json so the audit can compare the two.
/// </summary>
public sealed class DatePickerPage : ArcItemPage
{
    public override string ItemSlug => "date-picker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-filled", "state-open", "state-constrained"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var today = DateTime.Today;

        // A fixed weekday a few days out: deterministic, and never a day the preview disables (weekends).
        var selected = today.AddDays(6);
        while (selected.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            selected = selected.AddDays(1);
        }

        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("空字段", "显示占位提示，未选日期时没有清除按钮", Empty(today)),
            Card("已选日期", "格式化日期就地显示，右侧出现清除按钮", Filled(selected))));

        panel.Children.Add(Row(
            Card("受限范围", "MinDate、MaxDate 与禁用日一起生效", Constrained(today)),
            Card("打开的面板", "字段下方浮出的同一块月历，这里内联展示", OpenPanel(today, selected))));

        return panel;
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

    private static ArcDatePicker Empty(DateTime today) => new()
    {
        Width = 280,
        Label = "截止日期",
        Placeholder = "选择日期",
        MinDate = today
    };

    private static ArcDatePicker Filled(DateTime selected) => new()
    {
        Width = 280,
        Label = "开始日期",
        SelectedDate = selected,
        ShowToday = true,
        Description = "订阅从这一天开始。"
    };

    private static ArcDatePicker Constrained(DateTime today) => new()
    {
        Width = 280,
        Label = "预约日期",
        Placeholder = "只能选工作日",
        MinDate = today,
        MaxDate = today.AddDays(45),
        DisabledDates = day => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
        Description = "周末与 45 天以外的日期不可选。"
    };

    private static Control OpenPanel(DateTime today, DateTime selected)
    {
        var month = new DateTime(selected.Year, selected.Month, 1);

        var calendar = new ArcCalendar
        {
            DisplayMonth = month,
            SelectedDate = selected,
            MinDate = today,
            MaxDate = today.AddDays(45),
            DisabledDates = day => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            ShowToday = true
        };

        return PickerDemo.Surface(calendar);
    }
}
