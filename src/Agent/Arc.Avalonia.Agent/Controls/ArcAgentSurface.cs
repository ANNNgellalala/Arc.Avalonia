using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using UiAttentionItem = Arc.Avalonia.AgentUI.Controls.ArcAttentionItem;
using UiAttentionKind = Arc.Avalonia.AgentUI.Controls.ArcAttentionKind;
using UiAttentionRisk = Arc.Avalonia.AgentUI.Controls.ArcAttentionRisk;
using UiAttentionStatus = Arc.Avalonia.AgentUI.Controls.ArcAttentionItemStatus;
using UiDockStatus = Arc.Avalonia.AgentUI.Controls.ArcDockStatus;
using UiRunBarStatus = Arc.Avalonia.AgentUI.Controls.ArcRunBarStatus;

namespace Arc.Avalonia.Agent;

/// <summary>
/// The Agent top-layer composition root. It lays out the projected workspace - run bar, the
/// <see cref="ArcAgentConversation"/> (bubbles for messages, full-width event cards for run events), the
/// approval/attention/background surfaces and the Chat composer - and turns every intent into an
/// <see cref="ArcAgentCommand"/> for the host. It creates no events and performs no I/O: a host pushes events
/// through <see cref="ArcDeltaCoalescer"/> via the supplied sink.
///
/// With no transport attached it renders the deterministic <see cref="ArcAgentWorkspace.CreateSample"/>
/// projection, which keeps Gallery screenshots and design review host-free.
/// </summary>
public class ArcAgentSurface : Panel, IArcAgentCommandSink
{
    private readonly ArcRunControlBar _runBar = new();
    private readonly ArcAgentConversation _conversation = new();
    private readonly ArcAgentApprovalTray _approvalTray = new();
    private readonly ArcChatComposer _composer = new();
    private readonly ArcAgentAttentionInbox _attentionInbox = new() { Height = 220 };
    private readonly ArcBackgroundRunDock _dock = new();
    private readonly ScrollViewer _rail;
    private readonly ArcDeltaCoalescer _coalescer;
    private readonly IArcAgentEventSink _sink;
    private IArcAgentWorkspace? _workspace;
    private IArcAgentTransport? _transport;
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

        _composer.Send += OnComposerSend;
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

        _conversation.ApprovalDecisionRequested += (_, args) =>
            SendForActive(new ArcApprovalCommand(
                ActiveRunId(),
                new ArcApprovalDecision(args.RequestId, args.Approved, DenyReason: args.Reason)));
        _conversation.AnswerRequested += (_, args) =>
            SendForActive(new ArcAnswerCommand(
                ActiveRunId(),
                new ArcQuestionAnswer(args.RequestId, args.SelectedOptions, args.FreeText, args.Deferred)));
        _conversation.RetryRequested += (_, args) =>
            SendForActive(new ArcRetryCommand(ActiveRunId(), args.CallId));

        var railStack = ArcUi.Stack(8);
        railStack.Children.Add(_approvalTray);
        railStack.Children.Add(_dock);
        railStack.Children.Add(_attentionInbox);
        _rail = new ScrollViewer
        {
            Content = railStack,
            Width = 320,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsVisible = false
        };

