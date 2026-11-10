using System.Text;
using Avalonia.Threading;
using PuraMarkdown.Core;
using PuraMarkdown.Core;

namespace Arc.Avalonia.Markdown;

/// <summary>Streaming lifecycle state.</summary>
public enum ArcMarkdownStreamingState
{
    Created,
    Streaming,
    Completed,
    Aborted,
}

/// <summary>Tuning for <see cref="ArcMarkdownStreamingSession"/>.</summary>
public sealed class ArcMarkdownStreamingOptions
{
    /// <summary>Characters kept in the unstable tail window before a stable double newline is forced through.</summary>
    public int TailWindowChars { get; init; } = 16 * 1024;

    /// <summary>Hard limit for the tail window; larger unclosed blocks pause tail refresh until completion.</summary>
    public int MaxTailChars { get; init; } = 1024 * 1024;

    /// <summary>Whether the view follows the newest content.</summary>
    public bool AutoScrollToEnd { get; init; } = true;

    /// <summary>Apply every append synchronously on the calling thread (tests/console hosts that have no UI loop).</summary>
    public bool Synchronous { get; init; }
}

/// <summary>Streaming diagnostics: how much was parsed, how often and where the stable boundary sits.</summary>
public sealed record ArcMarkdownStreamingStatistics(
    int Flushes,
    int Parses,
    int CommittedChars,
    int TailChars,
    int TotalChars)
{
    /// <summary>Number of times the stable prefix was committed into the rendered document.</summary>
    public int Commits { get; init; }

    /// <summary>Number of commits forced by <see cref="ArcMarkdownStreamingOptions.TailWindowChars"/> overflow.</summary>
    public int ForcedCommits { get; init; }

    /// <summary>Characters already reflected by the last applied render window.</summary>
    public int AppliedChars { get; init; }

    /// <summary>Characters processed by the worker (may exceed <see cref="AppliedChars"/> while the tail is paused).</summary>
    public int ProcessedChars { get; init; }

    /// <summary>Number of background worker failures (the last exception is exposed on the session).</summary>
    public int WorkerErrors { get; init; }
}

/// <summary>
/// Append-only streaming session for <see cref="ArcMarkdownView"/>. Text is committed at stable block
/// boundaries (top-level blank lines outside open fences) and only the committed window plus the unstable
/// tail window are parsed and re-rendered, so a long answer does not rebuild the whole document on every
/// token. Parsing runs on a background worker; rendering is coalesced onto the UI dispatcher frame by frame.
/// <para>
/// <see cref="Append"/> may be called from any thread. <see cref="CompleteAsync"/> parses the whole text once
/// and replaces the streamed windows, so the final result is identical to a one-shot render.
/// </para>
/// </summary>
public sealed partial class ArcMarkdownStreamingSession : IAsyncDisposable
{
    private const string CompletedAnnouncement = "回复已完成";

    private readonly ArcMarkdownView _view;
    private readonly ArcMarkdownStreamingOptions _options;
    private readonly ArcMarkdownOptions _markdownOptions;
    private readonly PuraSyntaxOptions _syntaxOptions;
    private readonly StringBuilder _text = new();
    private readonly object _gate = new();
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly List<FlushWaiter> _flushWaiters = [];

    private PuraDocument? _committed;
    private Exception? _lastWorkerError;
    private Task? _worker;
    private ArcMarkdownStreamingState _state;
    private int _committedOffset;
    private int _version;
    private int _flushes;
    private int _parses;
    private int _commits;
    private int _forcedCommits;
    private int _workerErrors;
    private int _appliedChars;
    private int _processedChars;
    private int _lastAppliedVersion;
    private bool _follow;
    private bool _closed;
    private bool _finishStarted;
    private bool _tailPaused;
    private bool _disposed;

    internal ArcMarkdownStreamingSession(ArcMarkdownView view, ArcMarkdownStreamingOptions options)
    {
        _view = view;
        _options = options;
        _markdownOptions = view.CurrentOptions;
        _syntaxOptions = ArcMarkdownView.SyntaxOptions(_markdownOptions);
        _follow = options.AutoScrollToEnd;
        _state = ArcMarkdownStreamingState.Created;
    }

    /// <summary>Current lifecycle state (thread safe).</summary>
    public ArcMarkdownStreamingState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>Text received so far.</summary>
    public string CurrentText
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    /// <summary>Characters appended but not yet reflected by a rendered window.</summary>
    public int PendingChars
    {
        get
        {
            lock (_gate)
            {
                return Math.Max(0, _text.Length - _appliedChars);
            }
        }
    }

