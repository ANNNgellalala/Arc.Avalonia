using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Slope chart's completeness preview: the channel comparison with one highlighted item, the same chart
/// without a highlight, the highlighted chart with ranks hidden and the empty state. Declared state keys
/// match Docs/slope-chart.json so the audit can compare the two.
/// </summary>
public sealed class SlopeChartPage : ArcItemPage
{
    public override string ItemSlug => "slope-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-highlight", "variant-no-highlight", "state-no-ranks", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("强调一条", "HighlightKey=Email，其余线条弱化", Highlight()),
            Card("不设强调", "HighlightKey 为 null，所有线条同级", NoHighlight())));

        panel.Children.Add(Row(
            Card("隐藏排名", "ShowRanks=false，只保留名称与数值", NoRanks()),
            Card("空数据", "Items 为空时画出 No data", Empty())));

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

    private static Control Card(string title, string note, ArcSlopeChart chart)
    {
        chart.Width = 340;
        chart.Height = 200;
        return DemoUi.Card(title, note, chart);
    }

    private static ArcSlopeChart Highlight() => new()
    {
        Items = ChartsData.Channels,
        StartLabel = "Q3",
        EndLabel = "Q4",
        HighlightKey = "Email",
        ValueFormat = Percent
    };

    private static ArcSlopeChart NoHighlight() => new()
    {
        Items = ChartsData.Channels,
        StartLabel = "Q3",
        EndLabel = "Q4",
        ValueFormat = Percent
    };

    private static ArcSlopeChart NoRanks() => new()
    {
        Items = ChartsData.Channels,
        StartLabel = "Q3",
        EndLabel = "Q4",
        HighlightKey = "Email",
        ShowRanks = false,
        ValueFormat = Percent
    };

    private static ArcSlopeChart Empty() => new()
    {
        Items = Array.Empty<SlopeItem>(),
        StartLabel = "Q3",
        EndLabel = "Q4"
    };

    private static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
