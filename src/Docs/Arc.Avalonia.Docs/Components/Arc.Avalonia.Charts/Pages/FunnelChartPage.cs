using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Funnel chart's completeness preview: the four-stage checkout funnel with conversion shares, a three-stage
/// signup funnel, and a checkout funnel with the conversion labels switched off. Every declared state is
/// visible without interaction; stage counts are fixed.
/// </summary>
public sealed class FunnelChartPage : ArcItemPage
{
    public override string ItemSlug => "funnel-chart";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-checkout", "variant-signup", "state-conversion", "state-no-conversion"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("结账四段", "四个有序步骤，右侧标出每一级从上一级留下的比例。", CheckoutStages(), showConversion: true),
            Card("注册三段", "更短的漏斗：访问定价页 → 开始试用 → 激活。", SignupStages(), showConversion: true)));

        panel.Children.Add(Card("无转化标注", "ShowConversion=false：只留阶段名与数量，形状仍表达逐级收窄。",
            CheckoutStages(), showConversion: false));

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

    private static Control Card(string title, string note, IReadOnlyList<ArcFunnelStage> stages, bool showConversion)
    {
        var funnel = new ArcFunnelChart
        {
            Stages = stages,
            ShowConversion = showConversion,
            ValueSuffix = " sessions",
            Width = 420,
            Height = 236
        };

        return DemoUi.Card(title, note, funnel);
    }

    private static IReadOnlyList<ArcFunnelStage> CheckoutStages() => new[]
    {
        new ArcFunnelStage("Visited store", 100000),
        new ArcFunnelStage("Added to cart", 16420),
        new ArcFunnelStage("Started checkout", 9840),
        new ArcFunnelStage("Purchased", 3590)
    };

    private static IReadOnlyList<ArcFunnelStage> SignupStages() => new[]
    {
        new ArcFunnelStage("Visited pricing", 24000),
        new ArcFunnelStage("Started trial", 9300),
        new ArcFunnelStage("Activated", 4100)
    };
}
