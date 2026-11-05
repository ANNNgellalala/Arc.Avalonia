using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Bar chart's completeness preview: the default column chart with grid and average line, the same data
/// with the average line off, with the grid off, with a custom bar brush, and the empty state. Declared
/// state keys match Docs/bar-chart.json so the audit can compare the two.
/// </summary>
public sealed class BarChartPage : ArcItemPage
{
    public override string ItemSlug => "bar-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-columns", "state-no-average", "state-no-grid", "state-custom-brush", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("默认柱列", "网格与平均值虚线同时显示", Columns()),
            Card("关闭平均线", "ShowAverage=false，只保留网格", NoAverage())));

        panel.Children.Add(Row(
            Card("关闭网格", "ShowGrid=false，只留轴线与类别标签", NoGrid()),
            Card("自定义柱色", "BarBrush 覆盖默认的系列色", CustomBrush())));

        panel.Children.Add(Card("空数据", "Values 为空时画出 No data", Empty()));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 0);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, ArcBarChart chart)
    {
        chart.Width = 340;
        chart.Height = 150;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcBarChart Columns() => new()
    {
        Values = ChartsData.WeekdayMinutes,
        Labels = ChartsData.WeekdayLabels,
        ValueSuffix = " min"
    };

    private static ArcBarChart NoAverage() => new()
    {
        Values = ChartsData.WeekdayMinutes,
        Labels = ChartsData.WeekdayLabels,
        ShowAverage = false,
        ValueSuffix = " min"
    };

    private static ArcBarChart NoGrid() => new()
    {
        Values = ChartsData.WeekdayMinutes,
        Labels = ChartsData.WeekdayLabels,
        ShowGrid = false,
        ValueSuffix = " min"
    };

    private static ArcBarChart CustomBrush() => new()
    {
        Values = ChartsData.WeekdayMinutes,
        Labels = ChartsData.WeekdayLabels,
        BarBrush = ArcResources.Brush(ArcTokens.Series2),
        ValueSuffix = " min"
    };

    private static ArcBarChart Empty() => new()
    {
        Values = Array.Empty<double>(),
        Labels = Array.Empty<string>()
    };
}