    /// <summary>Last background worker exception, if any. A failed pass never throws into the host.</summary>
    public Exception? LastWorkerError
    {
        get
        {
            lock (_gate)
            {
                return _lastWorkerError;
            }
        }
    }

    /// <summary>True when an unclosed block exceeded <see cref="ArcMarkdownStreamingOptions.MaxTailChars"/>.</summary>
    public bool IsTailPaused
    {
        get
        {
            lock (_gate)
            {
                return _tailPaused;
            }
        }
    }

    /// <summary>Raised after a streaming window has been applied to the view (UI thread).</summary>
    public event EventHandler? Updated;

    /// <summary>Raised once when the session completes and the final document has been rendered (UI thread).</summary>
    public event EventHandler? Completed;

    /// <summary>Raised once when the session is aborted and the partial document has been rendered (UI thread).</summary>
    public event EventHandler? Aborted;

    /// <summary>Statistics snapshot.</summary>
    public ArcMarkdownStreamingStatistics Statistics
    {
        get
        {
            lock (_gate)
            {
                return new ArcMarkdownStreamingStatistics(
                    _flushes,
                    _parses,
                    _committedOffset,
                    Math.Max(0, _text.Length - _committedOffset),
                    _text.Length)
                {
                    Commits = _commits,
                    ForcedCommits = _forcedCommits,
                    AppliedChars = _appliedChars,
                    ProcessedChars = _processedChars,
                    WorkerErrors = _workerErrors,
                };
            }
        }
    }

    /// <summary>Appends a text delta and schedules a coalesced UI update. Safe from any thread.</summary>
    public void Append(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        lock (_gate)
        {
            if (_state is ArcMarkdownStreamingState.Completed or ArcMarkdownStreamingState.Aborted || _closed)
            {
                return;
            }

            if (_state == ArcMarkdownStreamingState.Created)
            {
                _state = ArcMarkdownStreamingState.Streaming;
            }

            _text.Append(delta);
            if (!_options.Synchronous && _worker is null)
            {
                _worker = Task.Run(WorkerLoopAsync);
            }

            WakeWorkerLocked();
        }

        if (_options.Synchronous)
        {
            ApplySynchronously();
        }
    }

