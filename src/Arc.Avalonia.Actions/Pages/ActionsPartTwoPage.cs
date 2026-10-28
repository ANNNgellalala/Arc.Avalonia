using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>In-place confirmations, the light and dark control, and the two menu surfaces.</summary>
public sealed class ActionsPartTwoPage : ArcPage
{
    public override string Title => "Actions, part two";

    public override string Group => "Actions";

    public override string? Description =>
        "In-place confirmation, appearance and the menus, each shown in the state that explains it best.";

    public override int Order => 201;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            ActionDemos.Section(
                "Confirm morph",
                "A destructive button that asks the question in place, then shows a spinner, a result and Undo. Escape or an outside press cancels.",
                ConfirmMorphDemo()),
            ActionDemos.Section(
                "Theme switcher",
                "The light and dark control for a top bar; it swaps the sun and moon and applies the next appearance through the theme manager.",
                ThemeSwitcherDemo(context)),
            ActionDemos.Section(
                "Dropdown menu",
                "A labelled trigger for a short list of commands, with shortcut hints, a separator and one destructive item at the end.",
                DropdownDemo()),
            ActionDemos.Section(
                "Context menu",
                "Secondary actions on a piece of content, opened at the pointer or from the keyboard with Shift F10.",
                ContextDemo())
        }
    };

    private static Control ConfirmMorphDemo()
    {
        var rest = new ArcConfirmMorph
        {
            Label = "Delete project",
            Icon = "trash-2",
            Prompt = "Delete 3 files?"
        };

        var asking = new ArcConfirmMorph
        {
            Label = "Revoke access",
            Icon = "ban",
            Prompt = "Revoke Maya's access?",
            ConfirmLabel = "Revoke",
            State = ArcConfirmMorphState.Confirming
        };

        var done = new ArcConfirmMorph
        {
            Label = "Archive board",
            Icon = "archive",
            DoneLabel = "Archived",
            State = ArcConfirmMorphState.Done,
            Undo = () => System.Threading.Tasks.Task.CompletedTask
        };

        return ActionDemos.Row(rest, asking, done);
    }

    private static Control ThemeSwitcherDemo(ArcDemoContext context)
    {
        var scheme = context.Dark ? ArcColorScheme.Dark : ArcColorScheme.Light;

        var labelled = new ArcThemeSwitcher { Scheme = scheme };
        var iconOnly = new ArcThemeSwitcher { IconOnly = true, Scheme = scheme };

        return ActionDemos.Row(labelled, iconOnly);
    }

    private static Control DropdownDemo()
    {
        var menu = new ArcDropdownMenu
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Actions",
            Icon = "more-horizontal",
            Items = new[]
            {
                new ArcMenuItem { Label = "Rename", Icon = "pencil", Shortcut = "⌘R" },
                new ArcMenuItem { Label = "Duplicate", Icon = "copy", Shortcut = "⌘D" },
                new ArcMenuItem { Label = "Pin to sidebar", Icon = "pin" },
                new ArcMenuItem
                {
                    Label = "Delete project",
                    Icon = "trash-2",
                    IsDestructive = true,
                    SeparatorBefore = true
                }
            },
            IsOpen = true
        };

        var closed = new ArcDropdownMenu
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Sort",
            Icon = "arrow-down-up",
            Items = new[]
            {
                new ArcMenuItem { Label = "Newest first" },
                new ArcMenuItem { Label = "Oldest first" }
            }
        };

        return ActionDemos.Row(ActionDemos.Reserved(215, 215, menu), closed);
    }

    private static Control ContextDemo()
    {
        var target = new Border
        {
            Width = 320,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new ArcIcon { Icon = "file", Size = 20, VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = "Q3 forecast.xlsx", Classes = { "arc-label" } },
                            new TextBlock { Text = "4.2 MB · edited today", Classes = { "arc-caption" } }
                        }
                    }
                }
            }
        };

        var menu = new ArcContextMenu
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Actions for Q3 forecast.xlsx",
            Content = target,
            IsOpen = true,
            Items = new[]
            {
                new ArcMenuItem { Label = "Open", Icon = "external-link" },
                new ArcMenuItem { Label = "Copy link", Icon = "link", Shortcut = "⌘C" },
                new ArcMenuItem { Label = "Show in folder", Icon = "folder", IsChecked = true },
                new ArcMenuItem
                {
                    Label = "Delete file",
                    Icon = "trash-2",
                    IsDestructive = true,
                    SeparatorBefore = true
                }
            }
        };

        return ActionDemos.Reserved(250, menu);
    }
}
