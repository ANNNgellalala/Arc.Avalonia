using System;
using System.Collections.Generic;
using System.Text;

namespace Arc.Avalonia.Agent;

/// <summary>Where a run lives inside the workspace. The leaf controls already differentiate these: the
/// background dock shows <see cref="Background"/>, the attention inbox aggregates across all kinds, and the
/// sub-agent board renders <see cref="SubAgent"/> under its <c>ParentRunId</c>.</summary>
public enum ArcAgentRunKind
{
    Foreground,
    Background,
    SubAgent
}

/// <summary>The visible stage of one run. The host drives transitions; the top layer only projects them.</summary>
public enum ArcRunPhase
{
    Idle,
    Planning,
    Running,
    AwaitingApproval,
    AwaitingInput,
    Paused,
    Done,
    Failed,
    Stopped,
    TakenOver
}

/// <summary>Who produced a message in the conversation.</summary>
public enum ArcAgentRole
{
    User,
    Assistant,
    Reasoning,
    Tool,
    System,
    SubAgent
}

/// <summary>Delivery state of one message.</summary>
public enum ArcMessageStatus
{
    Pending,
    Streaming,
    Done,
    Failed,
    Skipped
}

/// <summary>Lifecycle of one tool call.</summary>
public enum ArcToolCallStatus
{
    Pending,
    Running,
    AwaitingApproval,
    Succeeded,
    Failed,
    Denied
}

/// <summary>Lifecycle of one plan step.</summary>
public enum ArcStepStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped
}

/// <summary>Decision state of one approval request.</summary>
public enum ArcApprovalState
{
    Pending,
    Approved,
    Denied,
    Deferred,
    Expired
}

/// <summary>How long an approval applies. The top layer displays the choice; the host enforces it.</summary>
public enum ArcApprovalScope
{
    Once,
    Task,
    Hour,
    Always
}

/// <summary>What an attention entry is about.</summary>
public enum ArcAttentionKind
{
    Approval,
    Question,
    Failure,
    Routine,
    Suggestion,
    Tool
}

/// <summary>How urgently an attention entry needs the person.</summary>
public enum ArcAttentionLevel
{
    Info,
    Important,
    Blocking
}

/// <summary>Lifecycle of an attention entry.</summary>
public enum ArcAttentionState
{
    New,
    Seen,
    InProgress,
    Resolved,
    Expired
}

/// <summary>Kind of artifact a run produced.</summary>
public enum ArcArtifactKind
{
    File,
    Diff,
    Url,
    Json,
    Text,
    Image,
    Table
}

/// <summary>Risk level shown on tool and approval surfaces.</summary>
public enum ArcRiskLevel
{
    Low,
    Medium,
    High
}

/// <summary>Result of feeding one event into a session or the workspace.</summary>
public enum ArcApplyResult
{
    /// <summary>The event was applied in sequence.</summary>
    Accepted,
    /// <summary>The event id was already seen (idempotent replay).</summary>
    Duplicate,
    /// <summary>The sequence has a gap; the event was buffered until the gap fills.</summary>
    OutOfOrder,
    /// <summary>No session exists for the run id (yet); buffered as an orphan.</summary>
    UnknownRun,
    /// <summary>The event was buffered after the orphan window was exhausted.</summary>
    Orphaned
}

/// <summary>Coarse change category raised by a session or workspace.</summary>
public enum ArcAgentChangeKind
{
    SessionCreated,
    EventApplied,
    PhaseChanged,
    QueueChanged,
    Reset
}

/// <summary>Stable identity for an event: the run, the monotonic sequence and an idempotency key.</summary>
public readonly record struct ArcEventStamp(string RunId, long Sequence, string EventId)
{
    /// <summary>Convenience factory that derives the event id from run + sequence.</summary>
    public static ArcEventStamp Create(string runId, long sequence, string? eventId = null) =>
        new(runId, sequence, eventId ?? $"{runId}:{sequence}");
}

/// <summary>The run a session projects. Cross-run references (attention, artifacts, approvals, sub-agents)
/// carry this record so the host can route a command without knowing the UI object graph.</summary>
public readonly record struct ArcAgentRunRef(
    string RunId,
    string SessionId,
    ArcAgentRunKind Kind = ArcAgentRunKind.Foreground,
    string? ParentRunId = null,
    string? Label = null)
{
    /// <summary>True when this run is a background run (the dock consumes it).</summary>
    public bool IsBackground => Kind == ArcAgentRunKind.Background;

    /// <summary>True when this run is a parallel sub-agent (the board consumes it).</summary>
    public bool IsSubAgent => Kind == ArcAgentRunKind.SubAgent;
}

