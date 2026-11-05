using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Date range picker's completeness preview: the empty field, the field showing an applied range, and the
/// open panel composed inline from the same pieces the popup hosts (preset rail, two months, footer) with the
/// Last 7 days preset highlighted. Declared state keys match Docs/date-range-picker.json.
/// </summary>
public sealed class DateRangePickerPage : ArcItemPage
{
    public override string ItemSlug => "date-range-picker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-filled", "state-open", "panel-preset"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var today = DateTime.Today;

        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("空字段", "显示占位提示，还没有应用过的区间。", new ArcDateRangePicker
            {
                Width = 280,
                Label = "Report period",
                Placeholder = "选择日期区间",
                MinDate = today
            }),
            Card("已应用区间", "字段显示起止两端的摘要。", new ArcDateRangePicker
            {
                Width = 320,
                Label = "Report period",
                Range = new ArcDateRange(today.AddDays(-6), today),
                Description = "两次选择之后按 Apply 提交。"
            })));

        panel.Children.Add(Card("打开的面板", "面板锚定在字段下方，这里内联展示同一套内容。", OpenPanel(today)));

        return panel;
    }

    private static Control OpenPanel(DateTime today)
    {
        var rail = new StackPanel { Width = 150, Spacing = 2 };
        rail.Children.Add(new TextBlock
        {
            Text = "Presets",
            Classes = { "arc-caption" },
            Margin = new Thickness(10, 0, 0, 6)
        });
        foreach (var preset in ArcDateRangePreset.Defaults)
        {
            rail.Children.Add(PickerDemo.OptionRow(preset.Label, preset.Label == "Last 7 days"));
        }

        var start = today.AddDays(-6);
        var end = today;
        var firstMonth = new DateTime(start.Year, start.Month, 1);
        var left = new ArcCalendar { DisplayMonth = firstMonth, RangeStart = start, RangeEnd = end };
        var right = new ArcCalendar { DisplayMonth = firstMonth.AddMonths(1), RangeStart = start, RangeEnd = end };
        var months = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { left, right }
        };

        var summary = new TextBlock
        {
            Text = $"{ArcDateMath.Range(start, end)} \u00b7 {(end - start).Days + 1} days",
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };
        var cancel = new ArcButton { Content = "Cancel", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        var apply = new ArcButton { Content = "Apply", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm };

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        footer.Children.Add(summary);
        Grid.SetColumn(cancel, 1);
        Grid.SetColumn(apply, 2);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(16, 0, 0, 0) };
        content.Children.Add(months);
        content.Children.Add(new Border { Classes = { "picker-separator" } });
        content.Children.Add(footer);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto") };
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(rail);
        grid.Children.Add(content);

        return PickerDemo.Surface(grid);
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
