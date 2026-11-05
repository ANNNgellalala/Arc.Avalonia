using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Gauge's completeness preview: the percentage gauge with thresholds and target, a free 0-500 GB range,
/// a gauge whose only signal is its tone, the percentage gauge without a target and a negative-to-positive
/// temperature range. Declared state keys match Docs/gauge.json so the audit can compare the two.
/// </summary>
public sealed class GaugePage : ArcItemPage
{
    public override string ItemSlug => "gauge";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-percent", "variant-range", "variant-tone", "state-no-target", "state-min-max"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("百分比与阈值", "72%，含三档阈值与 85% 目标", Percent()),
            Card("自由区间", "0–500 GB 的存储占用，目标 425 GB", Range())));

        panel.Children.Add(Row(
            Card("单一语义色", "没有阈值时由 Tone 决定圆环颜色", Tone()),
            Card("无目标", "Target 为 null，不画目标刻度", NoTarget())));

        panel.Children.Add(Row(
            Card("自定义最小最大", "Minimum=-10、Maximum=50 的温度区间", MinMax())));

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

    private static Control Card(string title, string note, ArcGauge gauge)
    {
        gauge.Width = 340;
        gauge.Height = 170;
        return DemoUi.Card(title, note, gauge);
    }

    private static ArcGauge Percent() => new()
    {
        Value = 72,
        Minimum = 0,
        Maximum = 100,
        Label = "Storage used",
        Detail = "360 of 500 GB",
        Target = 85,
        ValueSuffix = "%",
        Thresholds = new[]
        {
            new GaugeThreshold(0, ChartTone.Success, "Healthy"),
            new GaugeThreshold(70, ChartTone.Warning, "Filling up"),
            new GaugeThreshold(90, ChartTone.Danger, "Critical")
        }
    };

    private static ArcGauge Range() => new()
    {
        Value = 360,
        Minimum = 0,
        Maximum = 500,
        Label = "Storage used",
        Detail = "capacity 500 GB",
        Target = 425,
        ValueSuffix = " GB"
    };

    private static ArcGauge Tone() => new()
    {
        Value = 96,
        Minimum = 0,
        Maximum = 100,
        Label = "Uptime",
        Detail = "last 24 hours",
        Tone = ChartTone.Success,
        ValueSuffix = "%"
    };

    private static ArcGauge NoTarget() => new()
    {
        Value = 72,
        Minimum = 0,
        Maximum = 100,
        Label = "Storage used",
        Detail = "360 of 500 GB",
        ValueSuffix = "%",
        Thresholds = new[]
        {
            new GaugeThreshold(0, ChartTone.Success, "Healthy"),
            new GaugeThreshold(70, ChartTone.Warning, "Filling up")
        }
    };

    private static ArcGauge MinMax() => new()
    {
        Value = 31,
        Minimum = -10,
        Maximum = 50,
        Label = "Server room",
        Detail = "-10 to 50 °C",
        Tone = ChartTone.Warning,
        ValueSuffix = "°C"
    };
}
