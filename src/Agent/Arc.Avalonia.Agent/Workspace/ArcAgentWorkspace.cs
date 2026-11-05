using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Agent;

/// <summary>Change notification raised by the workspace.</summary>
public sealed class ArcAgentWorkspaceChangedEventArgs : EventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcAgentWorkspaceChangedEventArgs(
        ArcAgentChangeKind kind,
        IArcAgentSession? session = null,
        ArcAgentEvent? @event = null,
        ArcApplyResult result = ArcApplyResult.Accepted)
    {
        Kind = kind;
        Session = session;
        Event = @event;
        Result = result;
    }

    /// <summary>What changed.</summary>
    public ArcAgentChangeKind Kind { get; }

    /// <summary>The session affected, when applicable.</summary>
    public IArcAgentSession? Session { get; }

    /// <summary>The event that caused the change, when applicable.</summary>
    public ArcAgentEvent? Event { get; }

    /// <summary>The reducer result for the event.</summary>
    public ArcApplyResult Result { get; }
}

/// <summary>
/// UI-side multi-run coordinator. A host pushes <see cref="ArcAgentEvent"/>s here; the workspace routes each
/// event to the session for its <c>RunId</c>, folds it, and keeps the cross-run approval/attention/artifact
/// queues in one place. It never executes I/O, never owns timers and never creates events itself.
/// </summary>
public interface IArcAgentWorkspace
{
    /// <summary>Sessions in creation order (foreground, background and sub-agent runs).</summary>
    IReadOnlyList<IArcAgentSession> Sessions { get; }

    /// <summary>The foreground session currently shown, if any.</summary>
    IArcAgentSession? Active { get; }

    /// <summary>Identity of every known run, including background and sub-agent runs.</summary>
    IReadOnlyList<ArcAgentRunRef> Runs { get; }

    /// <summary>Runs that should appear in the background dock.</summary>
    IReadOnlyList<ArcAgentRunRef> BackgroundRuns { get; }

    /// <summary>Approval requests across every run, serialised by the queue.</summary>
    IReadOnlyList<ArcApprovalRequest> PendingApprovals { get; }

    /// <summary>Cross-run approval queue.</summary>
    ArcApprovalQueue Approvals { get; }

    /// <summary>Cross-run attention queue.</summary>
    ArcAttentionQueue Attention { get; }

    /// <summary>Cross-run artifact index.</summary>
    ArcArtifactIndex Artifacts { get; }

    /// <summary>Finds a session by session id or run id.</summary>
    IArcAgentSession? GetSession(string sessionOrRunId);

    /// <summary>Makes a session the active foreground session.</summary>
    bool Activate(string sessionOrRunId);

    /// <summary>Feeds one event into the projection; returns the reducer result.</summary>
    ArcApplyResult Apply(ArcAgentEvent @event);

    /// <summary>Feeds a batch of events; raises one change notification for the whole batch.</summary>
    int ApplyRange(IEnumerable<ArcAgentEvent> events);

    /// <summary>Clears every session and queue.</summary>
    void Reset();

    /// <summary>Raised after a session is created or an event changes the projection.</summary>
    event EventHandler<ArcAgentWorkspaceChangedEventArgs>? Changed;
}

/// <summary>The default pure-reducer workspace implementation.</summary>
public sealed class ArcAgentWorkspace : IArcAgentWorkspace
{
    private const int MaxOrphansPerRun = 256;

    private readonly List<ArcAgentSessionModel> _sessions = new();
    private readonly Dictionary<string, ArcAgentSessionModel> _byRunId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcAgentSessionModel> _bySessionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedDictionary<long, ArcAgentEvent>> _orphans = new(StringComparer.Ordinal);
    private int _batchDepth;
    private bool _batchDirty;
    private ArcApplyResult _lastResult = ArcApplyResult.Accepted;
    private IArcAgentSession? _lastSession;
    private ArcAgentEvent? _lastEvent;

    /// <inheritdoc />
    public event EventHandler<ArcAgentWorkspaceChangedEventArgs>? Changed;

    /// <inheritdoc />
    public IReadOnlyList<IArcAgentSession> Sessions => _sessions;