/// <summary>One event in the agent event stream. The top layer only projects these; it never creates them.</summary>
public abstract record ArcAgentEvent(ArcEventStamp Stamp, DateTimeOffset At)
{
    /// <summary>The event category, useful for diagnostics and the item view registry.</summary>
    public abstract ArcAgentEventKind EventKind { get; }
}

/// <summary>Event families understood by the presentation runtime.</summary>
public enum ArcAgentEventKind
{
    RunStarted,
    RunPhaseChanged,
    StepStarted,
    StepFinished,
    MessageStarted,
    MessageDelta,
    MessageEnded,
    ReasoningDelta,
    ToolCallStarted,
    ToolArgsDelta,
    ToolCallEnded,
    ApprovalRequested,
    ApprovalResolved,
    QuestionRequested,
    AttentionRaised,
    ArtifactPublished,
    StateSnapshot,
    StateDelta,
    Custom
}

/// <summary>A run is now known to the workspace. Every other event for that run must follow it.</summary>
public sealed record ArcRunStartedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcAgentRunRef Run)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.RunStarted;
}

/// <summary>The run moved to a new visible phase.</summary>
public sealed record ArcRunPhaseChangedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcRunPhase Phase)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.RunPhaseChanged;
}

/// <summary>A plan step started.</summary>
public sealed record ArcStepStartedEvent(ArcEventStamp Stamp, DateTimeOffset At, string StepId, string Title)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.StepStarted;
}

/// <summary>A plan step finished.</summary>
public sealed record ArcStepFinishedEvent(ArcEventStamp Stamp, DateTimeOffset At, string StepId, ArcStepStatus Status)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.StepFinished;
}

/// <summary>A message began streaming.</summary>
public sealed record ArcMessageStartedEvent(ArcEventStamp Stamp, DateTimeOffset At, string MessageId, ArcAgentRole Role)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.MessageStarted;
}

/// <summary>A token/chunk delta for an existing message. Append-only; the reducer keeps it O(1).</summary>
public sealed record ArcMessageDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At, string MessageId, string Delta)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.MessageDelta;
}

/// <summary>A message finished streaming (successfully or with a failure status).</summary>
public sealed record ArcMessageEndedEvent(ArcEventStamp Stamp, DateTimeOffset At, string MessageId, ArcMessageStatus Status = ArcMessageStatus.Done)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.MessageEnded;
}

/// <summary>Reasoning/thinking delta; rendered as a collapsible thinking row.</summary>
public sealed record ArcReasoningDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At, string ReasoningId, string Delta)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ReasoningDelta;
}

/// <summary>A tool call started.</summary>
public sealed record ArcToolCallStartedEvent(
    ArcEventStamp Stamp,
    DateTimeOffset At,
    string CallId,
    string ToolName,
    string Intent,
    ArcRiskLevel Risk = ArcRiskLevel.Low)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ToolCallStarted;
}

/// <summary>Tool arguments streamed as a JSON fragment. Append-only and order-sensitive.</summary>
public sealed record ArcToolArgsDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At, string CallId, string DeltaJson)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ToolArgsDelta;
}

/// <summary>A tool call finished.</summary>
public sealed record ArcToolCallEndedEvent(
    ArcEventStamp Stamp,
    DateTimeOffset At,
    string CallId,
    ArcToolCallStatus Status,
    string? ResultSummary = null)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ToolCallEnded;
}

/// <summary>The host asks the person to approve a risky tool call.</summary>
public sealed record ArcApprovalRequestedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcApprovalRequest Request)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ApprovalRequested;
}

/// <summary>The host resolved an approval request (approved/denied/expired...).</summary>
public sealed record ArcApprovalResolvedEvent(
    ArcEventStamp Stamp,
    DateTimeOffset At,
    string RequestId,
    ArcApprovalState State)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ApprovalResolved;
}

/// <summary>The host asks a structured question; the answer is a command, not a state change.</summary>
public sealed record ArcQuestionRequestedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcQuestionRequest Request)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.QuestionRequested;
}

/// <summary>Something needs the person's attention outside the current conversation.</summary>
public sealed record ArcAttentionRaisedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcAttentionEntry Entry)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.AttentionRaised;
}

/// <summary>A run produced an artifact (file/diff/url/json...). The host supplies the display model.</summary>
public sealed record ArcArtifactPublishedEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcArtifact Artifact)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.ArtifactPublished;
}

