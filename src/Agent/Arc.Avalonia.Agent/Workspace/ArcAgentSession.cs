using System;
using System.Collections.Generic;
using System.Linq;
using PuraMarkdown.Agent;
using PuraMarkdown.Agent.Chat;

namespace Arc.Avalonia.Agent;

/// <summary>Change notification raised by <see cref="IArcAgentSession"/>.</summary>
public sealed class ArcAgentSessionChangedEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcAgentSessionChangedEventArgs(ArcAgentChangeKind kind, ArcAgentEvent? @event = null, string? itemId = null)
    {
        Kind = kind;
        Event = @event;
        ItemId = itemId;
    }

    /// <summary>What changed.</summary>
    public ArcAgentChangeKind Kind { get; }

    /// <summary>The event that caused the change, when applicable.</summary>
    public ArcAgentEvent? Event { get; }

    /// <summary>The affected message/tool/step id, when applicable.</summary>
    public string? ItemId { get; }
}

/// <summary>
/// One run's read-only projection: messages, tool calls, steps and the visible run state. The host pushes
/// <see cref="ArcAgentEvent"/>s through the workspace; the session folds them deterministically and raises
/// <see cref="Changed"/>. Implementations never perform I/O and never own timers.
/// </summary>
public interface IArcAgentSession
{
    /// <summary>Stable session id.</summary>
    string SessionId { get; }

    /// <summary>Identity of the run this session projects.</summary>
    ArcAgentRunRef RunRef { get; }

    /// <summary>Curated run state bound by the run bar and transcript.</summary>
    ArcAgentRunState Run { get; }

    /// <summary>Messages in arrival order (the upstream chat message model, rendered by PuraMarkdown.Agent).</summary>
    IReadOnlyList<PuraChatMessage> Messages { get; }

    /// <summary>Tool calls in arrival order (the upstream tool-call model).</summary>
    IReadOnlyList<PuraToolCall> ToolCalls { get; }

    /// <summary>Plan steps in arrival order.</summary>
    IReadOnlyList<ArcAgentStep> Steps { get; }

    /// <summary>Every accepted event in application order (the replay/debug record).</summary>
    IReadOnlyList<ArcAgentEvent> Timeline { get; }

    /// <summary>Display snapshot used for state restores and tests.</summary>
    ArcAgentSessionSnapshot Snapshot();

    /// <summary>Raised after an event is folded into the projection.</summary>
    event EventHandler<ArcAgentSessionChangedEventArgs>? Changed;
}

/// <summary>
/// The pure reducer behind <see cref="IArcAgentSession"/>. Sequence handling is deliberately explicit:
/// duplicate ids are ignored, gaps are buffered (bounded) and applied when they fill, and stale sequences
/// are ignored - so a host that retries or reorders events cannot corrupt the view state.
/// </summary>
internal sealed class ArcAgentSessionModel : IArcAgentSession
{
    private const int MaxPendingEvents = 256;

