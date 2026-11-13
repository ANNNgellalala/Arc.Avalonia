using Avalonia.Threading;
using PuraMarkdown.UI;

namespace Arc.Avalonia.Markdown;

/// <summary>Streaming lifecycle state.</summary>
public enum ArcMarkdownStreamingState
{
    /// <summary>Created, no append yet.</summary>
    Created,

    /// <summary>Appending.</summary>
    Streaming,

    /// <summary>Completed normally.</summary>
    Completed,

    /// <summary>Aborted (or faulted) keeping partial content.</summary>
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

/// <summary>Streaming diagnostics: transported from the upstream <see cref="PuraStreamingSession"/>.</summary>
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
/// Append-only streaming session for <see cref="ArcMarkdownView"/>. 0.2 wraps the upstream
/// <see cref="PuraStreamingSession"/> (PuraMarkdown.UI): stable-boundary commits, background parsing, tail
/// window and UI coalescing all live upstream; Arc keeps this public surface for compatibility.
/// </summary>
public sealed partial class ArcMarkdownStreamingSession : IAsyncDisposable
{
    private readonly ArcMarkdownView _view;
    private readonly PuraStreamingSession _inner;
    private readonly ArcMarkdownStreamingOptions _options;
    private bool _disposed;
    private bool _announcedCompleted;
    private bool _announcedAborted;

    internal ArcMarkdownStreamingSession(
        ArcMarkdownView view,
        PuraStreamingSession inner,
        ArcMarkdownStreamingOptions options)
    {
        _view = view;
        _inner = inner;
        _options = options;
        _inner.Completed += OnInnerCompleted;
        _inner.Aborted += OnInnerAborted;
        _inner.Faulted += OnInnerFaulted;
    }

    /// <summary>Current lifecycle state (thread safe).</summary>
    public ArcMarkdownStreamingState State => MapState(_inner.State);

    /// <summary>Text received so far.</summary>
    public string CurrentText => _inner.CurrentText;

    /// <summary>Characters appended but not yet reflected by a rendered window.</summary>
    public int PendingChars => _inner.PendingChars;

    /// <summary>Last background worker exception, if any.</summary>
    public Exception? LastWorkerError => _inner.LastWorkerError;

    /// <summary>True when an unclosed block exceeded the tail budget.</summary>
    public bool IsTailPaused => _inner.Statistics.TailDeferred;

    /// <summary>Raised after a streaming window has been applied (best effort; upstream does not expose per-window callbacks).</summary>
    public event EventHandler? Updated;

    /// <summary>Raised once when the session completes and the final document has been rendered.</summary>
    public event EventHandler? Completed;

    /// <summary>Raised once when the session is aborted and the partial document has been rendered.</summary>
    public event EventHandler? Aborted;

    /// <summary>Statistics snapshot transported from the upstream session.</summary>
    public ArcMarkdownStreamingStatistics Statistics
    {
        get
        {
            var s = _inner.Statistics;
            return new ArcMarkdownStreamingStatistics(
                s.FlushCount,
                0,
                s.CommittedChars,
                s.TailChars,
                s.TotalChars)
            {
                Commits = s.Commits,
                ForcedCommits = 0,
                AppliedChars = s.AppliedChars,
                ProcessedChars = Math.Max(0, s.TotalChars - s.TailChars),
                WorkerErrors = _inner.LastWorkerError is null ? 0 : 1,
            };
        }
    }

    /// <summary>Appends a text delta and schedules a coalesced UI update.</summary>
    /// <param name="delta">Text delta.</param>
    public void Append(string delta) => _inner.Append(delta);

    /// <summary>Appends a text delta without blocking the caller.</summary>
    /// <param name="delta">Text delta.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task AppendAsync(ReadOnlyMemory<char> delta, CancellationToken cancellationToken = default)
        => _inner.AppendAsync(delta, cancellationToken);

    /// <summary>Waits until every delta appended before this call has been processed by a worker pass.</summary>
    /// <returns>Completion task.</returns>
    public async Task FlushAsync()
    {
        await _inner.FlushAsync().ConfigureAwait(false);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Alias for <see cref="FlushAsync"/>.</summary>
    public Task WaitForIdleAsync() => FlushAsync();

    /// <summary>Completes the stream: the upstream parses/renders the whole text once, replacing windows.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        await _inner.CompleteAsync(cancellationToken).ConfigureAwait(false);
        OnFinished(faulted: false);
    }

    /// <summary>Aborts the stream, keeping and rendering the partial text.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AbortAsync(CancellationToken cancellationToken = default)
    {
        await _inner.AbortAsync(cancellationToken).ConfigureAwait(false);
        OnFinished(faulted: false);
    }

    internal void AbortSilently() => _ = AbortAsync();

    internal void NotifyUserScrolled()
    {
        // 0.2: follow mode is owned by Arc's outer ScrollViewer; nothing to forward.
    }

    internal void NotifyUserScrolledToEnd()
    {
        // 0.2: follow mode is owned by Arc's outer ScrollViewer; nothing to forward.
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inner.Completed -= OnInnerCompleted;
        _inner.Aborted -= OnInnerAborted;
        _inner.Faulted -= OnInnerFaulted;
        await _inner.DisposeAsync().ConfigureAwait(false);
    }

    private void OnInnerCompleted(object? sender, EventArgs e) => OnFinished(faulted: false);

    private void OnInnerAborted(object? sender, EventArgs e) => OnFinished(faulted: false);

    private void OnInnerFaulted(object? sender, PuraStreamingFaultedEventArgs e) => OnFinished(faulted: true);

    private void OnFinished(bool faulted)
    {
        var text = _inner.CurrentText;
        void Finish()
        {
            _view.AdoptStreamedText(text);
            if (_options.AutoScrollToEnd)
            {
                _view.ScrollToEnd();
            }

            if (faulted)
            {
                if (!_announcedAborted)
                {
                    _announcedAborted = true;
                    Aborted?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            var state = _inner.State;
            if (state == PuraStreamingState.Completed && !_announcedCompleted)
            {
                _announcedCompleted = true;
                Completed?.Invoke(this, EventArgs.Empty);
            }
            else if (state != PuraStreamingState.Completed && !_announcedAborted)
            {
                _announcedAborted = true;
                Aborted?.Invoke(this, EventArgs.Empty);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Finish();
        }
        else
        {
            Dispatcher.UIThread.Post(Finish, DispatcherPriority.Background);
        }
    }

    private static ArcMarkdownStreamingState MapState(PuraStreamingState state) => state switch
    {
        PuraStreamingState.Completed => ArcMarkdownStreamingState.Completed,
        PuraStreamingState.Aborted or PuraStreamingState.Faulted => ArcMarkdownStreamingState.Aborted,
        PuraStreamingState.Created => ArcMarkdownStreamingState.Created,
        _ => ArcMarkdownStreamingState.Streaming,
    };
}
