using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Brush chart's completeness preview: a recent window with launch and incident events, the same series
/// showing its full range, the recent window without events and the empty state. Declared state keys match
/// Docs/brush-chart.json so the audit can compare the two. The 240 daily readings are generated from a fixed
/// formula, never at random, so the preview is identical between runs.
/// </summary>
public sealed class BrushChartPage : ArcItemPage
{
    private static readonly DateTime FirstDay = new(2025, 10, 1);

    public override string ItemSlug => "brush-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-window", "variant-full-range", "state-no-events", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("选中窗口", "WindowStart=0.55、WindowEnd=1，只看最近一段", Window(includeEvents: true)),
            Card("全段范围", "WindowStart=0、WindowEnd=1，主图显示整段历史", FullRange())));

        panel.Children.Add(Row(
            Card("无事件标记", "不传 Events 时主图与概览条都不画事件", Window(includeEvents: false)),
            Card("空数据", "Series 为空时画出 No data yet", Empty())));

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

    private static Control Card(string title, string note, ArcBrushChart chart)
    {
        chart.Width = 340;
        chart.Height = 190;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcBrushChart Window(bool includeEvents) => new()
    {
        Series = DailyUsers(),
        Start = FirstDay,
        WindowStart = 0.55,
        WindowEnd = 1.0,
        ValueSuffix = " users",
        Events = includeEvents
            ? new[]
            {
                new ChartEvent(146, "v2 launch", ChartTone.Accent, "Offline mode and shared spaces"),
                new ChartEvent(213, "Incident", ChartTone.Danger, "Queue backlog, 41 minutes")
            }
            : null
    };

    private static ArcBrushChart FullRange() => new()
    {
        Series = DailyUsers(),
        Start = FirstDay,
        WindowStart = 0,
        WindowEnd = 1,
        ValueSuffix = " users",
        Events = new[]
        {
            new ChartEvent(146, "v2 launch", ChartTone.Accent),
            new ChartEvent(213, "Incident", ChartTone.Danger)
        }
    };

    private static ArcBrushChart Empty() => new()
    {
        Series = Array.Empty<double>(),
        Labels = Array.Empty<string>()
    };

    /// <summary>Eight months of deterministic daily readings with a slight weekly and monthly rhythm.</summary>
    private static double[] DailyUsers()
    {
        var values = new double[240];
        for (var index = 0; index < values.Length; index++)
        {
            var trend = 2380 + index * 7.4;
            var weekly = 250 * Math.Sin(index * 2 * Math.PI / 7 + 0.6);
            var monthly = 130 * Math.Sin(index * 2 * Math.PI / 30.4 + 1.2);
            var noise = 85 * Math.Sin(index * 1.7) + 48 * Math.Cos(index * 0.53);
            var spike = index % 37 == 0 ? 380 : 0;
            values[index] = Math.Round(trend + weekly + monthly + noise + spike);
        }

        return values;
    }
}
