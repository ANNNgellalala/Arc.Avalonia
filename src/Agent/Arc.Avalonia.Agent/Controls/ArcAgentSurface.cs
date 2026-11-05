using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using UiAttentionItem = Arc.Avalonia.AgentUI.Controls.ArcAttentionItem;
using UiAttentionKind = Arc.Avalonia.AgentUI.Controls.ArcAttentionKind;
using UiAttentionRisk = Arc.Avalonia.AgentUI.Controls.ArcAttentionRisk;
using UiAttentionStatus = Arc.Avalonia.AgentUI.Controls.ArcAttentionItemStatus;
using UiDockStatus = Arc.Avalonia.AgentUI.Controls.ArcDockStatus;
using UiEvent = Arc.Avalonia.AgentUI.Controls.ArcAgentEvent;
using UiEventKind = Arc.Avalonia.AgentUI.Controls.ArcAgentEventKind;
using UiEventStatus = Arc.Avalonia.AgentUI.Controls.ArcAgentEventStatus;
using UiRunBarStatus = Arc.Avalonia.AgentUI.Controls.ArcRunBarStatus;
using UiTranscriptStatus = Arc.Avalonia.AgentUI.Controls.ArcAgentTranscriptStatus;

namespace Arc.Avalonia.Agent;

/// <summary>
/// The Agent top-layer composition root. It wires the workspace projection to the existing leaf controls
/// (transcript, run bar, approval sheet, attention inbox, background dock) plus the new composer and approval
/// tray, and turns every intent into an <see cref="ArcAgentCommand"/> for the host. It creates no events and
/// performs no I/O: a host pushes events through <see cref="ArcDeltaCoalescer"/> via the supplied sink.
///
/// With no transport attached it renders the deterministic <see cref="ArcAgentWorkspace.CreateSample"/>
/// projection, which keeps Gallery screenshots and design review host-free.
/// </summary>
public class ArcAgentSurface : Panel, IArcAgentCommandSink
{
    private readonly ArcRunControlBar _runBar = new();
    private readonly ArcAgentTranscript _transcript = new() { Height = 340 };
    private readonly ArcAgentApprovalTray _approvalTray = new();
    private readonly ArcAgentComposer _composer = new();
    private readonly ArcAgentAttentionInbox _attentionInbox = new() { Height = 220 };
    private readonly ArcBackgroundRunDock _dock = new();
    private readonly ArcDeltaCoalescer _coalescer;
    private readonly IArcAgentEventSink _sink;
    private IArcAgentWorkspace? _workspace;
    private IArcAgentTransport? _transport;
    private IArcAgentSession? _synced;
    private int _cursor;
    private bool _showAttentionInbox = true;
    private bool _showBackgroundDock = true;
    private string? _lastRunId;

