using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Time picker's completeness preview: the empty field, a filled 12-hour field, a filled 24-hour field and the
/// open list composed inline from the same option rows the popup hosts, scrolled to the current value.
/// Declared state keys match Docs/time-picker.json so the audit can compare the two.
/// </summary>
public sealed class TimePickerPage : ArcItemPage
{
    public override string ItemSlug => "time-picker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-filled", "state-24h", "state-open"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("空字段", "显示占位提示，没有选中值。", new ArcTimePicker
            {
                Width = 260,
                Label = "Start time",
                Placeholder = "选择时间",
                Value = null
            }),
            Card("12 小时", "字段显示带 AM/PM 的时刻，值仍按 24 小时保存。", new ArcTimePicker
            {
                Width = 260,
                Label = "Start time",
                Value = new TimeSpan(14, 30, 0),
                MinuteStep = 15
            })));

        panel.Children.Add(Row(
            Card("24 小时", "Format=TwentyFourHour，字段显示 HH:mm。", new ArcTimePicker
            {
                Width = 260,
                Label = "Reminder",
                Value = new TimeSpan(8, 45, 0),
                Format = ArcTimeFormat.TwentyFourHour,
                MinuteStep = 5
            }),
            Card("打开列表", "面板锚定在字段下方，这里内联展示同一份列表。", ListPanel())));

        return panel;
    }

    private static Control ListPanel()
    {
        var list = new StackPanel { Width = 224, Spacing = 2 };
        foreach (var (label, selected) in new[]
                 {
                     ("1:00 PM", false), ("1:15 PM", false), ("1:30 PM", false), ("1:45 PM", false),
                     ("2:00 PM", false), ("2:15 PM", false), ("2:30 PM", true)
                 })
        {
            list.Children.Add(PickerDemo.OptionRow(label, selected));
        }

        return PickerDemo.Surface(list, new Thickness(8));
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
