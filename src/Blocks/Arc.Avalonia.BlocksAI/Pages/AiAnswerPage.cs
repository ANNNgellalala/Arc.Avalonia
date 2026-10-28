using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.BlocksAI.AiKit;

namespace Arc.Avalonia.BlocksAI.Pages;

/// <summary>
/// AI answer: one question set like an article. Reasoning folds open, sources sit as small cards, citations
/// light the sentence they support, and feedback, versions and follow-ups sit under the answer.
/// </summary>
public sealed class AiAnswerPage : ArcItemPage
{
    public override string ItemSlug => "ai-answer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An answer page where one question gets one well formatted, cited answer that people read and share.",
        "Docs and help center assistants that must show where each claim comes from.",
        "Internal knowledge search where the answer mixes prose, steps and a code sample."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI chat for a back and forth conversation, or research run for multi step research."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The block is a labelled region named by the question and the answer is busy while it streams.",
        "The reasoning and sources rows are buttons with an expanded state; closed panels are inert.",
        "Citations are links named by source number and title, and a lit sentence is a visual aid only."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Thinking shimmers for about a second, then settles to Thought for 9s as the first words arrive.",
        "Answers stream in word chunks; reduced motion keeps the chunked arrival but drops every fade."
    };

    public override string? ImplementationNotes =>
        "The answer, sources and usage are sample data. A citation lights its paragraph and source card instead "
        + "of opening a hover preview, and model marks are monogram discs.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Answer ready in 18.4s with 3 sources. Simulated answer.");
        var rating = 0;
        var version = 2;
        var versionLabel = K.Caption($"{version} / 2");

        var paragraphs = new List<Border>();
        var sourceCards = new List<Border>();

        void Select(int number)
        {
            for (var i = 0; i < paragraphs.Count; i++)
            {
                paragraphs[i].Background = i == number - 1 ? K.Tint(ArcTokens.Accent, 0.09) : Brushes.Transparent;
            }

            for (var i = 0; i < sourceCards.Count; i++)
            {
                sourceCards[i].BorderBrush = i == number - 1 ? K.Brush(ArcTokens.Accent) : K.Brush(ArcTokens.BorderSubtle);
                sourceCards[i].BorderThickness = new Thickness(i == number - 1 ? 2 : 1);
            }

            status.Text = $"Source {number} is highlighted with the sentence it supports.";
        }

        Control Sentence(string text, int number)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(-8, 0, 0, 0),
                Child = K.Paragraph(text, Select, maxWidth: 700)
            };
            paragraphs.Add(border);
            return border;
        }

        Control Source(int number, string title, string domain, string initial)
        {
            var card = K.SourceCard(number, title, domain, initial);
            if (card is Border border)
            {
                sourceCards.Add(border);
                border.Focusable = true;
                border.Cursor = new Cursor(StandardCursorType.Hand);
                AutomationProperties.SetName(border, $"Source {number}: {title}, {domain}");
                border.PointerPressed += (_, _) => Select(number);
                border.KeyDown += (_, args) =>
                {
                    if (args.Key is Key.Enter or Key.Space)
                    {
                        Select(number);
                        args.Handled = true;
                    }
                };
            }

            return card;
        }

        var reasoningBody = new Border
        {
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            Child = K.Text("Passkeys remove shared secrets, so the rollout risk is recovery: anyone who loses a "
                + "device needs a fallback that is not a password reset loop. I compared three published rollout "
                + "patterns and kept the staged one.", "arc-caption", 680)
        };

        var reasoningButton = K.Btn("Thought for 9s", ArcButtonVariant.Ghost, ArcButtonSize.Sm,
            trailingIcon: "chevron-down", icon: "sparkles");
        AutomationProperties.SetName(reasoningButton, "Thought for 9s, show reasoning");
        reasoningButton.Click += (_, _) =>
        {
            reasoningBody.IsVisible = !reasoningBody.IsVisible;
            status.Text = reasoningBody.IsVisible ? "Reasoning shown." : "Reasoning folded.";
        };

        var up = K.IconBtn("thumbs-up", "Good answer");
        var down = K.IconBtn("minus-circle", "Bad answer");
        up.Click += (_, _) =>
        {
            rating = rating == 1 ? 0 : 1;
            up.Variant = rating == 1 ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost;
            down.Variant = ArcButtonVariant.Ghost;
            status.Text = rating == 1 ? "Marked as a good answer. Simulated feedback." : "Feedback cleared.";
        };
        down.Click += (_, _) =>
        {
            rating = rating == -1 ? 0 : -1;
            down.Variant = rating == -1 ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost;
            up.Variant = ArcButtonVariant.Ghost;
            status.Text = rating == -1 ? "Marked as a bad answer. Simulated feedback." : "Feedback cleared.";
        };

        var copy = K.IconBtn("copy", "Copy answer");
        copy.Click += (_, _) => status.Text = "Answer copied. Clipboard content is simulated.";

        var regenerate = K.Chip("Try again", icon: "refresh-cw");
        regenerate.Click += (_, _) =>
        {
            version = version == 2 ? 1 : 2;
            versionLabel.Text = $"{version} / 2";
            status.Text = version == 1
                ? "More concise version shown. The earlier version is kept."
                : "Full version shown again. Simulated answer.";
        };

        var share = K.IconBtn("share", "Share answer");
        share.Click += (_, _) => status.Text = "Link copied. Sharing is simulated.";

        var answerHead = K.Row(8,
            K.Mark("anthropic", 18),
            K.Text("Claude Opus 5.5", "arc-caption"),
            K.Dot(ArcTokens.BorderStrong, 3),
            K.Caption("1,240 tokens"),
            K.Dot(ArcTokens.BorderStrong, 3),
            K.Caption("18.4s"));

        var sources = new WrapPanel { Orientation = Orientation.Horizontal };
        sources.Children.Add(Source(1, "Passkey autofill", "web.dev", "W"));
        sources.Children.Add(Source(2, "Recovery patterns", "docs.acme.dev", "D"));
        sources.Children.Add(Source(3, "Rollout checklist", "nist.gov", "N"));

        var related = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var question in new[]
                 {
                     "What should we measure before each rollout stage?",
                     "Which fallback works for shared devices?"
                 })
        {
            var chip = K.Chip(question, icon: "arrow-up-right");
            chip.Click += (_, _) => status.Text = "Related question asked. A new answer would replace this one.";
            chip.Margin = new Thickness(0, 0, 6, 4);
            related.Children.Add(chip);
        }

        var composer = new ArcInput { Placeholder = "Ask a follow-up", Height = 42 };
        AutomationProperties.SetName(composer, "Follow-up question");
        var send = K.IconBtn("arrow-up", "Send question", variant: ArcButtonVariant.Primary);
        send.Width = 34;
        send.Height = 34;
        send.Padding = new Thickness(0);
        send.Click += (_, _) =>
        {
            status.Text = string.IsNullOrWhiteSpace(composer.Value)
                ? "Type a question first."
                : "Answer ready. Simulated follow-up.";
        };

        var composerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        composerRow.Children.Add(composer);
        Grid.SetColumn(send, 1);
        send.Margin = new Thickness(6, 0, 0, 0);
        composerRow.Children.Add(send);

        var topBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        topBar.Children.Add(K.Row(6,
            K.Text("Passkey rollout", "arc-label"),
            K.Caption("Today · 1 question")));
        var topRight = K.Row(4, K.Chip("New thread", icon: "plus"), share);
        topRight.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(topRight, 1);
        topBar.Children.Add(topRight);

        var actions = K.Row(6,
            copy,
            regenerate,
            up,
            down,
            K.Dot(ArcTokens.BorderStrong, 3),
            versionLabel,
            K.IconBtn("chevron-left", "Previous version", size: 12),
            K.IconBtn("chevron-right", "Next version", size: 12),
            K.Dot(ArcTokens.BorderStrong, 3),
            K.Mark("anthropic", 15),
            K.Caption("1,240 tokens"),
            K.Caption("18.4s"));

        var content = new StackPanel
        {
            Spacing = 6,
            MaxWidth = 760,
            Children =
            {
                topBar,
                K.Divider(2),
                K.Text("How do we roll out passkeys without locking anyone out?", "arc-heading", 700),
                K.Row(10, reasoningButton, K.Caption("3 sources read")),
                reasoningBody,
                sources,
                K.Divider(2),
                Sentence("Roll passkeys out in stages, and keep one password fallback until the recovery path is "
                    + "proven. The published patterns agree on enrolling a small group first and watching recovery "
                    + "attempts rather than enrolment numbers. [1]", 1),
                Sentence("Measure recovery, not just sign-in: a failed fallback locks people out for good. [2]", 2),
                K.Markdown("1. Enable passkeys for staff accounts only\n"
                    + "2. Watch recovery attempts for two weeks", null, 700),
                actions,
                K.Caption("Related questions"),
                related,
                composerRow,
                status
            }
        };
        var centering = new Border { Child = content, HorizontalAlignment = HorizontalAlignment.Stretch, MaxWidth = 820 };
        return K.Surface(centering, new Thickness(22, 12, 22, 12));
    }
}
