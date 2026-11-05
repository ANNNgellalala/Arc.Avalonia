using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Overlays.Controls;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>
/// Popover's completeness preview: a rich open surface (heading, copy and an action) and a menu-style open
/// surface with three rows. Both are pinned open inside a fixed stage, the way a page would anchor them.
/// Declared state keys match Docs/popover.json so the audit can compare the two.
/// </summary>
public sealed class PopoverPage : ArcItemPage
{
    public override string ItemSlug => "popover";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "variant-rich", "variant-menu"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Captioned(Rich(), "富内容浮层：标题、说明与一个动作按钮，点外部或按 Escape 关闭。"));
        panel.Children.Add(Captioned(Menu(), "菜单式浮层：三行可点的行内操作。"));
        return panel;
    }

    private static Control Rich()
    {
        var stage = ActionDemos.Stage(640, 220, mockPage: false);

        var popover = new ArcPopover
        {
            Label = "Share settings",
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24),
            IsOpen = true,
            Trigger = new ArcButton
            {
                Content = "Share",
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm
            },
            Content = new StackPanel
            {
                Spacing = 10,
                Width = 260,
                Children =
                {
                    new TextBlock { Text = "Share with the team", Classes = { "arc-label" } },
                    new TextBlock
                    {
                        Text = "Anyone in Acme Studio with the link can view this board.",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap
                    },
                    new ArcButton
                    {
                        Content = "Copy link",
                        Variant = ArcButtonVariant.Secondary,
                        Size = ArcButtonSize.Sm,
                        HorizontalAlignment = HorizontalAlignment.Left
                    }
                }
            }
        };

        stage.Children.Add(popover);
        return stage;
    }

    private static Control Menu()
    {
        var stage = ActionDemos.Stage(440, 220, mockPage: false);

        var popover = new ArcPopover
        {
            Label = "Row actions",
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(24),
            IsOpen = true,
            Trigger = new ArcButton
            {
                Content = "More",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            },
            Content = new StackPanel
            {
                Spacing = 2,
                Width = 200,
                Children =
                {
                    MenuRow("Duplicate"),
                    MenuRow("Move to project"),
                    MenuRow("Archive")
                }
            }
        };

        stage.Children.Add(popover);
        return stage;
    }

    private static ArcButton MenuRow(string label) => new()
    {
        Content = label,
        Variant = ArcButtonVariant.Ghost,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        FontWeight = FontWeight.Normal
    };

    private static Control Captioned(Control stage, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(stage);
        panel.Children.Add(DemoUi.Caption(caption));
        return panel;
    }
}
