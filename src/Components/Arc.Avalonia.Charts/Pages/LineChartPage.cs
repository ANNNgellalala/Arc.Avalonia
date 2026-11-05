using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Line chart's completeness preview: single and multi series, a dashed comparison series, area on and off,
/// smooth and straight paths, the grid switched off and the empty state. Declared state keys match
/// Docs/line-chart.json so the audit can compare the two. Sample data is deterministic and shared with the
/// other chart pages, so the preview never changes between runs.
/// </summary>
public sealed class LineChartPage : ArcItemPage
{
    /// <summary>A flat target line so the dashed state reads as "target" next to the actual series.</summary>
    private static readonly double[] TargetLine = { 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100, 105 };

    public override string ItemSlug => "line-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-single", "variant-multi", "variant-dashed",
        "state-area", "state-no-area", "state-smooth", "state-straight",
        "state-no-grid", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("单序列", "只有一条线时不出现图例", Single()),
            Card("多序列 + 图例", "点击图例项隐藏或恢复一条线", Multi())));

        panel.Children.Add(Row(
            Card("虚线序列", "目标线用 Dashed 画成虚线", Dashed()),
            Card("面积填充", "ShowArea 开启，填充第一条可见序列", Area())));

        panel.Children.Add(Row(
            Card("无填充", "ShowArea=false，只保留线条", NoArea()),
            Card("平滑曲线", "Smooth=true，曲线不会越过数据点", Smooth())));

        panel.Children.Add(Row(
            Card("直线段", "Smooth=false，按点直连", Straight()),
            Card("无网格", "ShowGrid=false，只留轴线与刻度值", NoGrid())));

        panel.Children.Add(Card("空数据", "Series 为空时画出 No data", Empty()));

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

    private static Control Card(string title, string note, ArcLineChart chart)
    {
        chart.Width = 340;
        chart.Height = 140;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcLineChart Single() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        ValueSuffix = " signups"
    };

    private static ArcLineChart Multi() => new()
    {
        Series = new[]
        {
            new ChartSeries("This period", ChartsData.ThisPeriod),
            new ChartSeries("Previous", ChartsData.PreviousPeriod, Dashed: true)
        },
        Labels = ChartsData.Weeks,
        ValueSuffix = " signups"
    };

    private static ArcLineChart Dashed() => new()
    {
        Series = new[]
        {
            new ChartSeries("Actual", ChartsData.ThisPeriod),
            new ChartSeries("Target", TargetLine, Dashed: true)
        },
        Labels = ChartsData.Weeks,
        ShowArea = false,
        ValueSuffix = " signups"
    };

    private static ArcLineChart Area() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        ShowArea = true
    };

    private static ArcLineChart NoArea() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        ShowArea = false
    };

    private static ArcLineChart Smooth() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        Smooth = true,
        ShowArea = false
    };

    private static ArcLineChart Straight() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        Smooth = false,
        ShowArea = false
    };

    private static ArcLineChart NoGrid() => new()
    {
        Series = new[] { new ChartSeries("Signups", ChartsData.ThisPeriod) },
        Labels = ChartsData.Weeks,
        ShowGrid = false,
        ShowArea = false
    };

    private static ArcLineChart Empty() => new()
    {
        Series = Array.Empty<ChartSeries>(),
        Labels = Array.Empty<string>()
    };
}
