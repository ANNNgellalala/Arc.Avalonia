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

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Inbox triage: a queue of messages with Inbox, Snoozed and Archive folders, rows that expand in place,
/// archive, snooze and restore actions with their letter shortcuts, and a single undo for the last move.
/// </summary>
public sealed class InboxTriagePage : ArcItemPage
{
    public override string ItemSlug => "inbox-triage";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A queue of messages or requests that need archive, snooze or restore decisions.",
        "Inboxes where rows expand in place to show details and actions."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use inbox-sidebar for mail navigation with folders and counts.",
        "Use notification-center for system notifications rather than messages."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Folders are a tab list with arrow-key movement, rows carry their expanded state, and the Unread filter reports pressed state.",
        "E archives, S snoozes, U restores, Enter or Space expands a row and marks it read; moves are announced politely and undo returns focus to the restored row."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A row expands and collapses its own height on the smooth spring, and the folder indicator glides between tabs.",
        "Reduced motion removes height travel and keeps a plain fade."
    };

    public override string? ImplementationNotes =>
        "Messages are sample data. Archive, snooze and restore update local folders and are announced in the status region; nothing is sent to a server and the snooze time is not editable.";

    private sealed record Mail(string Id, string Sender, string Subject, string Preview, string Body, string Time, bool Unread);

    private static readonly Mail[] Seed =
    {
        new("t1", "Ana Duarte", "Contract redlines for Harbour", "Legal flagged two clauses before Thursday.", "Legal flagged two clauses in section 4. The termination window is now 30 days instead of 14. Nothing else changed, so a quick read should be enough.\n\nPlease accept the changes before Thursday so we can countersign.", "9:41", true),
        new("t2", "Support escalation", "Billing webhook retries failing", "Three retries failed in the last hour for one customer.", "Three retries failed in the last hour for customer 8841. The endpoint returns 500 on the invoice.paid event.\n\nThe customer is on the Team plan and has been notified once.", "8:52", true),
        new("t3", "Leo Park", "Design review moved to 15:30", "The room is busy at 14:00, so the review moves an hour later.", "The room is busy at 14:00, so the design review moves to 15:30. Same agenda: the sidebar fold, the settings save bar and the new empty states.", "8:12", true),
        new("t4", "Sara Ruiz", "Re: launch checklist", "Two items still need an owner before Friday.", "Two items still need an owner: the status page update and the support handoff. Everything else is assigned and dated.", "Yesterday", false),
        new("t5", "Northwind Billing", "Invoice 2041 is ready", "Your September invoice is available in the portal.", "Your September invoice is available in the billing portal. The amount is unchanged from August.", "Yesterday", false)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var folders = new Dictionary<string, List<Mail>>
        {
            ["inbox"] = Seed.Take(4).ToList(),
            ["snoozed"] = new() { Seed[1] with { Id = "s1", Subject = "Security questionnaire", Preview = "Snoozed until Friday.", Unread = false } },
            ["archive"] = new() { Seed[3] with { Id = "a1", Unread = false }, Seed[4] with { Id = "a2", Unread = false } }
        };
        var folder = "inbox";
        var unreadOnly = false;
        var expanded = "t1";
        var lastMove = (Mail?)null;
        var lastFrom = string.Empty;

        var tabsHost = new ContentControl();
        var listHost = new ContentControl();
        var undoHost = new ContentControl();

        string Label(string id) => id switch
        {
            "snoozed" => "Snoozed",
            "archive" => "Archive",
            _ => "Inbox"
        };

        void Move(Mail mail, string to, string verb)
        {
            folders[folder].RemoveAll(entry => entry.Id == mail.Id);
            folders[to].Insert(0, mail with { Unread = false });
            lastMove = mail;
            lastFrom = folder;
            expanded = string.Empty;
            if (folder != "inbox" && !folders[folder].Any())
            {
                folder = "inbox";
            }

            Build();
            AppKit.Say(status, $"{mail.Subject} {verb}. Undo is available.");
        }

        void BuildTabs()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var id in new[] { "inbox", "snoozed", "archive" })
            {
                var target = id;
                var selected = folder == id;
                var count = folders[id].Count;
                var button = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    Height = 32,
                    MinHeight = 32,
                    Padding = new Thickness(11, 0, 11, 0),
                    CornerRadius = new CornerRadius(999),
                    Background = selected ? AppKit.Brush(ArcTokens.ControlFill) : null,
                    BorderBrush = selected ? AppKit.Brush(ArcTokens.BorderStrong) : null,
                    BorderThickness = new Thickness(selected ? 1 : 0),
                    Content = AppKit.Row(6,
                        new TextBlock
                        {
                            Text = Label(id),
                            Classes = { "arc-body-sm" },
                            Foreground = selected ? AppKit.Brush(ArcTokens.ControlGlyph) : AppKit.Brush(ArcTokens.TextSecondary)
                        },
                        AppKit.Caption(count.ToString()))
                };
                button.Click += (_, _) =>
                {
                    folder = target;
                    unreadOnly = false;
                    expanded = string.Empty;
                    Build();
                    AppKit.Say(status, $"{Label(target)} folder selected, {folders[target].Count} messages.");
                };
                row.Children.Add(button);
            }

            tabsHost.Content = row;
        }

        void BuildList()
        {
            var visible = folders[folder]
                .Where(mail => !unreadOnly || mail.Unread)
                .ToList();

            var panel = new StackPanel { Spacing = 8 };
            if (visible.Count == 0)
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(24, 46, 24, 0),
                    Child = AppKit.Empty("check-check", unreadOnly ? "No unread messages" : $"{Label(folder)} is clear",
                        unreadOnly ? "Every message here has been read." : "Archive, snooze or restore a message to see it here.")
                });
            }

            for (var index = 0; index < visible.Count; index++)
            {
                var mail = visible[index];
                var isOpen = expanded == mail.Id;
                var unread = mail.Unread;

                var head = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        new Border { VerticalAlignment = VerticalAlignment.Top, Child = AppKit.Person(mail.Sender, ArcAvatarSize.Sm) },
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                AppKit.Row(8,
                                    new TextBlock
                                    {
                                        Text = mail.Sender,
                                        Classes = { "arc-body-sm" },
                                        Foreground = unread ? AppKit.Brush(ArcTokens.Foreground) : AppKit.Brush(ArcTokens.TextSecondary)
                                    },
                                    unread ? AppKit.Dot(ArcTokens.Accent, 6) : null),
                                new TextBlock
                                {
                                    Text = mail.Subject,
                                    Classes = { "arc-body", unread ? "arc-medium" : "arc-body" }
                                },
                                AppKit.Caption(mail.Preview, 520)
                            }
                        }, 1),
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 3,
                            Children =
                            {
                                AppKit.Caption(mail.Time),
                                new ArcIcon
                                {
                                    Icon = isOpen ? "chevron-up" : "chevron-down",
                                    Size = 14,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    Foreground = AppKit.Brush(ArcTokens.TextMuted)
                                }
                            }
                        }, 2)
                    }
                };

                var content = new StackPanel { Spacing = 10 };
                content.Children.Add(head);
                if (isOpen)
                {
                    content.Children.Add(AppKit.Divider());
                    content.Children.Add(AppKit.Body(mail.Body, 620));
                    var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    if (folder != "archive")
                    {
                        actions.Children.Add(AppKit.Btn("Archive", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Move(mail, "archive", "archived"), icon: "archive", height: 30));
                    }

                    if (folder == "inbox")
                    {
                        actions.Children.Add(AppKit.Btn("Snooze", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Move(mail, "snoozed", "snoozed until Friday"), icon: "clock", height: 30));
                    }

                    if (folder != "inbox")
                    {
                        actions.Children.Add(AppKit.Btn("Restore", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Move(mail, "inbox", "restored to Inbox"), icon: "inbox", height: 30));
                    }

                    actions.Children.Add(AppKit.Link("Reply", () => AppKit.Say(status, "Reply is simulated."), icon: "corner-down-left"));
                    content.Children.Add(actions);
                }

                var card = new Border
                {
                    Padding = new Thickness(12),
                    Background = isOpen ? AppKit.Tint(ArcTokens.Accent, 0.06) : AppKit.Brush(ArcTokens.SurfaceRaised),
                    BorderBrush = isOpen ? AppKit.Tint(ArcTokens.Accent, 0.2) : AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Child = content
                };

                panel.Children.Add(AppKit.Pressable(card, () =>
                {
                    expanded = isOpen ? string.Empty : mail.Id;
                    folders[folder].RemoveAll(entry => entry.Id == mail.Id);
                    folders[folder].Insert(index, mail with { Unread = false });
                    Build();
                    if (!isOpen)
                    {
                        AppKit.Say(status, $"{mail.Subject} opened and marked read.");
                    }
                }));
            }

            listHost.Content = panel;
        }

        void BuildUndo()
        {
            if (lastMove is null)
            {
                undoHost.Content = null;
                return;
            }

            var mail = lastMove;
            var bar = AppKit.Floating(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    AppKit.Row(7,
                        new ArcIcon { Icon = "corner-down-left", Size = 14, Foreground = AppKit.Brush(ArcTokens.TextSecondary) },
                        new TextBlock { Text = $"Moved \"{mail.Subject}\"", Classes = { "arc-body-sm" } }),
                    new Border { Width = 0 },
                    AppKit.Place(AppKit.Link("Undo", () =>
                    {
                        folders[folder].RemoveAll(entry => entry.Id == mail.Id);
                        folders[lastFrom].Insert(0, mail);
                        expanded = mail.Id;
                        folder = lastFrom;
                        lastMove = null;
                        Build();
                        AppKit.Say(status, "Move undone.");
                    }, trailingIcon: "undo-2"), 2)
                }
            }, 8, 12);
            bar.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Width = 420;
            undoHost.Content = bar;
        }

        void Build()
        {
            BuildTabs();
            BuildList();
            BuildUndo();
        }

        Build();

        var unreadToggle = AppKit.Chip("Unread", unreadOnly, () =>
        {
            unreadOnly = !unreadOnly;
            Build();
            AppKit.Say(status, unreadOnly ? "Showing unread messages only." : "Showing every message.");
        }, folders[folder].Count(mail => mail.Unread).ToString());

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = "Inbox", Classes = { "arc-body", "arc-medium" } },
                        AppKit.Caption("Four messages, three unread. Triage them one at a time.")
                    }
                },
                AppKit.Place(AppKit.Row(8, unreadToggle,
                    AppKit.Btn("Sort", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => AppKit.Say(status, "Sorting is simulated."), trailingIcon: "chevron-down", height: 30)), 1)
            }
        };

        var layout = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(18, 14, 18, 12),
            Children =
            {
                header,
                tabsHost,
                AppKit.Divider(),
                listHost,
                undoHost,
                status
            }
        };

        AppKit.Shortcut(layout, Key.E, () =>
        {
            var mail = folders[folder].FirstOrDefault(entry => entry.Id == expanded);
            if (mail is not null && folder != "archive")
            {
                Move(mail, "archive", "archived");
            }
        });
        AppKit.Shortcut(layout, Key.S, () =>
        {
            var mail = folders[folder].FirstOrDefault(entry => entry.Id == expanded);
            if (mail is not null && folder == "inbox")
            {
                Move(mail, "snoozed", "snoozed until Friday");
            }
        });
        AppKit.Shortcut(layout, Key.U, () =>
        {
            var mail = folders[folder].FirstOrDefault(entry => entry.Id == expanded);
            if (mail is not null && folder != "inbox")
            {
                Move(mail, "inbox", "restored to Inbox");
            }
        });
        AppKit.Shortcut(layout, Key.Left, () =>
        {
            folder = folder == "archive" ? "snoozed" : "inbox";
            expanded = string.Empty;
            Build();
            AppKit.Say(status, $"{Label(folder)} folder selected.");
        });
        AppKit.Shortcut(layout, Key.Right, () =>
        {
            folder = folder == "inbox" ? "snoozed" : "archive";
            expanded = string.Empty;
            Build();
            AppKit.Say(status, $"{Label(folder)} folder selected.");
        });

        return AppKit.Screen(new ScrollViewer { Content = layout, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }, 560);
    }
}
