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
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Sidebar rail: the frame of a product app. Navigation groups expand in place, the sidebar folds to a
/// 64 px icon rail on the bracket shortcut, and the breadcrumb and content follow the selected route.
/// The content is sample data and the narrow-width navigation sheet is noted as an approximation.
/// </summary>
public sealed class SidebarRailPage : ArcItemPage
{
    public override string ItemSlug => "sidebar-rail";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The frame of a product app with navigation, breadcrumbs and a content area.",
        "Apps with nested nav groups that should fold into an icon rail."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use workspace-sidebar when workspace switching matters more than the shell.",
        "Use docs-sidebar for documentation rather than product routes."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Nav rows expose the current route, and group headers carry their expanded state.",
        "[ folds the rail when focus is not in a field; rail icons carry tooltips, and route changes are announced in a status region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The sidebar folds to the rail on a spring and groups expand by height; the content header rises into the new route.",
        "Reduced motion makes folding and group changes instant."
    };

    public override string? ImplementationNotes =>
        "The reference swaps the sidebar for a modal sheet below 640 px of container width. The port renders at a fixed gallery width, so the sheet is not reproduced; the rail fold, nested groups and route changes are all live.";

    private sealed record NavItem(string Id, string Label, string Icon, string Group, string Crumb);

    private static readonly NavItem[] Items =
    {
        new("overview", "Overview", "layout-dashboard", "Design", "Workspace / Overview"),
        new("harbour", "Harbour", "folder", "Design", "Workspace / Projects / Harbour"),
        new("atlas", "Atlas analytics", "chart-line", "Design", "Workspace / Projects / Atlas analytics"),
        new("lattice", "Lattice", "grid-3x3", "Design", "Workspace / Projects / Lattice"),
        new("team", "Team", "users", "Design", "Workspace / Team"),
        new("releases", "Releases", "zap", "Develop", "Workspace / Releases"),
        new("settings", "Settings", "settings", "Develop", "Workspace / Settings")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var active = "overview";
        var collapsed = false;
        var designOpen = true;
        var developOpen = true;

        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("236,*") };
        var sidebarHost = new ContentControl();
        var contentHost = new ContentControl();
        Grid.SetColumn(sidebarHost, 0);
        Grid.SetColumn(contentHost, 1);
        shell.Children.Add(sidebarHost);
        shell.Children.Add(contentHost);

        void Navigate(string id)
        {
            active = id;
            Build();
            var item = Items.First(entry => entry.Id == id);
            AppKit.Say(status, $"{item.Label} opened. Routing is simulated.");
        }

        Control RailButton(string icon, string label, bool selected, Action onClick)
        {
            var button = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(11),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.12) : null,
                Child = new ArcIcon
                {
                    Icon = icon,
                    Size = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextSecondary)
                }
            };
            ToolTip.SetTip(button, label);
            return AppKit.Pressable(button, onClick);
        }

        Control Group(string label, bool open, Action toggle, string[] ids)
        {
            var panel = new StackPanel { Spacing = 1 };
            var head = new Border
            {
                Padding = new Thickness(9, 5, 9, 5),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock { Text = label, Classes = { "arc-caption", "arc-muted" } },
                        AppKit.Place(new ArcIcon
                        {
                            Icon = open ? "chevron-up" : "chevron-down",
                            Size = 12,
                            Foreground = AppKit.Brush(ArcTokens.TextMuted)
                        }, 1)
                    }
                }
            };
            panel.Children.Add(AppKit.Pressable(head, toggle));
            if (open)
            {
                foreach (var id in ids)
                {
                    var item = Items.First(entry => entry.Id == id);
                    var target = item;
                    panel.Children.Add(AppKit.NavRow(item.Icon, item.Label, active == item.Id, () => Navigate(target.Id)));
                }
            }

            return panel;
        }

        void BuildSidebar()
        {
            var content = new StackPanel { Spacing = 3 };

            if (collapsed)
            {
                content.Children.Add(RailButton("layout-dashboard", "Overview", active == "overview", () => Navigate("overview")));
                content.Children.Add(new Border { Height = 6 });
                foreach (var item in new[] { Items[1], Items[2], Items[3] })
                {
                    var target = item;
                    content.Children.Add(RailButton(item.Icon, item.Label, active == item.Id, () => Navigate(target.Id)));
                }

                content.Children.Add(new Border { Height = 6 });
                foreach (var item in new[] { Items[4], Items[5], Items[6] })
                {
                    var target = item;
                    content.Children.Add(RailButton(item.Icon, item.Label, active == item.Id, () => Navigate(target.Id)));
                }
            }
            else
            {
                content.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 9,
                    Margin = new Thickness(2, 0, 2, 8),
                    Children =
                    {
                        AppKit.Mark("compass", 28, 14),
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 0,
                            Children =
                            {
                                new TextBlock { Text = "Northwind", Classes = { "arc-body-sm", "arc-medium" } },
                                AppKit.Caption("Workspace")
                            }
                        }, 1),
                        AppKit.Place(AppKit.Pill("Pro", ArcBadgeTone.Accent), 2)
                    }
                });

                content.Children.Add(Group("Design", designOpen, () =>
                {
                    designOpen = !designOpen;
                    Build();
                }, new[] { "overview", "harbour", "atlas", "lattice", "team" }));

                content.Children.Add(new Border { Height = 4 });
                content.Children.Add(Group("Develop", developOpen, () =>
                {
                    developOpen = !developOpen;
                    Build();
                }, new[] { "releases", "settings" }));

                content.Children.Add(new Border { Height = 8 });
                content.Children.Add(AppKit.Divider());
                content.Children.Add(new Border
                {
                    Margin = new Thickness(2, 8, 2, 0),
                    Child = AppKit.Stack(6,
                        AppKit.Row(6, new TextBlock { Text = "Usage", Classes = { "arc-caption" } }, AppKit.Place(AppKit.Caption("68%"), 0)),
                        AppKit.Bar(0.68),
                        AppKit.Caption("6.8M of 10M events"))
                });
            }

            var footer = new StackPanel { Spacing = 8 };
            footer.Children.Add(AppKit.Divider());
            if (collapsed)
            {
                footer.Children.Add(RailButton("panel-right", "Expand the sidebar", false, () =>
                {
                    collapsed = false;
                    Build();
                    AppKit.Say(status, "Sidebar expanded.");
                }));
            }
            else
            {
                footer.Children.Add(new Border
                {
                    Padding = new Thickness(2, 6, 2, 0),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 8,
                        Children =
                        {
                            AppKit.Person("Maya Chen", ArcAvatarSize.Sm),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    new TextBlock { Text = "Maya Chen", Classes = { "arc-body-sm" } },
                                    AppKit.Caption("maya@northwind.co")
                                }
                            }, 1),
                            AppKit.Place(AppKit.IconBtn("panel-left", "Collapse the sidebar to the rail", () =>
                            {
                                collapsed = true;
                                Build();
                                AppKit.Say(status, "Sidebar collapsed to the icon rail. Press [ to expand.");
                            }, 26), 2)
                        }
                    }
                });
            }

            sidebarHost.Content = new Border
            {
                Background = AppKit.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(collapsed ? 8 : 12, 14, collapsed ? 8 : 12, 12),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,Auto"),
                    Children =
                    {
                        new ScrollViewer
                        {
                            Content = content,
                            ClipToBounds = true,
                            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                        },
                        AppKit.Place(footer, 0, 1)
                    }
                }
            };
            shell.ColumnDefinitions[0].Width = new GridLength(collapsed ? 64 : 236);
        }

        void BuildContent()
        {
            var item = Items.First(entry => entry.Id == active);
            var crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var crumb in item.Crumb.Split('/'))
            {
                if (crumbs.Children.Count > 0)
                {
                    crumbs.Children.Add(new ArcIcon
                    {
                        Icon = "chevron-right",
                        Size = 12,
                        Foreground = AppKit.Brush(ArcTokens.TextMuted),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                crumbs.Children.Add(new TextBlock
                {
                    Text = crumb.Trim(),
                    Classes = { "arc-caption" },
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var topBar = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    AppKit.IconBtn("panel-left", "Toggle the sidebar rail", () =>
                    {
                        collapsed = !collapsed;
                        Build();
                        AppKit.Say(status, collapsed ? "Sidebar collapsed." : "Sidebar expanded.");
                    }, 28),
                    AppKit.Place(new Border { Child = crumbs, VerticalAlignment = VerticalAlignment.Center }, 1),
                    AppKit.Place(AppKit.Row(8,
                        AppKit.IconBtn("search", "Search the workspace", () => AppKit.Say(status, "Workspace search opens in the full product."), 28),
                        new Border
                        {
                            Padding = new Thickness(6),
                            Child = new Grid
                            {
                                Children =
                                {
                                    new ArcIcon { Icon = "bell", Size = 16, Foreground = AppKit.Brush(ArcTokens.TextSecondary) },
                                    new Border
                                    {
                                        Width = 6,
                                        Height = 6,
                                        CornerRadius = new CornerRadius(3),
                                        Background = AppKit.Brush(ArcTokens.Accent),
                                        HorizontalAlignment = HorizontalAlignment.Right,
                                        VerticalAlignment = VerticalAlignment.Top
                                    }
                                }
                            }
                        },
                        AppKit.Person("Maya Chen", ArcAvatarSize.Sm)), 2)
                }
            };

            var metrics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
            var metricData = new (string Label, string Value, string Delta, string Note)[]
            {
                ("Active users", "12,480", "+8.2%", "vs last week"),
                ("Deploys", "38", "+4", "this week"),
                ("Error rate", "0.24%", "-0.06", "past 24 hours")
            };
            for (var index = 0; index < metricData.Length; index++)
            {
                var metric = metricData[index];
                var tile = AppKit.Card(AppKit.Stack(6,
                    AppKit.Caption(metric.Label),
                    new TextBlock { Text = metric.Value, Classes = { "arc-subtitle" } },
                    AppKit.Row(6,
                        AppKit.Pill(metric.Delta, index == 2 ? ArcBadgeTone.Success : ArcBadgeTone.Accent),
                        AppKit.Caption(metric.Note))), 12, 14);
                Grid.SetColumn(tile, index);
                metrics.Children.Add(tile);
            }

            var feeds = new[]
            {
                ("Deploy finished", "harbour-web · production", "Success", "2m ago"),
                ("New member joined", "Ana Duarte accepted the invite", "Info", "18m ago"),
                ("Build failed", "atlas-analytics · main", "Failed", "1h ago"),
                ("Rollback complete", "harbour-web · production", "Success", "3h ago")
            };

            var feed = new StackPanel { Spacing = 0 };
            foreach (var entry in feeds)
            {
                var tone = entry.Item3 == "Failed" ? ArcBadgeTone.Danger : entry.Item3 == "Success" ? ArcBadgeTone.Success : ArcBadgeTone.Neutral;
                feed.Children.Add(new Border
                {
                    Padding = new Thickness(0, 9, 0, 9),
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            AppKit.Disc(entry.Item3 == "Failed" ? "triangle-alert" : "activity", 28, 13),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = entry.Item1, Classes = { "arc-body-sm" } },
                                    AppKit.Caption(entry.Item2)
                                }
                            }, 1),
                            AppKit.Place(AppKit.Pill(entry.Item3, tone), 2),
                            AppKit.Place(AppKit.Caption(entry.Item4), 3)
                        }
                    }
                });
            }

            var body = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            AppKit.Caption(item.Group),
                            new TextBlock { Text = item.Label, Classes = { "arc-subtitle" } }
                        }
                    },
                    metrics,
                    AppKit.Card(AppKit.Stack(8,
                        AppKit.Row(6,
                            new TextBlock { Text = "Recent activity", Classes = { "arc-label" } },
                            AppKit.Caption("· live")),
                        feed,
                        AppKit.Link("View the release log", () => AppKit.Say(status, "Release log navigation is simulated."), trailingIcon: "arrow-right")), 14),
                    status
                }
            };

            var header = new Border
            {
                Height = 46,
                Padding = new Thickness(16, 0, 16, 0),
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = topBar
            };

            contentHost.Content = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    header,
                    AppKit.Place(new Border { Padding = new Thickness(20, 16, 20, 16), Child = body }, 0, 1)
                }
            };
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
            AppKit.Say(status, collapsed ? "Sidebar folded to the rail." : "Sidebar unfolded.");
        });
        AppKit.Shortcut(shell, Key.Escape, () =>
        {
            if (collapsed)
            {
                collapsed = false;
                Build();
                AppKit.Say(status, "Navigation sheet closed.");
            }
        });

        return AppKit.Screen(shell, 570);
    }
}
