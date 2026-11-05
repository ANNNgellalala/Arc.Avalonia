using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Overlays.Controls;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>
/// Tooltip's completeness preview: the bubble pinned open below its trigger and pinned open above it, each
/// inside a fixed stage with room for the overlay canvas. Declared state keys match Docs/tooltip.json so the
/// audit can compare the two.
/// </summary>
public sealed class TooltipPage : ArcItemPage
{
    public override string ItemSlug => "tooltip";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "side-bottom", "side-top"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Captioned(Bottom(), "下方气泡：默认方向，出现在触发器下方 8px。"));
        panel.Children.Add(Captioned(Top(), "上方气泡：出现在触发器上方 8px，顶部空间不足时会自动翻到下方。"));
        return panel;
    }

    private static Control Bottom()
    {
        var stage = ActionDemos.Stage(440, 120, mockPage: false);

        var tip = new ArcTooltip
        {
            Text = "Copy the install command",
            Side = ArcTooltipSide.Bottom,
            Delay = 0,
            IsOpen = true,
            Margin = new Thickness(24, 14),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new ArcButton
            {
                Content = "Copy",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            }
        };

        stage.Children.Add(tip);
        return stage;
    }

    private static Control Top()
    {
        var stage = ActionDemos.Stage(440, 130, mockPage: false);

        var tip = new ArcTooltip
        {
            Text = "Saves to Drafts, not to the live page",
            Side = ArcTooltipSide.Top,
            Delay = 0,
            IsOpen = true,
            Margin = new Thickness(24, 80),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new ArcButton
            {
                Content = "Save draft",
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm
            }
        };

        stage.Children.Add(tip);
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
