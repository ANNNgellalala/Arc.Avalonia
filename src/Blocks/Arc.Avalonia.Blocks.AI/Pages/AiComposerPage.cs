using System;
using System.Collections.Generic;
using System.Linq;
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
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// AI composer: the prompt box at the bottom of an assistant. A minimal stage holds a finished exchange,
/// and the composer carries attachment chips, web search and extended thinking toggles, a model chip and a
/// send control. Sending appends the message and a simulated reply; nothing leaves the page.
/// </summary>
public sealed class AiComposerPage : ArcItemPage
{
    public override string ItemSlug => "ai-composer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The prompt box of an assistant, a coding agent or an in-product AI feature where people attach files and pick a model.",
        "A new chat screen that opens on suggestions and then shows the first exchange.",
        "Any place that needs a send control that becomes Stop while a reply is streaming."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI chat for a full conversation with history and message actions, or model picker to only choose a model."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter sends and Shift+Enter adds a line; Escape stops a running reply.",
        "The field is labelled Message and stays a real multiline text box, so screen readers, undo and IME input work.",
        "Toolbar toggles are buttons with a pressed state, and the send control changes its accessible name with its state."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The send arrow lifts out and a stop square scales in on the press spring while a reply streams.",
        "Replies arrive in word chunks; reduced motion keeps every state and drops the chunk fade."
    };

    public override string? ImplementationNotes =>
        "The demo opens on a finished exchange so the block is complete at rest. The / command list, @ mentions, "
        + "dictation and uploads are represented by an attachment chip: no file leaves the page, no microphone "
        + "opens and every reply is scripted. Stop and the progress ring are documented but not animated.";

    private sealed record Turn(string Text, bool Self, IReadOnlyList<string>? Files = null);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var turns = new List<Turn>
        {
            new("Summarise yesterday's checkout incident.", true),
            new("The incident started at **14:02**, when the retry queue backed up behind a slow payment call. [1]\n\n"
                + "- 312 failed payments, all recovered by 14:41\n"
                + "- The retry window was raised from 30 to 90 seconds [2]",
                false)
        };

        var webSearch = false;
        var thinking = true;
        var modelIndex = 0;

        var thread = new StackPanel { Spacing = 14 };
        var threadScroller = K.Scroller(thread, 172);
        var status = K.Caption("Reply finished. Simulated replies come from the page, not a model.");
        var counter = K.Caption("0 / 4000");
        var field = new ArcTextarea
        {
            Placeholder = "Ask anything, / for commands, @ to add context",
            Rows = 2,
            AutoGrow = false,
            MaxLength = 4000,
            Height = 64,
            Label = "Message",
            Description = "Enter sends, Shift Enter adds a line."
        };

        var attachRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var attachments = new List<string> { "trace-0418.json" };
        ArcButton send = null!;

        void UpdateSend()
        {
            var hasText = !string.IsNullOrWhiteSpace(field.Value);
            send.IsEnabled = hasText;
            send.Opacity = hasText ? 1 : 0.45;
            AutomationProperties.SetName(send, hasText ? "Send message" : "Send message, type to enable");
        }

        void RenderThread()
        {
            thread.Children.Clear();
            foreach (var turn in turns)
            {
                thread.Children.Add(turn.Self ? SelfBubble(turn) : AssistantTurn(turn));
            }
        }

        Control SelfBubble(Turn turn)
        {
            var body = new StackPanel { Spacing = 7 };
            body.Children.Add(K.Text(turn.Text, "arc-body-sm"));
            if (turn.Files is { Count: > 0 })
            {
                body.Children.Add(K.RowWrap(6, turn.Files.Select(file => K.Chip(file, icon: "file")).ToArray()));
            }

            return new Border
            {
                Background = K.Tint(ArcTokens.Accent, 0.1),
                BorderBrush = K.Tint(ArcTokens.Accent, 0.22),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(14, 9, 14, 9),
                MaxWidth = 540,
                Child = body,
                HorizontalAlignment = HorizontalAlignment.Right
            };
        }

        Control AssistantTurn(Turn turn)
        {
            var header = K.Row(8,
                K.Disc("sparkles", 22, true, 12),
                K.Text("Assistant", "arc-label"),
                K.Caption("Claude Opus 5.5"));

            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(K.Markdown(turn.Text, maxWidth: 700));

            return K.Stack(6, header, body);
        }

        var modelChip = new K.ModelChip(K.Models[0]);
        modelChip.Button.Click += (_, _) =>
        {
            modelIndex = (modelIndex + 1) % 4;
            var model = K.Models[modelIndex];
            modelChip.Set(model);
            status.Text = $"{model.Name} chosen for the next message.";
        };

        var searchChip = K.Chip("Web search", icon: "globe");
        var thinkChip = K.Chip("Thinking", icon: "sparkles", selected: true);

        send = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            IsEnabled = false,
            Content = new ArcIcon { Icon = "arrow-up", Size = 15, HorizontalAlignment = HorizontalAlignment.Center },
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(send, "Send message, type to enable");

        void RefreshAttachments()
        {
            attachRow.Children.Clear();
            attachRow.IsVisible = attachments.Count > 0;
            foreach (var file in attachments.ToList())
            {
                var name = file;
                var pill = new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9999),
                    Padding = new Thickness(8, 2, 2, 2),
                    Margin = new Thickness(0, 0, 6, 4),
                    Child = K.Row(4,
                        new ArcIcon { Icon = "file", Size = 12 },
                        K.Caption(name),
                        K.IconBtn("x", $"Remove {name}", () =>
                        {
                            attachments.Remove(name);
                            RefreshAttachments();
                            status.Text = $"{name} removed from the message.";
                        }, size: 11))
                };
                attachRow.Children.Add(pill);
            }
        }

        void Send()
        {
            var text = (field.Value ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            turns.Add(new Turn(text, true, attachments.Count > 0 ? attachments.ToArray() : null));
            turns.Add(new Turn(
                "Here is what the incident log shows. The retry queue backed up behind a slow payment call at **14:02**, "
                + "and raising the retry window from 30 to 90 seconds cleared the backlog by **14:41**. [1]\n\n"
                + "- 312 failed payments, all recovered\n- No duplicate charges left open", false));
            field.Value = string.Empty;
            attachments.Clear();
            RefreshAttachments();
            RenderThread();
            threadScroller.Offset = new Vector(0, threadScroller.Extent.Height);
            status.Text = "Sent with a simulated reply. Nothing left the page.";
            UpdateSend();
        }

        send.Click += (_, _) => Send();
        field.ValueChanged += (_, _) =>
        {
            counter.Text = $"{field.Value?.Length ?? 0} / 4000";
            UpdateSend();
        };
        field.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && !args.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                Send();
                args.Handled = true;
            }
        };

        searchChip.Click += (_, _) =>
        {
            webSearch = !webSearch;
            searchChip.Variant = webSearch ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary;
            status.Text = webSearch ? "Web search on. Replies will cite live sites." : "Web search off.";
        };
        thinkChip.Click += (_, _) =>
        {
            thinking = !thinking;
            thinkChip.Variant = thinking ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary;
            status.Text = thinking ? "Extended thinking on." : "Extended thinking off.";
        };

        RefreshAttachments();
        RenderThread();
        UpdateSend();

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        toolbar.Children.Add(K.Row(6,
            K.IconBtn("plus", "Add files or a screenshot"),
            searchChip,
            thinkChip));
        var rightTools = K.Row(6, modelChip.Button, K.IconBtn("mic", "Dictate"), send);
        Grid.SetColumn(rightTools, 2);
        toolbar.Children.Add(rightTools);

        var composer = new Border
        {
            Background = K.Brush(ArcTokens.Surface),
            BorderBrush = K.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12, 9, 9, 8),
            Child = K.Stack(7, attachRow, field, toolbar)
        };

        var starters = new StackPanel { Spacing = 5 };
        starters.Children.Add(K.Caption("Starters"));
        var starterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, prompt, icon) in new[]
                 {
                     ("Summarise this release", "Summarise this release and list the risky changes.", "list"),
                     ("Draft a migration note", "Draft a migration note for the retry window change.", "file"),
                     ("Find the failing check", "Find the failing check in last night's CI run.", "search")
                 })
        {
            var chip = K.Chip(label, icon: icon);
            chip.Click += (_, _) =>
            {
                field.Value = prompt;
                field.Focus();
                status.Text = "Starter inserted. Edit it, then press Enter to send.";
                UpdateSend();
            };
            chip.Margin = new Thickness(0, 0, 6, 4);
            starterRow.Children.Add(chip);
        }

        starters.Children.Add(starterRow);

        var statusRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        statusRow.Children.Add(status);
        Grid.SetColumn(counter, 1);
        statusRow.Children.Add(counter);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(K.Row(8,
            K.Disc("message-circle", 24, true, 13),
            K.Text("Assistant", "arc-label"),
            K.Pill("Simulated", ArcBadgeTone.Neutral)));
        var headerRight = K.Row(8, K.Caption("Checkout incident"), K.Chip("New chat", icon: "plus"));
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(headerRight, 1);
        header.Children.Add(headerRight);

        return K.Surface(new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20, 14, 20, 14),
            Children =
            {
                header,
                K.Divider(0),
                threadScroller,
                composer,
                statusRow,
                starters
            }
        });
    }
}
