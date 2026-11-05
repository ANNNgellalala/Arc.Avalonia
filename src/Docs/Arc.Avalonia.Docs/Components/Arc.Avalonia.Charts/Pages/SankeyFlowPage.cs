using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Charts.Pages;

/// <summary>
/// Sankey flow's completeness preview: the signup journey that splits and recombines, a plain pipeline with
/// one node per column, and the same journey with node labels switched off. All three are static, and the
/// sample data is deterministic.
/// </summary>
public sealed class SankeyFlowPage : ArcItemPage
{
    public override string ItemSlug => "sankey-flow";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-journey", "variant-chain", "state-no-labels"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("分流与合流", "注册来源分成激活与未激活，激活再分成 Free 与 Pro；悬停节点追踪整条路径。",
                AdvancedChartsData.SignupNodes(), AdvancedChartsData.SignupLinks(), labels: true),
            Card("单链管道", "每列只有一个节点时，Sankey 读作一条带宽流；适合展示上传 → 校验 → 入库这类线性流程。",
                PipelineNodes(), PipelineLinks(), labels: true)));

        panel.Children.Add(Card("无节点标签", "ShowLabels=false：节点名收起，只留节点与带宽，靠悬停读数识别。",
            AdvancedChartsData.SignupNodes(), AdvancedChartsData.SignupLinks(), labels: false));

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

    private static Control Card(
        string title,
        string note,
        IReadOnlyList<ArcSankeyNode> nodes,
        IReadOnlyList<ArcSankeyLink> links,
        bool labels)
    {
        var flow = new ArcSankeyFlow
        {
            Nodes = nodes,
            Links = links,
            ShowLabels = labels,
            ValueSuffix = " people",
            Width = 420,
            Height = 236
        };

        return DemoUi.Card(title, note, flow);
    }

    private static IReadOnlyList<ArcSankeyNode> PipelineNodes() => new[]
    {
        new ArcSankeyNode("upload", "Upload"),
        new ArcSankeyNode("parse", "Parse"),
        new ArcSankeyNode("validate", "Validate"),
        new ArcSankeyNode("store", "Store")
    };

    private static IReadOnlyList<ArcSankeyLink> PipelineLinks() => new[]
    {
        new ArcSankeyLink("upload", "parse", 940),
        new ArcSankeyLink("parse", "validate", 880),
        new ArcSankeyLink("validate", "store", 812)
    };
}
