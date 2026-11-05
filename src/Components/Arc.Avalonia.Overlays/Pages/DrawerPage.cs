using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Overlays.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>
/// Drawer's completeness preview: an open right-side sheet with a fixed footer and an open left-side sheet
/// without one. Both are hosted in a fixed stage because a drawer fills the nearest panel and dims what is
/// behind it. Declared state keys match Docs/drawer.json so the audit can compare the two.
/// </summary>
public sealed class DrawerPage : ArcItemPage
{
    public override string ItemSlug => "drawer";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "side-right", "side-left", "layout-footer", "layout-no-footer"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Captioned(Right(), "右侧抽屉：正文滚动，Apply / Reset 固定在底栏。"));
        panel.Children.Add(Captioned(Left(), "左侧抽屉：活动流没有底栏，用 Escape 或点遮罩收起。"));
        return panel;
    }

    private static Control Right()
    {
        var stage = ActionDemos.Stage(860, 300);

        var drawer = new ArcDrawer
        {
            Title = "Filters",
            Description = "Narrow the list without leaving the page.",
            Side = ArcDrawerSide.Right,
            IsOpen = true,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    FilterRow("Status", "Active"),
                    FilterRow("Owner", "Maya Chen"),
                    FilterRow("Updated", "Last 7 days")
                }
            }
        };

        var reset = new ArcButton { Content = "Reset", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm };
        var apply = new ArcButton { Content = "Apply filters", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm };
        reset.Click += (_, _) => drawer.IsOpen = false;
        apply.Click += (_, _) => drawer.IsOpen = false;
        drawer.Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { reset, apply }
        };

        stage.Children.Add(drawer);
        return stage;
    }

    private static Control Left()
    {
        var stage = ActionDemos.Stage(440, 300);

        var drawer = new ArcDrawer
        {
            Title = "Activity",
            Description = "The same sheet from the left edge.",
            Side = ArcDrawerSide.Left,
            IsOpen = true,
            Content = DemoUi.Body("Maya merged the checkout branch. Sam requested changes on the pricing page.")
        };

        stage.Children.Add(drawer);
        return stage;
    }

    private static Control FilterRow(string label, string value)
    {
        var valueBlock = new TextBlock
        {
            Text = value,
            Classes = { "arc-body-sm", "arc-secondary" },
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(valueBlock, 1);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, Classes = { "arc-body-sm" } });
        grid.Children.Add(valueBlock);

        return new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            Padding = new Thickness(14, 12),
            Child = grid
        };
    }

    private static Control Captioned(Control stage, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(stage);
        panel.Children.Add(DemoUi.Caption(caption));
        return panel;
    }
}
