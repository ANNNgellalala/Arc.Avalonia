using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Mention input's completeness preview: empty / filled / disabled states, the open suggestion list, a
/// multiline field and the submit-on-enter composer. Declared state keys match Docs/mention-input.json.
/// </summary>
public sealed class MentionInputPage : ArcItemPage
{
    public override string ItemSlug => "mention-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-filled", "state-suggestions",
        "variant-multiline", "variant-composer", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var suggesting = new ArcMentionInput
        {
            Width = 360,
            Label = "回复",
            MinRows = 2,
            People = People(),
            Channels = Channels(),
            Description = "输入 @ 或 # 触发建议。"
        };
        var suggestionsShown = false;
        Later(suggesting, input =>
        {
            if (suggestionsShown)
            {
                return;
            }

            suggestionsShown = true;
            input.Insert("看起来不错，");
            input.OpenSuggestions(ArcMentionKind.Person);
        });

        var filled = new ArcMentionInput
        {
            Width = 360,
            Label = "已填写",
            MinRows = 2,
            People = People(),
            Channels = Channels(),
            Value = FilledValue(),
            Description = "提及是带 id 的令牌。"
        };

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("状态",
            Field(new ArcMentionInput
            {
                Width = 360,
                Label = "空",
                MinRows = 2,
                Placeholder = "写评论，输入 @ 提及成员",
                People = People(),
                Channels = Channels()
            }),
            Field(filled),
            Field(new ArcMentionInput
            {
                Width = 360,
                Label = "禁用",
                MinRows = 2,
                People = People(),
                Channels = Channels(),
                Value = FilledValue(),
                IsEnabled = false
            })));

        var suggestionStage = new StackPanel { Spacing = 6, Width = 360 };
        suggestionStage.Children.Add(new TextBlock { Text = "录入 @ 后弹出人员建议", Classes = { "arc-caption" } });
        suggestionStage.Children.Add(new Border { Height = 320, Child = suggesting });

        panel.Children.Add(Row("建议与变体",
            suggestionStage,
            Field(new ArcMentionInput
            {
                Width = 360,
                Label = "多行",
                MinRows = 4,
                MaxRows = 8,
                People = People(),
                Channels = Channels(),
                Value = new ArcMentionValue("为财务团队做的安静仪表盘，三月上线。\n第二行展示随内容长高。")
            }),
            Field(new ArcMentionInput
            {
                Width = 360,
                Label = "快捷回复",
                MinRows = 1,
                MaxRows = 4,
                SubmitOnEnter = true,
                Placeholder = "写一条，Enter 发送",
                People = People(),
                Channels = Channels()
            })));

        return panel;
    }

    private static IList<ArcMentionPerson> People() => new List<ArcMentionPerson>
    {
        new("u1", "Maya Chen", "Design lead"),
        new("u2", "Theo Park", "Engineer"),
        new("u3", "Priya Raman", "Product manager"),
        new("u4", "Sam Okafor", "Support")
    };

    private static IList<ArcMentionChannel> Channels() => new List<ArcMentionChannel>
    {
        new("c1", "launch", "Release planning", 14),
        new("c2", "design-system", "Tokens and components", 32),
        new("c3", "support", "Customer reports", 8)
    };

    private static ArcMentionValue FilledValue()
    {
        const string text = "谢谢 @Maya Chen 的评审，已在 #launch 安排发布。";
        var mentions = new List<ArcMention>();

        var person = text.IndexOf("@Maya Chen", StringComparison.Ordinal);
        if (person >= 0)
        {
            mentions.Add(new ArcMention(ArcMentionKind.Person, "u1", "Maya Chen", person, person + "@Maya Chen".Length));
        }

        var channel = text.IndexOf("#launch", StringComparison.Ordinal);
        if (channel >= 0)
        {
            mentions.Add(new ArcMention(ArcMentionKind.Channel, "c1", "launch", channel, channel + "#launch".Length));
        }

        return new ArcMentionValue(text, mentions);
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 16, 16);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    private static Control Field(Control control) => control;

    /// <summary>Runs page-side setup once the control is laid out, so the suggestion list can be shown.</summary>
    private static T Later<T>(T control, Action<T> action)
        where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => action(control), DispatcherPriority.Background);
        return control;
    }
}
