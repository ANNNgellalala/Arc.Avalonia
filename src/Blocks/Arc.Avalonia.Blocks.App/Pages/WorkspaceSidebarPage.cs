using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Workspace sidebar: a multi-workspace product sidebar with a switcher, live search, favourites that can
/// be reordered from the keyboard, inline project creation, and a fold to the 64 px icon rail. Projects are
/// sample data and nothing is persisted.
/// </summary>
public sealed class WorkspaceSidebarPage : ArcItemPage
{
    public override string ItemSlug => "workspace-sidebar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-workspace-header", "variant-search", "variant-favorites",
        "variant-project-list", "variant-content-pane"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The main navigation of a multi-workspace app.",
        "Sidebars with reorderable favourites and inline project creation."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use sidebar-rail when the block should own the whole app shell.",
        "Use docs-sidebar for a documentation tree instead of product routes."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The workspace switcher is a menu button with Escape to dismiss, and search is a labelled field.",
        "[ toggles the rail unless a field has focus; Alt+Arrow keys reorder the focused favourite; Escape clears the search."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The sidebar width springs between 272 px and the 64 px rail, and counts roll with the value.",
        "Reduced motion swaps the width spring and reorder travel for instant changes."
    };

    public override string? ImplementationNotes =>
        "Workspaces, favourites and projects are sample data. Dragging a favourite is replaced by Alt+Arrow reorder on the focused row, because the port avoids gesture-only affordances; creating a project and switching workspace only update local state.";

    private sealed record Workspace(string Name, string Plan, string Icon);

    private sealed record Item(string Id, string Name, string Icon, string Workspace, int Count);

    private static readonly Workspace[] Workspaces =
    {
        new("Northwind", "Team, 8 seats", "layers"),
        new("Northwind Labs", "Team, 3 seats", "box"),
        new("Maya Chen", "Personal", "user")
    };

    private static readonly Item[] FavoriteSeed =
    {
        new("roadmap", "Product roadmap", "compass", "Northwind", 4),
        new("standup", "Daily standup", "message-circle", "Northwind", 12),
        new("inbox", "Support inbox", "inbox", "Northwind", 7),
        new("launch", "Launch brief", "flag", "Northwind", 2)
    };

    private static readonly Item[] ProjectSeed =
    {
        new("harbour", "Harbour", "folder", "Northwind", 24),
        new("atlas", "Atlas analytics", "chart-line", "Northwind", 18),
        new("lattice", "Lattice", "grid-3x3", "Northwind", 9),
        new("orchard", "Orchard", "folder", "Northwind", 31)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var favorites = FavoriteSeed.ToList();
        var projects = ProjectSeed.ToList();
        var workspace = 0;
        var query = string.Empty;
        var activeId = "roadmap";
        var activeName = "Product roadmap";
        var collapsed = false;
        var switcherOpen = false;
        var addingProject = false;

        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("272,*") };
        var sidebarHost = new ContentControl();
        var menuHost = new ContentControl();
        var contentHost = new ContentControl();
        var sidebarPanel = new Panel { Children = { sidebarHost, menuHost } };

        Grid.SetColumn(sidebarPanel, 0);
        Grid.SetColumn(contentHost, 1);
        shell.Children.Add(sidebarPanel);
        shell.Children.Add(contentHost);

        void Open(string id, string name)
        {
            activeId = id;
            activeName = name;
            switcherOpen = false;
            Build();
            AppKit.Say(status, $"{name} opened. Routing is simulated.");
        }

        void MoveFavorite(int delta)
        {
            var index = favorites.FindIndex(item => item.Id == activeId);
            if (index < 0)
            {
                return;
            }

            var target = index + delta;
            if (target < 0 || target >= favorites.Count)
            {
                return;
            }

            (favorites[index], favorites[target]) = (favorites[target], favorites[index]);
            Build();
            AppKit.Say(status, $"{favorites[target].Name} moved to position {target + 1}.");
        }

        void CommitProject(ArcInput input)
        {
            var name = (input.Value ?? string.Empty).Trim();
            addingProject = false;
            if (name.Length > 0)
            {
                var id = "new-" + (projects.Count + 1);
                projects.Insert(0, new Item(id, name, "folder", Workspaces[workspace].Name, 0));
                activeId = id;
                activeName = name;
                Build();
                AppKit.Say(status, $"{name} created locally.");
            }
            else
            {
                Build();
            }
        }

        Control BuildSwitcher()
        {
            var panel = new StackPanel { Spacing = 2 };
            for (var index = 0; index < Workspaces.Length; index++)
            {
                var target = index;
                var entry = Workspaces[index];
                var selected = index == workspace;
                var row = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.1) : null,
                    Padding = new Thickness(8, 7, 8, 7),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            AppKit.Mark(entry.Icon, 28, 14),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    new TextBlock { Text = entry.Name, Classes = { "arc-body-sm" } },
                                    AppKit.Caption(entry.Plan)
                                }
                            }, 1),
                            AppKit.Place(selected
                                ? (Control)new ArcIcon { Icon = "check", Size = 14, Foreground = AppKit.Brush(ArcTokens.Accent) }
                                : new Border { Width = 0 }, 2)
                        }
                    }
                };
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    workspace = target;
                    switcherOpen = false;
                    activeId = target == 0 ? "roadmap" : target == 1 ? "lattice" : "standup";
                    activeName = favorites.Concat(projects).FirstOrDefault(item => item.Id == activeId)?.Name ?? "Product roadmap";
                    Build();
                    AppKit.Say(status, $"{entry.Name} workspace selected.");
                }));
            }

            panel.Children.Add(AppKit.Divider(6));
            panel.Children.Add(AppKit.NavRow("plus", "New workspace", false, () =>
            {
                switcherOpen = false;
                Build();
                AppKit.Say(status, "Creating a workspace is simulated in this preview.");
            }, compact: true));
            panel.Children.Add(AppKit.NavRow("settings", "Workspace settings", false, () =>
            {
                switcherOpen = false;
                Build();
                AppKit.Say(status, "Workspace settings open in the full product.");
            }, compact: true));

            return AppKit.Floating(panel, 6, 14, 244);
        }

        void BuildSidebar()
        {
            var visibleFavorites = favorites
                .Where(item => Matches(item.Name))
                .ToList();
            var visibleProjects = projects
                .Where(item => Matches(item.Name))
                .ToList();

            bool Matches(string name) => string.IsNullOrWhiteSpace(query)
                || name.Contains(query, StringComparison.OrdinalIgnoreCase);

            var column = new StackPanel { Spacing = 2 };
            var current = Workspaces[workspace];

            if (collapsed)
            {
                column.Children.Add(Row(center: AppKit.Mark(current.Icon, 36, 17), onTap: () =>
                {
                    switcherOpen = !switcherOpen;
                    Build();
                }, margin: 8));
                column.Children.Add(AppKit.Divider(6));
                foreach (var item in favorites)
                {
                    column.Children.Add(RailIcon(item.Icon, item.Name));
                }

                column.Children.Add(AppKit.Divider(6));
                foreach (var item in projects.Take(4))
                {
                    column.Children.Add(RailIcon(item.Icon, item.Name));
                }

                column.Children.Add(new Border { Height = 1, Background = AppKit.Brush(ArcTokens.BorderSubtle), Margin = new Thickness(12, 8) });
                column.Children.Add(RailIcon("panel-right", "Expand the sidebar", () =>
                {
                    collapsed = false;
                    Build();
                    AppKit.Say(status, "Sidebar expanded.");
                }));
            }
            else
            {
                var header = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(6, 5, 6, 5),
                    Background = switcherOpen ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            AppKit.Mark(current.Icon, 28, 14),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    new TextBlock { Text = current.Name, Classes = { "arc-body-sm", "arc-medium" } },
                                    AppKit.Caption(current.Plan)
                                }
                            }, 1),
                            AppKit.Place(new ArcIcon
                            {
                                Icon = switcherOpen ? "chevron-up" : "chevron-down",
                                Size = 14,
                                Foreground = AppKit.Brush(ArcTokens.TextMuted)
                            }, 2)
                        }
                    }
                };
                column.Children.Add(AppKit.Pressable(header, () =>
                {
                    switcherOpen = !switcherOpen;
                    Build();
                }));

                column.Children.Add(new Border { Height = 4 });
                column.Children.Add(AppKit.Search("Search", text =>
                {
                    query = text;
                    BuildSidebar();
                }, 216, query, "["));
                column.Children.Add(new Border { Height = 8 });

                column.Children.Add(SectionLabel("Favorites", favorites.Count));
                if (visibleFavorites.Count == 0)
                {
                    column.Children.Add(EmptyRow("No favorites match"));
                }

                foreach (var item in visibleFavorites)
                {
                    var target = item;
                    column.Children.Add(AppKit.NavRow(item.Icon, item.Name, activeId == item.Id,
                        () => Open(target.Id, target.Name), count: item.Count > 0 ? item.Count.ToString() : null));
                }

                column.Children.Add(new Border { Height = 6 });
                column.Children.Add(SectionLabel("Projects", projects.Count));
                foreach (var item in visibleProjects)
                {
                    var target = item;
                    column.Children.Add(AppKit.NavRow(item.Icon, item.Name, activeId == item.Id,
                        () => Open(target.Id, target.Name), count: item.Count.ToString()));
                }

                if (visibleProjects.Count == 0)
                {
                    column.Children.Add(EmptyRow("No projects match"));
                }

                if (addingProject)
                {
                    var input = new ArcInput { Placeholder = "Project name", Width = 216, HorizontalAlignment = HorizontalAlignment.Left };
                    input.AttachToRow(CommitProject, () =>
                    {
                        addingProject = false;
                        Build();
                        AppKit.Say(status, "New project cancelled.");
                    });
                    column.Children.Add(new Border { Margin = new Thickness(6, 2, 6, 0), Child = input });
                }
                else
                {
                    column.Children.Add(AppKit.NavRow("plus", "New project", false, () =>
                    {
                        addingProject = true;
                        Build();
                    }, compact: true));
                }
            }

            var footer = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
            footer.Children.Add(AppKit.Divider());
            if (collapsed)
            {
                footer.Children.Add(Row(center: AppKit.Person("Maya Chen", ArcAvatarSize.Sm), margin: 6));
            }
            else
            {
                footer.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 9,
                    Children =
                    {
                        AppKit.Person("Maya Chen", ArcAvatarSize.Sm),
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 0,
                            Children =
                            {
                                new TextBlock { Text = "Maya Chen", Classes = { "arc-body-sm" } },
                                AppKit.Caption("Starter plan")
                            }
                        }, 1),
                        AppKit.Place(AppKit.IconBtn("panel-left", "Collapse the sidebar to the rail", () =>
                        {
                            collapsed = true;
                            Build();
                            AppKit.Say(status, "Sidebar collapsed to the icon rail. Press [ to expand.");
                        }, 26), 2)
                    }
                });
            }


            var sidebar = new Border
            {
                Background = AppKit.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(collapsed ? 8 : 12, 12, collapsed ? 8 : 12, 12),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,Auto"),
                    Children =
                    {
                        new ScrollViewer
                        {
                            Content = column,
                            ClipToBounds = true,
                            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                        },
                        AppKit.Place(footer, 0, 1)
                    }
                }
            };

            sidebarHost.Content = sidebar;
            menuHost.Content = switcherOpen && !collapsed ? BuildSwitcherWithOverlay() : null;
            shell.ColumnDefinitions[0].Width = new GridLength(collapsed ? 64 : 272);
        }

        Control BuildSwitcherWithOverlay()
        {
            var menu = BuildSwitcher();
            menu.HorizontalAlignment = HorizontalAlignment.Left;
            menu.VerticalAlignment = VerticalAlignment.Top;
            menu.Margin = new Thickness(8, 60, 0, 0);
            return menu;
        }

        Control RailIcon(string icon, string label, Action? onClick = null)
        {
            var target = label;
            var button = AppKit.IconBtn(icon, label, () =>
            {
                var entry = favorites.Concat(projects).FirstOrDefault(item => item.Name == target);
                if (entry is not null)
                {
                    Open(entry.Id, entry.Name);
                }
                else if (onClick is not null)
                {
                    onClick();
                }
            }, 36);
            button.HorizontalAlignment = HorizontalAlignment.Center;
            return new Border { Margin = new Thickness(0, 2), Child = button };
        }

        void BuildContent()
        {
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            AppKit.Caption($"{Workspaces[workspace].Name} / Projects"),
                            new TextBlock { Text = activeName, Classes = { "arc-subtitle" } }
                        }
                    },
                    AppKit.Place(AppKit.Row(6,
                        AppKit.Btn("Share", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Share dialog is simulated."), icon: "share", height: 32),
                        AppKit.Person("Ana Duarte", ArcAvatarSize.Sm),
                        AppKit.Person("Leo Park", ArcAvatarSize.Sm),
                        AppKit.Person("Sara Ruiz", ArcAvatarSize.Sm)), 1)
                }
            };

            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
            var statData = new (string Label, string Value, string Note)[]
            {
                ("Open tasks", "24", "4 added this week"),
                ("In review", "6", "2 waiting on you"),
                ("Cycle", "Day 8", "of 14")
            };
            for (var index = 0; index < statData.Length; index++)
            {
                var stat = statData[index];
                var tile = AppKit.Card(AppKit.Stat(stat.Label, stat.Value, stat.Note), 12, 14);
                Grid.SetColumn(tile, index);
                stats.Children.Add(tile);
            }

            var tasks = new[]
            {
                ("Rewrite onboarding checklist", "Maya Chen", "In review", "2h"),
                ("Audit empty states", "Leo Park", "Open", "5h"),
                ("Ship the command palette", "Sara Ruiz", "Done", "Yesterday"),
                ("Tighten the settings nav", "Ana Duarte", "Open", "2d")
            };

            var taskList = new StackPanel { Spacing = 0 };
            foreach (var task in tasks)
            {
                var tone = task.Item3 == "Done" ? ArcBadgeTone.Success : task.Item3 == "In review" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral;
                taskList.Children.Add(new Border
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
                            new StackPanel
                            {
                                Spacing = 2,
                                Children =
                                {
                                    new TextBlock { Text = task.Item1, Classes = { "arc-body-sm" } },
                                    AppKit.Caption($"{task.Item2} · updated {task.Item4}")
                                }
                            },
                            AppKit.Place(AppKit.Pill(task.Item3, tone), 1),
                            AppKit.Place(AppKit.Person(task.Item2, ArcAvatarSize.Xs), 2)
                        }
                    }
                });
            }

            var content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    header,
                    stats,
                    AppKit.Card(AppKit.Stack(10,
                        AppKit.Row(6,
                            new TextBlock { Text = "Active tasks", Classes = { "arc-label" } },
                            AppKit.Caption("· 4 of 24")),
                        taskList,
                        AppKit.Link("Open the full task list", () => AppKit.Say(status, "Navigating to tasks is simulated."), trailingIcon: "arrow-right")), 14),
                    status
                }
            };

            contentHost.Content = new Border { Padding = new Thickness(22, 18, 22, 18), Child = content };
        }

        void Build()
        {
            BuildSidebar();
            BuildContent();
        }

        Build();

        AppKit.Shortcut(shell, Key.OemOpenBrackets, () =>
        {
            collapsed = !collapsed;
            Build();
            AppKit.Say(status, collapsed ? "Sidebar collapsed. Press [ to expand." : "Sidebar expanded.");
        });

        AppKit.Shortcut(shell, Key.Up, () => MoveFavorite(-1), modifiers => modifiers.HasFlag(KeyModifiers.Alt));
        AppKit.Shortcut(shell, Key.Down, () => MoveFavorite(1), modifiers => modifiers.HasFlag(KeyModifiers.Alt));
        AppKit.Shortcut(shell, Key.Escape, () =>
        {
            if (switcherOpen)
            {
                switcherOpen = false;
                Build();
            }
            else if (!string.IsNullOrEmpty(query))
            {
                query = string.Empty;
                Build();
                AppKit.Say(status, "Search cleared.");
            }
        });

        return AppKit.Screen(shell, 580);
    }

    private static Control SectionLabel(string text, int count) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Margin = new Thickness(9, 4, 9, 2),
        Children =
        {
            new TextBlock { Text = text, Classes = { "arc-caption", "arc-muted" } },
            AppKit.Place(AppKit.Caption(count.ToString()), 1)
        }
    };

    private static Control EmptyRow(string text) => new Border
    {
        Padding = new Thickness(9, 6, 9, 6),
        Child = AppKit.Caption(text)
    };

    private static Control Row(Control center, Action? onTap = null, double margin = 0)
    {
        var border = new Border
        {
            Margin = new Thickness(margin),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = center
        };
        return AppKit.Pressable(border, onTap);
    }
}

/// <summary>Small input helpers shared by the shell blocks that create a row inline.</summary>
internal static class AppKitInput
{
    /// <summary>Commits on Enter and cancels on Escape, the two keys the reference documents.</summary>
    public static void AttachToRow(this ArcInput input, Action<ArcInput> commit, Action cancel)
    {
        input.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                commit(input);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                cancel();
                args.Handled = true;
            }
        });
    }
}
