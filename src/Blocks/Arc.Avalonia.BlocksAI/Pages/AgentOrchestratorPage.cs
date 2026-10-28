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
using K = Arc.Avalonia.BlocksAI.AiKit;

namespace Arc.Avalonia.BlocksAI.Pages;

/// <summary>
/// Agent orchestrator: a lead agent fans a goal out to parallel worker lanes and merges what comes back. The
/// concurrency control, lane details and totals are page state; the run itself is simulated.
/// </summary>
public sealed class AgentOrchestratorPage : ArcItemPage
{
    public override string ItemSlug => "agent-orchestrator";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A multi agent run where a lead model splits a task across subagents, such as research or audits.",
        "Debug panels that need per worker progress, tool calls, errors, tokens and cost in one view.",
        "Explaining the cost and speed tradeoff of parallel agents with a live concurrency control."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use agent run for one agent working through steps, or agent tasks for independent background jobs."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The goal is the card heading and the plan, the lanes and the summary are labelled sections.",
        "Each lane is a button with an expanded state named by its subtask, model and status.",
        "Every lane status pairs a glyph with a word: waiting, queued, running, returned or failed."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A dispatch sends a short coloured packet down the connector before the lane starts, so cause precedes effect.",
        "Reduced motion removes the pulses and the shimmer, keeps every state and never starts a run on its own."
    };

    public override string? ImplementationNotes =>
        "The run is a snapshot: lanes already returned, one earlier retry recorded, and the totals computed from "
        + "the sample numbers. Connectors between the lead and the lanes are not drawn, because measuring them "
        + "from layout is out of scope for a static block. Concurrency changes labels and queue order only.";

    private sealed record Lane(string Model, string Subtask, string Tool, string Output, string Tokens, string Time,
        string Status, string Transcript);

    private static readonly Lane[] Lanes =
    {
        new("Opus 5.5", "Compare pricing pages", "web search", "Four competitors list annual prices openly.", "12.4k", "8.2s",
            "Returned", "Read four pages, two hid annual pricing behind a toggle."),
        new("Sonnet 5.5", "Extract plan limits", "fetch page", "Seat caps: 3, 10, 25 and unlimited.", "9.8k", "7.4s",
            "Returned", "Parsed the limits table and normalised seats per plan."),
        new("Haiku 5.5", "Check enterprise terms", "read file", "Two vendors require a quote.", "6.1k", "3.1s",
            "Returned", "Scanned the terms PDF and flagged which pages quote on request."),
        new("Gemini 4", "Summarise differences", "write findings", "Ours sits mid market on price.", "14.2k", "9.6s",
            "Running", "Drafting the closing comparison against our current tiers.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var concurrency = 3;
        var status = K.Caption("Four subtasks planned, three returned. The run is simulated.");
        var lanesHost = new Panel();
        var expanded = new HashSet<string>();

        void RenderLanes()
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*,8,*,8,*") };
            for (var index = 0; index < Lanes.Length; index++)
            {
                var lane = Lanes[index];
                var queued = index >= concurrency;
                var state = queued ? "Queued" : lane.Status;
                var token = state switch
                {
                    "Returned" => ArcTokens.Success,
                    "Running" => ArcTokens.Accent,
                    "Queued" => ArcTokens.TextMuted,
                    _ => ArcTokens.Danger
                };

                var body = new StackPanel { Spacing = 5 };
                body.Children.Add(K.Row(5,
                    K.Mark(lane.Model.StartsWith("Gemini") ? "google" : "anthropic", 16),
                    K.Caption(lane.Model)));
                body.Children.Add(K.Text(lane.Subtask, "arc-caption", 170));
                body.Children.Add(K.Row(4,
                    new ArcIcon { Icon = "wand-2", Size = 10, Foreground = K.Brush(ArcTokens.TextMuted) },
                    K.Caption(lane.Tool)));
                body.Children.Add(K.Mono(lane.Output, 10.5));
                body.Children.Add(K.Row(4,
                    new ArcIcon { Icon = state == "Returned" ? "circle-check" : state == "Queued" ? "clock" : "loader-circle",
                        Size = 11, Foreground = K.Brush(token) },
                    K.Caption(state)));
                body.Children.Add(K.Caption($"{lane.Tokens} tokens · {lane.Time}"));

                if (expanded.Contains(lane.Subtask))
                {
                    body.Children.Add(K.Divider(3));
                    body.Children.Add(K.Text(lane.Transcript, "arc-caption", 170));
                }

                var card = new Border
                {
                    Background = queued ? K.Brush(ArcTokens.SurfaceMuted) : K.Brush(ArcTokens.Surface),
                    BorderBrush = state == "Running" ? K.Tint(ArcTokens.Accent, 0.4) : K.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 8, 10, 8),
                    Opacity = queued ? 0.6 : 1,
                    Child = body
                };
                card.Focusable = true;
                card.Cursor = new Cursor(StandardCursorType.Hand);
                AutomationProperties.SetName(card, $"{lane.Subtask}, {lane.Model}, {state}");
                var subtask = lane.Subtask;
                card.PointerPressed += (_, _) =>
                {
                    if (!expanded.Add(subtask))
                    {
                        expanded.Remove(subtask);
                    }

                    RenderLanes();
                    status.Text = expanded.Contains(subtask) ? "Transcript shown." : "Transcript folded.";
                };

                Grid.SetColumn(card, index * 2);
                grid.Children.Add(card);
            }

            lanesHost.Children.Clear();
            lanesHost.Children.Add(grid);
        }

        RenderLanes();

        var concurrencyControl = K.Segmented(
            new[] { ("1", "1"), ("2", "2"), ("3", "3"), ("4", "4") },
            "3",
            value =>
            {
                concurrency = int.TryParse(value, out var parsed) ? parsed : 3;
                RenderLanes();
                status.Text = $"Concurrency set to {concurrency}. Lanes beyond the limit wait in the queue.";
            },
            "Concurrency");

        var run = K.Btn("Run again", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            status.Text = "Run again started with four subtasks. The run is simulated.";
        }, icon: "play");

        var totals = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 18 };
        var totalCells = new Control[]
        {
            K.Stack(1, K.Caption("Wall time"), K.Text("18.4s", "arc-body-sm")),
            K.Stack(1, K.Caption("Sequential"), K.Text("41.2s", "arc-body-sm")),
            K.Stack(1, K.Caption("Speedup"), K.Text("2.2×", "arc-body-sm")),
            K.Stack(1, K.Caption("Cost"), K.Text("$0.21", "arc-body-sm"))
        };
        for (var index = 0; index < totalCells.Length; index++)
        {
            Grid.SetColumn(totalCells[index], index);
            totals.Children.Add(totalCells[index]);
        }

        var content = new StackPanel
        {
            Spacing = 10,
            MaxWidth = 900,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Compare our pricing page with four competitors", "arc-heading", 560),
                            K.Caption("Lead: Claude Opus 5.5 · 4 worker lanes · 48.2k tokens · $0.21")),
                        K.Row(8, concurrencyControl, run)
                    }
                },
                K.Divider(2),
                K.Row(7,
                    new ArcIcon { Icon = "target", Size = 13, Foreground = K.Brush(ArcTokens.Accent) },
                    K.Caption("Plan"),
                    K.Caption("4 subtasks · one retry on Check enterprise terms")),
                lanesHost,
                new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 8, 12, 8),
                    Child = totals
                },
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 980
        }, new Thickness(22, 15, 22, 15));
    }
}
