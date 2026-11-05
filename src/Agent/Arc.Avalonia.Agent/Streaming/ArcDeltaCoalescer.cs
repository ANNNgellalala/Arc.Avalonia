using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Minimal scheduler abstraction used by <see cref="ArcDeltaCoalescer"/>. The default implementation wraps
/// Avalonia's existing <see cref="Dispatcher"/>, so the top layer never owns a frame clock or a timer; tests
/// (and headless hosts) can inject a synchronous/queued dispatcher instead.
/// </summary>
public interface IArcUiDispatcher
{
    /// <summary>True when the caller is already on the UI thread.</summary>
    bool CheckAccess();

    /// <summary>Queues an action on the UI thread.</summary>
    void Post(Action action);
}

/// <summary>The production scheduler: Avalonia's dispatcher, no custom clock.</summary>
public sealed class DispatcherUiDispatcher : IArcUiDispatcher
{
    /// <summary>Shared instance.</summary>
    public static DispatcherUiDispatcher Instance { get; } = new();

    /// <inheritdoc />
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action) => Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
}

/// <summary>A queued dispatcher for headless tests: nothing runs until <see cref="RunPending"/>.</summary>
public sealed class QueuedUiDispatcher : IArcUiDispatcher
{
    private readonly Queue<Action> _actions = new();

    /// <inheritdoc />
    public bool CheckAccess() => true;

    /// <inheritdoc />
    public void Post(Action action) => _actions.Enqueue(action);

    /// <summary>Number of queued actions.</summary>
    public int Pending => _actions.Count;

    /// <summary>Runs every queued action once.</summary>
    public int RunPending()
    {
        var count = _actions.Count;
        while (_actions.Count > 0)
        {
            _actions.Dequeue()();
        }

        return count;
    }
}

/// <summary>A coalesced batch handed to the workspace in one projection pass.</summary>
public sealed record ArcAgentEventBatch(IReadOnlyList<ArcAgentEvent> Events);

/// <summary>
/// Batches high-frequency deltas into one projection pass per dispatcher callback. No timer, no 16 ms
/// constant, no frame clock: it consumes the scheduler Avalonia already runs. Text deltas are merged by the
/// reducer (append-only), tool argument deltas stay in order, and the pending queue is bounded so a runaway
/// producer cannot grow memory without limit.
/// </summary>
public sealed class ArcDeltaCoalescer
{
    private readonly object _gate = new();
    private readonly List<ArcAgentEvent> _pending = new();
    private readonly IArcUiDispatcher _dispatcher;
    private readonly int _maxBatchSize;
    private bool _scheduled;
    private int _flushCount;
    private int _pendingHighWatermark;

    /// <summary>Creates a coalescer.</summary>
    /// <param name="dispatcher">Scheduler to post the flush to; defaults to Avalonia's dispatcher.</param>
    /// <param name="maxBatchSize">Maximum pending events before the next flush is forced.</param>
    public ArcDeltaCoalescer(IArcUiDispatcher? dispatcher = null, int maxBatchSize = 2048)
    {
        _dispatcher = dispatcher ?? DispatcherUiDispatcher.Instance;
        _maxBatchSize = Math.Max(16, maxBatchSize);
    }

    /// <summary>Raised once per dispatcher callback with every event collected since the last flush.</summary>
    public event EventHandler<ArcAgentEventBatch>? Flush;

    /// <summary>Number of flushes performed (tests assert batching: N enqueues, 1 flush).</summary>
    public int FlushCount => _flushCount;

    /// <summary>Current number of buffered events.</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    /// <summary>Highest pending count observed; useful for the streaming guard rails.</summary>
    public int PendingHighWatermark => _pendingHighWatermark;

    /// <summary>Queues one event; O(1). The actual projection happens on the dispatcher callback.</summary>
    public void Enqueue(ArcAgentEvent @event)
    {
        var forceFlush = false;
        lock (_gate)
        {
            _pending.Add(@event);
            if (_pending.Count > _pendingHighWatermark)
            {
                _pendingHighWatermark = _pending.Count;
            }

            if (_pending.Count >= _maxBatchSize)
            {
                forceFlush = true;
            }
            else if (!_scheduled)
            {
                _scheduled = true;
                _dispatcher.Post(FlushCore);
            }
        }

        if (forceFlush)
        {
            FlushNow();
        }
    }

    /// <summary>Queues a batch of events.</summary>
    public void EnqueueRange(IEnumerable<ArcAgentEvent> events)
    {
        foreach (var @event in events)
        {
            Enqueue(@event);
        }
    }

    /// <summary>Flushes synchronously (used when a run completes, or by headless tests).</summary>
    public void FlushNow() => FlushCore();

    private void FlushCore()
    {
        ArcAgentEvent[] batch;
        lock (_gate)
        {
            if (_pending.Count == 0)
            {
                _scheduled = false;
                return;
            }

            batch = _pending.ToArray();
            _pending.Clear();
            _scheduled = false;
            _flushCount++;
        }

        Flush?.Invoke(this, new ArcAgentEventBatch(batch));
    }
}

