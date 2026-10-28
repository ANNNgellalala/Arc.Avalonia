using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Notification center: a bell trigger with an unread badge that opens a page-hosted panel of updates with
/// All and Unread views, rows that expand, per-row read and dismiss actions, and bulk actions. The panel is
/// composed over the app header rather than mounted in a popup so it renders in screenshots.
/// </summary>
public sealed class NotificationCenterPage : ArcItemPage
{
    public override string ItemSlug => "notification-center";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A bell in the app header with an unread badge and a recent-updates panel.",
        "Notifications people can mark read, expand and dismiss in bulk."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use toast for transient messages that should disappear on their own.",
        "Use inbox-triage for a full-page inbox of messages."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The trigger's label includes the unread count; the panel heading names it, and read state is carried by text as well as the dot.",
        "Enter or Space opens the panel and toggles a row, Escape closes it and returns focus to the trigger, and bulk actions are ordinary buttons."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The bell tilts while open, the unread count rolls in the direction of the change, and rows open their own height on a spring.",
        "Reduced motion replaces the tilt and height travel with instant fades."
    };

    public override string? ImplementationNotes =>
        "Updates are sample data and read state is local. The reference mounts the panel in a portal from a Radix popover; the port hosts it inside the block because the offscreen gallery has no window popup layer.";

    private sealed record Update(string Id, string Title, string Description, string Time, string Tone, bool Read, string Actor);

    private static readonly Update[] Seed =
    {
        new("n1", "Deploy finished", "harbour-web shipped 24 changes to production. Smoke tests passed.", "2m", "success", false, string.Empty),
        new("n2", "Ana commented", "Legal flagged two clauses in the Harbour agreement.", "18m", "info", false, "Ana Duarte"),
        new("n3", "Budget at 82%", "The Atlas analytics budget is close to its monthly cap.", "1h", "warning", false, string.Empty),
        new("n4", "Weekly digest is ready", "12 projects moved and 2 reviews are waiting.", "4h", "info", true, string.Empty)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var updates = Seed.ToList();
        var open = true;
        var view = "all";
        var expanded = "n2";

        var triggerHost = new ContentControl();
        var panelHost = new ContentControl();

        int Unread() => updates.Count(update => !update.Read);

        void Toggle(string id)
        {
            expanded = expanded == id ? string.Empty : id;
            var update = updates.First(entry => entry.Id == id);
            if (update is { Read: false })
            {
                updates[updates.IndexOf(update)] = update with { Read = true };
            }

            Build();
        }

        void BuildTrigger()
        {
            var badge = new Panel
            {
                Width = 34,
                Height = 34,
                Children =
                {
                    new Border
                    {
                        Width = 34,
                        Height = 34,
                        CornerRadius = new CornerRadius(10),
                        Background = open ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                        Child = new ArcIcon
                        {
                            Icon = "bell",
                            Size = 17,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = AppKit.Brush(ArcTokens.TextSecondary)
                        }
                    },
                    new Border
                    {
                        MinWidth = 15,
                        Height = 15,
                        CornerRadius = new CornerRadius(8),
                        Background = AppKit.Brush(ArcTokens.Accent),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Padding = new Thickness(4, 0, 4, 0),
                        Child = new TextBlock
                        {
                            Text = Unread().ToString(),
                            FontSize = 10,
                            Foreground = AppKit.Brush(ArcTokens.AccentForeground),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            ToolTip.SetTip(badge, Unread() > 0 ? $"Notifications, {Unread()} unread" : "Notifications");
            triggerHost.Content = AppKit.Pressable(badge, () =>
            {
                open = !open;
                Build();
                AppKit.Say(status, open ? "Notifications open." : "Notifications closed.");
            });
        }

        void BuildPanel()
        {
            if (!open)
            {
                panelHost.Content = null;
                return;
            }

            var visible = updates.Where(update => view == "all" || !update.Read).ToList();
            var panel = new StackPanel { Spacing = 8 };

            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 1,
                        Children =
                        {
                            new TextBlock { Text = "Notifications", Classes = { "arc-body", "arc-medium" } },
                            AppKit.Caption($"{Unread()} unread of {updates.Count}")
                        }
                    },
                    AppKit.Place(AppKit.IconBtn("x", "Close notifications", () =>
                    {
                        open = false;
                        Build();
                        AppKit.Say(status, "Notifications closed.");
                    }, 26), 1)
                }
            });

            panel.Children.Add(AppKit.Segmented(new[] { ("all", "All"), ("unread", "Unread") }, view, next =>
            {
                view = next;
                Build();
                AppKit.Say(status, next == "all" ? "Showing every update." : "Showing unread updates only.");
            }, 168));

            var rows = new StackPanel { Spacing = 1 };
            if (visible.Count == 0)
            {
                rows.Children.Add(new Border
                {
                    Padding = new Thickness(10, 26, 10, 22),
                    Child = AppKit.Empty("check-check", "You are caught up", "No unread notifications.")
                });
            }

            foreach (var update in visible)
            {
                var target = update;
                var isOpen = expanded == update.Id;
                var tone = update.Tone switch
                {
                    "success" => ArcTokens.Success,
                    "warning" => ArcTokens.Warning,
                    _ => ArcTokens.Accent
                };
                var mark = string.IsNullOrWhiteSpace(update.Actor)
                    ? AppKit.Disc(update.Tone == "success" ? "check" : update.Tone == "warning" ? "triangle-alert" : "info", 30, 14)
                    : AppKit.Person(update.Actor, ArcAvatarSize.Sm);

                var body = new StackPanel { Spacing = 6 };
                body.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 9,
                    Children =
                    {
                        new Border { VerticalAlignment = VerticalAlignment.Top, Child = mark },
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 1,
                            Children =
                            {
                                AppKit.Row(6,
                                    new TextBlock
                                    {
                                        Text = update.Title,
                                        Classes = { "arc-body-sm" },
                                        Foreground = update.Read ? AppKit.Brush(ArcTokens.TextSecondary) : AppKit.Brush(ArcTokens.Foreground)
                                    },
                                    update.Read ? null : AppKit.Dot(ArcTokens.Accent, 6)),
                                new TextBlock
                                {
                                    Text = update.Description,
                                    Classes = { "arc-caption" },
                                    TextWrapping = TextWrapping.Wrap,
                                    MaxWidth = 268,
                                    Foreground = AppKit.Brush(ArcTokens.TextMuted)
                                }
                            }
                        }, 1),
                        AppKit.Place(AppKit.Caption(update.Time), 2)
                    }
                });

                if (isOpen)
                {
                    body.Children.Add(AppKit.Divider());
                    body.Children.Add(AppKit.Caption(update.Tone == "warning"
                        ? "Open the budget report to raise the cap."
                        : "Kept for 30 days, then archived.", 280));
                    body.Children.Add(AppKit.Row(14,
                        AppKit.Link(update.Read ? "Mark unread" : "Mark read", () =>
                        {
                            updates[updates.IndexOf(target)] = target with { Read = !target.Read };
                            Build();
                            AppKit.Say(status, target.Read ? "Marked unread." : "Marked read.");
                        }),
                        AppKit.Link("Dismiss", () =>
                        {
                            updates.Remove(target);
                            expanded = string.Empty;
                            Build();
                            AppKit.Say(status, "Notification dismissed.");
                        })));
                }

                var row = new Border
                {
                    Padding = new Thickness(8, 7, 8, 7),
                    CornerRadius = new CornerRadius(10),
                    Background = isOpen ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                    Child = body
                };
                rows.Children.Add(AppKit.Pressable(row, () => Toggle(target.Id)));
            }

            panel.Children.Add(rows);
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    AppKit.Link("Mark all read", () =>
                    {
                        updates = updates.Select(update => update with { Read = true }).ToList();
                        Build();
                        AppKit.Say(status, "Every notification marked read.");
                    }),
                    AppKit.Place(AppKit.Link("Clear read", () =>
                    {
                        updates = updates.Where(update => !update.Read).ToList();
                        expanded = string.Empty;
                        Build();
                        AppKit.Say(status, "Read notifications cleared.");
                    }), 1)
                }
            });

            var floating = AppKit.Floating(panel, 12, 18, 372);
            floating.HorizontalAlignment = HorizontalAlignment.Right;
            floating.VerticalAlignment = VerticalAlignment.Top;
            floating.Margin = new Thickness(0, 46, 14, 0);
            panelHost.Content = floating;
        }

        void Build()
        {
            BuildTrigger();
            BuildPanel();
        }

        Build();

        Control Card(string title, string note, string icon)
        {
            return new Border
            {
                Padding = new Thickness(14),
                Background = AppKit.Brush(ArcTokens.SurfaceRaised),
                BorderBrush = AppKit.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Child = AppKit.Stack(8,
                    AppKit.Disc(icon, 32, 15),
                    new TextBlock { Text = title, Classes = { "arc-body-sm", "arc-medium" } },
                    AppKit.Caption(note, 200))
            };
        }

        var page = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(18, 12, 18, 12),
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 1,
                            Children =
                            {
                                new TextBlock { Text = "Workspace", Classes = { "arc-body-sm", "arc-medium" } },
                                AppKit.Caption("Northwind · Team plan")
                            }
                        },
                        AppKit.Place(AppKit.IconBtn("search", "Search the workspace", () => AppKit.Say(status, "Search is simulated."), 30), 1),
                        AppKit.Place(new Border { Child = triggerHost }, 2)
                    }
                },
                AppKit.Divider(),
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10,
                    Children =
                    {
                        Card("Release 2.4", "Ships Thursday with the new sidebar.", "zap"),
                        AppKit.Place(Card("Review queue", "6 pull requests waiting on you.", "code"), 1),
                        AppKit.Place(Card("Budget", "Atlas analytics at 82% of the cap.", "chart-pie"), 2)
                    } },
                AppKit.Card(AppKit.Stack(8,
                    AppKit.Row(6,
                        new TextBlock { Text = "This week", Classes = { "arc-label" } },
                        AppKit.Caption("· 4 releases")),
                    AppKit.Line("Changes shipped", "86"),
                    AppKit.Line("Reviews completed", "24"),
                    AppKit.Line("Open incidents", "0", accent: false)), 12),
                status
            }
        };

        var layer = new Panel { Children = { page, panelHost } };
        return AppKit.Screen(layer, 556);
    }
}
