using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Support conversation: a ticket thread with quick replies, an attachment affordance, a typing indicator and
/// a resolve toggle. The messages and the canned reply are sample data; sending appends locally and the reply
/// arrives on a timer, so the thread reads like a live conversation without a backend.
/// </summary>
public sealed class SupportConversationPage : ArcItemPage
{
    public override string ItemSlug => "support-conversation";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A support chat or ticket thread with quick replies and attachments.",
        "Help flows that should show a typing indicator and a resolve toggle."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A floating help launcher; use support-widget."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "New messages are announced in a polite live region, and the send, attach and typing elements have labels.",
        "The resolve toggle states the ticket status in words and can be reversed without losing the thread."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Messages rise into the thread on the shared spring and the typing indicator fades in place.",
        "Reduced motion removes the offsets and uses instant transitions."
    };

    public override string? ImplementationNotes =>
        "Messages, the canned reply and the agent are sample data. Attaching a file opens the picker in the reference; here the button reports that attachments are simulated. Replies are queued with a local timer and no network call is made.";

    private sealed record Message(string Author, string Text, string Time, bool Mine, string Initials);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var messages = new List<Message>
        {
            new("Maya Chen", "Hi! I can see your workspace is on the Growth plan. What is happening with the invoice?", "09:41", false, "MC"),
            new("You", "The September invoice shows 14 seats but we only have 12 active members.", "09:42", true, "YO"),
            new("Maya Chen", "That is usually a deactivated member who kept a seat. Let me check the seat list before we change the invoice.", "09:43", false, "MC")
        };
        var resolved = false;
        var typing = false;
        var status = Ck.Status();

        var threadHost = new StackPanel { Spacing = 10 };
        var typingHost = new ContentControl();
        var quickHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var composer = Ck.Input("Message support", null, null, null);
        composer.HorizontalAlignment = HorizontalAlignment.Stretch;

        void BuildTyping()
        {
            typingHost.Content = typing
                ? Ck.Row(8,
                    Ck.Person("Maya Chen", ArcAvatarSize.Xs),
                    new Border
                    {
                        Background = Ck.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(10, 6, 10, 6),
                        Child = Ck.Caption("Maya is typing…")
                    })
                : null;
        }

        void BuildThread()
        {
            threadHost.Children.Clear();
            foreach (var message in messages)
            {
                var body = new Border
                {
                    Background = message.Mine
                        ? Ck.Tint(ArcTokens.Accent, 0.12)
                        : Ck.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 9, 12, 9),
                    MaxWidth = 470,
                    Child = new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            Ck.Text(message.Text, "arc-body-sm"),
                            Ck.Caption(message.Time)
                        }
                    }
                };

                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = message.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left
                };
                if (!message.Mine)
                {
                    row.Children.Add(Ck.Person(message.Author, ArcAvatarSize.Xs));
                }

                row.Children.Add(body);
                if (message.Mine)
                {
                    row.Children.Add(Ck.Person(message.Author, ArcAvatarSize.Xs));
                }

                threadHost.Children.Add(row);
            }

            BuildTyping();
        }

        void Send(string text, bool canned)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            messages.Add(new Message("You", text.Trim(), "09:4" + (messages.Count % 10), true, "YO"));
            BuildThread();
            Ck.Say(status, canned ? "Message sent with a quick reply." : "Message sent to Maya.");

            if (!canned)
            {
                composer.Value = string.Empty;
            }

            typing = true;
            BuildTyping();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                messages.Add(new Message("Maya Chen",
                    "I removed the seat from the inactive member and reissued the invoice. You will see a credit note for $49 on the next billing screen.",
                    "09:45", false, "MC"));
                typing = false;
                BuildThread();
                Ck.Say(status, "Maya replied and the invoice was reissued.");
            };
            timer.Start();
        }

        foreach (var reply in new[]
                 {
                     "Send me the seat list", "Reissue the invoice", "We were double charged"
                 })
        {
            var text = reply;
            quickHost.Children.Add(Ck.Btn(text, ArcButtonVariant.Secondary, ArcButtonSize.Sm,
                () => Send(text, true), height: 32));
        }

        BuildThread();

        var resolve = Ck.Toggle("Resolved", false, value =>
        {
            resolved = value;
            Ck.Say(status, resolved ? "Ticket marked resolved. You can reopen it any time." : "Ticket reopened.");
        });

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(Ck.Person("Maya Chen", ArcAvatarSize.Md));
        header.Children.Add(Place(new StackPanel
        {
            Spacing = 1,
            Children =
            {
                Ck.Text("Maya Chen · Support", "arc-body-sm arc-medium"),
                Ck.Caption("Typically replies in 2 minutes · ticket 4812")
            }
        }, 1));
        header.Children.Add(Place(resolve, 2));

        var composerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        composerRow.Children.Add(Ck.IconBtn("upload", "Attach a PDF", () => Ck.Say(status, "Attachments open the file picker in the reference; this specimen simulates it."), 32));
        composerRow.Children.Add(Place(composer, 1));
        composerRow.Children.Add(Place(Ck.IconBtn("smile", "Add a reaction", () => Ck.Say(status, "Reactions are not part of this specimen."), 32), 2));
        composerRow.Children.Add(Place(Ck.Btn("Send", ArcButtonVariant.Primary, ArcButtonSize.Md,
            () => Send(composer.Value ?? string.Empty, false), icon: "send", height: 38), 3));

        var threadCard = Ck.Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                threadHost,
                typingHost,
                Ck.Divider(),
                Ck.Caption("Quick replies"),
                quickHost,
                composerRow,
                Ck.Row(6,
                    new ArcIcon { Icon = "upload", Size = 12, Foreground = Ck.Brush(ArcTokens.TextMuted) },
                    Ck.Caption("PDF up to 10 MB · enter to send"))
            }
        }, 14);

        var card = new StackPanel
        {
            Spacing = 12,
            Children = { header, Ck.Divider(), threadCard, status }
        };

        return Ck.Specimen(new Border { Child = card, Width = 780, HorizontalAlignment = HorizontalAlignment.Center }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