/// <summary>A full snapshot used to restore a conversation. Replaces the session projection.</summary>
public sealed record ArcStateSnapshotEvent(ArcEventStamp Stamp, DateTimeOffset At, ArcAgentSessionSnapshot Snapshot)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.StateSnapshot;
}

/// <summary>An opaque host state delta. The MVP accepts it without interpreting it.</summary>
public sealed record ArcStateDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At, string DeltaJson)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.StateDelta;
}

/// <summary>Escape hatch for host-private event shapes; rendered as a plain row.</summary>
public sealed record ArcCustomEvent(ArcEventStamp Stamp, DateTimeOffset At, string Kind, string Payload)
    : ArcAgentEvent(Stamp, At)
{
    /// <inheritdoc />
    public override ArcAgentEventKind EventKind => ArcAgentEventKind.Custom;
}

/// <summary>One streamed message. Text is append-only so a token delta costs O(1); the string is cached
/// only when a view asks for it (once per flush, not once per delta).</summary>
public sealed class ArcAgentMessage
{
    private readonly StringBuilder _text = new();
    private string? _cached;

    /// <summary>Stable id used by the transcript row and the view registry.</summary>
    public required string Id { get; init; }

    /// <summary>Who produced the message.</summary>
    public ArcAgentRole Role { get; init; }

    /// <summary>Delivery state.</summary>
    public ArcMessageStatus Status { get; set; }

    /// <summary>When the message started.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>The accumulated text.</summary>
    public string Text => _cached ??= _text.ToString();

    /// <summary>True while deltas are still arriving.</summary>
    public bool IsStreaming => Status == ArcMessageStatus.Streaming;

    internal void Append(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        _text.Append(delta);
        _cached = null;
    }

    internal void SetText(string text)
    {
        _text.Clear();
        _text.Append(text);
        _cached = null;
    }
}

/// <summary>One tool call in a session.</summary>
public sealed class ArcToolCall
{
    private readonly StringBuilder _arguments = new();
    private string? _cachedArguments;

    /// <summary>Stable call id.</summary>
    public required string Id { get; init; }

    /// <summary>The tool name shown as the card subtitle.</summary>
    public required string ToolName { get; init; }

    /// <summary>Plain-language intent shown above the arguments.</summary>
    public string Intent { get; init; } = string.Empty;

    /// <summary>Risk level used by the approval card.</summary>
    public ArcRiskLevel Risk { get; init; }

    /// <summary>Lifecycle status.</summary>
    public ArcToolCallStatus Status { get; set; }

    /// <summary>Accumulated JSON arguments (display only; the host owns parsing).</summary>
    public string Arguments => _cachedArguments ??= _arguments.ToString();

    /// <summary>Optional result summary.</summary>
    public string? ResultSummary { get; set; }

    internal void AppendArguments(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        _arguments.Append(delta);
        _cachedArguments = null;
    }
}

/// <summary>One plan step in a session.</summary>
public sealed class ArcAgentStep
{
    /// <summary>Stable step id.</summary>
    public required string Id { get; init; }

    /// <summary>Step title.</summary>
    public required string Title { get; init; }

    /// <summary>Step state.</summary>
    public ArcStepStatus Status { get; set; }
}

/// <summary>An approval argument the person may edit before approving.</summary>
public sealed record ArcApprovalArgument(string Name, string Value, bool Editable = false);

/// <summary>A request to approve one risky tool call.</summary>
public sealed class ArcApprovalRequest
{
    /// <summary>Stable request id (idempotency key for decisions).</summary>
    public required string RequestId { get; init; }

    /// <summary>The run that requested it.</summary>
    public required string RunId { get; init; }

    /// <summary>The tool call id.</summary>
    public required string CallId { get; init; }

    /// <summary>The tool being called.</summary>
    public required string ToolName { get; init; }

    /// <summary>Plain-language intent.</summary>
    public string Intent { get; init; } = string.Empty;

    /// <summary>Risk level.</summary>
    public ArcRiskLevel Risk { get; init; }

    /// <summary>Arguments as proposed by the agent.</summary>
    public IReadOnlyList<ArcApprovalArgument> Arguments { get; init; } = Array.Empty<ArcApprovalArgument>();

    /// <summary>Side-effect lines rendered as a preview.</summary>
    public IReadOnlyList<string> SideEffects { get; init; } = Array.Empty<string>();

