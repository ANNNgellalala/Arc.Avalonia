using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Animated counter's completeness preview: the currency form, a one-decimal reading, a percentage, a
/// compact format and the minimal number without a label. Declared state keys match Docs/animated-counter.json
/// so the audit can compare the two. The preview shows the settled value, so nothing depends on animation
/// timing.
/// </summary>
public sealed class AnimatedCounterPage : ArcItemPage
{
    public override string ItemSlug => "animated-counter";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-currency", "variant-decimal", "variant-percent", "variant-compact", "variant-minimal"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("货币", "Prefix=$，月度经常性收入", Currency()),
            Card("一位小数", "Decimals=1，用于平均耗时这类连续量", Decimal())));

        panel.Children.Add(Row(
            Card("百分比", "Suffix=%，用于可用率等比率", Percent()),
            Card("紧凑格式", "ValueFormat 把 1250000 写成 1.3M", Compact())));

        panel.Children.Add(Row(
            Card("无标签", "只有数字，适合嵌在别处", Minimal())));

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

    private static Control Card(string title, string note, ArcChartAnimatedCounter counter)
    {
        counter.Width = 340;
        counter.Height = 100;
        return DemoUi.Card(title, note, counter);
    }

    private static ArcChartAnimatedCounter Currency() => new()
    {
        Value = 1284,
        Prefix = "$",
        Label = "Monthly recurring revenue"
    };

    private static ArcChartAnimatedCounter Decimal() => new()
    {
        Value = 4.8,
        Decimals = 1,
        Suffix = " s",
        Label = "Average load time"
    };

    private static ArcChartAnimatedCounter Percent() => new()
    {
        Value = 96.4,
        Decimals = 1,
        Suffix = "%",
        Label = "Uptime, last 30 days"
    };

    private static ArcChartAnimatedCounter Compact() => new()
    {
        Value = 1250000,
        Label = "Requests this month",
        ValueFormat = CompactNumber
    };

    private static ArcChartAnimatedCounter Minimal() => new()
    {
        Value = 48
    };

    private static string CompactNumber(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000)
        {
            return (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (abs >= 1_000)
        {
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
