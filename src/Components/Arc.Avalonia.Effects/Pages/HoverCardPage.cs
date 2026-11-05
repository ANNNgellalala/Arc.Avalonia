using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Hover card's completeness preview: the built-in profile card pinned open to the right of its @mention
/// trigger, and a custom link preview pinned open below its trigger. Both live in a fixed stage so the open
/// state is static. Declared state keys match Docs/hover-card.json so the audit can compare the two.
/// </summary>
public sealed class HoverCardPage : ArcItemPage
{
    public override string ItemSlug => "hover-card";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "side-right", "side-bottom", "variant-profile", "variant-custom"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Captioned(Profile(), "人物资料：Side=Right，悬停或聚焦 @maya 打开。"));
        panel.Children.Add(Captioned(Custom(), "自定义内容：Side=Bottom，链接摘要用同一块卡片。"));
        return panel;
    }

    private static Control Profile()
    {
        var stage = EffectDemos.Stage(500, 230, mockPage: false);

        var card = new ArcHoverCard
        {
            Side = ArcHoverCardSide.Right,
            Align = ArcHoverCardAlign.Center,
            OpenDelay = 0,
            IsOpen = true,
            Margin = new Thickness(24, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Trigger = new ArcButton
            {
                Content = "@maya",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            },
            Content = new ArcHoverProfile
            {
                Name = "Maya Chen",
                Role = "Product designer, Payments",
                Bio = "Checkout and pricing.",
                Stats = new[]
                {
                    new ArcHoverStat { Label = "Projects", Value = "12" },
                    new ArcHoverStat { Label = "Reviews", Value = "48" }
                },
                Meta = "Berlin · local time 14:20"
            }
        };

        stage.Children.Add(card);
        return stage;
    }

    private static Control Custom()
    {
        var stage = EffectDemos.Stage(500, 230, mockPage: false);

        var card = new ArcHoverCard
        {
            Side = ArcHoverCardSide.Bottom,
            Align = ArcHoverCardAlign.Start,
            OpenDelay = 0,
            IsOpen = true,
            Margin = new Thickness(24, 20),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Trigger = new ArcButton
            {
                Content = "Q3 launch plan",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            },
            Content = new StackPanel
            {
                Spacing = 4,
                Width = 240,
                Children =
                {
                    new TextBlock { Text = "Q3 launch plan", Classes = { "arc-label" } },
                    new TextBlock
                    {
                        Text = "acme.com/docs/q3-launch",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Planning doc · updated 2 days ago",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        stage.Children.Add(card);
        return stage;
    }

    private static Control Captioned(Control stage, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(stage);
        panel.Children.Add(DemoUi.Caption(caption));
        return panel;
    }
}
