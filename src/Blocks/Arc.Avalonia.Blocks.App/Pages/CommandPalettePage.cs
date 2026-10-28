using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Command palette: app-wide search over grouped commands with shortcut hints and a single moving
/// highlight. The palette is hosted inside the block on a scrim so it renders in a screenshot; the
/// reference renders it inline and expects you to place it in a dialog yourself.
/// </summary>
public sealed class CommandPalettePage : ArcItemPage
{
    public override string ItemSlug => "command-palette";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "App-wide command search opened with Cmd or Ctrl plus K.",
        "Jumping to pages, creating items and running actions from one input."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use select or combobox to pick a value inside a form.",
        "Use dropdown-menu for a short list of actions beside a trigger."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The input is a combobox whose active descendant is the highlighted option, so focus never leaves it; groups are labelled.",
        "Cmd/Ctrl+K opens the palette, Arrow keys move, Home and End jump to the ends, Enter runs the active row, and Escape clears the query then closes."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The panel rises in on a spring and the results frame springs to the filtered height; the highlight slides between rows.",
        "Reduced motion removes the entrance and the highlight travel."
    };

    public override string? ImplementationNotes =>
        "Commands and their handlers are sample data: running one closes the palette and reports the action in the status region instead of routing. The palette is inside the block rather than a window popup, so it stays visible in screenshots and needs no overlay host.";

    private sealed record Command(string Id, string Label, string Description, string Group, string Shortcut, string Icon, string Keywords);

    private static readonly Command[] Commands =
    {
        new("new-project", "New project", "Create a project in Northwind", "Create", "N", "plus", "add workspace"),
        new("new-doc", "New document", "Start a blank document", "Create", "D", "file", "write page"),
        new("invite", "Invite teammate", "Send an email invite", "Create", "I", "users", "member people"),
        new("inbox", "Go to inbox", "Jump to the support inbox", "Navigate", "G I", "inbox", "mail support"),
        new("settings", "Open settings", "Workspace and account settings", "Navigate", "⌘,", "settings", "preferences"),
        new("profile", "Go to your profile", "Your account page", "Navigate", "G P", "user", "account"),
        new("theme", "Toggle theme", "Switch between light and dark", "Actions", "⌘J", "sun", "dark light appearance"),
        new("copy-link", "Copy current link", "Copy the page URL to the clipboard", "Actions", "⌘⇧C", "link", "share url"),
        new("sign-out", "Sign out", "End this session", "Actions", "⇧⌘Q", "log-out", "logout exit")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var query = string.Empty;
        var active = 1;
        var open = true;

        var paletteHost = new ContentControl();

        List<Command> Results() => string.IsNullOrWhiteSpace(query)
            ? Commands.ToList()
            : Commands
                .Where(command => command.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || command.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || command.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || command.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        void Run(Command command)
        {
            open = false;
            Build();
            AppKit.Say(status, $"{command.Label} ran. Handlers are simulated in this preview.");
        }

        void BuildPalette()
        {
            if (!open)
            {
                paletteHost.Content = null;
                return;
            }

            var results = Results();
            active = Math.Clamp(active, 0, Math.Max(0, results.Count - 1));

            var panel = new StackPanel { Spacing = 8 };
            var field = AppKit.Search("Search commands", text =>
            {
                query = text;
                active = 0;
                BuildPalette();
            }, 0, query);
            field.ShortcutHint = "⌘K";
            panel.Children.Add(field);
            panel.Children.Add(AppKit.Divider());

            var list = new StackPanel { Spacing = 1 };
            if (results.Count == 0)
            {
                list.Children.Add(new Border
                {
                    Padding = new Thickness(10, 18, 10, 18),
                    Child = AppKit.Stack(4,
                        AppKit.Caption("No commands match that search"),
                        AppKit.Caption("Try \"invite\", \"theme\" or \"settings\"."))
                });
            }

            foreach (var group in results.GroupBy(command => command.Group))
            {
                list.Children.Add(new Border
                {
                    Padding = new Thickness(9, 8, 9, 3),
                    Child = new TextBlock { Text = group.Key, Classes = { "arc-caption", "arc-muted" } }
                });
                foreach (var command in group)
                {
                    var index = results.IndexOf(command);
                    var selected = index == active;
                    var row = new Border
                    {
                        CornerRadius = new CornerRadius(9),
                        Padding = new Thickness(9, 7, 9, 7),
                        Background = selected ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                            ColumnSpacing = 9,
                            Children =
                            {
                                new ArcIcon
                                {
                                    Icon = command.Icon,
                                    Size = 15,
                                    Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                                },
                                AppKit.Place(new StackPanel
                                {
                                    Spacing = 0,
                                    Children =
                                    {
                                        new TextBlock
                                        {
                                            Text = command.Label,
                                            Classes = { "arc-body-sm" },
                                            Foreground = AppKit.Brush(ArcTokens.Foreground)
                                        },
                                        new TextBlock
                                        {
                                            Text = command.Description,
                                            Classes = { "arc-caption" },
                                            Foreground = AppKit.Brush(ArcTokens.TextMuted)
                                        }
                                    }
                                }, 1),
                                AppKit.Place(AppKit.Keycap(command.Shortcut), 2)
                            }
                        }
                    };
                    list.Children.Add(AppKit.Pressable(row, () => Run(command)));
                }
            }

            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 330,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = list
            });
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Row(12,
                AppKit.Row(5, AppKit.Keycap("↑"), AppKit.Keycap("↓"), AppKit.Caption("move")),
                AppKit.Row(5, AppKit.Keycap("↵"), AppKit.Caption("run")),
                AppKit.Row(5, AppKit.Keycap("esc"), AppKit.Caption("close")),
                AppKit.Place(AppKit.Caption($"{results.Count} commands"), 0)));

            var floating = AppKit.Floating(panel, 12, 18, 560);
            floating.HorizontalAlignment = HorizontalAlignment.Center;
            floating.VerticalAlignment = VerticalAlignment.Top;
            floating.Margin = new Thickness(0, 28, 0, 0);
            paletteHost.Content = floating;
        }

        void Build()
        {
            BuildPalette();
        }

        Build();

        var scrim = new Border { Background = AppKit.Brush(ArcTokens.Scrim) };
        var layer = new Panel { Children = { WorkspaceBehind(), AppKit.Pressable(scrim, () =>
        {
            open = false;
            Build();
            AppKit.Say(status, "Palette closed.");
        }), paletteHost } };

        AppKit.Shortcut(layer, Key.K, () =>
        {
            open = true;
            Build();
            AppKit.Say(status, "Command palette opened.");
        }, modifiers => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));
        AppKit.Shortcut(layer, Key.Down, () =>
        {
            active = Math.Min(Results().Count - 1, active + 1);
            BuildPalette();
        });
        AppKit.Shortcut(layer, Key.Up, () =>
        {
            active = Math.Max(0, active - 1);
            BuildPalette();
        });
        AppKit.Shortcut(layer, Key.Home, () =>
        {
            active = 0;
            BuildPalette();
        });
        AppKit.Shortcut(layer, Key.End, () =>
        {
            active = Math.Max(0, Results().Count - 1);
            BuildPalette();
        });
        AppKit.Shortcut(layer, Key.Enter, () =>
        {
            var results = Results();
            if (results.Count > 0)
            {
                Run(results[Math.Clamp(active, 0, results.Count - 1)]);
            }
        });
        AppKit.Shortcut(layer, Key.Escape, () =>
        {
            if (!string.IsNullOrEmpty(query))
            {
                query = string.Empty;
                active = 0;
                Build();
                AppKit.Say(status, "Query cleared.");
            }
            else if (open)
            {
                open = false;
                Build();
                AppKit.Say(status, "Palette closed.");
            }
        });

        var screen = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                layer,
                AppKit.Place(new Border
                {
                    Padding = new Thickness(18, 0, 18, 10),
                    Background = AppKit.Brush(ArcTokens.Surface),
                    Child = status
                }, 0, 1)
            }
        };

        return AppKit.Screen(screen, 520);
    }

    /// <summary>The workspace page behind the scrim, so the overlay reads as a layer over real content.</summary>
    private static Control WorkspaceBehind()
    {
        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        var data = new (string Title, string Note, string Icon)[]
        {
            ("Harbour", "24 open tasks", "folder"),
            ("Atlas analytics", "18 open tasks", "chart-line"),
            ("Lattice", "9 open tasks", "grid-3x3")
        };
        for (var index = 0; index < data.Length; index++)
        {
            var card = new Border
            {
                Padding = new Thickness(14),
                Background = AppKit.Brush(ArcTokens.SurfaceRaised),
                BorderBrush = AppKit.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Child = AppKit.Stack(8,
                    AppKit.Disc(data[index].Icon, 30, 14),
                    new TextBlock { Text = data[index].Title, Classes = { "arc-body-sm", "arc-medium" } },
                    AppKit.Caption(data[index].Note))
            };
            Grid.SetColumn(card, index);
            cards.Children.Add(card);
        }

        var rows = new StackPanel { Spacing = 0 };
        foreach (var row in new[] { ("Rewrite onboarding checklist", "In review", "Maya Chen"), ("Audit empty states", "Open", "Leo Park"), ("Ship the command palette", "Done", "Sara Ruiz") })
        {
            rows.Children.Add(new Border
            {
                Padding = new Thickness(0, 9, 0, 9),
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        new TextBlock { Text = row.Item1, Classes = { "arc-body-sm" } },
                        AppKit.Place(AppKit.Pill(row.Item2, row.Item2 == "Done" ? ArcBadgeTone.Success : ArcBadgeTone.Neutral), 1),
                        AppKit.Place(AppKit.Person(row.Item3, ArcAvatarSize.Xs), 2)
                    }
                }
            });
        }

        return new Border
        {
            Padding = new Thickness(20, 16, 20, 16),
            Background = AppKit.Brush(ArcTokens.Background),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    AppKit.Caption("Northwind / Projects"),
                                    new TextBlock { Text = "Product roadmap", Classes = { "arc-subtitle" } }
                                }
                            },
                            AppKit.Place(AppKit.Row(8,
                                AppKit.Btn("Share", ArcButtonVariant.Secondary, ArcButtonSize.Sm, null, icon: "share", height: 32),
                                AppKit.Person("Maya Chen", ArcAvatarSize.Sm)), 1)
                        }
                    },
                    cards,
                    AppKit.Card(AppKit.Stack(8,
                        new TextBlock { Text = "Active tasks", Classes = { "arc-label" } },
                        rows), 14)
                }
            }
        };
    }
}
