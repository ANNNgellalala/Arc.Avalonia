using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Ridgeline's completeness preview: five latency distributions with the first ridge highlighted, the same
/// plot reduced to three ridges, five ridges with no highlight and the empty state. Declared state keys match
/// Docs/ridgeline.json so the audit can compare the two.
/// </summary>
public sealed class RidgelinePage : ArcItemPage
{
    public override string ItemSlug => "ridgeline";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-five-ridges", "variant-three-ridges", "state-no-highlight", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("五条山脊", "五个接口的延迟分布，第一位使用完整强调色", FiveRidges(0)),
            Card("三条山脊", "只留三组时每条曲线更舒展", ThreeRidges())));

        panel.Children.Add(Row(
            Card("不强调", "HighlightIndex=-1，所有山脊同级", FiveRidges(-1)),
            Card("空数据", "没有分布时画出 No distributions", Empty())));

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

    private static Control Card(string title, string note, ArcRidgeline chart)
    {
        chart.Width = 340;
        chart.Height = 190;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcRidgeline FiveRidges(int highlight) => new()
    {
        Series = ChartsData.Ridges(),
        Unit = " ms",
        HighlightIndex = highlight
    };

    private static ArcRidgeline ThreeRidges() => new()
    {
        Series = new[]
        {
            new ChartSeries("API", Sample(180, 42, 0.3)),
            new ChartSeries("Web", Sample(230, 58, 1.4)),
            new ChartSeries("Mobile", Sample(286, 48, 2.6))
        },
        Unit = " ms",
        HighlightIndex = 2
    };

    private static ArcRidgeline Empty() => new()
    {
        Series = Array.Empty<ChartSeries>()
    };

    /// <summary>A deterministic, non-random spread around a mean so the density has a stable shape.</summary>
    private static double[] Sample(double mean, double spread, double phase)
    {
        var values = new double[38];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = mean
                            + spread * Math.Sin(index * 1.71 + phase)
                            + spread * 0.45 * Math.Cos(index * 0.67 + phase * 1.7)
                            + spread * 0.18 * Math.Sin(index * 0.23 + phase);
        }

        return values;
    }
}
