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
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Inbox sidebar: the shell of a mail app. Folders filter the list, rows carry quick actions, a message
/// opens in the reading pane, and Compose grows into a composer that sends with the keyboard. Folders are
/// sample mailboxes; nothing is delivered.
/// </summary>
public sealed class InboxSidebarPage : ArcItemPage
{
    public override string ItemSlug => "inbox-sidebar";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The shell of a mail or messaging app with folders and counts.",
        "Mail clients with row quick actions and a compose window."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use inbox-triage for a focused one-at-a-time triage flow.",
        "Use notification-center for app notifications rather than mail."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Folders are buttons with their unread counts; the composer is a labelled dialog that returns focus when it closes.",
        "[ folds the rail unless a field has focus, E archives the open message, S stars it, Cmd/Ctrl+Enter sends, and Escape saves a draft."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Compose grows into the composer, a dropped message leaves the list, and the target folder count rolls.",
        "Reduced motion makes the morph and list changes instant."
    };

    public override string? ImplementationNotes =>
        "Folders, messages and sending are sample data. Dragging a row onto a folder is replaced by the row's Move action and the E shortcut, because OS drag requires a drag source the offscreen gallery cannot provide; Compose, Send and the Escape draft path are live.";

    private sealed record Message(
        string Id,
        string Folder,
        string From,
        string Subject,
        string Preview,
        string Time,
        bool Unread,
        bool Starred,
        string Body);

    private static readonly Message[] Seed =
    {
        new("m1", "inbox", "Ana Duarte", "Contract redlines for Harbour", "Legal flagged two clauses in section 4. I attached the marked copy so you can accept the changes.", "9:41", true, true,
            "Hi Maya,\n\nLegal flagged two clauses in section 4 of the Harbour agreement. I attached the marked copy so you can accept the changes before Thursday.\n\nThe only substantive change is the termination window, which is now 30 days instead of 14.\n\nThanks,\nAna"),
        new("m2", "inbox", "Leo Park", "Design review moved to 15:30", "The room is busy at 14:00, so the review moves an hour later. Same agenda.", "8:12", true, false,
            "The room is busy at 14:00, so the design review moves to 15:30. Same agenda: the sidebar fold, the settings save bar and the new empty states."),
        new("m3", "inbox", "Sara Ruiz", "Re: launch checklist", "Added the rollback step and the smoke test owner. Two items still need a name next to them.", "Yesterday", false, true,
            "Added the rollback step and the smoke test owner. Two items still need a name next to them: the status page update and the support handoff."),
        new("m4", "inbox", "Northwind Billing", "Invoice 2041 is ready", "Your September invoice is available in the billing portal.", "Yesterday", false, false,
            "Your September invoice is available in the billing portal. The amount is unchanged from August."),
        new("m5", "starred", "Ana Duarte", "Contract redlines for Harbour", "Legal flagged two clauses in section 4. I attached the marked copy so you can accept the changes.", "9:41", false, true,
            "Hi Maya,\n\nLegal flagged two clauses in section 4 of the Harbour agreement. I attached the marked copy so you can accept the changes before Thursday.\n\nThanks,\nAna"),
        new("m6", "drafts", "Draft", "Q4 planning notes", "Draft saved two days ago. Three sections still empty.", "2d", false, false,
            "Q4 planning notes\n\n- Hiring plan: \n- Platform bets: \n- Budget: "),
        new("m7", "snoozed", "Tom Hale", "Security questionnaire", "Snoozed until Friday. The questionnaire is mostly the standard set.", "Fri", false, false,
            "The security questionnaire is mostly the standard set with two new questions about data residency."),
        new("m8", "archive", "Ines Ma", "Welcome to Northwind", "Your account is ready. Start with the workspace tour.", "Aug 12", false, false,
            "Your account is ready. Start with the workspace tour, then invite the rest of your team.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var messages = Seed.ToList();
        var folder = "inbox";
        var open = "m1";
        var composing = false;
        var collapsed = false;

        var folderHost = new ContentControl();
        var listHost = new ContentControl();
        var readerHost = new ContentControl();
        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("196,304,*") };

        string[] Folders = { "inbox", "starred", "snoozed", "sent", "drafts", "archive" };

        string FolderLabel(string id) => id switch
        {
            "inbox" => "Inbox",
            "starred" => "Starred",
            "snoozed" => "Snoozed",
            "sent" => "Sent",
            "drafts" => "Drafts",
            "archive" => "Archive",
            _ => id
        };

        string FolderIcon(string id) => id switch
        {
            "inbox" => "inbox",
            "starred" => "star",
            "snoozed" => "clock",
            "sent" => "send",
            "drafts" => "file",
            "archive" => "archive",
            _ => "folder"
        };

        int Unread(string id) => messages.Count(message => message.Folder == id && message.Unread);

        Control FolderRow(string id)
        {
            var target = id;
            var count = Unread(id);
            return AppKit.NavRow(FolderIcon(id), FolderLabel(id), folder == id, () =>
            {
                folder = target;
                composing = false;
                open = messages.FirstOrDefault(message => message.Folder == target)?.Id ?? string.Empty;
                Build();
                AppKit.Say(status, $"{FolderLabel(target)} selected, {messages.Count(message => message.Folder == target)} messages.");
            }, count > 0 ? count.ToString() : null);
        }

        void BuildFolders()
        {
            var panel = new StackPanel { Spacing = 3 };
            if (collapsed)
            {
                panel.Children.Add(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6),
                    Child = AppKit.IconBtn("pencil", "Compose", () => OpenComposer(), 34)
                });
                foreach (var id in Folders)
                {
                    panel.Children.Add(new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 1),
                        Child = AppKit.IconBtn(FolderIcon(id), FolderLabel(id), () =>
                        {
                            folder = id;
                            open = messages.FirstOrDefault(message => message.Folder == id)?.Id ?? string.Empty;
                            Build();
                        }, 34)
                    });
                }
            }
            else
            {
                panel.Children.Add(AppKit.Btn("Compose", ArcButtonVariant.Primary, ArcButtonSize.Sm, OpenComposer, icon: "pencil", height: 34, stretch: true));
                panel.Children.Add(new Border { Height = 8 });
                foreach (var id in Folders)
                {
                    panel.Children.Add(FolderRow(id));
                }

                panel.Children.Add(new Border { Height = 8 });
                panel.Children.Add(new Border
                {
                    Margin = new Thickness(9, 2, 9, 4),
                    Child = new TextBlock { Text = "Labels", Classes = { "arc-caption", "arc-muted" } }
                });
                foreach (var label in new[] { "Design", "Billing", "Support" })
                {
                    var value = label;
                    panel.Children.Add(AppKit.NavRow("tag", label, false, () => AppKit.Say(status, $"{value} label filter is simulated.")));
                }


            }

            var footer = new StackPanel { Spacing = 6 };
            footer.Children.Add(AppKit.Divider());
            if (collapsed)
            {
                footer.Children.Add(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = AppKit.IconBtn("panel-right", "Expand the sidebar", () =>
                    {
                        collapsed = false;
                        Build();
                        AppKit.Say(status, "Sidebar expanded.");
                    }, 32)
                });
            }
            else
            {
                footer.Children.Add(new Grid
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
                                AppKit.Caption("Team plan")
                            }
                        }, 1),
                        AppKit.Place(AppKit.IconBtn("panel-left", "Collapse the sidebar", () =>
                        {
                            collapsed = true;
                            Build();
                            AppKit.Say(status, "Sidebar collapsed to the rail.");
                        }, 26), 2)
                    }
                });
            }

            folderHost.Content = new Border
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
                            Content = panel,
                            ClipToBounds = true,
                            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                        },
                        AppKit.Place(footer, 0, 1)
                    }
                }
            };
        }

        void OpenComposer()
        {
            composing = true;
            folder = "drafts";
            Build();
            AppKit.Say(status, "Composer opened. Cmd or Ctrl+Enter sends, Escape saves a draft.");
        }

        void BuildList()
        {
            var visible = messages
                .Where(message => message.Folder == folder)
                .OrderByDescending(message => message.Unread)
                .ToList();

            var header = new Border
            {
                Padding = new Thickness(14, 10, 12, 10),
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 0,
                            Children =
                            {
                                new TextBlock { Text = FolderLabel(folder), Classes = { "arc-body-sm", "arc-medium" } },
                                AppKit.Caption($"{visible.Count} messages · {visible.Count(message => message.Unread)} unread")
                            }
                        },
                        AppKit.Place(AppKit.Row(4,
                            AppKit.IconBtn("filter", "Filter messages", () => AppKit.Say(status, "Filtering is simulated."), 26),
                            AppKit.IconBtn("chevrons-up-down", "Sort by newest", () => AppKit.Say(status, "Sorted by newest."), 26)), 1)
                    }
                }
            };

            var rows = new StackPanel { Spacing = 0 };
            if (visible.Count == 0)
            {
                rows.Children.Add(new Border
                {
                    Padding = new Thickness(20, 40, 20, 0),
                    Child = AppKit.Empty("inbox", $"{FolderLabel(folder)} is empty", "Messages you move here will show up in this list.")
                });
            }

            foreach (var message in visible)
            {
                var target = message;
                var selected = !composing && open == message.Id;
                var row = new Border
                {
                    Padding = new Thickness(12, 9, 10, 9),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.09) : null,
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            new Border { VerticalAlignment = VerticalAlignment.Top, Child = AppKit.Person(message.From, ArcAvatarSize.Xs) },
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 2,
                                Children =
                                {
                                    new Grid
                                    {
                                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                                        ColumnSpacing = 8,
                                        Children =
                                        {
                                            new TextBlock
                                            {
                                                Text = message.From,
                                                Classes = { "arc-body-sm", message.Unread ? "arc-medium" : "arc-body-sm" },
                                                TextTrimming = TextTrimming.CharacterEllipsis
                                            },
                                            AppKit.Place(AppKit.Caption(message.Time), 1)
                                        }
                                    },
                                    new TextBlock
                                    {
                                        Text = message.Subject,
                                        Classes = { "arc-body-sm" },
                                        TextTrimming = TextTrimming.CharacterEllipsis
                                    },
                                    AppKit.Caption(message.Preview, 260)
                                }
                            }, 1),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 5,
                                Children =
                                {
                                    message.Unread ? AppKit.Dot(ArcTokens.Accent, 6) : new Border { Height = 6 },
                                    AppKit.Pressable(new Border
                                    {
                                        Width = 18,
                                        Height = 18,
                                        Child = new ArcIcon
                                        {
                                            Icon = "star",
                                            Size = 13,
                                            Foreground = message.Starred ? AppKit.Brush(ArcTokens.Warning) : AppKit.Brush(ArcTokens.TextMuted)
                                        }
                                    }, () =>
                                    {
                                        messages[messages.IndexOf(target)] = target with { Starred = !target.Starred };
                                        BuildList();
                                        AppKit.Say(status, target.Starred ? "Star removed." : "Message starred.");
                                    })
                                }
                            }, 2)
                        }
                    }
                };

                rows.Children.Add(AppKit.Pressable(row, () =>
                {
                    open = target.Id;
                    composing = false;
                    messages[messages.IndexOf(target)] = target with { Unread = false };
                    Build();
                    AppKit.Say(status, $"{target.Subject} opened.");
                }));
            }

            listHost.Content = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children = { header, AppKit.Place(rows, 0, 1) }
            };
        }

        void BuildReader()
        {
            if (composing)
            {
                var to = new ArcInput { Placeholder = "name@company.com", Width = 300 };
                var subject = new ArcInput { Placeholder = "Subject", Value = "Q4 planning notes" };
                var body = new Arc.Avalonia.Inputs.Controls.ArcTextarea
                {
                    Placeholder = "Write a message",
                    Value = "Sharing the Q4 plan for review before Thursday.\n\n",
                    Rows = 5,
                    AutoGrow = false
                };
                var send = AppKit.Btn("Send", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                {
                    composing = false;
                    Build();
                    AppKit.Say(status, "Message sent from the preview. Nothing left the machine.");
                }, icon: "send", height: 32);

                var composer = new Border
                {
                    Margin = new Thickness(16),
                    Padding = new Thickness(16),
                    Background = AppKit.Brush(ArcTokens.SurfaceRaised),
                    BorderBrush = AppKit.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    BoxShadow = AppKit.Shadow(),
                    Child = new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                                Children =
                                {
                                    AppKit.Stack(2,
                                        new TextBlock { Text = "New message", Classes = { "arc-body-sm", "arc-medium" } },
                                        AppKit.Caption("Draft saved automatically")),
                                    AppKit.Place(AppKit.IconBtn("x", "Close the composer and save a draft", () =>
                                    {
                                        composing = false;
                                        folder = "drafts";
                                        Build();
                                        AppKit.Say(status, "Draft saved.");
                                    }, 26), 1)
                                }
                            },
                            AppKit.Field("To", to),
                            subject,
                            body,
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                                Children =
                                {
                                    AppKit.Row(6,
                                        AppKit.IconBtn("paperclip", "Attach a file", () => AppKit.Say(status, "Attachments are simulated."), 28),
                                        AppKit.IconBtn("smile", "Insert an emoji", () => AppKit.Say(status, "Emoji picker is simulated."), 28),
                                        AppKit.Caption("Cmd or Ctrl+Enter to send")),
                                    AppKit.Place(send, 1)
                                }
                            }
                        }
                    }
                };

                AppKit.Shortcut(composer, Key.Enter, () =>
                {
                    composing = false;
                    Build();
                    AppKit.Say(status, "Message sent from the preview.");
                }, modifiers => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));

                readerHost.Content = composer;
                return;
            }

            var message = messages.FirstOrDefault(entry => entry.Id == open) ?? messages.FirstOrDefault(entry => entry.Folder == folder);
            if (message is null)
            {
                readerHost.Content = AppKit.Empty("mail", "Nothing open", "Pick a message from the list to read it here.");
                return;
            }

            var actions = AppKit.Row(6,
                AppKit.Btn("Reply", ArcButtonVariant.Primary, ArcButtonSize.Sm, OpenComposer, icon: "corner-down-left", height: 30),
                AppKit.Btn("Forward", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Forwarding is simulated."), height: 30),
                AppKit.IconBtn("star", "Star this message", () =>
                {
                    messages[messages.IndexOf(message)] = message with { Starred = !message.Starred };
                    Build();
                    AppKit.Say(status, message.Starred ? "Star removed." : "Message starred.");
                }, 30),
                AppKit.IconBtn("archive", "Archive this message", () =>
                {
                    messages[messages.IndexOf(message)] = message with { Folder = "archive", Unread = false };
                    folder = "inbox";
                    open = messages.FirstOrDefault(entry => entry.Folder == "inbox")?.Id ?? string.Empty;
                    Build();
                    AppKit.Say(status, "Message archived. Press E on a row to archive it next time.");
                }, 30));

            var paragraphs = new StackPanel { Spacing = 9 };
            foreach (var paragraph in message.Body.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    continue;
                }

                paragraphs.Children.Add(AppKit.Body(paragraph, 460));
            }

            readerHost.Content = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(18, 14, 18, 14),
                Children =
                {
                    new TextBlock { Text = message.Subject, Classes = { "arc-subtitle" } },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            AppKit.Person(message.From, ArcAvatarSize.Md),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = message.From, Classes = { "arc-body-sm", "arc-medium" } },
                                    AppKit.Caption($"to Maya Chen · {message.Time}")
                                }
                            }, 1),
                            AppKit.Place(AppKit.Pill(message.Unread ? "Unread" : "Read", message.Unread ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral), 2)
                        }
                    },
                    AppKit.Divider(),
                    paragraphs,
                    AppKit.Tile(new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            AppKit.Disc("file", 30, 14),
                            AppKit.Place(AppKit.Stack(1,
                                new TextBlock { Text = "harbour-agreement-redlines.pdf", Classes = { "arc-body-sm" } },
                                AppKit.Caption("248 KB · PDF")), 1),
                            AppKit.Place(AppKit.IconBtn("download", "Download the attachment", () => AppKit.Say(status, "Downloads are simulated."), 28), 2)
                        }
                    }, 10, 12, hairline: true),
                    AppKit.Divider(),
                    actions
                }
            };
        }

        void Build()
        {
            BuildFolders();
            BuildList();
            BuildReader();
            shell.ColumnDefinitions[0].Width = new GridLength(collapsed ? 64 : 196);
        }

        var listColumn = new Border
        {
            BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = listHost
        };
        Grid.SetColumn(listColumn, 1);
        Grid.SetColumn(readerHost, 2);
        shell.Children.Add(folderHost);
        shell.Children.Add(listColumn);
        shell.Children.Add(readerHost);

        Build();

        AppKit.Shortcut(shell, Key.OemOpenBrackets, () =>
        {
            collapsed = !collapsed;
            Build();
            AppKit.Say(status, collapsed ? "Sidebar collapsed to the rail." : "Sidebar expanded.");
        });
        AppKit.Shortcut(shell, Key.E, () =>
        {
            if (composing)
            {
                return;
            }

            var message = messages.FirstOrDefault(entry => entry.Id == open && entry.Folder != "archive");
            if (message is null)
            {
                return;
            }

            messages[messages.IndexOf(message)] = message with { Folder = "archive", Unread = false };
            open = messages.FirstOrDefault(entry => entry.Folder == folder)?.Id ?? string.Empty;
            Build();
            AppKit.Say(status, $"{message.Subject} archived.");
        });
        AppKit.Shortcut(shell, Key.S, () =>
        {
            var message = messages.FirstOrDefault(entry => entry.Id == open);
            if (message is null)
            {
                return;
            }

            messages[messages.IndexOf(message)] = message with { Starred = !message.Starred };
            Build();
            AppKit.Say(status, message.Starred ? "Star removed." : "Message starred.");
        });
        AppKit.Shortcut(shell, Key.Escape, () =>
        {
            if (composing)
            {
                composing = false;
                folder = "drafts";
                Build();
                AppKit.Say(status, "Composer closed and the draft saved.");
            }
        });

        return AppKit.Screen(shell, 570);
    }
}