        var main = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        main.Children.Add(_conversation);
        Grid.SetColumn(_rail, 1);
        main.Children.Add(_rail);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10 };
        grid.Children.Add(_runBar);
        Grid.SetRow(main, 1);
        grid.Children.Add(main);
        Grid.SetRow(_composer, 2);
        grid.Children.Add(_composer);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Background),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(14),
            Child = grid
        });

        ArcUi.Name(this, "Agent surface", "Conversation bubbles and event cards, run controls, approvals, background run and the attention rail in one host-embeddable surface.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Workspace = ArcAgentWorkspace.CreateSample();
    }

    /// <summary>The conversation surface (bubbles + full-width event cards).</summary>
    public ArcAgentConversation Conversation => _conversation;

    /// <summary>The Chat composer used for user turns.</summary>
    public ArcChatComposer Composer => _composer;

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
            _conversation.Workspace = value;
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

    /// <summary>Shows the attention surface when it has entries.</summary>
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
    public static readonly RoutedEvent<ArcAgentCommandRoutedEventArgs> CommandRequestedEvent =
        RoutedEvent.Register<ArcAgentSurface, ArcAgentCommandRoutedEventArgs>("CommandRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="CommandRequested"/>.</summary>
    public event EventHandler<ArcAgentCommandRoutedEventArgs>? CommandRequestedRouted
    {
        add => AddHandler(CommandRequestedEvent, value);
        remove => RemoveHandler(CommandRequestedEvent, value);
    }

    public event EventHandler<ArcAgentCommand>? CommandRequested;

    /// <summary>The command sink leaf controls can use; the surface itself implements it.</summary>
    public IArcAgentCommandSink Commands => this;

    /// <inheritdoc />
    public static readonly RoutedEvent<ArcAgentCommandRoutedEventArgs> CommandRaisedEvent =
        RoutedEvent.Register<ArcAgentSurface, ArcAgentCommandRoutedEventArgs>("CommandRaised", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="CommandRaised"/>.</summary>
    public event EventHandler<ArcAgentCommandRoutedEventArgs>? CommandRaisedRouted
    {
        add => AddHandler(CommandRaisedEvent, value);
        remove => RemoveHandler(CommandRaisedEvent, value);
    }

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
        RaiseEvent(new ArcAgentCommandRoutedEventArgs(CommandRaisedEvent, command));
        CommandRequested?.Invoke(this, command);
        RaiseEvent(new ArcAgentCommandRoutedEventArgs(CommandRequestedEvent, command));
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

    private async void OnComposerSend(object? sender, ArcChatDraft draft)
    {
        if (_transport is null || string.IsNullOrWhiteSpace(draft.Text))
        {
            return;
        }

        try
        {
            var attachments = draft.Attachments.Count == 0
                ? null
                : draft.Attachments
                    .Select(attachment => new ArcAttachment(attachment.Name, attachment.Name, attachment.Size))
                    .ToArray();
            _lastRunId = await _transport.StartTurnAsync(
                new ArcAgentTurnRequest(draft.Text, Attachments: attachments),
                _sink).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _conversation.AppendSystemNotice($"The host transport failed: {exception.Message}");
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
        _conversation.Workspace = workspace;

        if (workspace is not null && active is not null)
        {
            _runBar.Status = MapRunBarStatus(active.Run.Phase);
            _runBar.CurrentStep = active.Run.CurrentStep ?? active.Run.Run.Label ?? "Waiting for the next step";
            _runBar.StepLabel = active.Run.Run.Label;
            _runBar.ElapsedSeconds = active.Run.ElapsedSeconds;
            _composer.IsRunning = active.Run.Phase is ArcRunPhase.Running or ArcRunPhase.Planning
                or ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput or ArcRunPhase.Paused;

            var items = workspace.Attention.Entries.Select(MapAttention).ToArray();
            _attentionInbox.Items = items;
            _attentionInbox.IsVisible = _showAttentionInbox && items.Length > 0;

            var background = workspace.BackgroundRuns.FirstOrDefault();
            if (background.RunId is not null &&
                workspace.GetSession(background.RunId) is { } backgroundSession &&
                _showBackgroundDock)
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
        else
        {
            _runBar.Status = UiRunBarStatus.Stopped;
            _runBar.CurrentStep = "Waiting for a run";
            _runBar.StepLabel = null;
            _runBar.ElapsedSeconds = 0;
            _composer.IsRunning = false;
            _attentionInbox.Items = Array.Empty<UiAttentionItem>();
            _attentionInbox.IsVisible = false;
            _dock.IsVisible = false;
        }

        var hasApprovals = _workspace?.PendingApprovals.Count > 0;
        _rail.IsVisible = hasApprovals || _dock.IsVisible || _attentionInbox.IsVisible;
    }

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
