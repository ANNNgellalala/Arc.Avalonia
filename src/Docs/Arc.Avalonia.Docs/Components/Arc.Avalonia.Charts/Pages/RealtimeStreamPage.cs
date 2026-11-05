using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Realtime stream's completeness preview: two series while live, and a single series seeded through the
/// Values shortcut with the stream paused. Both readings are static in the shot; the window size and the
/// seed values are fixed, so the shape repeats between runs.
/// </summary>
public sealed class RealtimeStreamPage : ArcItemPage
{
    private static readonly double[] SingleReadings =
    {
        182, 176, 191, 204, 188, 172, 165, 179, 212, 196, 184, 178
    };

    public override string ItemSlug => "realtime-stream";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-two-series", "variant-single-series", "state-live", "state-paused"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("两条序列，运行中", "p95 与 p50 同时滚动，右上角的实时状态点表示窗口在前进。", twoSeries: true),
            Card("单条序列，已暂停", "Values 快捷入口只传一条序列；IsLive=false 冻结视图，可以停下来读某一点。",
                twoSeries: false)));

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

    private static Control Card(string title, string note, bool twoSeries)
    {
        var stream = new ArcRealtimeStream
        {
            WindowSize = 36,
            Interval = TimeSpan.FromMilliseconds(500),
            ValueSuffix = " ms",
            Width = 420,
            Height = 236
        };

        if (twoSeries)
        {
            stream.Series = AdvancedChartsData.LatencySeeds();
            stream.IsLive = true;
        }
        else
        {
            stream.Values = SingleReadings;
            stream.IsLive = false;
        }

        return DemoUi.Card(title, note, stream);
    }
}
