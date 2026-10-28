using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>Dialogs, drawers and tooltips, shown inside a muted stage so the overlay layers are visible.</summary>
public sealed class OverlaysPage : ArcPage
{
    public override string Title => "Overlays";

    public override string Group => "Actions";

    public override string? Description =>
        "Modal surfaces and a hover tip. These layers are hosted inside the page rather than a popup window, " +
        "so a dialog fills the panel it is placed in and dims everything behind it.";

    public override int Order => 203;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            ActionDemos.Section(
                "Dialog",
                "A decision that has to interrupt: title, one line of description and two actions. Escape and a backdrop press close it.",
                DialogDemo()),
            ActionDemos.Section(
                "Drawer",
                "A side sheet for longer content that keeps the page in view, from either edge, with the same modal rules as a dialog.",
                DrawerDemo()),
            ActionDemos.Section(
                "Tooltip",
                "One line of supporting text on hover or keyboard focus, for controls whose label does not say it all.",
                TooltipDemo())
        }
    };

    private static Control DialogDemo()
    {
        var stage = ActionDemos.Stage(880, 300);

        var dialog = new ArcDialog
        {
            Title = "Delete workspace",
            Description = "Every project, file and invite in Acme Studio is removed. This cannot be undone.",
            IsOpen = true,
            Content = new TextBlock
            {
                Text = "12 projects, 48 files and 3 integrations will be deleted.",
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 340,
                HorizontalAlignment = HorizontalAlignment.Left
            }
        };

        var cancel = new ArcButton { Content = "Cancel", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        var confirm = new ArcButton { Content = "Delete workspace", Variant = ArcButtonVariant.Danger, Size = ArcButtonSize.Sm };
        cancel.Click += (_, _) => dialog.IsOpen = false;
        confirm.Click += (_, _) => dialog.IsOpen = false;
        dialog.Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { cancel, confirm }
        };

        stage.Children.Add(dialog);
        return stage;
    }

    private static Control DrawerDemo()
    {
        var right = ActionDemos.Stage(480, 320);
        var rightDrawer = new ArcDrawer
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

        var apply = new ArcButton { Content = "Apply filters", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm };
        var reset = new ArcButton { Content = "Reset", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm };
        apply.Click += (_, _) => rightDrawer.IsOpen = false;
        reset.Click += (_, _) => rightDrawer.IsOpen = false;
        rightDrawer.Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { reset, apply }
        };
        right.Children.Add(rightDrawer);

        var left = ActionDemos.Stage(480, 320);
        var leftDrawer = new ArcDrawer
        {
            Title = "Activity",
            Description = "The same sheet from the left edge.",
            Side = ArcDrawerSide.Left,
            IsOpen = true,
            Content = new TextBlock
            {
                Text = "Maya merged the checkout branch. Sam requested changes on the pricing page.",
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            }
        };
        left.Children.Add(leftDrawer);

        return ActionDemos.Row(right, left);
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

    private static Control TooltipDemo()
    {
        var open = new ArcTooltip
        {
            Side = ArcTooltipSide.Bottom,
            IsOpen = true,
            Delay = 0,
            Text = "Saves to Drafts, not to the live page",
            Content = new ArcButton
            {
                Content = "Save draft",
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm
            }
        };

        var hover = new ArcTooltip
        {
            Side = ArcTooltipSide.Bottom,
            Delay = 0,
            Text = "Copy the install command",
            Content = new ArcButton
            {
                Content = "Copy",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            }
        };

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "The first tip is pinned open for the page; the second appears on hover or keyboard focus.",
                    Classes = { "arc-caption" }
                },
                ActionDemos.Reserved(80, ActionDemos.Row(open, hover))
            }
        };
    }
}
