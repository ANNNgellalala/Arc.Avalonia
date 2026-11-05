using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Arc.Avalonia.Agent;

/// <summary>
/// The single host boundary for producing events. A host implements this (HTTP/WS/IPC/local process) and pushes
/// <see cref="ArcAgentEvent"/>s into the supplied sink. The top layer only projects them.
/// </summary>
public interface IArcAgentTransport
{
    /// <summary>Starts a new user turn. Returns the run id the host assigned.</summary>
    Task<string> StartTurnAsync(ArcAgentTurnRequest request, IArcAgentEventSink sink, CancellationToken cancellationToken = default);

    /// <summary>Sends a control command (stop/steer/approve/takeover...) to the run's host runtime.</summary>
    Task SendCommandAsync(ArcAgentCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Host → UI event callback. Implementations must tolerate calls from a background thread.</summary>
public interface IArcAgentEventSink
{
    /// <summary>Pushes one event.</summary>
    void Push(ArcAgentEvent @event);

    /// <summary>Signals that a run finished with the given phase.</summary>
    void Complete(string runId, ArcRunPhase phase);

    /// <summary>Signals a transport-level failure for a run.</summary>
    void Fail(string runId, string message);
}

/// <summary>
/// UI → host command exit. The composite surface implements it; leaf controls call it instead of knowing about
/// the transport. The host-side implementation lives in <see cref="IArcAgentTransport.SendCommandAsync"/>.
/// </summary>
public interface IArcAgentCommandSink
{
    /// <summary>Sends a command.</summary>
    Task SendAsync(ArcAgentCommand command, CancellationToken cancellationToken = default);

    /// <summary>Raised with every command for logging/observability (optional for hosts that only poll).</summary>
    event EventHandler<ArcAgentCommand>? CommandRaised;
}

/// <summary>Host-provided environment capabilities. Every member is optional for a headless/test host.</summary>
public interface IArcAgentServices
{
    /// <summary>Opens a URL in the host's browser.</summary>
    Task OpenExternalAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>Copies text to the host clipboard.</summary>
    Task CopyTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Asks the host for a yes/no confirmation.</summary>
    Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default);
}

/// <summary>The no-op services used when a host does not care about navigation/clipboard/confirmation.</summary>
public sealed class ArcAgentNullServices : IArcAgentServices
{
    /// <summary>Shared instance.</summary>
    public static ArcAgentNullServices Instance { get; } = new();

    /// <inheritdoc />
    public Task OpenExternalAsync(Uri uri, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task CopyTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

/// <summary>Base command sent from the UI to the host runtime.</summary>
public abstract record ArcAgentCommand(string RunId);

/// <summary>Immediate stop, or stop after the current safe point.</summary>
public sealed record ArcStopCommand(string RunId, bool AfterCurrentStep = false) : ArcAgentCommand(RunId);

/// <summary>Pause the run at the next safe point.</summary>
public sealed record ArcPauseCommand(string RunId) : ArcAgentCommand(RunId);

/// <summary>Resume a paused or taken-over run.</summary>
public sealed record ArcResumeCommand(string RunId) : ArcAgentCommand(RunId);

/// <summary>Insert a new instruction into a running plan.</summary>
public sealed record ArcSteerCommand(string RunId, string Instruction) : ArcAgentCommand(RunId);

/// <summary>Take over the run manually; the host decides how the agent hands back.</summary>
public sealed record ArcTakeoverCommand(string RunId) : ArcAgentCommand(RunId);

/// <summary>Send an approval decision.</summary>
public sealed record ArcApprovalCommand(string RunId, ArcApprovalDecision Decision) : ArcAgentCommand(RunId);

/// <summary>Answer a structured question.</summary>
public sealed record ArcAnswerCommand(string RunId, ArcQuestionAnswer Answer) : ArcAgentCommand(RunId);

/// <summary>Ask the host to retry one failed event.</summary>
public sealed record ArcRetryCommand(string RunId, string EventId) : ArcAgentCommand(RunId);

/// <summary>Open an item (attention entry, artifact, ...) through the host.</summary>
public sealed record ArcOpenItemCommand(string RunId, string ItemId) : ArcAgentCommand(RunId);
