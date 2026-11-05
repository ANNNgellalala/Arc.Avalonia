using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Reactions' completeness preview: a bar without your reaction, one with your reaction highlighted, an
/// empty bar that only keeps the add button, a row with several reactions and a bar with a three digit
/// count. Declared state keys match Docs/reactions.json so the audit can compare the two lists.
/// </summary>
public sealed class ReactionsPage : ArcItemPage
{
    public override string ItemSlug => "reactions";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-unreacted", "state-reacted", "state-empty",
        "variant-multiple", "variant-large-count"
    };

    private static readonly IList<ArcReactionOption> Options = new List<ArcReactionOption>
    {
        new("like", "Like", "thumbs-up"),
        new("love", "Love", "heart"),
        new("wow", "Wow", "sparkles"),
        new("party", "Celebrate", "star")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Captioned(
            "未回应",
            new ArcReactions
            {
                Label = "Reactions on the design review",
                Options = Options,
                DefaultValue = new List<ArcReaction>
                {
                    new("like", 12),
                    new("love", 4)
                }
            },
            "胶囊只有计数；多个不同回应并列在同一行。"));

        panel.Children.Add(Captioned(
            "已回应",
            new ArcReactions
            {
                Label = "Reactions on the design review",
                Options = Options,
                DefaultValue = new List<ArcReaction>
                {
                    new("like", 12),
                    new("love", 4, reacted: true)
                }
            },
            "你已经回应的胶囊取强调色描边与淡底，读屏名称追加 including yours。"));

        panel.Children.Add(Captioned(
            "空态",
            new ArcReactions
            {
                Label = "Reactions on the release note",
                Options = Options,
                DefaultValue = new List<ArcReaction>()
            },
            "还没有任何回应时只留下加号入口，托盘仍可打开。"));

        panel.Children.Add(Captioned(
            "大计数",
            new ArcReactions
            {
                Label = "Reactions on the release note",
                Options = Options,
                DefaultValue = new List<ArcReaction>
                {
                    new("love", 128, reacted: true),
                    new("party", 9)
                }
            },
            "三位数计数使用等宽数字，滚动到新值时宽度不变。"));

        return panel;
    }

    private static Control Captioned(string title, ArcReactions bar, string caption)
    {
        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-label" } });
        panel.Children.Add(bar);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
