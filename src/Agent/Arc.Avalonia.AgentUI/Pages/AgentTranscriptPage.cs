using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.AgentUI.Pages;

/// <summary>
/// Gallery page for the agent transcript. One instance switches between a streaming run, a run waiting on
/// approval and a stopped run, and the line under it reports the retry intent raised by failed rows.
/// </summary>
public sealed class AgentTranscriptPage : ArcPage
{
    public override string Title => "Agent transcript";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "A read-only, event-typed record of an agent run: messages, thinking, tool calls, plans, approvals, questions, results and errors in one stream.";

    public override int Order => 20001;

    public override Control Build(ArcDemoContext context)
    {
        var transcript = new ArcAgentTranscript { Height = 460 };
        var log = DemoUi.Caption("Retry intents raised by failed rows appear here.");
        transcript.RetryRequested += (_, evt) =>
            log.Text = $"RetryRequested · {evt.Title} · the host would re-run this event.";

        void Apply(string state)
        {
            var events = state switch
            {
                "approval" => AwaitingApproval(),
                "stopped" => Stopped(),
                _ => Running()
            };

            transcript.Events = events;
            transcript.IsStreaming = state == "running";
            transcript.Status = state switch
            {
                "approval" => ArcAgentTranscriptStatus.AwaitingInput,
                "stopped" => ArcAgentTranscriptStatus.Stopped,
                _ => ArcAgentTranscriptStatus.Running
            };
            log.Text = state switch
            {
                "approval" => "Status · AwaitingInput · the run is blocked on a decision.",
                "stopped" => "Status · Stopped · the host stopped the run after the current tool call.",
                _ => "Status · Running · IsStreaming draws the live marker while events arrive."
            };
        }

        var buttons = DemoUi.Row(
            StateButton("Streaming run", "running", Apply),
            StateButton("Waiting on approval", "approval", Apply),
            StateButton("Stopped run", "stopped", Apply));

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                transcript,
                log
            }
        };

        Apply("running");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Agent transcript",
                    "Events arrive from the host in run order. Click a row to expand its detail; a failed event offers Retry, which raises an intent instead of re-running anything inside the control.",
                    content)
            }
        };
    }

    private static Control StateButton(string label, string state, Action<string> apply)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        ArcUi.Name(button, $"{label} transcript state");
        button.Click += (_, _) => apply(state);
        return button;
    }

    private static IReadOnlyList<ArcAgentEvent> Running()
    {
        var now = DateTimeOffset.Now;
        return new List<ArcAgentEvent>
        {
            new()
            {
                Id = "r1",
                Kind = ArcAgentEventKind.Message,
                Title = "Add rate limiting to the public API and show me the diff.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-4)
            },
            new()
            {
                Id = "r2",
                Kind = ArcAgentEventKind.Thinking,
                Title = "Reading the middleware chain before touching the limiter.",
                Detail = "The logger wraps every route, so the limiter can sit beside it without changing the router.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-3.7)
            },
            new()
            {
                Id = "r3",
                Kind = ArcAgentEventKind.Tool,
                Title = "Read src/middleware/limits.ts",
                Detail = "42 lines. Exports createLimiter(options) with a fixed window counter.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-3.2)
            },
            new()
            {
                Id = "r4",
                Kind = ArcAgentEventKind.Plan,
                Title = "5 steps: sliding window limiter, Redis counters, 429 response, tests, docs",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-2.8)
            },
            new()
            {
                Id = "r5",
                Kind = ArcAgentEventKind.Tool,
                Title = "Run the request test suite",
                Detail = "Connection refused: Redis is not running on localhost:6379.",
                Status = ArcAgentEventStatus.Failed,
                Timestamp = now.AddMinutes(-1.6)
            },
            new()
            {
                Id = "r6",
                Kind = ArcAgentEventKind.Tool,
                Title = "Edit src/middleware/limits.ts",
                Detail = "Replaced the fixed window counter with a sliding window over a 60 second TTL.",
                Status = ArcAgentEventStatus.Running,
                Timestamp = now.AddMinutes(-0.4)
            }
        };
    }

    private static IReadOnlyList<ArcAgentEvent> AwaitingApproval()
    {
        var events = new List<ArcAgentEvent>(Running());
        var now = DateTimeOffset.Now;
        events.Add(new ArcAgentEvent
        {
            Id = "a1",
            Kind = ArcAgentEventKind.Tool,
            Title = "Edit api/reference/limits.md",
            Detail = "Will add a Rate limits section and one example 429 response.",
            Status = ArcAgentEventStatus.Pending,
            Timestamp = now
        });
        events.Add(new ArcAgentEvent
        {
            Id = "a2",
            Kind = ArcAgentEventKind.Approval,
            Title = "Approve writing the API reference update?",
            Detail = "Risk: low. The change touches documentation only and can be reverted from the run diff.",
            Status = ArcAgentEventStatus.Pending,
            Timestamp = now.AddSeconds(2)
        });
        events.Add(new ArcAgentEvent
        {
            Id = "a3",
            Kind = ArcAgentEventKind.Ask,
            Title = "Which Redis host should the tests use?",
            Detail = "Options: localhost:6379, the docker compose service, or skip the integration tests.",
            Status = ArcAgentEventStatus.Pending,
            Timestamp = now.AddSeconds(3)
        });
        return events;
    }

    private static IReadOnlyList<ArcAgentEvent> Stopped()
    {
        var events = new List<ArcAgentEvent>(Running());
        events.RemoveAll(evt => evt.Status is ArcAgentEventStatus.Running or ArcAgentEventStatus.Failed);
        var now = DateTimeOffset.Now;
        events.Add(new ArcAgentEvent
        {
            Id = "s1",
            Kind = ArcAgentEventKind.Tool,
            Title = "Edit src/middleware/limits.ts",
            Detail = "Stopped by the user after the limiter write; the partial diff is kept for review.",
            Status = ArcAgentEventStatus.Skipped,
            Timestamp = now.AddMinutes(-1)
        });
        events.Add(new ArcAgentEvent
        {
            Id = "s2",
            Kind = ArcAgentEventKind.Message,
            Title = "Stopped before the tests ran. The diff is ready to review.",
            Status = ArcAgentEventStatus.Done,
            Timestamp = now
        });
        return events;
    }
}
