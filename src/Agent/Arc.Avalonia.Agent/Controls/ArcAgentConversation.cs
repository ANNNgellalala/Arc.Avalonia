using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// The agent conversation: one scrollable timeline where human/assistant messages render as
/// <see cref="ArcChatBubble"/> (user right, assistant left, Markdown body) and every run event renders as a
/// full-width card in timeline order - tool calls through <see cref="ArcAgentToolCallCard"/>, reasoning as a
/// collapsible card, approvals as approve/deny event cards, structured questions through
/// <see cref="ArcAgentChoiceCard"/> (free text and "answer later" included), and compact cards for steps,
/// artifacts, attention entries and run phase changes.
/// <para>
/// There is deliberately no "rows mode" switch: the conversation always uses bubbles for messages and cards
/// for events. It only projects the session it is given, appends/updates rows incrementally as events arrive
/// (streaming bodies update in place) and never performs I/O, timers or model calls.
/// </para>
/// </summary>
public class ArcAgentConversation : Panel, IDisposable
{
    /// <summary>The workspace to project; the active session is shown.</summary>
    public static readonly StyledProperty<IArcAgentWorkspace?> WorkspaceProperty =
        AvaloniaProperty.Register<ArcAgentConversation, IArcAgentWorkspace?>(nameof(Workspace));

    /// <summary>Optional single session; takes precedence over <see cref="Workspace"/>.</summary>
    public static readonly StyledProperty<IArcAgentSession?> SessionProperty =
        AvaloniaProperty.Register<ArcAgentConversation, IArcAgentSession?>(nameof(Session));

    /// <summary>Markdown environment for bubbles and reasoning cards; defaults to the agent pipeline.</summary>
    public static readonly StyledProperty<ArcMarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ArcAgentConversation, ArcMarkdownOptions?>(nameof(MarkdownOptions));

    /// <summary>Shows the conversation header (title, run state, counts).</summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<ArcAgentConversation, bool>(nameof(ShowHeader), true);

    /// <summary>Header title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcAgentConversation, string>(nameof(Title), "Conversation");