    /// <summary>Creates the surface with the sample workspace so it renders without a host.</summary>
    public ArcAgentSurface()
    {
        _coalescer = new ArcDeltaCoalescer();
        _coalescer.Flush += OnFlush;
        _sink = new SurfaceEventSink(this);

        _runBar.StopRequested += (_, _) => SendForActive(new ArcStopCommand(ActiveRunId()));
        _runBar.StopAfterStepRequested += (_, _) => SendForActive(new ArcStopCommand(ActiveRunId(), AfterCurrentStep: true));
        _runBar.PauseRequested += (_, _) => SendForActive(new ArcPauseCommand(ActiveRunId()));
        _runBar.ResumeRequested += (_, _) => SendForActive(new ArcResumeCommand(ActiveRunId()));
        _runBar.SteerRequested += (_, instruction) => SendForActive(new ArcSteerCommand(ActiveRunId(), instruction));
        _runBar.TakeoverRequested += (_, _) => SendForActive(new ArcTakeoverCommand(ActiveRunId()));

        _composer.SubmitRequested += OnComposerSubmit;
        _composer.StopRequested += (_, _) => SendForActive(new ArcStopCommand(ActiveRunId()));
        _approvalTray.DecisionRequested += (_, decision) => SendForActive(new ArcApprovalCommand(ActiveRunId(), decision));
        _attentionInbox.ItemActionRequested += (_, entryId) => SendForActive(new ArcOpenItemCommand(ActiveRunId(), entryId));
        _attentionInbox.Dismissed += (_, entryId) => _workspace?.Attention.Resolve(entryId);
        _dock.PauseRequested += (_, _) => SendForActive(new ArcPauseCommand(ActiveRunId()));
        _dock.StopRequested += (_, _) => SendForActive(new ArcStopCommand(ActiveRunId()));
        _dock.ExpandRequested += (_, _) =>
        {
            var background = _workspace?.BackgroundRuns.FirstOrDefault();
            if (background is { } run)
            {
                _workspace?.Activate(run.SessionId);
            }
        };

        var stack = ArcUi.Stack(10);
        stack.Children.Add(_runBar);
        stack.Children.Add(_transcript);
        stack.Children.Add(_approvalTray);
        stack.Children.Add(_dock);
        stack.Children.Add(_attentionInbox);
        stack.Children.Add(_composer);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Background),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(14),
            Child = new ScrollViewer
            {
                Content = stack,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        });

        ArcUi.Name(this, "Agent surface", "Conversation, run controls, approvals, background run and the attention inbox in one host-embeddable surface.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Workspace = ArcAgentWorkspace.CreateSample();
    }

    /// <summary>The workspace this surface projects.</summary>
    public IArcAgentWorkspace? Workspace
    {
        get => _workspace;
        set
        {
            if (ReferenceEquals(_workspace, value))
            {
                return;
            }

            if (_workspace is not null)
            {
                _workspace.Changed -= OnWorkspaceChanged;
            }

            _workspace = value;
            _approvalTray.Queue = value?.Approvals;
            if (_workspace is not null)
            {
                _workspace.Changed += OnWorkspaceChanged;
            }

            Refresh();
        }
    }

    /// <summary>The host transport. Null means host-free sample rendering.</summary>
    public IArcAgentTransport? Transport
    {
        get => _transport;
        set => _transport = value;
    }

    /// <summary>Optional host environment services (open/copy/confirm).</summary>
    public IArcAgentServices Services { get; set; } = ArcAgentNullServices.Instance;

    /// <summary>The view registry used for custom item rendering.</summary>
    public ArcAgentItemViewRegistry ViewRegistry { get; } = new();

    /// <summary>Shows the attention inbox section when it has entries.</summary>
    public bool ShowAttentionInbox
    {
        get => _showAttentionInbox;
        set
        {
            _showAttentionInbox = value;
            Refresh();
        }
    }

    /// <summary>Shows the background dock when a background run exists.</summary>
    public bool ShowBackgroundDock
    {
        get => _showBackgroundDock;
        set
        {
            _showBackgroundDock = value;
            Refresh();
        }
    }

    /// <summary>Raised with every command sent to the host (also available through <see cref="CommandRaised"/>).</summary>
    public event EventHandler<ArcAgentCommand>? CommandRequested;

    /// <summary>The command sink leaf controls can use; the surface itself implements it.</summary>
    public IArcAgentCommandSink Commands => this;

    /// <inheritdoc />
    public event EventHandler<ArcAgentCommand>? CommandRaised;

    /// <summary>Attaches a workspace, transport and optional services in one call.</summary>
    public void Attach(IArcAgentWorkspace workspace, IArcAgentTransport? transport = null, IArcAgentServices? services = null)
    {
        Workspace = workspace;
        Transport = transport;
        if (services is not null)
        {
            Services = services;
        }
    }

    /// <summary>Starts a user turn through the host transport (no-op without a transport).</summary>
    public async Task<string?> StartTurnAsync(string text, string? model = null)
    {
        if (_transport is null || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        _lastRunId = await _transport.StartTurnAsync(new ArcAgentTurnRequest(text, model), _sink).ConfigureAwait(false);
        return _lastRunId;
    }

    /// <inheritdoc />
    public async Task SendAsync(ArcAgentCommand command, CancellationToken cancellationToken = default)
    {
        CommandRaised?.Invoke(this, command);
        CommandRequested?.Invoke(this, command);
        if (_transport is not null)
        {
            await _transport.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private void SendForActive(ArcAgentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RunId))
        {
            return;
        }

        _ = SendAsync(command);
    }

    private string ActiveRunId()
    {
        var active = _workspace?.Active;
        if (active is not null)
        {
            return active.RunRef.RunId;
        }

        return _lastRunId ?? string.Empty;
    }

    private async void OnComposerSubmit(object? sender, ArcComposerSubmit submit)
    {
        try
        {
            await StartTurnAsync(submit.Text, submit.Model);
        }
        catch (Exception exception)
        {
            _transcript.AppendOrUpdate(new UiEvent
            {
                Id = $"composer-error-{Guid.NewGuid():N}",
                Kind = UiEventKind.Error,
                Title = "The host transport failed: " + exception.Message,
                Detail = exception.Message,
                Status = UiEventStatus.Failed,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    private void OnFlush(object? sender, ArcAgentEventBatch batch)
    {
        _workspace?.ApplyRange(batch.Events);
    }

    private void OnWorkspaceChanged(object? sender, ArcAgentWorkspaceChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var workspace = _workspace;
        var active = workspace?.Active;
        if (workspace is null || active is null)
        {
            return;
        }

        if (!ReferenceEquals(active, _synced))
        {
            _synced = active;
            _transcript.Events = Array.Empty<UiEvent>();
            _transcript.ResetIncremental();
            _cursor = 0;
        }

        var timeline = active.Timeline;
        for (; _cursor < timeline.Count; _cursor++)
        {
            var mapped = MapEvent(timeline[_cursor], active);
            if (mapped is not null)
            {
                _transcript.AppendOrUpdate(mapped);
            }
        }

        _transcript.Status = MapTranscriptStatus(active.Run.Phase);
        _transcript.IsStreaming = active.Run.Phase is ArcRunPhase.Running or ArcRunPhase.Planning or ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput;
        _runBar.Status = MapRunBarStatus(active.Run.Phase);
        _runBar.CurrentStep = active.Run.CurrentStep ?? active.Run.Run.Label ?? "Waiting for the next step";
        _runBar.StepLabel = active.Run.Run.Label;
        _runBar.ElapsedSeconds = active.Run.ElapsedSeconds;
        _composer.IsRunning = active.Run.Phase is ArcRunPhase.Running or ArcRunPhase.Planning or ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput or ArcRunPhase.Paused;

        var items = workspace.Attention.Entries.Select(MapAttention).ToArray();
        _attentionInbox.Items = items;
        _attentionInbox.IsVisible = _showAttentionInbox && items.Length > 0;

        var background = workspace.BackgroundRuns.FirstOrDefault();
        if (background.RunId is not null && workspace.GetSession(background.RunId) is { } backgroundSession && _showBackgroundDock)
        {
            _dock.IsVisible = true;
            _dock.Status = MapDockStatus(backgroundSession.Run.Phase);
            _dock.TaskLabel = background.Label ?? backgroundSession.Run.CurrentStep ?? "Background run";
            _dock.Progress = backgroundSession.Run.Phase is ArcRunPhase.Done ? 1 : 0.55;
        }
        else
        {
            _dock.IsVisible = false;
        }
    }

    private static UiEvent? MapEvent(ArcAgentEvent @event, IArcAgentSession session) => @event switch
    {
        ArcRunStartedEvent started => New(
            $"run-{started.Run.RunId}", UiEventKind.Plan, "Run started",
            started.Run.Label ?? started.Run.Kind.ToString(), UiEventStatus.Running, started.At),
        ArcRunPhaseChangedEvent phase => New(
            $"phase-{phase.Stamp.RunId}-{phase.Phase}", UiEventKind.Plan, $"Run {phase.Phase}",
            string.Empty, MapPhaseEventStatus(phase.Phase), phase.At),
        ArcStepStartedEvent step => New(
            $"step-{step.StepId}", UiEventKind.Plan, step.Title, string.Empty, UiEventStatus.Running, step.At),
        ArcStepFinishedEvent step => New(
            $"step-{step.StepId}", UiEventKind.Plan, StepTitle(session, step.StepId), string.Empty,
            MapStepStatus(step.Status), step.At),
        ArcMessageStartedEvent message => New(
            $"message-{message.MessageId}", UiEventKind.Message, RoleLabel(message.Role),
            string.Empty, UiEventStatus.Running, message.At),
        ArcMessageDeltaEvent delta => New(
            $"message-{delta.MessageId}", UiEventKind.Message, MessageText(session, delta.MessageId),
            string.Empty, UiEventStatus.Running, delta.At),
        ArcMessageEndedEvent ended => New(
            $"message-{ended.MessageId}", UiEventKind.Message, MessageText(session, ended.MessageId),
            string.Empty, MapMessageStatus(ended.Status), ended.At),
        ArcReasoningDeltaEvent reasoning => New(
            $"reasoning-{reasoning.ReasoningId}", UiEventKind.Thinking, ReasoningText(session, reasoning.ReasoningId),
            string.Empty, UiEventStatus.Running, reasoning.At),
        ArcToolCallStartedEvent tool => New(
            $"tool-{tool.CallId}", UiEventKind.Tool, tool.ToolName, tool.Intent, UiEventStatus.Running, tool.At),
        ArcToolArgsDeltaEvent args => New(
            $"tool-{args.CallId}", UiEventKind.Tool, ToolTitle(session, args.CallId), string.Empty,
            UiEventStatus.Running, args.At),
        ArcToolCallEndedEvent toolEnd => New(
            $"tool-{toolEnd.CallId}", UiEventKind.Tool, ToolTitle(session, toolEnd.CallId),
            toolEnd.ResultSummary ?? string.Empty, MapToolStatus(toolEnd.Status), toolEnd.At),
        ArcApprovalRequestedEvent approval => New(
            $"approval-{approval.Request.RequestId}", UiEventKind.Approval,
            $"Approval needed: {approval.Request.ToolName}", approval.Request.Intent,
            UiEventStatus.Pending, approval.At),
        ArcApprovalResolvedEvent resolved => New(
            $"approval-{resolved.RequestId}", UiEventKind.Approval, "Approval resolved",
            resolved.State.ToString(), MapApprovalStatus(resolved.State), resolved.At),
        ArcQuestionRequestedEvent question => New(
            $"question-{question.Request.RequestId}", UiEventKind.Ask, question.Request.Question,
            string.Empty, UiEventStatus.Pending, question.At),
        ArcAttentionRaisedEvent attention => New(
            $"attention-{attention.Entry.EntryId}", UiEventKind.Ask, attention.Entry.Title,
            attention.Entry.Detail, UiEventStatus.Pending, attention.At),
        ArcArtifactPublishedEvent artifact => New(
            $"artifact-{artifact.Artifact.ArtifactId}", UiEventKind.Result, artifact.Artifact.Title,
            artifact.Artifact.Subtitle ?? artifact.Artifact.Kind.ToString(), UiEventStatus.Done, artifact.At),
        ArcCustomEvent custom => New(
            $"custom-{custom.Stamp.EventId}", UiEventKind.Message, custom.Kind, custom.Payload,
            UiEventStatus.Done, custom.At),
        _ => null
    };

    private static UiEvent New(
        string id,
        UiEventKind kind,
        string title,
        string detail,
        UiEventStatus status,
        DateTimeOffset at) => new()
    {
        Id = id,
        Kind = kind,
        Title = string.IsNullOrWhiteSpace(title) ? "(no title)" : title,
        Detail = detail,
        Status = status,
        Timestamp = at
    };

    private static string MessageText(IArcAgentSession session, string id) =>
        session.Messages.FirstOrDefault(message => message.Id == id)?.Text ?? string.Empty;

    private static string ReasoningText(IArcAgentSession session, string id) =>
        session.Messages.FirstOrDefault(message => message.Id == id)?.Text ?? string.Empty;

    private static string ToolTitle(IArcAgentSession session, string id) =>
        session.ToolCalls.FirstOrDefault(call => call.Id == id)?.ToolName ?? "Tool call";

    private static string StepTitle(IArcAgentSession session, string id) =>
        session.Steps.FirstOrDefault(step => step.Id == id)?.Title ?? "Step";

    private static string RoleLabel(ArcAgentRole role) => role switch
    {
        ArcAgentRole.User => "You",
        ArcAgentRole.Assistant => "Assistant",
        ArcAgentRole.Reasoning => "Thinking",
        ArcAgentRole.Tool => "Tool",
        ArcAgentRole.SubAgent => "Sub-agent",
        _ => "System"
    };

    private static UiTranscriptStatus MapTranscriptStatus(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Done => UiTranscriptStatus.Done,
        ArcRunPhase.Failed => UiTranscriptStatus.Failed,
        ArcRunPhase.Stopped or ArcRunPhase.TakenOver => UiTranscriptStatus.Stopped,
        ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput => UiTranscriptStatus.AwaitingInput,
        ArcRunPhase.Running or ArcRunPhase.Planning => UiTranscriptStatus.Running,
        _ => UiTranscriptStatus.Idle
    };

    private static UiRunBarStatus MapRunBarStatus(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Paused => UiRunBarStatus.Paused,
        ArcRunPhase.TakenOver => UiRunBarStatus.TakenOver,
        ArcRunPhase.Done or ArcRunPhase.Failed or ArcRunPhase.Stopped => UiRunBarStatus.Stopped,
        _ => UiRunBarStatus.Running
    };

    private static UiDockStatus MapDockStatus(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Paused => UiDockStatus.Paused,
        ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput => UiDockStatus.NeedsAttention,
        ArcRunPhase.Done or ArcRunPhase.Failed or ArcRunPhase.Stopped => UiDockStatus.Idle,
        _ => UiDockStatus.Running
    };

    private static UiEventStatus MapPhaseEventStatus(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Done => UiEventStatus.Done,
        ArcRunPhase.Failed => UiEventStatus.Failed,
        ArcRunPhase.Stopped or ArcRunPhase.TakenOver => UiEventStatus.Skipped,
        _ => UiEventStatus.Running
    };

    private static UiEventStatus MapStepStatus(ArcStepStatus status) => status switch
    {
        ArcStepStatus.Done => UiEventStatus.Done,
        ArcStepStatus.Failed => UiEventStatus.Failed,
        ArcStepStatus.Skipped => UiEventStatus.Skipped,
        _ => UiEventStatus.Running
    };

    private static UiEventStatus MapMessageStatus(ArcMessageStatus status) => status switch
    {
        ArcMessageStatus.Done => UiEventStatus.Done,
        ArcMessageStatus.Failed => UiEventStatus.Failed,
        ArcMessageStatus.Skipped => UiEventStatus.Skipped,
        _ => UiEventStatus.Running
    };

    private static UiEventStatus MapToolStatus(ArcToolCallStatus status) => status switch
    {
        ArcToolCallStatus.Succeeded => UiEventStatus.Done,
        ArcToolCallStatus.Failed => UiEventStatus.Failed,
        ArcToolCallStatus.Denied => UiEventStatus.Skipped,
        _ => UiEventStatus.Running
    };

    private static UiEventStatus MapApprovalStatus(ArcApprovalState state) => state switch
    {
        ArcApprovalState.Approved => UiEventStatus.Done,
        ArcApprovalState.Denied => UiEventStatus.Skipped,
        ArcApprovalState.Expired => UiEventStatus.Failed,
        _ => UiEventStatus.Pending
    };

    private static UiAttentionItem MapAttention(ArcAttentionEntry entry) => new()
    {
        Id = entry.EntryId,
        Kind = entry.Kind switch
        {
            ArcAttentionKind.Approval => UiAttentionKind.Approval,
            ArcAttentionKind.Question => UiAttentionKind.Ask,
            ArcAttentionKind.Failure => UiAttentionKind.Failure,
            ArcAttentionKind.Routine => UiAttentionKind.Routine,
            _ => UiAttentionKind.Suggestion
        },
        Title = entry.Title,
        Summary = entry.Detail,
        Risk = entry.Level switch
        {
            ArcAttentionLevel.Blocking => UiAttentionRisk.High,
            ArcAttentionLevel.Important => UiAttentionRisk.Medium,
            _ => UiAttentionRisk.Low
        },
        Age = string.Empty,
        ActionLabel = "Open",
        Status = entry.State switch
        {
            ArcAttentionState.Seen => UiAttentionStatus.Seen,
            ArcAttentionState.InProgress => UiAttentionStatus.InProgress,
            ArcAttentionState.Resolved => UiAttentionStatus.Resolved,
            ArcAttentionState.Expired => UiAttentionStatus.Expired,
            _ => UiAttentionStatus.New
        }
    };

    private sealed class SurfaceEventSink : IArcAgentEventSink
    {
        private readonly ArcAgentSurface _surface;

        public SurfaceEventSink(ArcAgentSurface surface) => _surface = surface;

        public void Push(ArcAgentEvent @event) => _surface._coalescer.Enqueue(@event);

        public void Complete(string runId, ArcRunPhase phase)
        {
            // The host must push an explicit ArcRunPhaseChangedEvent: the presentation runtime never
            // manufactures events (sequence/id ownership stays with the host).
        }

        public void Fail(string runId, string message)
        {
            // Same as Complete: failures arrive as a host event so ordering and ids remain host-owned.
        }
    }
}


