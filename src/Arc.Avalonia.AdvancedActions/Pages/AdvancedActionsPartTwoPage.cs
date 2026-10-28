using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.AdvancedActions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedActions.Pages;

/// <summary>The expanding action pill and the account menu with its reserved, page-hosted panel.</summary>
public sealed class AdvancedActionsPartTwoPage : ArcPage
{
    public override string Title => "Advanced actions, part two";

    public override string Group => "Advanced actions";

    public override string? Description =>
        "A compact action pill that reveals one label at a time and confirms in place, and the account menu behind an avatar, hosted in the page so its floating panel is part of the layout demo.";

    public override int Order => 701;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Expanding button group",
                "Three to six related actions on one object, such as a message or a file. Icons stay compact; the one you point at or focus grows to show its label while the neighbours slide aside, and Archive or Delete confirms in place.",
                ExpandingDemo()),
            DemoUi.Section(
                "User menu",
                "The single account entry point in a top bar. The avatar opens a panel with identity, account destinations, an inline theme switch and a destructive sign out; the panel is hosted in a reserved box so it renders in the page flow.",
                UserMenuDemo(context))
        }
    };

    private static Control ExpandingDemo()
    {
        var group = new ArcExpandingButtonGroup
        {
            Label = "Message actions",
            DefaultExpanded = "reply",
            ConfirmDuration = 1400,
            Items = new[]
            {
                new ArcExpandingButtonItem
                {
                    Id = "reply",
                    Label = "Reply",
                    Icon = "message-circle",
                    OnSelect = () => Task.FromResult(true)
                },
                new ArcExpandingButtonItem
                {
                    Id = "forward",
                    Label = "Forward",
                    Icon = "send",
                    OnSelect = () => Task.FromResult(true)
                },
                new ArcExpandingButtonItem
                {
                    Id = "archive",
                    Label = "Archive",
                    Icon = "archive",
                    DoneLabel = "Archived",
                    OnSelect = async () =>
                    {
                        await Task.Delay(450);
                        return true;
                    }
                },
                new ArcExpandingButtonItem
                {
                    Id = "snooze",
                    Label = "Snooze",
                    Icon = "clock",
                    DoneLabel = "Snoozed",
                    OnSelect = () => Task.FromResult(true)
                },
                new ArcExpandingButtonItem
                {
                    Id = "delete",
                    Label = "Delete",
                    Icon = "trash-2",
                    DoneLabel = "Deleted",
                    Tone = ArcExpandingButtonTone.Danger,
                    OnSelect = () => Task.FromResult(true)
                }
            }
        };
        group.ActionInvoked += (_, _) => { };

        var small = new ArcExpandingButtonGroup
        {
            Label = "File actions",
            Size = ArcExpandingButtonSize.Sm,
            Items = new[]
            {
                new ArcExpandingButtonItem { Id = "open", Label = "Open", Icon = "file" },
                new ArcExpandingButtonItem
                {
                    Id = "copy-link",
                    Label = "Copy link",
                    Icon = "link",
                    DoneLabel = "Copied",
                    OnSelect = () => Task.FromResult(true)
                },
                new ArcExpandingButtonItem
                {
                    Id = "delete",
                    Label = "Delete",
                    Icon = "trash-2",
                    DoneLabel = "Deleted",
                    Tone = ArcExpandingButtonTone.Danger,
                    OnSelect = () => Task.FromResult(true)
                }
            }
        };

        return new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Row(group, small),
                Note("Arrow keys move between actions and the focused action reveals its label; Enter or Space runs it. Archive and Snooze show a check and a past tense word for ConfirmDuration, then settle back.")
            }
        };
    }

    private static Control UserMenuDemo(ArcDemoContext context)
    {
        var open = BuildMenu(context, open: true, showName: false, ArcUserMenuStatus.Available);
        var named = BuildMenu(context, open: false, showName: true, ArcUserMenuStatus.Busy);

        var stage = new Panel
        {
            Width = 380,
            Height = 370,
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = false
        };
        stage.Children.Add(new Border { Classes = { "user-menu-stage" } });

        var topbar = new Border
        {
            Height = 56,
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Top,
            Classes = { "user-menu-topbar" },
            Padding = new Thickness(16, 0, 10, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Acme Studio",
                        Classes = { "arc-label" },
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    WithColumn(open, 1)
                }
            }
        };
        stage.Children.Add(topbar);

        var namedStage = new Panel
        {
            Width = 300,
            Height = 150,
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = false
        };
        namedStage.Children.Add(new Border
        {
            Width = 300,
            Height = 64,
            VerticalAlignment = VerticalAlignment.Top,
            Classes = { "user-menu-settings-bar" },
            Padding = new Thickness(16, 0, 10, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Signed in as Maya",
                        Classes = { "arc-body-sm", "arc-secondary" },
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    WithColumn(named, 1)
                }
            }
        });

        var right = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Caption("The closed variant shows the name beside the avatar, and the busy presence mark."),
                namedStage
            }
        };

        return new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Row(stage, right),
                Note("The open panel is hosted in a reserved box, not a popup, so it sits in the page flow. Up and Down move between rows, Left and Right choose a theme, Escape and Tab close and return focus to the avatar, and an outside press closes it.")
            }
        };
    }

    private static Control WithColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static TextBlock Note(string text)
    {
        var block = DemoUi.Body(text);
        block.HorizontalAlignment = HorizontalAlignment.Left;
        return block;
    }

    private static ArcUserMenu BuildMenu(ArcDemoContext context, bool open, bool showName, ArcUserMenuStatus status)
    {
        var menu = new ArcUserMenu
        {
            UserName = "Maya Chen",
            Email = "maya@acme.com",
            Plan = "Pro",
            Status = status,
            ThemePreference = context.Dark ? ArcUserMenuTheme.Dark : ArcUserMenuTheme.Light,
            IsOpen = open,
            ShowName = showName,
            SignOutShortcut = "⇧⌘Q",
            Items = new[]
            {
                new ArcUserMenuItem { Label = "Profile", Icon = "user" },
                new ArcUserMenuItem { Label = "Settings", Icon = "settings", Shortcut = "⌘," },
                new ArcUserMenuItem { Label = "Billing", Icon = "credit-card" }
            },
            OnSignOut = async () => await Task.Delay(600)
        };

        menu.ThemeChanged += (_, theme) =>
        {
            if (Application.Current is not { } app || theme == ArcUserMenuTheme.System)
            {
                return;
            }

            ArcThemeManager.SetScheme(app, theme == ArcUserMenuTheme.Dark ? ArcColorScheme.Dark : ArcColorScheme.Light);
        };

        return menu;
    }
}