    /// <inheritdoc />
    public IArcAgentSession? Active { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<ArcAgentRunRef> Runs => _sessions.Select(session => session.RunRef).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ArcAgentRunRef> BackgroundRuns =>
        _sessions.Where(session => session.RunRef.IsBackground).Select(session => session.RunRef).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<ArcApprovalRequest> PendingApprovals => Approvals.Pending;

    /// <inheritdoc />
    public ArcApprovalQueue Approvals { get; } = new();

    /// <inheritdoc />
    public ArcAttentionQueue Attention { get; } = new();

    /// <inheritdoc />
    public ArcArtifactIndex Artifacts { get; } = new();

    /// <inheritdoc />
    public IArcAgentSession? GetSession(string sessionOrRunId) =>
        _bySessionId.TryGetValue(sessionOrRunId, out var bySession)
            ? bySession
            : _byRunId.TryGetValue(sessionOrRunId, out var byRun) ? byRun : null;

    /// <inheritdoc />
    public bool Activate(string sessionOrRunId)
    {
        var session = GetSession(sessionOrRunId);
        if (session is null)
        {
            return false;
        }

        Active = session;
        Raise(ArcAgentChangeKind.EventApplied);
        return true;
    }

    /// <inheritdoc />
    public ArcApplyResult Apply(ArcAgentEvent @event)
    {
        var result = ApplyCore(@event);
        _lastResult = result;
        _lastSession = GetSession(@event.Stamp.RunId) ?? _lastSession;
        _lastEvent = @event;
        if (_batchDepth == 0)
        {
            Raise(ArcAgentChangeKind.EventApplied, _lastSession, @event, result);
        }
        else
        {
            _batchDirty = true;
        }

        return result;
    }

    /// <inheritdoc />
    public int ApplyRange(IEnumerable<ArcAgentEvent> events)
    {
        _batchDepth++;
        var accepted = 0;
        try
        {
            foreach (var @event in events)
            {
                var result = ApplyCore(@event);
                _lastResult = result;
                _lastSession = GetSession(@event.Stamp.RunId) ?? _lastSession;
                _lastEvent = @event;
                if (result == ArcApplyResult.Accepted)
                {
                    accepted++;
                    _batchDirty = true;
                }
            }
        }
        finally
        {
            _batchDepth--;
        }

        if (_batchDirty && _batchDepth == 0)
        {
            _batchDirty = false;
            Raise(ArcAgentChangeKind.EventApplied, _lastSession, _lastEvent, _lastResult);
        }

        return accepted;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _sessions.Clear();
        _byRunId.Clear();
        _bySessionId.Clear();
        _orphans.Clear();
        Active = null;
        Approvals.Clear();
        Attention.Clear();
        Artifacts.Clear();
        Raise(ArcAgentChangeKind.Reset);
    }

    private ArcApplyResult ApplyCore(ArcAgentEvent @event)
    {
        if (@event is ArcRunStartedEvent started)
        {
            var runId = started.Run.RunId;
            if (!_byRunId.TryGetValue(runId, out var session))
            {
                session = new ArcAgentSessionModel(started.Run);
                _sessions.Add(session);
                _byRunId[runId] = session;
                _bySessionId[session.SessionId] = session;
                if (Active is null || started.Run.Kind == ArcAgentRunKind.Foreground)
                {
                    Active = session;
                }

                var created = session.Apply(@event);
                DrainOrphans(runId);
                return created;
            }
        }

        if (!_byRunId.TryGetValue(@event.Stamp.RunId, out var target))
        {
            return BufferOrphan(@event);
        }

        var result = target.Apply(@event);
        if (result != ArcApplyResult.Accepted)
        {
            return result;
        }

        ApplyCrossRunState(@event, target);
        return result;
    }

    private void ApplyCrossRunState(ArcAgentEvent @event, ArcAgentSessionModel session)
    {
        switch (@event)
        {
            case ArcApprovalRequestedEvent approval:
                Approvals.Enqueue(approval.Request);
                break;
            case ArcApprovalResolvedEvent resolved:
                Approvals.Resolve(resolved.RequestId);
                break;
            case ArcAttentionRaisedEvent attention:
                Attention.Upsert(attention.Entry);
                break;
            case ArcArtifactPublishedEvent artifact:
                Artifacts.Add(artifact.Artifact);
                break;
            case ArcRunPhaseChangedEvent phase when phase.Phase is ArcRunPhase.Done or ArcRunPhase.Failed or ArcRunPhase.Stopped:
                Approvals.ExpireRun(phase.Stamp.RunId);
                Attention.ExpireRun(phase.Stamp.RunId);
                break;
        }
    }

    private ArcApplyResult BufferOrphan(ArcAgentEvent @event)
    {
        if (!_orphans.TryGetValue(@event.Stamp.RunId, out var buffer))
        {
            buffer = new SortedDictionary<long, ArcAgentEvent>();
            _orphans[@event.Stamp.RunId] = buffer;
        }

        if (buffer.Count >= MaxOrphansPerRun)
        {
            return ArcApplyResult.Orphaned;
        }

        buffer[@event.Stamp.Sequence] = @event;
        return ArcApplyResult.UnknownRun;
    }

    private void DrainOrphans(string runId)
    {
        if (!_orphans.TryGetValue(runId, out var buffer))
        {
            return;
        }

        _orphans.Remove(runId);
        foreach (var @event in buffer.Values.ToArray())
        {
            _ = ApplyCore(@event);
        }
    }

    private void Raise(
        ArcAgentChangeKind kind,
        IArcAgentSession? session = null,
        ArcAgentEvent? @event = null,
        ArcApplyResult result = ArcApplyResult.Accepted)
    {
        Changed?.Invoke(this, new ArcAgentWorkspaceChangedEventArgs(kind, session, @event, result));
    }

    /// <summary>
    /// Builds a deterministic two-run demo projection for host-free rendering (Gallery shots, design review
    /// and tests). Foreground: a message, a tool call, a pending approval, an attention entry and a diff
    /// artifact. Background: a research run whose progress is visible in the dock.
    /// </summary>
    public static ArcAgentWorkspace CreateSample()
    {
        var workspace = new ArcAgentWorkspace();
        var foreground = new ArcAgentRunRef("run-fg", "session-fg", ArcAgentRunKind.Foreground, Label: "Build the migration plan");
        var background = new ArcAgentRunRef("run-bg", "session-bg", ArcAgentRunKind.Background, Label: "Background research");

        long seq = 0;
        ArcEventStamp Next(string runId) => ArcEventStamp.Create(runId, ++seq);

        var events = new List<ArcAgentEvent>
        {
            new ArcRunStartedEvent(Next("run-fg"), DateTimeOffset.UtcNow, foreground),
            new ArcStepStartedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "step-plan", "Plan the answer"),
            new ArcStepFinishedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "step-plan", ArcStepStatus.Done),
            new ArcMessageStartedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-user", ArcAgentRole.User),
            new ArcMessageDeltaEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-user", "Draft a migration plan for the agent layer."),
            new ArcMessageEndedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-user"),
            new ArcMessageStartedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-assistant", ArcAgentRole.Assistant),
            new ArcMessageDeltaEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-assistant", "I'll start by "),
            new ArcMessageDeltaEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-assistant", "mapping the services, "),
            new ArcMessageDeltaEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-assistant", "then I'll propose the surface changes."),
            new ArcToolCallStartedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "call-1", "repo.search", "Find every call site of ArcWindow.", ArcRiskLevel.Low),
            new ArcToolArgsDeltaEvent(Next("run-fg"), DateTimeOffset.UtcNow, "call-1", "{\"query\":\"ArcWindow\"}"),
            new ArcToolCallEndedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "call-1", ArcToolCallStatus.Succeeded, "6 matches"),
            new ArcApprovalRequestedEvent(Next("run-fg"), DateTimeOffset.UtcNow, new ArcApprovalRequest
            {
                RequestId = "approval-1",
                RunId = "run-fg",
                CallId = "call-2",
                ToolName = "shell.exec",
                Intent = "Apply the migration patch to two files.",
                Risk = ArcRiskLevel.High,
                Arguments = new[]
                {
                    new ArcApprovalArgument("repo", "arc/arc.avalonia"),
                    new ArcApprovalArgument("patch", "2 files · +48 / -12")
                },
                SideEffects = new[] { "Writes 2 files", "Runs dotnet build" }
            }),
            new ArcAttentionRaisedEvent(Next("run-fg"), DateTimeOffset.UtcNow, new ArcAttentionEntry
            {
                EntryId = "attention-1",
                RunId = "run-fg",
                Kind = ArcAttentionKind.Approval,
                Title = "Approve the migration patch",
                Detail = "Two files · high risk · waits for you",
                Level = ArcAttentionLevel.Blocking,
                State = ArcAttentionState.New,
                CreatedAt = DateTimeOffset.UtcNow
            }),
            new ArcArtifactPublishedEvent(Next("run-fg"), DateTimeOffset.UtcNow, new ArcArtifact
            {
                ArtifactId = "artifact-1",
                Kind = ArcArtifactKind.Diff,
                Title = "ArcWindow.cs",
                Subtitle = "+48 / -12",
                MimeType = "text/x-diff",
                DisplayText = "- old surface\n+ new surface",
                At = DateTimeOffset.UtcNow
            }),
            new ArcMessageEndedEvent(Next("run-fg"), DateTimeOffset.UtcNow, "m-assistant"),
            new ArcRunPhaseChangedEvent(Next("run-fg"), DateTimeOffset.UtcNow, ArcRunPhase.AwaitingApproval)
        };

        seq = 0;
        events.AddRange(new ArcAgentEvent[]
        {
            new ArcRunStartedEvent(Next("run-bg"), DateTimeOffset.UtcNow, background),
            new ArcStepStartedEvent(Next("run-bg"), DateTimeOffset.UtcNow, "bg-step", "Research the release notes"),
            new ArcMessageStartedEvent(Next("run-bg"), DateTimeOffset.UtcNow, "bg-message", ArcAgentRole.Assistant),
            new ArcMessageDeltaEvent(Next("run-bg"), DateTimeOffset.UtcNow, "bg-message", "Background research is 60% done."),
            new ArcMessageEndedEvent(Next("run-bg"), DateTimeOffset.UtcNow, "bg-message"),
            new ArcRunPhaseChangedEvent(Next("run-bg"), DateTimeOffset.UtcNow, ArcRunPhase.Running)
        });

        _ = workspace.ApplyRange(events);
        _ = workspace.Activate("session-fg");
        return workspace;
    }
}
