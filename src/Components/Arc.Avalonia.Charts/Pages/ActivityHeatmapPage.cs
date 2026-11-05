using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Activity heatmap's completeness preview: the last sixteen weeks, a full deterministic year and the empty
/// state. Declared state keys match Docs/activity-heatmap.json so the audit can compare the two.
/// </summary>
public sealed class ActivityHeatmapPage : ArcItemPage
{
    public override string ItemSlug => "activity-heatmap";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-sixteen-weeks", "variant-full-year", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("十六周", "最近十六周的活动，配合月份标签", SixteenWeeks()));
        panel.Children.Add(Card("整年", "五十二周，一年四季的密度变化", FullYear()));
        panel.Children.Add(Card("空数据", "Values 为空时画出 No activity", Empty()));

        return panel;
    }

    private static Control Card(string title, string note, ArcChartActivityHeatmap heatmap)
    {
        heatmap.Width = 760;
        heatmap.Height = 170;
        return DemoUi.Card(title, note, heatmap);
    }

    private static ArcChartActivityHeatmap SixteenWeeks() => new()
    {
        Values = ChartsData.Activity(),
        StartDate = new DateTime(2025, 12, 29),
        Unit = "contributions"
    };

    private static ArcChartActivityHeatmap FullYear() => new()
    {
        Values = YearActivity(),
        StartDate = new DateTime(2025, 1, 6),
        Unit = "sessions"
    };

    private static ArcChartActivityHeatmap Empty() => new()
    {
        Values = Array.Empty<int>(),
        StartDate = new DateTime(2025, 12, 29),
        Unit = "contributions"
    };

    /// <summary>A full year of deterministic counts with quieter weekends and a few empty days.</summary>
    private static int[] YearActivity()
    {
        var values = new int[52 * 7];
        for (var index = 0; index < values.Length; index++)
        {
            var weekday = index % 7;
            var value = (index * 13 + index * index * 3 + 7) % 19;
            if (weekday is 5 or 6)
            {
                value = (int)(value * 0.45);
            }

            if (index % 23 == 0)
            {
                value = 0;
            }

            values[index] = value;
        }

        return values;
    }
}
