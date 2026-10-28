using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part two: the range picker with its two-month panel, and the time picker with its scrolling
/// list. Both panels are composed from the same controls the popups host.
/// </summary>
public sealed class RangesAndTimePage : ArcPage
{
    public override string Title => "Ranges and time";

    public override string Group => "Pickers";

    public override int Order => 501;

    public override string Description =>
        "A start and an end day chosen together, and a time of day at a fixed minute step. The range panel " +
        "keeps a preset rail beside one or two months; the time list opens centred on the current value.";

    public override Control Build(ArcDemoContext context)
    {
        var today = DateTime.Today;
        var month = new DateTime(today.Year, today.Month, 1);

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Date range picker",
                    "Selection stays a draft until Apply. Escape, Cancel or an outside click discards it, and the preset rail jumps to named periods.",
                    BuildRange(today, month)),

                DemoUi.Section(
                    "Time picker",
                    "Values stay 24-hour internally while the field shows 12- or 24-hour labels. Arrow keys move through times and wrap, Enter picks and closes.",
                    BuildTime())
            }
        };
    }

    private static Control BuildRange(DateTime today, DateTime month)
    {
        var picker = new ArcDateRangePicker
        {
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Report period",
            Range = new ArcDateRange(today.AddDays(-13), today)
        };

        var heading = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { picker, DemoUi.Caption("The field shows the applied range. Below is the open panel at the same size.") }
        };

        return new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { heading, BuildRangePanel(month) }
        };
    }

    private static Control BuildRangePanel(DateTime month)
    {
        var rail = new StackPanel { Width = 156, Spacing = 2 };
        rail.Children.Add(new TextBlock { Text = "Presets", Classes = { "arc-caption" }, Margin = new Thickness(10, 0, 0, 6) });
        foreach (var preset in ArcDateRangePreset.Defaults)
        {
            rail.Children.Add(PickerDemo.OptionRow(preset.Label, preset.Label == "Last 7 days"));
        }

        var start = month.AddDays(2);
        var end = month.AddDays(11);
        var left = new ArcCalendar { DisplayMonth = month, RangeStart = start, RangeEnd = end };
        var right = new ArcCalendar { DisplayMonth = month.AddMonths(1), RangeStart = start, RangeEnd = end };

        var months = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        months.Children.Add(left);
        months.Children.Add(right);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var summary = new TextBlock
        {
            Text = $"{ArcDateMath.Range(start, end)} \u00b7 {(end - start).Days + 1} days",
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };
        var cancel = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = "Cancel",
            Margin = new Thickness(0, 0, 8, 0)
        };
        var apply = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Apply" };
        AutomationProperties.SetName(cancel, "Cancel range");
        AutomationProperties.SetName(apply, "Apply range");
        Grid.SetColumn(cancel, 1);
        Grid.SetColumn(apply, 2);
        footer.Children.Add(summary);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(months);
        content.Children.Add(new Border { Classes = { "picker-separator" } });
        content.Children.Add(footer);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto") };
        content.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(rail);
        grid.Children.Add(content);

        return PickerDemo.Surface(grid);
    }

    private static Control BuildTime()
    {
        var twelve = new ArcTimePicker
        {
            Width = 240,
            Label = "Start time",
            Value = new TimeSpan(14, 30, 0),
            MinuteStep = 15
        };

        var twentyFour = new ArcTimePicker
        {
            Width = 240,
            Label = "Reminder (24 h)",
            Value = new TimeSpan(8, 45, 0),
            Format = ArcTimeFormat.TwentyFourHour,
            MinuteStep = 5
        };

        var fields = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { twelve, twentyFour }
        };

        var list = new StackPanel { Width = 224, Spacing = 2 };
        foreach (var minutes in new[] { 13 * 60, 13 * 60 + 15, 13 * 60 + 30, 14 * 60, 14 * 60 + 15, 14 * 60 + 30, 14 * 60 + 45 })
        {
            var label = new DateTime(2000, 1, 1).AddMinutes(minutes).ToString("h:mm tt", ArcDateMath.Culture);
            list.Children.Add(PickerDemo.OptionRow(label, minutes == 14 * 60 + 30));
        }

        var listPanel = PickerDemo.Surface(list, new Thickness(8));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        row.Children.Add(fields);
        row.Children.Add(listPanel);

        var column = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(row);
        column.Children.Add(DemoUi.Caption("The open list caps at 250 px and scrolls itself; the field keeps the 12-hour label while the value stays 24-hour."));
        return column;
    }
}