    /// <summary>Appends a delta without blocking the caller.</summary>
    public Task AppendAsync(ReadOnlyMemory<char> delta, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Append(delta.ToString());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits until every delta appended before this call has been processed by a worker pass (and applied
    /// when the tail was not paused). Safe to await from the UI thread.
    /// </summary>
    public Task FlushAsync()
    {
        if (_options.Synchronous)
        {
            ApplySynchronously();
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (_state is ArcMarkdownStreamingState.Completed or ArcMarkdownStreamingState.Aborted || _closed)
            {
                return Task.CompletedTask;
            }

            if (_worker is null)
            {
                if (_text.Length == 0)
                {
                    return Task.CompletedTask;
                }

                _worker = Task.Run(WorkerLoopAsync);
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _flushWaiters.Add(new FlushWaiter(_text.Length, waiter));
            WakeWorkerLocked();
            return waiter.Task;
        }
    }

    /// <summary>Alias for <see cref="FlushAsync"/> that reads more naturally at call sites.</summary>
    public Task WaitForIdleAsync() => FlushAsync();

    /// <summary>Completes the stream: parses and renders the whole text once, replacing the windows.</summary>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await FinishAsync(ArcMarkdownStreamingState.Completed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Aborts the stream, keeping and rendering the partial text.</summary>
    public async Task AbortAsync(CancellationToken cancellationToken = default)
    {
        await FinishAsync(ArcMarkdownStreamingState.Aborted, cancellationToken).ConfigureAwait(false);
    }

    internal void AbortSilently()
    {
        if (!TryBeginFinish(ArcMarkdownStreamingState.Aborted, out var text))
        {
            return;
        }

        CancelWorker();

        void Render()
        {
            try
            {
                _view.ClearStreamingTail();
                _view.RenderDocument(ParseFull(text));
                _view.SetActiveSession(null);
            }
            catch (Exception ex)
            {
                _lastWorkerError = ex;
            }

            RaiseAborted();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Render();
        }
        else
        {
            Dispatcher.UIThread.Post(Render, DispatcherPriority.Background);
        }
    }

    internal void NotifyUserScrolled()
    {
        lock (_gate)
        {
            if (_options.AutoScrollToEnd)
            {
                _follow = false;
            }
        }
    }

    /// <summary>Re-arms follow mode once the reader scrolls back to the end (touch follow latch).</summary>
    internal void NotifyUserScrolledToEnd()
    {
        lock (_gate)
        {
            if (_options.AutoScrollToEnd)
            {
                _follow = true;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_finished.Task.IsCompleted)
        {
            AbortSilently();
        }

        try
        {
            await _finished.Task.ConfigureAwait(false);
        }
        catch
        {
            // The finished task never faults.
        }

        CancelWorker();
        var worker = _worker;
        if (worker is not null)
        {
            try
            {
                await worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        DrainWaiters();
        _signal.Dispose();
        _cts.Dispose();
    }

    private async Task FinishAsync(ArcMarkdownStreamingState finalState, CancellationToken cancellationToken)
    {
        if (!TryBeginFinish(finalState, out var text))
        {
            await _finished.Task.ConfigureAwait(false);
            return;
        }

        CancelWorker();
        var document = await ParseFinalAsync(text, cancellationToken).ConfigureAwait(false);
        await RunOnUiThreadAsync(() => RenderFinal(finalState, text, document)).ConfigureAwait(false);
        DrainWaiters();
        _finished.TrySetResult();
    }

    private bool TryBeginFinish(ArcMarkdownStreamingState finalState, out string text)
    {
        lock (_gate)
        {
            if (_finishStarted || _state is ArcMarkdownStreamingState.Completed or ArcMarkdownStreamingState.Aborted)
            {
                text = string.Empty;
                return false;
            }

            _finishStarted = true;
            _state = finalState;
            _closed = true;
            text = _text.ToString();
            return true;
        }
    }

    private async Task<PuraDocument> ParseFinalAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => ParseFull(text), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ParseFull(text);
        }
        catch (Exception ex)
        {
            _lastWorkerError = ex;
            return ParseFull(text);
        }
    }

    private void RenderFinal(ArcMarkdownStreamingState finalState, string text, PuraDocument document)
    {
        try
        {
            if (finalState == ArcMarkdownStreamingState.Completed)
            {
                _view.ClearStreamingTail();
            }

            _view.RenderDocument(document);
            _view.SetActiveSession(null);
            if (_follow)
            {
                _view.ScrollToEnd();
            }

            if (finalState == ArcMarkdownStreamingState.Completed)
            {
                _view.Announce(text.Length == 0 ? string.Empty : CompletedAnnouncement);
                RaiseCompleted();
            }
            else
            {
                RaiseAborted();
            }
        }
        catch (Exception ex)
        {
            _lastWorkerError = ex;
            if (finalState == ArcMarkdownStreamingState.Completed)
            {
                RaiseCompleted();
            }
            else
            {
                RaiseAborted();
            }
        }
    }

    private void RaiseCompleted()
    {
        try
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _lastWorkerError = ex;
        }
        finally
        {
            _finished.TrySetResult();
        }
    }

    private void RaiseAborted()
    {
        try
        {
            Aborted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _lastWorkerError = ex;
        }
        finally
        {
            _finished.TrySetResult();
        }
    }

    private async Task WorkerLoopAsync()
    {
        while (true)
        {
            try
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (IsClosed())
            {
                return;
            }

            var passChars = 0;
            try
            {
                lock (_gate)
                {
                    passChars = _text.Length;
                }

                var update = BuildWindows();
                if (update is not null)
                {
                    await ApplyOnUiThreadAsync(update).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    _lastWorkerError = ex;
                    _workerErrors++;
                }
            }
            finally
            {
                lock (_gate)
                {
                    _processedChars = Math.Max(_processedChars, passChars);
                }

                DrainWaiters();
            }

            if (IsClosed())
            {
                return;
            }
        }
    }

    private WindowUpdate? BuildWindows()
    {
        string text;
        int committedOffset;
        PuraDocument? committed;
        lock (_gate)
        {
            if (_closed || _state is ArcMarkdownStreamingState.Completed or ArcMarkdownStreamingState.Aborted)
            {
                return null;
            }

            if (_text.Length == 0)
            {
                return null;
            }

            text = _text.ToString();
            committedOffset = _committedOffset;
            committed = _committed;
        }

        var boundary = StreamingBlockScanner.FindStableBoundary(text, committedOffset);
        var forced = false;
        var tailLength = text.Length - boundary;
        if (boundary == committedOffset && tailLength > _options.TailWindowChars)
        {
            var lastBreak = text.LastIndexOf("\n\n", StringComparison.Ordinal);
            if (lastBreak >= committedOffset)
            {
                var candidate = lastBreak + 2;
                if (StreamingBlockScanner.FindStableBoundary(text[..candidate], 0) == candidate)
                {
                    boundary = candidate;
                    forced = true;
                }
            }
        }

        var newCommitted = committed;
        var committedChanged = false;
        if (boundary > committedOffset || committed is null)
        {
            var committedText = text[..boundary];
            newCommitted = Parse(committedText);
            committedChanged = true;
        }

        tailLength = text.Length - boundary;
        var tailPaused = tailLength > _options.MaxTailChars;
        PuraDocument? tail = null;
        if (!tailPaused && tailLength > 0)
        {
            tail = Parse(text[boundary..]);
        }

        lock (_gate)
        {
            if (_closed)
            {
                return null;
            }

            _tailPaused = tailPaused;
            if (committedChanged)
            {
                _committed = newCommitted;
                _committedOffset = boundary;
                _commits++;
                if (forced)
                {
                    _forcedCommits++;
                }
            }

            return new WindowUpdate(
                Interlocked.Increment(ref _version),
                newCommitted!,
                tail,
                text.Length,
                boundary,
                tailPaused);
        }
    }

    private void ApplySynchronously()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            // Synchronous mode is intended for tests/console hosts; rendering still has to happen on the UI
            // thread when a real application is running behind the view.
            Dispatcher.UIThread.InvokeAsync(ApplySynchronously, DispatcherPriority.Background).GetAwaiter().GetResult();
            return;
        }

        var update = BuildWindows();
        if (update is not null)
        {
            ApplyCore(update);
        }
    }

    private async Task ApplyOnUiThreadAsync(WindowUpdate update)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyCore(update);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => ApplyCore(update), DispatcherPriority.Background).GetTask().ConfigureAwait(false);
    }

    private void ApplyCore(WindowUpdate update)
    {
        lock (_gate)
        {
            if (_closed || _state is ArcMarkdownStreamingState.Completed or ArcMarkdownStreamingState.Aborted)
            {
                return;
            }

            if (update.Version < _lastAppliedVersion)
            {
                return;
            }

            _lastAppliedVersion = update.Version;
            _appliedChars = Math.Max(_appliedChars, update.TailPaused ? update.CommittedOffset : update.TextLength);
            _flushes++;
        }

        if (!ReferenceEquals(_view.ActiveSession, this))
        {
            return;
        }

        _view.ApplyStreamingWindows(update.Committed, update.Tail);
        if (_follow)
        {
            _view.ScrollToEnd();
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    private PuraDocument Parse(string text)
    {
        lock (_gate)
        {
            _parses++;
        }

        return _markdownOptions.Pipeline.Syntax.ParseDocument(text, Interlocked.Increment(ref _version), _syntaxOptions);
    }

    private PuraDocument ParseFull(string text)
    {
        lock (_gate)
        {
            _parses++;
        }

        return _markdownOptions.Pipeline.Syntax.ParseDocument(text, Interlocked.Increment(ref _version), _syntaxOptions);
    }

    private void WakeWorkerLocked()
    {
        try
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            // Another producer already signalled.
        }
        catch (ObjectDisposedException)
        {
            // Session disposed.
        }
    }

    private void CancelWorker()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        lock (_gate)
        {
            WakeWorkerLocked();
        }
    }

    private bool IsClosed()
    {
        lock (_gate)
        {
            return _closed;
        }
    }

    private void DrainWaiters()
    {
        List<FlushWaiter>? ready = null;
        lock (_gate)
        {
            if (_flushWaiters.Count > 0)
            {
                for (var i = _flushWaiters.Count - 1; i >= 0; i--)
                {
                    if (_flushWaiters[i].TargetChars > _processedChars)
                    {
                        continue;
                    }

                    (ready ??= []).Add(_flushWaiters[i]);
                    _flushWaiters.RemoveAt(i);
                }
            }
        }

        if (ready is null)
        {
            return;
        }

        foreach (var waiter in ready)
        {
            waiter.Completion.TrySetResult();
        }
    }

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background).GetTask().ConfigureAwait(false);
    }

    private sealed record FlushWaiter(int TargetChars, TaskCompletionSource Completion);

    private sealed record WindowUpdate(
        int Version,
        PuraDocument Committed,
        PuraDocument? Tail,
        int TextLength,
        int CommittedOffset,
        bool TailPaused);
}

