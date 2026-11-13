using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using PuraMarkdown.Agent;
using PuraMarkdown.Agent.Chat;
using PuraMarkdown.UI;
using PuraMarkdown.UI.Rendering;

namespace Arc.Avalonia.Agent;

/// <summary>
/// The agent transcript. Rendering is fully delegated to the upstream chat surface: the projection writes
/// <see cref="PuraChatMessage"/> rows (Markdown body, <see cref="PuraToolCall"/> cards, status/error) into an
/// <see cref="ArcChatView"/>, and the pura-agent components (choice / confirm) come from the upstream
/// <c>AgentPlugin</c> pipeline through synthesized <c>pura-agent</c> fences for pending questions/approvals.
/// Arc keeps only the presentation runtime (workspace/session reducer, approval queue) and this projection.
/// </summary>
public sealed class ArcAgentConversation : ContentControl, IDisposable
{
    /// <summary>Workspace projected into the transcript.</summary>
    public static readonly StyledProperty<IArcAgentWorkspace?> WorkspaceProperty =
        AvaloniaProperty.Register<ArcAgentConversation, IArcAgentWorkspace?>(nameof(Workspace));

    /// <summary>Single session shown instead of the workspace's active session.</summary>
    public static readonly StyledProperty<IArcAgentSession?> SessionProperty =
        AvaloniaProperty.Register<ArcAgentConversation, IArcAgentSession?>(nameof(Session));

    /// <summary>Render options; null keeps the Arc token adapter defaults.</summary>
    public static readonly StyledProperty<PuraMarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ArcAgentConversation, PuraMarkdownOptions?>(nameof(MarkdownOptions));

    /// <summary>Shows the header row.</summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<ArcAgentConversation, bool>(nameof(ShowHeader), true);

    /// <summary>Transcript title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcAgentConversation, string>(nameof(Title), "Conversation");

    /// <summary>Placeholder shown while there are no messages.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ArcAgentConversation, string>(nameof(EmptyText), "No messages yet.");

    /// <summary>Raised when an approval card reports approve/deny (routed companion).</summary>
    public static readonly RoutedEvent<ArcAgentApprovalDecisionEventArgs> ApprovalDecisionRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentApprovalDecisionEventArgs>(
            "ApprovalDecisionRequested", RoutingStrategies.Bubble);

    /// <summary>Raised when a choice card reports an answer or deferral (routed companion).</summary>
    public static readonly RoutedEvent<ArcAgentQuestionAnswerEventArgs> AnswerRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentQuestionAnswerEventArgs>(
            "AnswerRequested", RoutingStrategies.Bubble);

    /// <summary>Raised when a failed tool card asks to retry (routed companion; upstream cards have no actions).</summary>
    public static readonly RoutedEvent<ArcAgentToolRetryEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ArcAgentConversation, ArcAgentToolRetryEventArgs>(
            "RetryRequested", RoutingStrategies.Bubble);

    /// <summary>Prefix reserved for projection rows that do not come from session messages.</summary>
    private const string SyntheticPrefix = "arc:";

    private readonly Grid _root;
    private readonly Grid _header;
    private readonly TextBlock _titleText;
    private readonly TextBlock _summary;
    private readonly TextBlock _emptyCopy;
    private readonly Border _emptyHost;
    private readonly ArcChatView _chat;
    private readonly Dictionary<string, PuraChatMessage> _projected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _choiceOptions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _localNotices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _stepTitles = new(StringComparer.Ordinal);

    private IArcAgentWorkspace? _workspace;
    private IArcAgentSession? _session;
    private int _timelineCursor;
    private bool _syncing;
    private bool _disposed;

    /// <summary>Creates the transcript surface and its chrome.</summary>
    public ArcAgentConversation()
    {
        ClipToBounds = false;

        _titleText = ArcUi.Label(Title);
        _summary = ArcUi.Caption(string.Empty);
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(_titleText);
        titles.Children.Add(_summary);

        _header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        _header.Children.Add(titles);

        _chat = new ArcChatView
        {
            ShowComposer = false,
            AutoScrollMode = ArcChatAutoScrollMode.NearBottom,
        };
        _chat.InteractionOccurred += OnChatInteraction;
        _chat.Host.Warning += OnChatWarning;

        _emptyCopy = ArcUi.Caption(EmptyText);
        _emptyCopy.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyCopy.VerticalAlignment = VerticalAlignment.Center;
        _emptyCopy.Opacity = 0.7;
        _emptyHost = new Border
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _emptyCopy,
        };

