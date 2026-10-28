using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.BlocksAI.AiKit;

namespace Arc.Avalonia.BlocksAI.Pages;

/// <summary>
/// Model arena: a blind side by side comparison. One prompt, two answers, one vote, then the models are
/// revealed. The answers are sample text and the vote only lives on the page.
/// </summary>
public sealed class ModelArenaPage : ArcItemPage
{
    public override string ItemSlug => "model-arena";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Blind preference tests between two models on the same prompt, such as an internal bake-off.",
        "Prompt or model evaluation tools where people should judge answers without knowing the author.",
        "Showing latency, speed and cost differences live, side by side, on one real prompt."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use model picker to choose one model for a chat, or eval suite to score many prompts automatically."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "In blind mode no model name or mark reaches the accessibility tree until the vote is recorded.",
        "Each pane is a labelled region and its answer is an announced, focusable scroll region.",
        "Status always pairs an icon with a word, such as Done or Finished first, never colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Both answers stream at the same pace; voting crossfades the model name into the header where the letter stood.",
        "Reduced motion swaps labels in place, keeps every state and still reveals both models after the vote."
    };

    public override string? ImplementationNotes =>
        "Answers, timing and cost are sample data. Voting reveals the two model names and records the choice in a "
        + "status line; the leaderboard popover and the two-tab narrow layout are not shown because the page is "
        + "wide. Brand marks are monogram discs, as the port has no brand glyphs.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var voted = false;
        var status = K.Caption("Both answers are in. Vote to reveal which model wrote each. Simulated answers.");

        var headerA = K.Text("Model A", "arc-label");
        var headerB = K.Text("Model B", "arc-label");
        Border paneA = null!;
        Border paneB = null!;
        var result = new Border { IsVisible = false };
        var resultText = K.Text(string.Empty, "arc-body-sm", 620);
        var nextRound = K.Chip("Next round", icon: "refresh-cw");
        var voteRow = new WrapPanel { Orientation = Orientation.Horizontal };

        void Reveal(string choice)
        {
            voted = true;
            headerA.Text = "Model A · Claude Opus 5.5";
            headerB.Text = "Model B · GPT-6.1 Sol";
            AutomationProperties.SetName(paneA, "Model A, Claude Opus 5.5");
            AutomationProperties.SetName(paneB, "Model B, GPT-6.1 Sol");
            var verdict = choice switch
            {
                "a" => "You chose A: Claude Opus 5.5 over GPT-6.1 Sol.",
                "b" => "You chose B: GPT-6.1 Sol over Claude Opus 5.5.",
                "tie" => "You called it a tie between Claude Opus 5.5 and GPT-6.1 Sol.",
                _ => "You rated both answers bad. Neither model won this round."
            };
            resultText.Text = verdict;
            result.IsVisible = true;
            status.Text = verdict + " The vote is recorded on the page.";
        }

        ArcButton Vote(string label, string choice)
        {
            var button = K.Btn(label, ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Reveal(choice));
            button.Margin = new Thickness(0, 0, 6, 4);
            return button;
        }

        voteRow.Children.Add(Vote("A is better", "a"));
        voteRow.Children.Add(Vote("Tie", "tie"));
        voteRow.Children.Add(Vote("B is better", "b"));
        voteRow.Children.Add(Vote("Both bad", "bad"));

        Control Pane(string label, string answer, string metrics, string tone)
        {
            var answerBlock = new StackPanel { Spacing = 7 };
            answerBlock.Children.Add(K.Text(answer, "arc-body-sm", 420));
            answerBlock.Children.Add(K.Row(6,
                K.Caption(metrics.Split('|')[0]),
                K.Dot(ArcTokens.BorderStrong, 3),
                K.Caption(metrics.Split('|')[1]),
                K.Dot(ArcTokens.BorderStrong, 3),
                K.Caption(metrics.Split('|')[2])));

            var pane = new Border
            {
                Background = K.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(13, 11, 13, 11),
                Child = K.Stack(9,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        Children =
                        {
                            K.Dot(tone, 7),
                            label == "Model A" ? headerA : headerB,
                            K.Row(5,
                                new ArcIcon { Icon = "circle-check", Size = 12, Foreground = K.Brush(ArcTokens.Success) },
                                K.Caption(label == "Model A" ? "Done, finished first" : "Done"))
                        }
                    },
                    answerBlock)
            };

            if (pane.Child is StackPanel stack && stack.Children[0] is Grid head)
            {
                head.Children[1].Margin = new Thickness(6, 0, 0, 0);
                Grid.SetColumn(head.Children[1], 1);
                Grid.SetColumn(head.Children[2], 2);
            }

            return pane;
        }

        var paneGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        paneA = (Border)Pane("Model A", "Two things changed in the same deploy: the pool size went from 40 "
            + "to 12 connections, and the new tracing middleware buffers each response before flushing it.\n\n"
            + "The pool shrink explains the queueing. The buffer explains why the first byte arrives late even "
            + "when the service is idle.", "0.6s first token|42 tok/s|$0.004", ArcTokens.Series1);
        paneGrid.Children.Add(paneA);
        paneB = (Border)Pane("Model B", "Latency doubled because the deploy reduced the connection pool and added "
            + "response buffering in the tracing layer.\n\n"
            + "Restore the pool size first, then re-measure. The buffer is a constant cost, but the pool is what "
            + "turns a small spike into a queue.", "0.9s first token|31 tok/s|$0.003", ArcTokens.Series2);
        Grid.SetColumn(paneB, 2);
        paneGrid.Children.Add(paneB);

        var blindSwitch = K.Toggle("Blind", true, on =>
        {
            if (on)
            {
                headerA.Text = "Model A";
                headerB.Text = "Model B";
                AutomationProperties.SetName(paneA, "Model A");
                AutomationProperties.SetName(paneB, "Model B");
                status.Text = "Models hidden again. Vote to reveal.";
            }
            else if (voted)
            {
                headerA.Text = "Model A · Claude Opus 5.5";
                headerB.Text = "Model B · GPT-6.1 Sol";
                status.Text = "Models revealed by the Blind switch.";
            }
            else
            {
                headerA.Text = "Model A · hidden";
                headerB.Text = "Model B · hidden";
                status.Text = "Blind is off. Names appear after the vote, or stay hidden with Blind on.";
            }
        });

        var prompt = new ArcInput
        {
            Value = "Why did API latency double after Tuesday's deploy?",
            Height = 42,
            Label = null
        };
        AutomationProperties.SetName(prompt, "Prompt");
        var run = K.Btn("Run", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {

            voted = false;
            result.IsVisible = false;
            headerA.Text = "Model A";
            headerB.Text = "Model B";
            status.Text = "Run started on both models. Answers stream at the same pace. Simulated.";
        }, icon: "play");

        var samples = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var sample in new[]
                 {
                     "Why did latency double?", "Explain this stack trace",
                     "Write a migration note"
                 })
        {
            var chip = K.Chip(sample);
            chip.Click += (_, _) => prompt.Value = sample + " in the payments service.";
            chip.Margin = new Thickness(0, 4, 6, 0);
            samples.Children.Add(chip);
        }

        var runRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        runRow.Children.Add(prompt);
        Grid.SetColumn(run, 1);
        run.Margin = new Thickness(6, 0, 0, 0);
        runRow.Children.Add(run);

        result.Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { resultText, nextRound }
        };
        if (result.Child is Grid resultGrid)
        {
            Grid.SetColumn(resultGrid.Children[1], 1);
        }

        nextRound.Click += (_, _) =>
        {
            result.IsVisible = false;

            voted = false;
            headerA.Text = "Model A";
            headerB.Text = "Model B";
            status.Text = "New round started with the next sample prompt. Simulated.";
        };

        return K.Surface(new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(20, 14, 20, 14),
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Row(8,
                            K.Text("Blind comparison", "arc-label"),
                            K.Caption("1 prompt · 2 answers · 1 vote"),
                            K.Pill("Simulated", ArcBadgeTone.Neutral)),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children =
                            {
                                blindSwitch,
                                K.Chip("Leaderboard", icon: "trophy")
                            }
                        }
                    }
                },
                K.Divider(0),
                runRow,
                samples,
                paneGrid,
                K.Row(6, K.Caption("Vote"), new Border { Child = voteRow }),
                result,
                status
            }
        });
    }
}
