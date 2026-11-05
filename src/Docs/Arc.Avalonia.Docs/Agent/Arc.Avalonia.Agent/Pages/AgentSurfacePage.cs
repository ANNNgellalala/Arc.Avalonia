using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Agent;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Agent.Pages;

/// <summary>
/// Gallery page for the Agent top layer. It renders the deterministic sample workspace (foreground run,
/// background run, pending approval, attention entry, diff artifact) and lets a visitor push a simulated
/// streaming turn through the fake transport. The page owns the demo timer; the Agent library has none.
/// </summary>
public sealed class AgentSurfacePage : ArcPage
{
    public override string Title => "Agent surface";

    public override string Group => "P0 · Agent";

    public override string? Description =>
        "The top-layer composition root: transcript, run controls, approval tray, background dock and attention inbox over one host-injected workspace.";

    public override int Order => 20100;

    public override Control Build(ArcDemoContext context)
    {
        var workspace = ArcAgentWorkspace.CreateSample();
        var transport = new DemoTransport();
        var surface = new ArcAgentSurface { Height = 900 };
        surface.Attach(workspace, transport);

        var log = DemoUi.Caption("Commands and host events are printed here. The Agent library performs no I/O.");

        var stream = new ArcButton { Content = "Stream a reply", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm };
        stream.Click += (_, _) =>
        {
            log.Text = "transport.StartTurnAsync · the fake host pushes 60 deltas through the coalescer.";
            _ = surface.StartTurnAsync("Summarise the migration plan and show me the diff.");
        };

        var approve = new ArcButton { Content = "Approve the sample request", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        approve.Click += (_, _) =>
        {
            var session = workspace.GetSession("session-fg");
            var sequence = NextSequence(session);
            workspace.Apply(new ArcApprovalResolvedEvent(
                ArcEventStamp.Create("run-fg", sequence, $"approval-resolved-{sequence}"),
                DateTimeOffset.UtcNow,
                "approval-1",
                ArcApprovalState.Approved));
            log.Text = "ArcApprovalResolvedEvent · the host applied the decision; the queue advanced.";
        };

        var background = new ArcButton { Content = "Start a background run", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm };
        background.Click += (_, _) =>
        {
            var runRef = new ArcAgentRunRef(
                $"run-bg-{Guid.NewGuid():N}"[..12],
                $"session-bg-{Guid.NewGuid():N}"[..12],
                ArcAgentRunKind.Background,
                Label: "Nightly eval sweep");
            workspace.Apply(new ArcRunStartedEvent(ArcEventStamp.Create(runRef.RunId, 1), DateTimeOffset.UtcNow, runRef));
            workspace.Apply(new ArcRunPhaseChangedEvent(ArcEventStamp.Create(runRef.RunId, 2), DateTimeOffset.UtcNow, ArcRunPhase.Running));
            log.Text = "ArcRunStartedEvent + RunPhaseChangedEvent · the background dock shows the new run.";
        };

        var stop = new ArcButton { Content = "Stop foreground", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm };
        stop.Click += (_, _) =>
        {
            var session = workspace.GetSession("session-fg");
            var sequence = NextSequence(session);
            workspace.Apply(new ArcRunPhaseChangedEvent(
                ArcEventStamp.Create("run-fg", sequence, $"stop-{sequence}"),
                DateTimeOffset.UtcNow,
                ArcRunPhase.Stopped));
            log.Text = "RunPhaseChanged(Stopped) · the host stopped the run; the transcript keeps the partial result.";
        };

        var buttons = DemoUi.Row(stream, approve, background, stop);
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(surface);
        content.Children.Add(buttons);
        content.Children.Add(log);

        return new StackPanel
        {
            Spacing = 20,
            Children =
            {
                DemoUi.Section(
                    "Agent surface",
                    "One host-injected workspace drives the conversation, run bar, approval tray, background dock and attention inbox. The sample renders without a host; the buttons exercise the fake transport.",
                    content)
            }
        };
    }

    private static long NextSequence(IArcAgentSession? session) =>
        (session?.Timeline.LastOrDefault()?.Stamp.Sequence ?? 0) + 1;

    private sealed class DemoTransport : IArcAgentTransport
    {
        private int _turns;

        public Task<string> StartTurnAsync(ArcAgentTurnRequest request, IArcAgentEventSink sink, CancellationToken cancellationToken = default)
        {
            var turn = ++_turns;
            var runId = $"run-stream-{turn}";
            var sessionId = $"session-stream-{turn}";
            var runRef = new ArcAgentRunRef(runId, sessionId, ArcAgentRunKind.Foreground, Label: "Streaming demo");
            var sequence = 0L;
            void Push(ArcAgentEvent @event) => sink.Push(@event);

            Push(new ArcRunStartedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, runRef));
            Push(new ArcRunPhaseChangedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, ArcRunPhase.Running));
            Push(new ArcMessageStartedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"m-user-{turn}", ArcAgentRole.User));
            Push(new ArcMessageDeltaEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"m-user-{turn}", request.Text));
            Push(new ArcMessageEndedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"m-user-{turn}"));
            Push(new ArcMessageStartedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"m-assistant-{turn}", ArcAgentRole.Assistant));

            var chunk = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) =>
            {
                chunk++;
                Push(new ArcMessageDeltaEvent(
                    ArcEventStamp.Create(runId, ++sequence),
                    DateTimeOffset.UtcNow,
                    $"m-assistant-{turn}",
                    chunk == 60 ? "Done. The plan is ready for your review." : $"chunk {chunk:00} "));
                if (chunk < 60)
                {
                    return;
                }

                timer.Stop();
                Push(new ArcMessageEndedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"m-assistant-{turn}"));
                Push(new ArcToolCallStartedEvent(
                    ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"call-{turn}", "repo.diff", "Show the migration diff.", ArcRiskLevel.Low));
                Push(new ArcToolArgsDeltaEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"call-{turn}", "{\"files\":2}"));
                Push(new ArcToolCallEndedEvent(
                    ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, $"call-{turn}", ArcToolCallStatus.Succeeded, "2 files · +48 / -12"));
                Push(new ArcArtifactPublishedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, new ArcArtifact
                {
                    ArtifactId = $"artifact-{turn}",
                    Kind = ArcArtifactKind.Diff,
                    Title = "src/Agent/Arc.Avalonia.Agent",
                    Subtitle = "+48 / -12",
                    DisplayText = "- old surface\n+ new surface",
                    At = DateTimeOffset.UtcNow
                }));
                Push(new ArcRunPhaseChangedEvent(ArcEventStamp.Create(runId, ++sequence), DateTimeOffset.UtcNow, ArcRunPhase.Done));
            };
            timer.Start();
            return Task.FromResult(runId);
        }

        public Task SendCommandAsync(ArcAgentCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

