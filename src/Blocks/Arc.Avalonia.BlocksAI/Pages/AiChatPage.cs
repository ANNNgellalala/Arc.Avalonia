using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.BlocksAI.AiKit;

namespace Arc.Avalonia.BlocksAI.Pages;

/// <summary>
/// AI chat: a full conversation card with folding reasoning, a tool chip, cited sources and message actions.
/// Switching the model, rating a reply and expanding reasoning all change in place; new messages append a
/// scripted reply. Nothing is sent to a model.
/// </summary>
public sealed class AiChatPage : ArcItemPage
{
    public override string ItemSlug => "ai-chat";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A multi turn assistant inside a product page, drawer or dashboard card, with streaming answers and citations.",
        "Anywhere people retry, edit or rate answers and switch model mid conversation.",
        "Prototyping an LLM feature: the scripted thread stands in for a real model."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI composer when you only need the prompt box, or AI answer for one long cited answer."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each turn is an article; a streaming reply sets a busy state and one polite region announces start, finish and stop.",
        "Reasoning and tool rows are buttons with an expanded state, and citations are named by source number and title.",
        "Icon actions carry specific names: Copy, Retry, Good response, Bad response and Edit."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Replies stream in word chunks while the reasoning and tool rows fold open on a height spring with no bounce.",
        "Reduced motion drops the chunk fade and the travel, and keeps every label and announcement."
    };

    public override string? ImplementationNotes =>
        "Messages are rendered from plain strings with a small Markdown subset. Brand marks are monogram discs "
        + "because the port has no brand glyphs. Editing a message into a version, stopping a stream and the hover "
        + "reveal of actions are described in the notes but not animated: the specimen opens on a settled thread.";

    private sealed record Turn(string Text, bool Self);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var turns = new List<Turn>
        {
            new("Why did checkout p95 jump to 4.2 seconds yesterday?", true),
            new("Checkout p95 rose because the retry queue backed up behind a slow authorization call. [1]\n\n"
                + "- The queue reached 18,400 jobs at **14:07**, its highest point this year\n"
                + "- Retries were capped at 30 seconds, so each slow call multiplied the backlog [2]\n"
                + "- Raising the cap to 90 seconds cleared the queue by **14:41**",
                false)
        };

        var modelIndex = 1;
        var rating = 0;
        var reasoningOpen = false;

        var thread = new StackPanel { Spacing = 12 };
        var scroller = K.Scroller(thread, 356);
        var status = K.Caption("Reply finished in 6s, 2 sources cited. All replies are simulated.");

        var reasoningBody = new Border
        {
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            Child = K.Text(
                "The question compares yesterday's latency with the retry window. I checked the queue depth, the "
                + "timeout configuration and the deploy log for the payment service.", "arc-caption", 640)
        };

        void Render()
        {
            thread.Children.Clear();
            foreach (var turn in turns)
            {
                thread.Children.Add(turn.Self ? UserTurn(turn) : ReplyTurn(turn));
            }
        }

        Control UserTurn(Turn turn)
        {
            return new Border
            {
                Background = K.Tint(ArcTokens.Accent, 0.09),
                BorderBrush = K.Tint(ArcTokens.Accent, 0.2),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(13, 8, 13, 8),
                MaxWidth = 540,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = K.Text(turn.Text, "arc-body-sm")
            };
        }

        Control ReplyTurn(Turn turn)
        {
            var head = K.Row(7,
                K.Disc("sparkles", 22, true, 12),
                K.Text("Claude Sonnet 5.5", "arc-caption"),
                K.Dot(ArcTokens.BorderStrong, 3),
                K.Caption("Thought for 4s"));

            var reasoningButton = K.IconBtn("chevron-right", "Show reasoning", size: 12);
            reasoningButton.Click += (_, _) =>
            {
                reasoningOpen = !reasoningOpen;
                reasoningBody.IsVisible = reasoningOpen;
                reasoningButton.Content = new ArcIcon
                {
                    Icon = reasoningOpen ? "chevron-down" : "chevron-right",
                    Size = 12,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                AutomationProperties.SetName(reasoningButton, reasoningOpen ? "Hide reasoning" : "Show reasoning");
                AutomationProperties.SetHelpText(reasoningButton, reasoningOpen ? "Expanded" : "Collapsed");
                status.Text = reasoningOpen ? "Reasoning shown for this reply." : "Reasoning folded.";
            };
            head.Children.Add(reasoningButton);

            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(K.Markdown(turn.Text, maxWidth: 660));

            var tools = K.Row(6,
                K.Chip("Searched 6 sites", icon: "globe"),
                K.Chip("Read 3 files", icon: "file"),
                K.Chip("2 sources", icon: "link"));
            body.Children.Add(tools);

            var sources = new WrapPanel { Orientation = Orientation.Horizontal };
            sources.Children.Add(K.SourceCard(1, "Queue depth dashboard", "grafana.acme.dev", "G"));
            sources.Children.Add(K.SourceCard(2, "Payment retry policy", "docs.acme.dev", "D"));
            body.Children.Add(sources);

            var actions = K.Row(4,
                K.IconBtn("copy", "Copy answer"),
                K.IconBtn("refresh-cw", "Retry answer"),
                K.IconBtn("thumbs-up", "Good response"),
                K.IconBtn("minus-circle", "Bad response"),
                K.Dot(ArcTokens.BorderStrong, 3),
                K.Caption("2 / 2"),
                K.IconBtn("chevron-left", "Previous version", size: 12),
                K.IconBtn("chevron-right", "Next version", size: 12));
            body.Children.Add(actions);

            var ratingNote = K.Caption(string.Empty);
            ratingNote.IsVisible = false;
            body.Children.Add(ratingNote);

            var up = (ArcButton)actions.Children[2];
            var down = (ArcButton)actions.Children[3];
            up.Click += (_, _) =>
            {
                rating = rating == 1 ? 0 : 1;
                up.Variant = rating == 1 ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost;
                down.Variant = ArcButtonVariant.Ghost;
                ratingNote.Text = rating == 1 ? "Marked as a good response. Simulated feedback." : string.Empty;
                AutomationProperties.SetName(up, rating == 1 ? "Good response, selected" : "Good response");
            };
            down.Click += (_, _) =>
            {
                rating = rating == -1 ? 0 : -1;
                down.Variant = rating == -1 ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost;
                up.Variant = ArcButtonVariant.Ghost;
                ratingNote.Text = rating == -1 ? "Marked as a bad response. Simulated feedback." : string.Empty;
                AutomationProperties.SetName(down, rating == -1 ? "Bad response, selected" : "Bad response");
            };

            var stack = K.Stack(6, head, reasoningBody, body);
            return stack;
        }

        var modelChip = new K.ModelChip(K.Models[1]);
        modelChip.Button.Click += (_, _) =>
        {
            modelIndex = (modelIndex + 1) % K.Models.Length;
            var model = K.Models[modelIndex];
            modelChip.Set(model);
            status.Text = $"Next replies use {model.Name}. Earlier replies keep their own model.";
        };

        var composer = new ArcTextarea
        {
            Placeholder = "Ask a follow-up",
            Rows = 1,
            AutoGrow = false,
            Height = 46
        };
        AutomationProperties.SetName(composer, "Message");

        var send = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Content = new ArcIcon { Icon = "arrow-up", Size = 15, HorizontalAlignment = HorizontalAlignment.Center }
        };
        AutomationProperties.SetName(send, "Send message");

        void Send()
        {
            var text = (composer.Value ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            turns.Add(new Turn(text, true));
            turns.Add(new Turn("Here is the short version: the retry queue was capped too low for the slow "
                + "authorization call, so backlog grew faster than it drained. Raising the cap fixed it, and the "
                + "queue has been under 4 seconds since **14:45**.", false));
            composer.Value = string.Empty;
            Render();
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            status.Text = "Reply finished in 4s. Simulated reply.";
        }

        send.Click += (_, _) => Send();
        composer.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && !args.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                Send();
                args.Handled = true;
            }
        };

        Render();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(K.Row(6,
            K.Text("Checkout latency spike", "arc-label"),
            K.Caption("Sarah Chen · 12 turns")));
        var headerRight = K.Row(4,
            modelChip.Button,
            K.IconBtn("plus", "New chat"),
            K.IconBtn("more-horizontal", "Conversation menu"));
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(headerRight, 1);
        header.Children.Add(headerRight);

        var composerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        composerRow.Children.Add(composer);
        var composerRight = K.Row(4, K.IconBtn("plus", "Attach files"), send);
        Grid.SetColumn(composerRight, 1);
        composerRight.Margin = new Thickness(6, 0, 0, 0);
        composerRow.Children.Add(composerRight);

        return K.Surface(new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20, 14, 20, 14),
            Children =
            {
                header,
                K.Divider(0),
                scroller,
                K.Divider(0),
                composerRow,
                status
            }
        });
    }
}