    /// <summary>Copy shown while there is nothing to project.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ArcAgentConversation, string>(
            nameof(EmptyText), "No messages yet. Start the agent to see the conversation here.");

    /// <summary>Raised when an approval event card reports approve/deny (CLR companion).</summary>
    public static readonly RoutedEvent<ArcAgentApprovalDecisionEventArgs> ApprovalDecisionRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentApprovalDecisionEventArgs>(
            "ApprovalDecisionRequested", RoutingStrategies.Bubble);

    /// <summary>Raised when a structured question card reports an answer or a deferral (CLR companion).</summary>
    public static readonly RoutedEvent<ArcAgentQuestionAnswerEventArgs> AnswerRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentQuestionAnswerEventArgs>(
            "AnswerRequested", RoutingStrategies.Bubble);

    /// <summary>Raised when a failed tool card asks to retry (CLR companion).</summary>
    public static readonly RoutedEvent<ArcAgentToolRetryEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentToolRetryEventArgs>(
            "RetryRequested", RoutingStrategies.Bubble);

    private readonly StackPanel _rowsHost;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _badgeHost;
    private readonly TextBlock _summary;
    private readonly TextBlock _titleText;
    private readonly Grid _header;
    private readonly Border _divider;
    private readonly Dictionary<string, ArcChatBubble> _messageRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcAgentToolCallCard> _toolRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReasoningRow> _reasoningRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApprovalRow> _approvalRows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _questionRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StepRow> _stepRows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventRowKeys = new(StringComparer.Ordinal);
    private readonly List<ArcChatMessage> _localMessages = new();

    private IArcAgentWorkspace? _workspace;
    private IArcAgentSession? _session;
    private int _eventCursor;
    private Control? _emptyState;
    private TextBlock? _emptyCopy;
    private bool _firstSync = true;
    private bool _disposed;

    // Touch latch: a finger drag over the timeline pauses auto-follow and resets on capture loss.
    private const double DragSlop = 8;
    private Point _pointerOrigin;
    private bool _pointerPressed;
    private bool _userDragging;

    /// <summary>Creates the conversation surface and its chrome.</summary>
    public ArcAgentConversation()
    {
        ClipToBounds = false;

        _titleText = ArcUi.Label(Title);
        _summary = ArcUi.Caption(string.Empty);
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(_titleText);
        titles.Children.Add(_summary);

        _badgeHost = ArcUi.HStack(6);
        _badgeHost.VerticalAlignment = VerticalAlignment.Center;

        _header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        _header.Children.Add(titles);
        Grid.SetColumn(_badgeHost, 1);
        _header.Children.Add(_badgeHost);

        _divider = ArcUi.Divider();
        _rowsHost = new StackPanel { Spacing = 8 };
        _scroll = new ScrollViewer
        {
            Content = _rowsHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // The timeline owns its gestures: inner markdown/code scrollers never chain into this one, and
            // this one never chains into an outer page/sheet scroller. Scrollbars stay auto-hide.
            IsScrollChainingEnabled = false,
            AllowAutoHide = true,
            Padding = new Thickness(0, 2, 2, 2)
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 8 };
        grid.Children.Add(_header);
        Grid.SetRow(_divider, 1);
        grid.Children.Add(_divider);
        Grid.SetRow(_scroll, 2);
        grid.Children.Add(_scroll);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(12),
            Child = grid
        });

        ArcUi.Name(
            this,
            "Agent conversation",
            "A timeline of messages, tool calls, reasoning, approvals, questions, steps and artifacts.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        UpdateHeader();
        UpdateEmptyState();
    }

    /// <summary>The workspace to project.</summary>
    public IArcAgentWorkspace? Workspace
    {
        get => GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    /// <summary>Optional single session shown instead of the workspace's active session.</summary>
    public IArcAgentSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>Markdown environment for message and reasoning bodies.</summary>
    public ArcMarkdownOptions? MarkdownOptions
    {
        get => GetValue(MarkdownOptionsProperty);
        set => SetValue(MarkdownOptionsProperty, value);
    }

    /// <summary>Shows the header row.</summary>
    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    /// <summary>Header title.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Copy shown when the conversation is empty.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Events of the projected session, in order (empty when no session is attached).</summary>
    public IReadOnlyList<ArcAgentEvent> Timeline => _session?.Timeline ?? Array.Empty<ArcAgentEvent>();

    /// <summary>Raised when an approval event card reports approve/deny.</summary>
    public event EventHandler<ArcAgentApprovalDecisionEventArgs>? ApprovalDecisionRequested;

    /// <summary>Bubbling routed companion of <see cref="ApprovalDecisionRequested"/>.</summary>
    public event EventHandler<ArcAgentApprovalDecisionEventArgs>? ApprovalDecisionRequestedRouted
    {
        add => AddHandler(ApprovalDecisionRequestedEvent, value);
        remove => RemoveHandler(ApprovalDecisionRequestedEvent, value);
    }

    /// <summary>Raised when a structured question card reports an answer or a deferral.</summary>
    public event EventHandler<ArcAgentQuestionAnswerEventArgs>? AnswerRequested;

    /// <summary>Bubbling routed companion of <see cref="AnswerRequested"/>.</summary>
    public event EventHandler<ArcAgentQuestionAnswerEventArgs>? AnswerRequestedRouted
    {
        add => AddHandler(AnswerRequestedEvent, value);
        remove => RemoveHandler(AnswerRequestedEvent, value);
    }

    /// <summary>Raised when a failed tool card asks to retry.</summary>
    public event EventHandler<ArcAgentToolRetryEventArgs>? RetryRequested;

    /// <summary>Bubbling routed companion of <see cref="RetryRequested"/>.</summary>
    public event EventHandler<ArcAgentToolRetryEventArgs>? RetryRequestedRouted
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>Raised when a pura-agent component inside a bubble reports an interaction.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Raised for recoverable Markdown renderer warnings inside bubbles.</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs>? Warning;

    static ArcAgentConversation()
    {
        WorkspaceProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.RebindWorkspace());
        SessionProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.BindSession(conversation.Session, force: true));
        MarkdownOptionsProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.RebuildAll());
        ShowHeaderProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.ApplyHeaderVisibility());
        TitleProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.UpdateHeader());
        EmptyTextProperty.Changed.AddClassHandler<ArcAgentConversation>((conversation, _) => conversation.UpdateEmptyState());
    }

    /// <summary>Re-projects the session from its timeline (call after a manual snapshot restore).</summary>
    public void Refresh()
    {
        RebuildAll();
    }

    /// <summary>Scrolls the conversation to the newest row.</summary>
    public void ScrollToEnd()
    {
        if (_scroll is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _scroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    /// <summary>
    /// Appends a local, host-owned system notice (for example a transport failure). Local messages are not
    /// part of the session projection; they are kept after the timeline and survive re-renders.
    /// </summary>
    /// <param name="text">Plain text or Markdown body.</param>
    internal void AppendSystemNotice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var message = new ArcChatMessage
        {
            Role = ArcChatRole.System,
            Status = ArcChatMessageStatus.Complete,
            Markdown = text,
        };
        _localMessages.Add(message);
        EnsureMessageRow(message);
        UpdateEmptyState();
        ScrollToEnd();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeWorkspace();
        UnsubscribeSession();
        GC.SuppressFinalize(this);
    }

    private void RebindWorkspace()
    {
        UnsubscribeWorkspace();
        _workspace = Workspace;
        if (_workspace is not null)
        {
            _workspace.Changed += OnWorkspaceChanged;
        }

        BindSession(ResolveSession(), force: false);
    }

    private IArcAgentSession? ResolveSession() => Session ?? _workspace?.Active;

    private void OnWorkspaceChanged(object? sender, ArcAgentWorkspaceChangedEventArgs e)
    {
        BindSession(ResolveSession(), force: false);
        Sync();
    }

    private void BindSession(IArcAgentSession? session, bool force)
    {
        if (!force && ReferenceEquals(_session, session))
        {
            UpdateHeader();
            return;
        }

        UnsubscribeSession();
        _session = session;
        if (_session is not null)
        {
            _session.Changed += OnSessionChanged;
        }

        RebuildAll();
    }

    private void OnSessionChanged(object? sender, ArcAgentSessionChangedEventArgs e) => Sync();

    private void UnsubscribeWorkspace()
    {
        if (_workspace is not null)
        {
            _workspace.Changed -= OnWorkspaceChanged;
            _workspace = null;
        }
    }

    private void UnsubscribeSession()
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
            _session = null;
        }
    }

    private void RebuildAll()
    {
        ResetRows();
        _eventCursor = 0;
        _firstSync = true;
        Sync();
    }

    private void ResetRows()
    {
        _messageRows.Clear();
        _toolRows.Clear();
        _reasoningRows.Clear();
        _approvalRows.Clear();
        _questionRows.Clear();
        _stepRows.Clear();
        _eventRowKeys.Clear();
        _emptyState = null;
        _emptyCopy = null;
        _rowsHost.Children.Clear();
    }

    private void Sync()
    {
        if (_session is null)
        {
            UpdateHeader();
            UpdateEmptyState();
            return;
        }

        var timeline = _session.Timeline;
        if (timeline.Count < _eventCursor)
        {
            _eventCursor = 0;
            ResetRows();
        }

        var appended = false;
        for (; _eventCursor < timeline.Count; _eventCursor++)
        {
            var @event = timeline[_eventCursor];
            if (@event is ArcStateSnapshotEvent)
            {
                ResetRows();
                foreach (var message in _session.Messages)
                {
                    EnsureMessageRow(message);
                }

                appended = true;
                continue;
            }

            AppendRow(@event, _session);
            appended = true;
        }

        foreach (var local in _localMessages)
        {
            EnsureMessageRow(local);
        }

        UpdateHeader();
        UpdateEmptyState();

        // Show the beginning of the run on first render (design review and screenshots); later events follow
        // the reader only while the viewport is already near the end.
        if (appended && !_firstSync && !_pointerPressed && !_userDragging && IsNearBottom())
        {
            ScrollToEnd();
        }

        _firstSync = false;
    }

    private bool IsNearBottom()
    {
        var maximum = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        return maximum - Math.Max(0, _scroll.Offset.Y) <= 64;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerPressed = true;
        _pointerOrigin = e.GetPosition(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pointerOrigin.X) > DragSlop || Math.Abs(position.Y - _pointerOrigin.Y) > DragSlop)
        {
            _userDragging = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ResetPointerState();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ResetPointerState();
    }

    private void ResetPointerState()
    {
        _pointerPressed = false;
        _userDragging = false;
    }

    private void AppendRow(ArcAgentEvent @event, IArcAgentSession session)
    {
        switch (@event)
        {
            case ArcMessageStartedEvent started:
                AddMessageRow(session, started.MessageId);
                break;
            case ArcMessageDeltaEvent delta:
                AddMessageRow(session, delta.MessageId);
                break;
            case ArcMessageEndedEvent ended:
                AddMessageRow(session, ended.MessageId);
                break;
            case ArcReasoningDeltaEvent reasoning:
                AppendReasoning(reasoning);
                break;
            case ArcToolCallStartedEvent tool:
                AddToolRow(session, tool.CallId);
                break;
            case ArcToolArgsDeltaEvent args:
                AddToolRow(session, args.CallId);
                break;
            case ArcToolCallEndedEvent toolEnd:
                AddToolRow(session, toolEnd.CallId);
                break;
            case ArcApprovalRequestedEvent approval:
                AddApprovalRow(approval.Request);
                break;
            case ArcApprovalResolvedEvent resolved:
                ResolveApprovalRow(resolved.RequestId, resolved.State);
                break;
            case ArcQuestionRequestedEvent question:
                AddQuestionRow(question.Request);
                break;
            case ArcStepStartedEvent step:
                UpdateStepRow(session, step.StepId, step.Title);
                break;
            case ArcStepFinishedEvent step:
                UpdateStepRow(session, step.StepId, null);
                break;
            case ArcArtifactPublishedEvent artifact:
                AddEventRow($"artifact:{artifact.Artifact.ArtifactId}", BuildArtifactCard(artifact.Artifact));
                break;
            case ArcAttentionRaisedEvent attention:
                AddEventRow($"attention:{attention.Entry.EntryId}", BuildAttentionCard(attention.Entry));
                break;
            case ArcRunStartedEvent run:
                AddEventRow(
                    $"run:{run.Run.RunId}",
                    BuildCompactCard(
                        "circle-dot",
                        ArcTokens.Accent,
                        "Run started",
                        run.Run.Label ?? run.Run.Kind.ToString(),
                        null));
                break;
            case ArcRunPhaseChangedEvent phase:
                AddEventRow(
                    $"phase:{phase.Stamp.EventId}",
                    BuildCompactCard(
                        "activity",
                        PhaseToken(phase.Phase),
                        $"Run {PhaseWord(phase.Phase)}",
                        null,
                        phase.At.ToLocalTime().ToString("HH:mm")));
                break;
            case ArcCustomEvent custom:
                AddEventRow(
                    $"custom:{custom.Stamp.EventId}",
                    BuildCompactCard("message-circle", ArcTokens.TextSecondary, custom.Kind, custom.Payload, null));
                break;
            case ArcStateDeltaEvent delta:
                AddEventRow(
                    $"state:{delta.Stamp.EventId}",
                    BuildCompactCard("activity", ArcTokens.TextMuted, "State update", delta.DeltaJson, null));
                break;
        }
    }

    private void AddMessageRow(IArcAgentSession session, string messageId)
    {
        if (_messageRows.ContainsKey(messageId))
        {
            return;
        }

        var message = session.Messages.FirstOrDefault(candidate => string.Equals(candidate.Id, messageId, StringComparison.Ordinal));
        if (message is null)
        {
            return;
        }

        EnsureMessageRow(message);
    }

    private void EnsureMessageRow(ArcChatMessage message)
    {
        if (_messageRows.ContainsKey(message.Id))
        {
            return;
        }

        var bubble = new ArcChatBubble
        {
            Message = message,
            IsOwn = message.Role == ArcChatRole.User,
            ShowHeader = true,
            ShowStatus = true,
            ShowAvatar = message.Role != ArcChatRole.System,
            FirstInGroup = true,
            LastInGroup = true,
            MarkdownOptions = ResolveOptions(),
            Margin = new Thickness(0, 2, 0, 2)
        };
        bubble.InteractionOccurred += (_, args) => InteractionOccurred?.Invoke(this, args);
        bubble.Warning += (_, args) => Warning?.Invoke(this, args);

        _messageRows[message.Id] = bubble;
        _rowsHost.Children.Add(bubble);
    }

    private void AddToolRow(IArcAgentSession session, string callId)
    {
        var call = session.ToolCalls.FirstOrDefault(candidate => string.Equals(candidate.Id, callId, StringComparison.Ordinal));
        if (call is null)
        {
            return;
        }

        if (_toolRows.TryGetValue(callId, out var existing))
        {
            existing.Call = call;
            return;
        }

        var card = new ArcAgentToolCallCard { Call = call, Margin = new Thickness(0, 2, 0, 2) };
        card.ApproveRequested += (_, id) => RaiseApprovalDecision(id, Approved: true, null);
        card.DenyRequested += (_, id) => RaiseApprovalDecision(id, Approved: false, "Denied by the user.");
        card.RetryRequested += (_, id) => RaiseRetry(id);
        _toolRows[callId] = card;
        _rowsHost.Children.Add(card);
    }

    private void AppendReasoning(ArcReasoningDeltaEvent reasoning)
    {
        if (!_reasoningRows.TryGetValue(reasoning.ReasoningId, out var row))
        {
            var body = new ArcMarkdownView { Markdown = string.Empty, Options = ResolveOptions() };
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(ArcUi.Icon("sparkles", 14, ArcTokens.TextSecondary));
            header.Children.Add(ArcUi.Text("Reasoning", "arc-caption arc-secondary"));
            var expander = new Expander
            {
                Header = header,
                Content = body,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var surface = ArcUi.Surface(expander, 10);
            surface.Margin = new Thickness(0, 2, 0, 2);
            row = new ReasoningRow { Body = body };
            _reasoningRows[reasoning.ReasoningId] = row;
            _rowsHost.Children.Add(surface);
        }

        row.Text.Append(reasoning.Delta);
        row.Body.Markdown = row.Text.ToString();
    }

    private void AddApprovalRow(ArcApprovalRequest request)
    {
        if (_approvalRows.ContainsKey(request.RequestId))
        {
            return;
        }

        var row = new ApprovalRow(request, RaiseApprovalDecision);
        _approvalRows[request.RequestId] = row;
        _rowsHost.Children.Add(row.Control);
    }

    private void ResolveApprovalRow(string requestId, ArcApprovalState state)
    {
        if (_approvalRows.TryGetValue(requestId, out var row))
        {
            row.Apply(state);
        }
    }

    private void AddQuestionRow(ArcQuestionRequest request)
    {
        if (!_questionRows.Add(request.RequestId))
        {
            return;
        }

        var options = request.Options.Count > 0
            ? request.Options.Select(option => new ArcAgentChoiceOption(option, option)).ToArray()
            : Array.Empty<ArcAgentChoiceOption>();
        var model = new ArcAgentChoiceModel
        {
            Type = "question",
            Version = "1.0",
            Id = request.RequestId,
            Question = request.Question,
            Options = options,
            Multi = false,
            Min = options.Length > 0 ? 1 : 0,
            SubmitLabel = "Send answer"
        };
        var card = new ArcAgentChoiceCard
        {
            Model = model,
            AllowFreeText = request.AllowFreeText || options.Length == 0,
            AllowDefer = true,
            Margin = new Thickness(0, 2, 0, 2)
        };
        card.Submitted += (_, args) => RaiseAnswer(request.RequestId, args.SelectedIds, args.FreeText, deferred: false);
        card.DeferredRequested += (_, _) => RaiseAnswer(request.RequestId, Array.Empty<string>(), null, deferred: true);
        _rowsHost.Children.Add(card);
    }

    private void UpdateStepRow(IArcAgentSession session, string stepId, string? title)
    {
        var step = session.Steps.FirstOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.Ordinal));
        if (!_stepRows.TryGetValue(stepId, out var row))
        {
            row = new StepRow();
            _stepRows[stepId] = row;
            _rowsHost.Children.Add(row.Control);
        }

        row.Update(title ?? step?.Title ?? "Step", step?.Status ?? ArcStepStatus.Running);
    }

    private void AddEventRow(string key, Control control)
    {
        if (!_eventRowKeys.Add(key))
        {
            return;
        }

        control.Margin = new Thickness(0, 2, 0, 2);
        _rowsHost.Children.Add(control);
    }

    private ArcMarkdownOptions ResolveOptions() => MarkdownOptions ?? ArcAgentMarkdown.DefaultOptions;

    private void UpdateHeader()
    {
        _titleText.Text = Title;
        _header.IsVisible = ShowHeader;
        _divider.IsVisible = ShowHeader;
        if (!ShowHeader)
        {
            return;
        }

        _badgeHost.Children.Clear();
        var session = _session;
        if (session is null)
        {
            _summary.Text = "No run attached.";
            _badgeHost.Children.Add(ArcUi.Badge("Idle", ArcBadgeTone.Neutral));
            ArcUi.Name(this, "Agent conversation. No run attached.");
            return;
        }

        var messageCount = session.Messages.Count;
        var toolCount = session.ToolCalls.Count;
        _summary.Text =
            $"{messageCount} message{(messageCount == 1 ? string.Empty : "s")} · " +
            $"{toolCount} tool call{(toolCount == 1 ? string.Empty : "s")} · {PhaseWord(session.Run.Phase)}";
        _badgeHost.Children.Add(PhaseBadge(session.Run.Phase));
        if (session.Run.Phase is ArcRunPhase.Running or ArcRunPhase.Planning
            or ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput)
        {
            _badgeHost.Children.Add(ArcUi.Pill("Live", "activity", accent: true));
        }

        ArcUi.Name(
            this,
            $"Agent conversation, {messageCount} messages, {toolCount} tool calls, {PhaseWord(session.Run.Phase)}.");
    }

    private void ApplyHeaderVisibility()
    {
        _header.IsVisible = ShowHeader;
        _divider.IsVisible = ShowHeader;
    }

    private void UpdateEmptyState()
    {
        var empty = _session is null || _rowsHost.Children.Count == 0
            || (_rowsHost.Children.Count == 1 && ReferenceEquals(_rowsHost.Children[0], _emptyState));
        if (!empty)
        {
            if (_emptyState is not null && _rowsHost.Children.Contains(_emptyState))
            {
                _rowsHost.Children.Remove(_emptyState);
            }

            return;
        }

        if (_emptyState is null)
        {
            var copy = ArcUi.Body(EmptyText, 520);
            copy.HorizontalAlignment = HorizontalAlignment.Center;
            copy.TextAlignment = TextAlignment.Center;
            _emptyCopy = copy;
            var panel = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 24, 0, 24),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(ArcUi.Icon("message-circle", 22, ArcTokens.TextMuted));
            panel.Children.Add(copy);
            _emptyState = panel;
        }

        if (_emptyCopy is not null)
        {
            _emptyCopy.Text = EmptyText;
        }

        if (!_rowsHost.Children.Contains(_emptyState))
        {
            _rowsHost.Children.Insert(0, _emptyState);
        }
    }

    private void RaiseApprovalDecision(string requestId, bool Approved, string? reason)
    {
        var args = new ArcAgentApprovalDecisionEventArgs
        {
            RequestId = requestId,
            Approved = Approved,
            Reason = reason
        };
        ApprovalDecisionRequested?.Invoke(this, args);
        args.RoutedEvent = ApprovalDecisionRequestedEvent;
        RaiseEvent(args);
    }

    private void RaiseAnswer(string requestId, IReadOnlyList<string> selected, string? freeText, bool deferred)
    {
        var args = new ArcAgentQuestionAnswerEventArgs
        {
            RequestId = requestId,
            SelectedOptions = selected,
            FreeText = freeText,
            Deferred = deferred
        };
        AnswerRequested?.Invoke(this, args);
        args.RoutedEvent = AnswerRequestedEvent;
        RaiseEvent(args);
    }

    private void RaiseRetry(string callId)
    {
        var args = new ArcAgentToolRetryEventArgs { CallId = callId };
        RetryRequested?.Invoke(this, args);
        args.RoutedEvent = RetryRequestedEvent;
        RaiseEvent(args);
    }

    private static Control BuildCompactCard(
        string icon,
        string iconToken,
        string title,
        string? detail,
        string? trailing)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), ColumnSpacing = 8 };
        row.Children.Add(new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ArcIcon
            {
                Icon = icon,
                Size = 13,
                Foreground = ArcUi.Brush(iconToken),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var copy = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(ArcUi.Text(title, "arc-body-sm"));
        if (!string.IsNullOrWhiteSpace(detail))
        {
            copy.Children.Add(ArcUi.Caption(Trim(detail, 500)));
        }

        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);
        if (!string.IsNullOrWhiteSpace(trailing))
        {
            var caption = ArcUi.Caption(trailing);
            caption.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(caption, 2);
            row.Children.Add(caption);
        }

        return ArcUi.Surface(row, 10, radius: 18);
    }

    private static Control BuildArtifactCard(ArcArtifact artifact)
    {
        var stack = new StackPanel { Spacing = 2 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(ArcUi.Icon("file", 14, ArcTokens.Success));
        head.Children.Add(ArcUi.Text(artifact.Title, "arc-body-sm"));
        stack.Children.Add(head);
        if (!string.IsNullOrWhiteSpace(artifact.Subtitle))
        {
            stack.Children.Add(ArcUi.Caption(artifact.Subtitle));
        }

        if (!string.IsNullOrWhiteSpace(artifact.DisplayText))
        {
            stack.Children.Add(ArcUi.Mono(Trim(artifact.DisplayText, 400)));
        }

        return ArcUi.Surface(stack, 10, radius: 18);
    }

    private static Control BuildAttentionCard(ArcAttentionEntry entry)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), ColumnSpacing = 8 };
        row.Children.Add(new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ArcIcon
            {
                Icon = "bell",
                Size = 13,
                Foreground = ArcUi.Brush(ArcTokens.Warning),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var copy = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(ArcUi.Text(entry.Title, "arc-body-sm"));
        if (!string.IsNullOrWhiteSpace(entry.Detail))
        {
            copy.Children.Add(ArcUi.Caption(entry.Detail));
        }

        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);
        var pill = ArcUi.Pill(
            entry.Level.ToString(),
            "bell",
            accent: entry.Level == ArcAttentionLevel.Blocking);
        pill.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(pill, 2);
        row.Children.Add(pill);
        return ArcUi.Surface(row, 10, radius: 18);
    }

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static ArcBadge PhaseBadge(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Running or ArcRunPhase.Planning => ArcUi.Badge("Running", ArcBadgeTone.Accent),
        ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput => ArcUi.Badge("Awaiting input", ArcBadgeTone.Warning),
        ArcRunPhase.Paused => ArcUi.Badge("Paused", ArcBadgeTone.Warning),
        ArcRunPhase.Done => ArcUi.Badge("Done", ArcBadgeTone.Success),
        ArcRunPhase.Failed => ArcUi.Badge("Failed", ArcBadgeTone.Danger),
        ArcRunPhase.Stopped or ArcRunPhase.TakenOver => ArcUi.Badge("Stopped", ArcBadgeTone.Neutral),
        _ => ArcUi.Badge("Idle", ArcBadgeTone.Neutral)
    };

    private static string PhaseWord(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Planning => "planning",
        ArcRunPhase.Running => "the agent is working",
        ArcRunPhase.AwaitingApproval => "waiting for approval",
        ArcRunPhase.AwaitingInput => "waiting for your answer",
        ArcRunPhase.Paused => "paused",
        ArcRunPhase.Done => "run finished",
        ArcRunPhase.Failed => "the run hit an error",
        ArcRunPhase.Stopped => "run stopped",
        ArcRunPhase.TakenOver => "taken over",
        _ => "idle"
    };

    private static string PhaseToken(ArcRunPhase phase) => phase switch
    {
        ArcRunPhase.Done => ArcTokens.Success,
        ArcRunPhase.Failed => ArcTokens.Danger,
        ArcRunPhase.AwaitingApproval or ArcRunPhase.AwaitingInput => ArcTokens.Warning,
        _ => ArcTokens.TextSecondary
    };

    private sealed class ReasoningRow
    {
        internal required ArcMarkdownView Body { get; init; }

        internal StringBuilder Text { get; } = new();
    }

    private sealed class StepRow
    {
        private readonly TextBlock _title;
        private readonly StackPanel _badgeHost;

        internal StepRow()
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(8),
                Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new ArcIcon
                {
                    Icon = "list-ordered",
                    Size = 13,
                    Foreground = ArcUi.Brush(ArcTokens.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
            _title = ArcUi.Text("Step", "arc-body-sm");
            Grid.SetColumn(_title, 1);
            row.Children.Add(_title);
            _badgeHost = ArcUi.HStack(4);
            _badgeHost.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_badgeHost, 2);
            row.Children.Add(_badgeHost);
            Control = ArcUi.Surface(row, 10, radius: 18);
        }

        internal Control Control { get; }

        internal void Update(string title, ArcStepStatus status)
        {
            _title.Text = title;
            _badgeHost.Children.Clear();
            _badgeHost.Children.Add(StepBadge(status));
        }

        private static ArcBadge StepBadge(ArcStepStatus status) => status switch
        {
            ArcStepStatus.Done => ArcUi.Badge("Done", ArcBadgeTone.Success),
            ArcStepStatus.Failed => ArcUi.Badge("Failed", ArcBadgeTone.Danger),
            ArcStepStatus.Skipped => ArcUi.Badge("Skipped", ArcBadgeTone.Neutral),
            ArcStepStatus.Running => ArcUi.Badge("Running", ArcBadgeTone.Accent),
            _ => ArcUi.Badge("Pending", ArcBadgeTone.Neutral)
        };
    }

    private sealed class ApprovalRow
    {
        private readonly ArcBadge _badge;
        private readonly StackPanel _actions;
        private readonly TextBlock _state;
        private readonly Action<string, bool, string?> _decision;
        private readonly string _requestId;

        internal ApprovalRow(ArcApprovalRequest request, Action<string, bool, string?> decision)
        {
            _requestId = request.RequestId;
            _decision = decision;

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), ColumnSpacing = 8 };
            header.Children.Add(new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(8),
                Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new ArcIcon
                {
                    Icon = "shield",
                    Size = 13,
                    Foreground = ArcUi.Brush(ArcTokens.Warning),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            var copy = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(ArcUi.Text($"Approval needed: {request.ToolName}", "arc-body-sm arc-medium"));
            if (!string.IsNullOrWhiteSpace(request.Intent))
            {
                copy.Children.Add(ArcUi.Caption(request.Intent));
            }

            if (request.Arguments.Count > 0)
            {
                copy.Children.Add(ArcUi.Mono(Trim(
                    string.Join(" · ", request.Arguments.Select(argument => $"{argument.Name}={argument.Value}")),
                    240)));
            }

            Grid.SetColumn(copy, 1);
            header.Children.Add(copy);
            _badge = ArcUi.Badge("Needs approval", ArcBadgeTone.Warning);
            Grid.SetColumn(_badge, 2);
            header.Children.Add(_badge);

            _actions = ArcUi.HStack(6);
            var approve = ArcUi.Button("Approve", ArcButtonVariant.Primary, ArcButtonSize.Sm, "check");
            var deny = ArcUi.Button("Deny", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "x");
            ArcUi.Name(approve, $"Approve {request.ToolName}");
            ArcUi.Name(deny, $"Deny {request.ToolName}");
            approve.Click += (_, _) => _decision(_requestId, true, null);
            deny.Click += (_, _) => _decision(_requestId, false, "Denied by the user.");
            _actions.Children.Add(approve);
            _actions.Children.Add(deny);

            _state = ArcUi.Caption(string.Empty);
            _state.IsVisible = false;

            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(header);
            stack.Children.Add(_actions);
            stack.Children.Add(_state);
            Control = ArcUi.Surface(stack, 10, radius: 18);
        }

        internal Control Control { get; }

        internal void Apply(ArcApprovalState state)
        {
            _badge.Content = state switch
            {
                ArcApprovalState.Approved => "Approved",
                ArcApprovalState.Denied => "Denied",
                ArcApprovalState.Expired => "Expired",
                ArcApprovalState.Deferred => "Deferred",
                _ => "Needs approval"
            };
            _badge.Tone = state switch
            {
                ArcApprovalState.Approved => ArcBadgeTone.Success,
                ArcApprovalState.Denied => ArcBadgeTone.Neutral,
                ArcApprovalState.Expired => ArcBadgeTone.Danger,
                ArcApprovalState.Deferred => ArcBadgeTone.Warning,
                _ => ArcBadgeTone.Warning
            };
            _actions.IsVisible = false;
            _state.Text = state switch
            {
                ArcApprovalState.Approved => "Approved by the user.",
                ArcApprovalState.Denied => "Denied by the user.",
                ArcApprovalState.Expired => "The request expired before a decision.",
                ArcApprovalState.Deferred => "Deferred; the host decides when to ask again.",
                _ => string.Empty
            };
            _state.IsVisible = !string.IsNullOrWhiteSpace(_state.Text);
        }
    }
}
