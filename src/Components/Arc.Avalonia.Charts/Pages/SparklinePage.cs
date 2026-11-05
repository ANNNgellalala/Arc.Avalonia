using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Sparkline's completeness preview: the plain trend, a headline value with change, the success and danger
/// tones, the line without its area, without its last point and the empty state. Declared state keys match
/// Docs/sparkline.json so the audit can compare the two.
/// </summary>
public sealed class SparklinePage : ArcItemPage
{
    public override string ItemSlug => "sparkline";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-trend", "variant-headline", "variant-success", "variant-danger",
        "state-no-area", "state-no-last-point", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("纯趋势", "面积 + 末端点，没有标题数字", Trend()),
            Card("带标题数字", "Value=48、Change=+26%，趋势在数字下方", Headline())));

        panel.Children.Add(Row(
            Card("成功色", "Tone=Success，表示正向变化", Success()),
            Card("危险色", "Tone=Danger，表示负向变化", Danger())));

        panel.Children.Add(Row(
            Card("无面积", "ShowArea=false，只剩一条线", NoArea()),
            Card("无端点", "ShowLastPoint=false，不画末端圆点", NoLastPoint())));

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

    private static Control Card(string title, string note, ArcSparkline chart)
    {
        chart.Width = 340;
        chart.Height = 90;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcSparkline Trend() => new()
    {
        Values = ChartsData.Spark
    };

    private static ArcSparkline Headline() => new()
    {
        Values = ChartsData.Spark,
        Value = "48",
        Change = "+26%"
    };

    private static ArcSparkline Success() => new()
    {
        Values = ChartsData.Spark,
        Value = "48",
        Change = "+26%",
        Tone = ChartTone.Success
    };

    private static ArcSparkline Danger() => new()
    {
        Values = ChartsData.Spark,
        Value = "38",
        Change = "-12%",
        Tone = ChartTone.Danger
    };

    private static ArcSparkline NoArea() => new()
    {
        Values = ChartsData.Spark,
        ShowArea = false,
        Value = "48",
        Change = "+26%"
    };

    private static ArcSparkline NoLastPoint() => new()
    {
        Values = ChartsData.Spark,
        ShowLastPoint = false,
        Value = "48",
        Change = "+26%"
    };

    private static ArcSparkline Empty() => new()
    {
        Values = Array.Empty<double>()
    };
}