        var body = new Grid();
        body.Children.Add(_chat);
        body.Children.Add(_emptyHost);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 8 };
        grid.Children.Add(_header);
        Grid.SetRow(body, 1);
        grid.Children.Add(body);

        _root = new Grid();
        _root.Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(12),
            Child = grid,
        });
        Content = _root;

        ArcUi.Name(
            this,
            "Agent conversation",
            "A transcript of messages, tool calls, approvals and questions, rendered by PuraMarkdown.Agent.");
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

    /// <summary>Render options (pipeline + theme); null uses the Arc token adapter defaults.</summary>
    public PuraMarkdownOptions? MarkdownOptions
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

    /// <summary>Transcript title.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Placeholder shown while there are no messages.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>The upstream chat surface host (advanced scenarios: snapshots, scroll).</summary>
    public ArcChatView Chat => _chat;

    /// <summary>The session currently projected, if any.</summary>
    public IArcAgentSession? ActiveSession => _session;

    /// <summary>Accepted events of the projected session (replay/debug record).</summary>
    public IReadOnlyList<ArcAgentEvent> Timeline => _session?.Timeline ?? Array.Empty<ArcAgentEvent>();

    /// <summary>Raised when an approval card reports approve/deny (CLR companion).</summary>
    public event EventHandler<ArcAgentApprovalDecisionEventArgs>? ApprovalDecisionRequested;

    /// <summary>Bubbling companion to <see cref="ApprovalDecisionRequested"/>.</summary>
    public event EventHandler<ArcAgentApprovalDecisionEventArgs>? ApprovalDecisionRequestedRouted
    {
        add => AddHandler(ApprovalDecisionRequestedEvent, value);
        remove => RemoveHandler(ApprovalDecisionRequestedEvent, value);
    }

    /// <summary>Raised when a choice card reports an answer (CLR companion).</summary>
    public event EventHandler<ArcAgentQuestionAnswerEventArgs>? AnswerRequested;

    /// <summary>Bubbling companion to <see cref="AnswerRequested"/>.</summary>
    public event EventHandler<ArcAgentQuestionAnswerEventArgs>? AnswerRequestedRouted
    {
        add => AddHandler(AnswerRequestedEvent, value);
        remove => RemoveHandler(AnswerRequestedEvent, value);
    }

    /// <summary>
    /// Raised when a failed tool card asks to retry (CLR companion). Kept for API compatibility: the upstream
    /// tool-call card is display-only, so the conversation no longer produces this event by itself.
    /// </summary>
#pragma warning disable CS0067 // Kept for API compatibility; the upstream tool card is display-only.
    public event EventHandler<ArcAgentToolRetryEventArgs>? RetryRequested;
