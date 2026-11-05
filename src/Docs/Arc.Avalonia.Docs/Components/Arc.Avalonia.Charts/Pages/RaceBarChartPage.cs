using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Race bar chart's completeness preview: the first period with the play controls and six bars, and the
/// final period with three bars and the controls switched off. Both frames are static (IsPlaying=false) so
/// the ranking reads in a still; the frames are the shared deterministic product race.
/// </summary>
public sealed class RaceBarChartPage : ArcItemPage
{
    public override string ItemSlug => "race-bar-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-first-period", "variant-final-period", "variant-top-3",
        "state-controls", "state-no-controls"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("首期 + 播放控件", "FrameIndex=0：所有产品从一月起跑；底部有播放键与可拖拽的时间轴。",
                frameIndex: 0, topN: 6, controls: true),
            Card("末期 + 前三名", "FrameIndex=7：切换到八月并只保留前三名；ShowControls=false 收起时间轴。",
                frameIndex: 7, topN: 3, controls: false)));

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

    private static Control Card(string title, string note, int frameIndex, int topN, bool controls)
    {
        var race = new ArcRaceBarChart
        {
            Frames = AdvancedChartsData.ProductRace(),
            TopN = topN,
            ShowControls = controls,
            IsPlaying = false,
            ValueFormat = Dollars,
            Width = 420,
            Height = 252
        };

        race.FrameIndex = frameIndex;
        return DemoUi.Card(title, note, race);
    }

    private static string Dollars(double value) =>
        "$" + (value / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "K";
}