    private readonly List<PuraChatMessage> _messages = new();
    private readonly Dictionary<string, PuraChatMessage> _messageIndex = new(StringComparer.Ordinal);
    private readonly List<PuraToolCall> _toolCalls = new();
    private readonly Dictionary<string, PuraToolCall> _toolIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _toolPositions = new(StringComparer.Ordinal);
    private readonly List<ArcAgentStep> _steps = new();
    private readonly Dictionary<string, ArcAgentStep> _stepIndex = new(StringComparer.Ordinal);
    private readonly List<ArcAgentEvent> _timeline = new();
    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);
    private readonly SortedDictionary<long, ArcAgentEvent> _pending = new();
    private long _lastSequence;

    internal ArcAgentSessionModel(ArcAgentRunRef runRef)
    {
        RunRef = runRef;
        SessionId = runRef.SessionId;
        Run = new ArcAgentRunState { Run = runRef, StartedAt = DateTimeOffset.UtcNow };
    }

    /// <inheritdoc />
    public string SessionId { get; }

    /// <inheritdoc />
    public ArcAgentRunRef RunRef { get; private set; }

    /// <inheritdoc />
    public ArcAgentRunState Run { get; }

    /// <inheritdoc />
    public IReadOnlyList<PuraChatMessage> Messages => _messages;

    /// <inheritdoc />
    public IReadOnlyList<PuraToolCall> ToolCalls => _toolCalls;

    /// <inheritdoc />
    public IReadOnlyList<ArcAgentStep> Steps => _steps;

    /// <inheritdoc />
    public IReadOnlyList<ArcAgentEvent> Timeline => _timeline;

    /// <summary>Last applied sequence; exposed internally for workspace diagnostics.</summary>
    internal long LastSequence => _lastSequence;

    /// <inheritdoc />
    public event EventHandler<ArcAgentSessionChangedEventArgs>? Changed;

    /// <inheritdoc />
    public ArcAgentSessionSnapshot Snapshot() => new()
    {
        RunRef = RunRef,
        Phase = Run.Phase,
        Messages = _messages.ToArray(),
        ToolCalls = _toolCalls.ToArray(),
        Steps = _steps.ToArray()
    };

    internal ArcApplyResult Apply(ArcAgentEvent @event)
    {
        if (_seenIds.Contains(@event.Stamp.EventId))
        {
            return ArcApplyResult.Duplicate;
        }

        if (@event.Stamp.Sequence <= _lastSequence)
        {
            return ArcApplyResult.Duplicate;
        }

        if (@event.Stamp.Sequence > _lastSequence + 1)
        {
            if (_pending.Count >= MaxPendingEvents)
            {
                return ArcApplyResult.OutOfOrder;
            }

            _pending[@event.Stamp.Sequence] = @event;
            return ArcApplyResult.OutOfOrder;
        }

        Fold(@event);
        _seenIds.Add(@event.Stamp.EventId);
        _lastSequence = @event.Stamp.Sequence;

        while (_pending.TryGetValue(_lastSequence + 1, out var next))
        {
            _pending.Remove(_lastSequence + 1);
            Fold(next);
            _seenIds.Add(next.Stamp.EventId);
            _lastSequence++;
        }

        Changed?.Invoke(this, new ArcAgentSessionChangedEventArgs(ArcAgentChangeKind.EventApplied, @event));
        return ArcApplyResult.Accepted;
    }

    private void Fold(ArcAgentEvent @event)
    {        _timeline.Add(@event);

        switch (@event)
        {
            case ArcRunStartedEvent started:
                RunRef = started.Run;
                Run.Run = started.Run;
                Run.Phase = ArcRunPhase.Running;
                Run.StartedAt = started.At;
                break;

            case ArcRunPhaseChangedEvent phase:
                Run.Phase = phase.Phase;
                if (phase.Phase is ArcRunPhase.Done or ArcRunPhase.Failed or ArcRunPhase.Stopped or ArcRunPhase.TakenOver)
                {
                    Run.CurrentStep = null;
                }

                break;

            case ArcStepStartedEvent step:
                var model = new ArcAgentStep { Id = step.StepId, Title = step.Title, Status = ArcStepStatus.Running };
                _steps.Add(model);
                _stepIndex[step.StepId] = model;
                Run.CurrentStep = step.Title;
                break;

            case ArcStepFinishedEvent finished:
                if (_stepIndex.TryGetValue(finished.StepId, out var existingStep))
                {
                    existingStep.Status = finished.Status;
                }

                break;

            case ArcMessageStartedEvent messageStart:
                if (!_messageIndex.TryGetValue(messageStart.MessageId, out var startedMessage))
                {
                    startedMessage = new PuraChatMessage
                    {
                        Id = messageStart.MessageId,
                        Role = MapRole(messageStart.Role),
                        Status = PuraChatMessageStatus.Streaming,
                        CreatedAt = messageStart.At,
                    };
                    _messages.Add(startedMessage);
                    _messageIndex[messageStart.MessageId] = startedMessage;
                }
                else
                {
                    startedMessage.Status = PuraChatMessageStatus.Streaming;
                }

                break;

            case ArcMessageDeltaEvent delta:
                if (_messageIndex.TryGetValue(delta.MessageId, out var streaming))
                {
                    // Append-only delta: the upstream model raises PropertyChanged, so the chat view updates
                    // the rendered Markdown without re-creating the row.
                    streaming.Markdown += delta.Delta;
                    streaming.Status = PuraChatMessageStatus.Streaming;
                }

                break;

            case ArcMessageEndedEvent ended:
                if (_messageIndex.TryGetValue(ended.MessageId, out var completed))
                {
                    completed.Status = ended.Status;
                }

                break;

            case ArcReasoningDeltaEvent:
                // Reasoning no longer lives in Messages: the timeline keeps the deltas and the agent
                // conversation renders them as a collapsible reasoning card.
                break;

            case ArcToolCallStartedEvent toolStart:
                if (!_toolIndex.ContainsKey(toolStart.CallId))
                {
                    var call = new PuraToolCall
                    {
                        Id = toolStart.CallId,
                        Name = toolStart.ToolName,
                        Status = PuraToolCallStatus.Running,
                    };
                    _toolPositions[toolStart.CallId] = _toolCalls.Count;
                    _toolCalls.Add(call);
                    _toolIndex[toolStart.CallId] = call;
                }

                break;

            case ArcToolArgsDeltaEvent args:
                if (_toolIndex.TryGetValue(args.CallId, out var runningTool))
                {
                    // PuraToolCall is an immutable record: replace it and keep the list position stable.
                    var updated = runningTool with { Arguments = (runningTool.Arguments ?? string.Empty) + args.DeltaJson };
                    _toolIndex[args.CallId] = updated;
                    if (_toolPositions.TryGetValue(args.CallId, out var position))
                    {
                        _toolCalls[position] = updated;
                    }
                }

                break;

            case ArcToolCallEndedEvent toolEnd:
                if (_toolIndex.TryGetValue(toolEnd.CallId, out var finishedTool))
                {
                    var failed = toolEnd.Status is ArcToolCallStatus.Failed or ArcToolCallStatus.Denied;
                    var updated = finishedTool with
                    {
                        Status = MapToolStatus(toolEnd.Status),
                        Result = failed ? null : toolEnd.ResultSummary,
                        Error = failed ? toolEnd.ResultSummary ?? "Tool call failed" : null,
                    };
                    _toolIndex[toolEnd.CallId] = updated;
                    if (_toolPositions.TryGetValue(toolEnd.CallId, out var position))
                    {
                        _toolCalls[position] = updated;
                    }
                }

                break;

            case ArcStateSnapshotEvent snapshot:
                _messages.Clear();
                _messageIndex.Clear();
                _toolCalls.Clear();
                _toolIndex.Clear();
                _toolPositions.Clear();
                _steps.Clear();
                _stepIndex.Clear();
                RunRef = snapshot.Snapshot.RunRef;
                Run.Run = RunRef;
                Run.Phase = snapshot.Snapshot.Phase;
                foreach (var message in snapshot.Snapshot.Messages)
                {
                    _messages.Add(message);
                    _messageIndex[message.Id] = message;
                }

                foreach (var call in snapshot.Snapshot.ToolCalls)
                {
                    _toolPositions[call.Id] = _toolCalls.Count;
                    _toolCalls.Add(call);
                    _toolIndex[call.Id] = call;
                }

                foreach (var step in snapshot.Snapshot.Steps)
                {
                    _steps.Add(step);
                    _stepIndex[step.Id] = step;
                }

                Changed?.Invoke(this, new ArcAgentSessionChangedEventArgs(ArcAgentChangeKind.Reset));
                break;
        }
    }

    /// <summary>Maps the event protocol's role to the upstream chat role.</summary>
    internal static PuraChatRole MapRole(ArcAgentRole role) => role switch
    {
        ArcAgentRole.User => PuraChatRole.User,
        ArcAgentRole.System => PuraChatRole.System,
        ArcAgentRole.Tool => PuraChatRole.Tool,
        _ => PuraChatRole.Assistant,
    };

    /// <summary>Maps the Arc tool-call lifecycle onto the upstream tool-call status.</summary>
    internal static PuraToolCallStatus MapToolStatus(ArcToolCallStatus status) => status switch
    {
        ArcToolCallStatus.Succeeded => PuraToolCallStatus.Succeeded,
        ArcToolCallStatus.Failed or ArcToolCallStatus.Denied => PuraToolCallStatus.Failed,
        _ => PuraToolCallStatus.Running,
    };
}