#pragma warning restore CS0067

    /// <summary>Bubbling companion to <see cref="RetryRequested"/>.</summary>
    public event EventHandler<ArcAgentToolRetryEventArgs>? RetryRequestedRouted
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>Forwarded upstream interaction events (choice/confirm/tool-call components).</summary>
    public event EventHandler<PuraInteractionEventArgs>? InteractionOccurred;

    /// <summary>Forwarded upstream render warnings (limits, unsafe links...).</summary>
    public event EventHandler<PuraWarningEventArgs>? Warning;

    /// <summary>Adds a local system notice (host/transport errors); it survives reruns of the projection.</summary>
    public void AppendSystemNotice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var id = SyntheticPrefix + "notice:" + Guid.NewGuid().ToString("N");
        _localNotices.Add(id);
        EnsureSynthetic(id, "> " + text);
        UpdateEmptyState();
    }

    /// <summary>Re-projects the workspace/session into the transcript.</summary>
    public void Refresh() => Sync();

    /// <summary>Scrolls to the newest row.</summary>
    public void ScrollToEnd() => _chat.ScrollToEnd();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WorkspaceProperty)
        {
            AttachWorkspace(change.GetNewValue<IArcAgentWorkspace?>());
        }
        else if (change.Property == SessionProperty)
        {
            AttachSession(change.GetNewValue<IArcAgentSession?>());
        }
        else if (change.Property == MarkdownOptionsProperty && change.GetNewValue<PuraMarkdownOptions?>() is { } options)
        {
            _chat.Options = options;
        }
        else if (change.Property == ShowHeaderProperty || change.Property == TitleProperty)
        {
            UpdateHeader();
        }
        else if (change.Property == EmptyTextProperty)
        {
            _emptyCopy.Text = EmptyText;
            UpdateEmptyState();
        }
    }

    private void AttachWorkspace(IArcAgentWorkspace? workspace)
    {
        if (ReferenceEquals(_workspace, workspace))
        {
            return;
        }

        if (_workspace is not null)
        {
            _workspace.Changed -= OnWorkspaceChanged;
        }

        _workspace = workspace;
        if (_workspace is not null)
        {
            _workspace.Changed += OnWorkspaceChanged;
        }

        AttachSession(ResolveSession());
    }

    private void AttachSession(IArcAgentSession? session)
    {
        if (ReferenceEquals(_session, session))
        {
            Sync();
            return;
        }

        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
        }

        _session = session;
        _timelineCursor = 0;
        _choiceOptions.Clear();
        _stepTitles.Clear();
        ClearProjected(keepLocalNotices: true);

        if (_session is not null)
        {
            _session.Changed += OnSessionChanged;
        }

        Sync();
    }

    private IArcAgentSession? ResolveSession() => Session ?? _workspace?.Active;

    private void OnWorkspaceChanged(object? sender, ArcAgentWorkspaceChangedEventArgs e) => Sync();

    private void OnSessionChanged(object? sender, ArcAgentSessionChangedEventArgs e) => Sync();

    private void Sync()
    {
        if (_disposed || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var session = ResolveSession();
            if (!ReferenceEquals(session, _session))
            {
                // AttachSession calls Sync() itself, but that nested call is reentrancy-guarded; keep
                // projecting in this pass so the first event after attaching is never dropped.
                AttachSession(session);
            }

            var follow = _chat.Host.IsNearBottom;
            var streaming = SyncMessages(session);
            SyncTimeline(session);
            UpdateHeader();
            UpdateEmptyState();

            if (follow && streaming && _chat.AutoScrollMode != ArcChatAutoScrollMode.Never)
            {
                _chat.ScrollToEnd();
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Projects session messages and tool calls; returns true while the newest message streams.</summary>
    private bool SyncMessages(IArcAgentSession? session)
    {
        if (session is null)
        {
            ClearProjected(keepLocalNotices: true);
            return false;
        }

        var live = new HashSet<string>(StringComparer.Ordinal);
        var streaming = false;
        PuraChatMessage? toolHost = null;
        foreach (var source in session.Messages)
        {
            var copy = EnsureProjected(source);
            var markdown = FormatMessage(source);
            if (!string.Equals(copy.Markdown, markdown, StringComparison.Ordinal))
            {
                copy.Markdown = markdown;
            }

            if (copy.Status != source.Status)
            {
                copy.Status = source.Status;
            }

            if (!string.Equals(copy.Error, source.Error, StringComparison.Ordinal))
            {
                copy.Error = source.Error;
            }

            if (source.Role is PuraChatRole.Assistant or PuraChatRole.Tool)
            {
                toolHost = copy;
            }

            streaming |= source.Status == PuraChatMessageStatus.Streaming;
            live.Add(source.Id);
        }

        var calls = session.ToolCalls.Select(MapToolCall).ToArray();
        if (calls.Length > 0)
        {
            toolHost ??= EnsureSynthetic(SyntheticPrefix + "tools:" + session.SessionId, string.Empty);
            live.Add(toolHost.Id);
            var signature = ToolSignature(calls);
            if (!string.Equals(GetSignature(toolHost.Id), signature, StringComparison.Ordinal))
            {
                toolHost.ToolCalls = calls;
                _signatures[toolHost.Id] = signature;
            }
        }

        foreach (var id in _projected.Keys
                     .Where(id => !live.Contains(id) && !_localNotices.Contains(id) && !id.StartsWith(SyntheticPrefix, StringComparison.Ordinal))
                     .ToArray())
        {
            RemoveProjected(id);
        }

        return streaming;
    }

    private readonly Dictionary<string, string> _signatures = new(StringComparer.Ordinal);

    private string GetSignature(string id) => _signatures.TryGetValue(id, out var value) ? value : string.Empty;

    private static string ToolSignature(IReadOnlyList<PuraToolCall> calls) => string.Join(
        "|",
        calls.Select(call => $"{call.Id}:{call.Status}:{call.Arguments?.Length}:{call.Result?.Length}:{call.Error}"));

    private void SyncTimeline(IArcAgentSession? session)
    {
        if (session is null)
        {
            _timelineCursor = 0;
            return;
        }

        var timeline = session.Timeline;
        if (_timelineCursor > timeline.Count)
        {
            _timelineCursor = 0;
            ClearSynthetics(keepLocalNotices: true);
        }

        for (var index = _timelineCursor; index < timeline.Count; index++)
        {
            HandleTimelineEvent(timeline[index]);
        }

        _timelineCursor = timeline.Count;
    }

    private void HandleTimelineEvent(ArcAgentEvent @event)
    {
        switch (@event)
        {
            case ArcStateSnapshotEvent:
                // A snapshot replaces the projection: drop synthetic rows and let the next sync rebuild them.
                ClearSynthetics(keepLocalNotices: true);
                break;

            case ArcQuestionRequestedEvent question:
                _choiceOptions[question.Request.RequestId] = question.Request.Options.ToArray();
                EnsureSynthetic(
                    SyntheticPrefix + "question:" + question.Request.RequestId,
                    ChoiceFence(question.Request));
                break;

            case ArcApprovalRequestedEvent approval:
                EnsureSynthetic(
                    SyntheticPrefix + "approval:" + approval.Request.RequestId,
                    ConfirmFence(approval.Request));
                break;

            case ArcApprovalResolvedEvent resolved:
                RemoveProjected(SyntheticPrefix + "approval:" + resolved.RequestId);
                break;

            case ArcStepStartedEvent step:
                _stepTitles[step.StepId] = step.Title;
                EnsureSynthetic(
                    SyntheticPrefix + "step:" + step.StepId,
                    "**▸ " + step.Title + "**");
                break;

            case ArcStepFinishedEvent finished:
                var stepId = SyntheticPrefix + "step:" + finished.StepId;
                if (_projected.TryGetValue(stepId, out var stepRow))
                {
                    var title = _stepTitles.TryGetValue(finished.StepId, out var known) && !string.IsNullOrWhiteSpace(known)
                        ? " " + known
                        : string.Empty;
                    var icon = finished.Status switch
                    {
                        ArcStepStatus.Done => "✓",
                        ArcStepStatus.Failed => "✕",
                        ArcStepStatus.Skipped => "–",
                        _ => "▸",
                    };
                    stepRow.Markdown = "**" + icon + title + "**";
                }

                break;

            case ArcArtifactPublishedEvent artifact:
                EnsureSynthetic(
                    SyntheticPrefix + "artifact:" + artifact.Artifact.Title + ":" + artifact.Artifact.At.Ticks,
                    ArtifactMarkdown(artifact.Artifact));
                break;
        }
    }

    private PuraChatMessage EnsureProjected(PuraChatMessage source)
    {
        if (_projected.TryGetValue(source.Id, out var existing) && existing.Role == source.Role)
        {
            return existing;
        }

        if (existing is not null)
        {
            RemoveProjected(source.Id);
        }

        var copy = new PuraChatMessage
        {
            Id = source.Id,
            Role = source.Role,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
        };
        copy.Markdown = FormatMessage(source);
        _projected[source.Id] = copy;
        _chat.AddMessage(copy);
        return copy;
    }

    private PuraChatMessage EnsureSynthetic(string id, string markdown)
    {
        if (_projected.TryGetValue(id, out var existing))
        {
            if (!string.Equals(existing.Markdown, markdown, StringComparison.Ordinal))
            {
                existing.Markdown = markdown;
            }

            return existing;
        }

        var created = new PuraChatMessage
        {
            Id = id,
            Role = PuraChatRole.Assistant,
            Status = PuraChatMessageStatus.Complete,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        created.Markdown = markdown;
        _projected[id] = created;
        _chat.AddMessage(created);
        return created;
    }

    private void RemoveProjected(string id)
    {
        if (!_projected.Remove(id))
        {
            return;
        }

        _signatures.Remove(id);
        _localNotices.Remove(id);
        _chat.RemoveMessage(id);
    }

    private void ClearProjected(bool keepLocalNotices)
    {
        foreach (var id in _projected.Keys.ToArray())
        {
            if (keepLocalNotices && _localNotices.Contains(id))
            {
                continue;
            }

            RemoveProjected(id);
        }

        _signatures.Clear();
        _projected.Clear();
    }

    private void ClearSynthetics(bool keepLocalNotices)
    {
        foreach (var id in _projected.Keys
                     .Where(id => id.StartsWith(SyntheticPrefix, StringComparison.Ordinal) &&
                                  !(keepLocalNotices && _localNotices.Contains(id)))
                     .ToArray())
        {
            RemoveProjected(id);
        }
    }

    private void OnChatInteraction(object? sender, PuraInteractionEventArgs args)
    {
        InteractionOccurred?.Invoke(this, args);

        switch (args.ComponentType)
        {
            case "choice":
                var requestId = args.ComponentId ?? string.Empty;
                var selected = _choiceOptions.TryGetValue(requestId, out var options)
                    ? args.SelectedIds
                        .Select(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                                      index >= 0 && index < options.Length
                            ? options[index]
                            : id)
                        .ToArray()
                    : args.SelectedIds.ToArray();
                _choiceOptions.Remove(requestId);
                RemoveProjected(SyntheticPrefix + "question:" + requestId);
                RaiseAnswer(new ArcAgentQuestionAnswerEventArgs(AnswerRequestedEvent)
                {
                    RequestId = requestId,
                    SelectedOptions = selected,
                });
                break;

            case "confirm":
                var approvalId = args.ComponentId ?? string.Empty;
                RemoveProjected(SyntheticPrefix + "approval:" + approvalId);
                RaiseApproval(new ArcAgentApprovalDecisionEventArgs(ApprovalDecisionRequestedEvent)
                {
                    RequestId = approvalId,
                    Approved = args.Confirmed == true,
                });
                break;
        }
    }

    private void OnChatWarning(object? sender, PuraWarningEventArgs args) => Warning?.Invoke(this, args);

    private void RaiseApproval(ArcAgentApprovalDecisionEventArgs args)
    {
        ApprovalDecisionRequested?.Invoke(this, args);
        RaiseEvent(args);
    }

    private void RaiseAnswer(ArcAgentQuestionAnswerEventArgs args)
    {
        AnswerRequested?.Invoke(this, args);
        RaiseEvent(args);
    }

    private void UpdateHeader()
    {
        _titleText.Text = Title;
        _header.IsVisible = ShowHeader;

        var session = _session ?? ResolveSession();
        if (session is null)
        {
            _summary.Text = "No active run";
            return;
        }

        var step = string.IsNullOrWhiteSpace(session.Run.CurrentStep) ? string.Empty : " · " + session.Run.CurrentStep;
        _summary.Text = $"{session.Run.Phase}{step} · {session.Messages.Count} msg · {session.ToolCalls.Count} tools";
    }

    private void UpdateEmptyState() => _emptyHost.IsVisible = _chat.Messages.Count == 0;


    private static string FormatMessage(PuraChatMessage message)
    {
        var prefix = message.Role switch
        {
            PuraChatRole.User => "**You**\n\n",
            PuraChatRole.System => "> ",
            PuraChatRole.Tool => "**Tool result**\n\n",
            _ => string.Empty,
        };
        var body = prefix + message.Markdown;
        if (message.Status is PuraChatMessageStatus.Failed or PuraChatMessageStatus.Aborted &&
            !string.IsNullOrWhiteSpace(message.Error))
        {
            body += "\n\n---\n\n*" + message.Error + "*";
        }

        return body;
    }

    private static PuraToolCall MapToolCall(PuraToolCall call) => call;

    private static string ChoiceFence(ArcQuestionRequest request)
    {
        var options = request.Options
            .Select((option, index) => new Dictionary<string, object?>
            {
                ["id"] = index.ToString(CultureInfo.InvariantCulture),
                ["label"] = option,
            })
            .ToArray();
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "choice",
            ["version"] = "1.0",
            ["id"] = request.RequestId,
            ["question"] = request.Question,
            ["options"] = options,
            ["multi"] = false,
        };
        var note = request.AllowFreeText ? "\n\n> 也可以直接输入回答。" : string.Empty;
        return Fence(payload, note);
    }

    private static string ConfirmFence(ArcApprovalRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "confirm",
            ["version"] = "1.0",
            ["id"] = request.RequestId,
            ["question"] = $"Approve \"{request.ToolName}\"?",
            ["description"] = request.Intent,
            ["danger"] = request.Risk == ArcRiskLevel.High,
            ["confirmLabel"] = "Approve",
            ["cancelLabel"] = "Deny",
        };
        return Fence(payload);
    }

    private static string ArtifactMarkdown(ArcArtifact artifact)
    {
        var text = $"📄 **{artifact.Title}**";
        if (!string.IsNullOrWhiteSpace(artifact.Subtitle))
        {
            text += "\n\n" + artifact.Subtitle;
        }

        if (!string.IsNullOrWhiteSpace(artifact.DisplayText))
        {
            text += "\n\n`" + artifact.DisplayText.Replace("`", "'", StringComparison.Ordinal) + "`";
        }

        return text;
    }

    private static string Fence(object payload, string? note = null) =>
        "```pura-agent\n" + JsonSerializer.Serialize(payload) + "\n```" + (note ?? string.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
            _session = null;
        }

        if (_workspace is not null)
        {
            _workspace.Changed -= OnWorkspaceChanged;
            _workspace = null;
        }

        _chat.InteractionOccurred -= OnChatInteraction;
        _chat.Host.Warning -= OnChatWarning;
        _chat.Dispose();
        GC.SuppressFinalize(this);
    }
}