    /// <summary>When the request expires; null means no expiry shown.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>The decision the person sent back. The top layer emits it; the host enforces it.</summary>
public sealed record ArcApprovalDecision(
    string RequestId,
    bool Approved,
    ArcApprovalScope Scope = ArcApprovalScope.Once,
    IReadOnlyList<ArcApprovalArgument>? ModifiedArguments = null,
    string? DenyReason = null,
    string? DelegateTo = null);

/// <summary>One "waiting on you" entry aggregated by the workspace.</summary>
public sealed class ArcAttentionEntry
{
    /// <summary>Stable id; the workspace de-duplicates on it.</summary>
    public required string EntryId { get; init; }

    /// <summary>The run this entry belongs to.</summary>
    public required string RunId { get; init; }

    /// <summary>Entry kind, used for grouping.</summary>
    public ArcAttentionKind Kind { get; init; }

    /// <summary>One-line title.</summary>
    public required string Title { get; init; }

    /// <summary>One-line context.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Attention level.</summary>
    public ArcAttentionLevel Level { get; init; }

    /// <summary>Lifecycle state.</summary>
    public ArcAttentionState State { get; set; }

    /// <summary>When the entry was raised.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>An artifact produced by a run.</summary>
public sealed class ArcArtifact
{
    /// <summary>Stable artifact id.</summary>
    public required string ArtifactId { get; init; }

    /// <summary>Artifact kind.</summary>
    public required ArcArtifactKind Kind { get; init; }

    /// <summary>Display title.</summary>
    public required string Title { get; init; }

    /// <summary>Optional subtitle.</summary>
    public string? Subtitle { get; init; }

    /// <summary>MIME type supplied by the host.</summary>
    public string? MimeType { get; init; }

    /// <summary>Size in bytes supplied by the host.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Display text already read by the host; the top layer never reads files.</summary>
    public string? DisplayText { get; init; }

    /// <summary>When the artifact was published.</summary>
    public DateTimeOffset At { get; init; }
}

/// <summary>A structured question from the agent.</summary>
public sealed class ArcQuestionRequest
{
    /// <summary>Stable request id.</summary>
    public required string RequestId { get; init; }

    /// <summary>The run asking.</summary>
    public required string RunId { get; init; }

    /// <summary>The question text.</summary>
    public required string Question { get; init; }

    /// <summary>Selectable options.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>Whether a free-text answer is allowed.</summary>
    public bool AllowFreeText { get; init; }

    /// <summary>When the question expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>The answer to a structured question, sent as a command.</summary>
public sealed record ArcQuestionAnswer(
    string RequestId,
    IReadOnlyList<string> SelectedOptions,
    string? FreeText = null,
    bool Deferred = false);

/// <summary>Display snapshot of a run's projection. Used by <see cref="ArcStateSnapshotEvent"/> restores.</summary>
public sealed class ArcAgentSessionSnapshot
{
    /// <summary>The run this snapshot belongs to.</summary>
    public required ArcAgentRunRef RunRef { get; init; }

    /// <summary>The run phase at snapshot time.</summary>
    public ArcRunPhase Phase { get; init; }

    /// <summary>Messages at snapshot time.</summary>
    public IReadOnlyList<ArcAgentMessage> Messages { get; init; } = Array.Empty<ArcAgentMessage>();

    /// <summary>Tool calls at snapshot time.</summary>
    public IReadOnlyList<ArcToolCall> ToolCalls { get; init; } = Array.Empty<ArcToolCall>();

    /// <summary>Steps at snapshot time.</summary>
    public IReadOnlyList<ArcAgentStep> Steps { get; init; } = Array.Empty<ArcAgentStep>();
}

/// <summary>Curated run state the controls bind to.</summary>
public sealed class ArcAgentRunState
{
    /// <summary>Identity of the run.</summary>
    public ArcAgentRunRef Run { get; internal set; }

    /// <summary>Visible phase.</summary>
    public ArcRunPhase Phase { get; internal set; } = ArcRunPhase.Idle;

    /// <summary>Current step title, when known.</summary>
    public string? CurrentStep { get; internal set; }

    /// <summary>When the run started.</summary>
    public DateTimeOffset StartedAt { get; internal set; }

    /// <summary>Elapsed seconds supplied by the host if it wants a duration shown; never ticked here.</summary>
    public double ElapsedSeconds { get; internal set; }
}

/// <summary>The user turn handed to the host transport.</summary>
public sealed record ArcAgentTurnRequest(
    string Text,
    string? Model = null,
    IReadOnlyList<ArcAttachment>? Attachments = null);

/// <summary>One attachment chip on a message or in the composer.</summary>
public sealed record ArcAttachment(string Id, string Name, long SizeBytes = 0, string? MimeType = null);
