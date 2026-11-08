using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>Overall state of the run whose events an <see cref="ArcAgentTranscript"/> shows.</summary>
public enum ArcAgentTranscriptStatus
{
    /// <summary>No run is active; the transcript keeps its last events.</summary>
    Idle,

    /// <summary>The agent is producing events.</summary>
    Running,

    /// <summary>The agent paused on an approval or a structured question.</summary>
    AwaitingInput,

    /// <summary>The run finished successfully.</summary>
    Done,

    /// <summary>The run ended with an error event.</summary>
    Failed,

    /// <summary>The user or host stopped the run.</summary>
    Stopped
}

/// <summary>The kind of one event in an agent run.</summary>
public enum ArcAgentEventKind
{
    /// <summary>A chat message from the user or the agent.</summary>
    Message,

    /// <summary>Reasoning shown as a compact "thinking" row.</summary>
    Thinking,

    /// <summary>A tool call with arguments and a result.</summary>
    Tool,

    /// <summary>A plan update.</summary>
    Plan,

    /// <summary>An approval request that blocks the run.</summary>
    Approval,

    /// <summary>A structured question that blocks the run.</summary>
    Ask,

    /// <summary>A result or artifact the run produced.</summary>
    Result,

    /// <summary>A failure that needs attention.</summary>
    Error
}

/// <summary>Per-event state inside a transcript row.</summary>
public enum ArcAgentEventStatus
{
    /// <summary>Queued but not started.</summary>
    Pending,

    /// <summary>In progress, or still streaming in.</summary>
    Running,

    /// <summary>Finished successfully.</summary>
    Done,

    /// <summary>Failed; the row offers a retry intent.</summary>
    Failed,

    /// <summary>Skipped because it was no longer needed.</summary>
    Skipped
}

/// <summary>
/// One read-only event in an agent run: a message, a thought, a tool call, a plan update, an approval or
/// question prompt, a result or an error. The model is deliberately flat so a host can append or update
/// events without the control knowing where they come from.
/// </summary>
public sealed class ArcAgentEvent
{
    /// <summary>Stable id used for expansion state and retry events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The event family, rendered as the row's leading icon.</summary>
    public ArcAgentEventKind Kind { get; set; }

    /// <summary>One line summary; the row title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional expanded detail: arguments, results, citations or error text.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The event's own state; drives the trailing badge.</summary>
    public ArcAgentEventStatus Status { get; set; }

    /// <summary>When the event happened. A default value hides the timestamp column.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// True when the message came from the person. User messages keep the compact plain-text row; every other
    /// message body is rendered as Markdown so headings, lists, code and pura-agent components work.
    /// </summary>
    public bool IsUserMessage { get; set; }
}

/// <summary>
/// The trusted record of an agent run: one scrollable, event-typed list where messages, reasoning, tool
/// calls, plans, approvals, questions, results and errors share the same row anatomy (type icon, summary,
/// state badge, expandable detail). The control only renders the events it is given and raises
/// <see cref="RetryRequested"/> when a failed row asks the host to run it again; it never appends, removes
/// or rewrites events itself.
///
/// Accessibility: the transcript header announces its status and event count, every row header is an
/// <see cref="ArcButton"/> (Tab to it, Enter or Space expands it) and the expanded detail stays in reading
/// order after its row.
/// </summary>
public class ArcAgentTranscript : Panel
{
    /// <summary>Every event in run order. Sample events are shown until the host sets its own.</summary>
    public static readonly StyledProperty<IEnumerable<ArcAgentEvent>?> EventsProperty =
        AvaloniaProperty.Register<ArcAgentTranscript, IEnumerable<ArcAgentEvent>?>(nameof(Events), SampleEvents());

    /// <summary>The run's overall state, shown in the header and never changed by the control.</summary>
    public static readonly StyledProperty<ArcAgentTranscriptStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAgentTranscript, ArcAgentTranscriptStatus>(
            nameof(Status), ArcAgentTranscriptStatus.Running);

    /// <summary>When true, the header shows a quiet "Live" pill for streaming output.</summary>
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<ArcAgentTranscript, bool>(nameof(IsStreaming), true);

    /// <summary>Copy for the empty state.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ArcAgentTranscript, string>(
            nameof(EmptyText), "No events yet. Start the agent to see its work here.");

    /// <summary>Optional Markdown environment for assistant message bodies; null uses the default pipeline.</summary>
    public static readonly StyledProperty<ArcMarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ArcAgentTranscript, ArcMarkdownOptions?>(nameof(MarkdownOptions));

    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcButton> _rowButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _rowContainers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcAgentEvent> _rowEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _rowTitles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcMarkdownView> _rowBodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcAgentEventStatus> _rowStatuses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcMarkdownInteraction> _interactions = new(StringComparer.Ordinal);
    private bool _seedExpanded = true;
    private string? _pendingFocus;
    private StackPanel? _rowHost;
    private ScrollViewer? _scroll;
    private TextBlock? _summary;
    private Control? _emptyState;
    private int _incrementalTotal;
    private int _dropped;
    private int _fullRebuildCount;
    private int _viewportRows = 40;
    private int _maxRenderedRows = 80;
    private bool _autoScroll = true;
    private bool _scrollScheduled;

    /// <summary>
    /// Optional factory for the Markdown environment used by assistant message bodies. The
    /// <c>Arc.Avalonia.Agent</c> composition root registers a pipeline with the <c>pura-agent</c> plugin here;
    /// a host may set it too, or assign <see cref="MarkdownOptions"/> on one transcript. When no factory is
    /// set the transcript falls back to <see cref="ArcMarkdownOptions.Default"/> (plain Markdown, no agent
    /// components), which keeps this leaf package independent from the Agent top layer.
    /// </summary>
    public static Func<ArcMarkdownOptions>? DefaultMarkdownOptionsFactory { get; set; }

    static ArcAgentTranscript()
    {
        EventsProperty.Changed.AddClassHandler<ArcAgentTranscript>((transcript, _) =>
        {
            transcript._seedExpanded = true;
            transcript.Rebuild();
        });
        StatusProperty.Changed.AddClassHandler<ArcAgentTranscript>((transcript, _) => transcript.Rebuild());
        IsStreamingProperty.Changed.AddClassHandler<ArcAgentTranscript>((transcript, _) => transcript.Rebuild());
        EmptyTextProperty.Changed.AddClassHandler<ArcAgentTranscript>((transcript, _) => transcript.Rebuild());
        MarkdownOptionsProperty.Changed.AddClassHandler<ArcAgentTranscript>((transcript, _) => transcript.Rebuild());
    }

    public ArcAgentTranscript()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>Every event in run order.</summary>
    public IEnumerable<ArcAgentEvent>? Events
    {
        get => GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    /// <summary>Overall run state: idle, running, awaiting input, done, failed or stopped.</summary>
    public ArcAgentTranscriptStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Shows the live streaming marker beside the state badge.</summary>
    public bool IsStreaming
    {
        get => GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    /// <summary>Copy shown when <see cref="Events"/> is empty.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Markdown environment for assistant message bodies; null uses the static default factory.</summary>
    public ArcMarkdownOptions? MarkdownOptions
    {
        get => GetValue(MarkdownOptionsProperty);
        set => SetValue(MarkdownOptionsProperty, value);
    }

    /// <summary>
    /// Persisted pura-agent interactions reported by message bodies, keyed by component id. The transcript
    /// applies them when a row is rebuilt, so virtualization and status updates keep the chosen state.
    /// </summary>
    public IReadOnlyDictionary<string, ArcMarkdownInteraction> InteractionStates => _interactions;

    /// <summary>Raised when an interactive component inside an assistant message reports an interaction.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Stores one interaction for later row rebuilds (the host may also persist it).</summary>
    /// <param name="interaction">Recorded interaction.</param>
    public void SetInteractionState(ArcMarkdownInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        _interactions[interaction.ComponentId] = interaction;
        ApplyInteractionToBodies();
    }

    /// <summary>Restores a set of persisted interactions and re-applies them to the realized message bodies.</summary>
    /// <param name="states">Interactions keyed by component id.</param>
    public void ApplyInteractionStates(IEnumerable<ArcMarkdownInteraction> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        foreach (var state in states)
        {
            _interactions[state.ComponentId] = state;
        }

        ApplyInteractionToBodies();
    }

    /// <summary>Raised when a failed event's Retry action is pressed; the host decides whether to re-run it.</summary>
    public static readonly RoutedEvent<ArcAgentRetryRequestedRoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ArcAgentTranscript, ArcAgentRetryRequestedRoutedEventArgs>("RetryRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="RetryRequested"/>; incremental streaming behaviour is unchanged.</summary>
    public event EventHandler<ArcAgentRetryRequestedRoutedEventArgs>? RetryRequestedRouted
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    public event EventHandler<ArcAgentEvent>? RetryRequested;

    /// <summary>
    /// M2-G4 incremental streaming: rows appended through <see cref="Append"/> are added at the tail without
    /// rebuilding the tree. The rendered window is bounded by <see cref="MaxRenderedRows"/> so a 10,000-event
    /// run never realizes a 10,000-control visual tree; older rows are dropped from the live tree (their count
    /// is still reported by <see cref="TotalEventCount"/>). Full scrollback virtualization remains a later
    /// milestone; existing pages that assign <see cref="Events"/> keep the original rebuild path.
    /// </summary>
    public int FullRebuildCount => _fullRebuildCount;

    /// <summary>Number of row controls currently realized in the body.</summary>
    public int RenderedRowCount => _rowHost?.Children.Count ?? 0;

    /// <summary>Number of events accepted through the incremental path.</summary>
    public int TotalEventCount => _incrementalTotal;

    /// <summary>Number of rows dropped from the front because the rendered window was full.</summary>
    public int DroppedRowCount => _dropped;

    /// <summary>Rows assumed visible in the viewport; the default window is twice this.</summary>
    public int ViewportRows
    {
        get => _viewportRows;
        set
        {
            _viewportRows = Math.Max(1, value);
            MaxRenderedRows = Math.Max(_viewportRows, _viewportRows * 2);
        }
    }

    /// <summary>Maximum number of realized rows in incremental mode (defaults to 2 × <see cref="ViewportRows"/>).</summary>
    public int MaxRenderedRows
    {
        get => _maxRenderedRows;
        set
        {
            _maxRenderedRows = Math.Max(1, value);
            TrimWindow();
        }
    }

    /// <summary>Appends one event at the tail (O(1) in the number of previously rendered rows) and updates the
    /// row in place when its id already exists (streaming deltas).</summary>
    public void Append(ArcAgentEvent evt)
    {
        AppendOrUpdate(evt);
    }

    /// <summary>Appends a batch and scrolls once.</summary>
    public void AppendRange(IEnumerable<ArcAgentEvent> events)
    {
        foreach (var evt in events)
        {
            AppendOrUpdate(evt);
        }

        ScheduleAutoScroll();
    }

    /// <summary>Appends a row, or replaces the existing row with the same id without rebuilding the tree.</summary>
    public void AppendOrUpdate(ArcAgentEvent evt)
    {
        if (_rowHost is null)
        {
            Rebuild();
        }

        if (_rowHost is null)
        {
            return;
        }

        if (_rowEvents.TryGetValue(evt.Id, out _) && _rowContainers.TryGetValue(evt.Id, out var existing))
        {
            if (_rowStatuses.TryGetValue(evt.Id, out var previousStatus) && previousStatus == evt.Status)
            {
                if (_rowBodies.TryGetValue(evt.Id, out var bodyView))
                {
                    // Streaming deltas of a Markdown message only change the source: re-set it in place.
                    bodyView.Markdown = MarkdownBody(evt);
                    _rowEvents[evt.Id] = evt;
                    _incrementalTotal++;
                    UpdateHeader();
                    AutomationProperties.SetName(this, $"Agent transcript, {_incrementalTotal} events, {StatusWord(Status)}");
                    return;
                }

                if (_rowTitles.TryGetValue(evt.Id, out var titleBlock))
                {
                    // Streaming deltas only change the text: mutate the existing TextBlock instead of rebuilding.
                    titleBlock.Text = string.IsNullOrWhiteSpace(evt.Title) ? "(no title)" : evt.Title;
                    _rowEvents[evt.Id] = evt;
                    _incrementalTotal++;
                    UpdateHeader();
                    AutomationProperties.SetName(this, $"Agent transcript, {_incrementalTotal} events, {StatusWord(Status)}");
                    return;
                }
            }

            var index = _rowHost.Children.IndexOf(existing);
            if (index >= 0)
            {
                _rowBodies.Remove(evt.Id);
                var replacement = BuildRow(evt);
                _rowHost.Children[index] = replacement;
                _rowContainers[evt.Id] = replacement;
                _rowEvents[evt.Id] = evt;
                _rowStatuses[evt.Id] = evt.Status;
                _incrementalTotal++;
                UpdateHeader();
                AutomationProperties.SetName(this, $"Agent transcript, {_incrementalTotal} events, {StatusWord(Status)}");
                return;
            }
        }

        var row = BuildRow(evt);
        if (_emptyState is not null && _rowHost.Children.Contains(_emptyState))
        {
            _rowHost.Children.Remove(_emptyState);
            _emptyState = null;
        }

        _rowHost.Children.Add(row);
        _rowContainers[evt.Id] = row;
        _rowEvents[evt.Id] = evt;
        _rowStatuses[evt.Id] = evt.Status;
        _incrementalTotal++;
        TrimWindow();
        UpdateHeader();
        AutomationProperties.SetName(this, $"Agent transcript, {_incrementalTotal} events, {StatusWord(Status)}");
        ScheduleAutoScroll();
    }

    /// <summary>Clears rows accumulated through the incremental path.</summary>
    public void ResetIncremental()
    {
        _incrementalTotal = 0;
        _dropped = 0;
        _rowEvents.Clear();
        _rowContainers.Clear();
        _rowTitles.Clear();
        _rowBodies.Clear();
        _rowStatuses.Clear();
        _rowButtons.Clear();
        _rowHost?.Children.Clear();
        if (_rowHost is not null)
        {
            _emptyState = BuildEmptyState();
            _rowHost.Children.Add(_emptyState);
        }

        UpdateHeader();
    }

    private void TrimWindow()
    {
        if (_rowHost is null)
        {
            return;
        }

        while (_rowHost.Children.Count > MaxRenderedRows)
        {
            var oldest = _rowHost.Children[0];
            _rowHost.Children.RemoveAt(0);
            foreach (var pair in _rowContainers.ToArray())
            {
                if (ReferenceEquals(pair.Value, oldest))
                {
                    _rowContainers.Remove(pair.Key);
                    _rowEvents.Remove(pair.Key);
                    _rowTitles.Remove(pair.Key);
                    _rowBodies.Remove(pair.Key);
                    _rowStatuses.Remove(pair.Key);
                    _rowButtons.Remove(pair.Key);
                    break;
                }
            }

            _dropped++;
        }
    }

    private void UpdateHeader()
    {
        if (_summary is not null)
        {
            _summary.Text = Summary(_incrementalTotal);
        }
    }

    private void ScheduleAutoScroll()
    {
        if (!_autoScroll || _scroll is null || _scrollScheduled)
        {
            return;
        }

        _scrollScheduled = true;
        var scroll = _scroll;
        Dispatcher.UIThread.Post(
            () =>
            {
                _scrollScheduled = false;
                scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
            },
            DispatcherPriority.Loaded);
    }

    private void Rebuild()
    {
        _fullRebuildCount++;
        var events = (Events ?? Array.Empty<ArcAgentEvent>()).ToList();

        if (_seedExpanded)
        {
            _seedExpanded = false;
            _expanded.Clear();
            var seed = events.FirstOrDefault(item => item.Status == ArcAgentEventStatus.Failed
                                                     && !string.IsNullOrWhiteSpace(item.Detail))
                       ?? events.LastOrDefault(item => item.Kind is ArcAgentEventKind.Tool
                                                       && !string.IsNullOrWhiteSpace(item.Detail));
            if (seed is not null)
            {
                _expanded.Add(seed.Id);
            }
        }

        Children.Clear();
        _rowButtons.Clear();
        _rowContainers.Clear();
        _rowEvents.Clear();
        _rowTitles.Clear();
        _rowBodies.Clear();
        _rowStatuses.Clear();
        _rowHost = null;
        _scroll = null;
        _summary = null;
        _incrementalTotal = events.Count;
        _dropped = 0;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 10
        };

        root.Children.Add(BuildHeader(events.Count));

        var divider = ArcUi.Divider();
        Grid.SetRow(divider, 1);
        root.Children.Add(divider);

        var list = new StackPanel { Spacing = 2 };
        _rowHost = list;
        _scroll = new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        if (events.Count == 0)
        {
            _emptyState = BuildEmptyState();
            list.Children.Add(_emptyState);
        }
        else
        {
            _emptyState = null;
            foreach (var evt in events)
            {
                var row = BuildRow(evt);
                list.Children.Add(row);
                _rowContainers[evt.Id] = row;
                _rowEvents[evt.Id] = evt;
            }
        }

        Grid.SetRow(_scroll, 2);
        root.Children.Add(_scroll);

        var live = new TextBlock { Opacity = 0, Height = 1, IsHitTestVisible = false };
        AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Polite);
        root.Children.Add(live);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(16),
            Child = root
        });

        ArcUi.Name(
            this,
            $"Agent transcript, {events.Count} events, {StatusWord(Status)}",
            "A read-only record of the agent's messages, tool calls, plans, approvals and results.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        if (_pendingFocus is not null)
        {
            var id = _pendingFocus;
            _pendingFocus = null;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (_rowButtons.TryGetValue(id, out var button))
                    {
                        button.Focus();
                    }
                },
                DispatcherPriority.Loaded);
        }
    }

    private Control BuildHeader(int count)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(ArcUi.Label("Transcript"));
        _summary = ArcUi.Caption(Summary(count));
        titles.Children.Add(_summary);
        header.Children.Add(titles);

        var state = ArcUi.HStack(6);
        state.Children.Add(StatusBadge(Status));
        if (IsStreaming && Status is ArcAgentTranscriptStatus.Running or ArcAgentTranscriptStatus.AwaitingInput)
        {
            state.Children.Add(ArcUi.Pill("Live", "activity", accent: true));
        }

        Grid.SetColumn(state, 1);
        header.Children.Add(state);
        return header;
    }

    private string Summary(int count)
    {
        var noun = count == 1 ? "event" : "events";
        return Status switch
        {
            ArcAgentTranscriptStatus.AwaitingInput => $"{count} {noun} · waiting for your input",
            ArcAgentTranscriptStatus.Done => $"{count} {noun} · run finished",
            ArcAgentTranscriptStatus.Failed => $"{count} {noun} · the run hit an error",
            ArcAgentTranscriptStatus.Stopped => $"{count} {noun} · run stopped",
            ArcAgentTranscriptStatus.Idle => $"{count} {noun} · ready when you are",
            _ => $"{count} {noun} · the agent is working"
        };
    }

    private Control BuildRow(ArcAgentEvent evt)
    {
        // Assistant/system messages are the transcript "body": render them as Markdown so headings, lists,
        // code and pura-agent components work. User messages keep the compact plain-text row.
        if (evt.Kind == ArcAgentEventKind.Message && !evt.IsUserMessage && !string.IsNullOrWhiteSpace(MarkdownBody(evt)))
        {
            return BuildMessageRow(evt);
        }

        var expanded = _expanded.Contains(evt.Id);

        var headerContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("26,*,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        headerContent.Children.Add(KindIcon(evt.Kind));

        var title = ArcUi.Text(evt.Title, "arc-body-sm");
        _rowTitles[evt.Id] = title;
        title.VerticalAlignment = VerticalAlignment.Center;
        if (evt.Status is ArcAgentEventStatus.Done or ArcAgentEventStatus.Skipped)
        {
            title.Foreground = ArcUi.Brush(ArcTokens.TextSecondary);
        }
        else if (evt.Status == ArcAgentEventStatus.Pending)
        {
            title.Foreground = ArcUi.Brush(ArcTokens.TextMuted);
        }

        Grid.SetColumn(title, 1);
        headerContent.Children.Add(title);

        if (evt.Timestamp != default)
        {
            var time = ArcUi.Text(evt.Timestamp.ToLocalTime().ToString("h:mm tt", DisplayCulture), "arc-caption",
                wrap: TextWrapping.NoWrap);
            time.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(time, 2);
            headerContent.Children.Add(time);
        }

        var badge = StatusBadge(evt.Status);
        badge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(badge, 3);
        headerContent.Children.Add(badge);

        var chevron = ArcUi.Icon(expanded ? "chevron-up" : "chevron-down", 14, ArcTokens.TextMuted);
        chevron.Margin = new Thickness(2, 0, 4, 0);
        Grid.SetColumn(chevron, 4);
        headerContent.Children.Add(chevron);

        var headerButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(6, 4, 6, 4),
            Content = headerContent
        };
        AutomationProperties.SetName(headerButton,
            $"{KindWord(evt.Kind)}: {evt.Title}. {StatusWord(evt.Status)}.");
        AutomationProperties.SetHelpText(headerButton,
            expanded ? "Expanded. Press Enter or Space to collapse the detail." : "Press Enter or Space to show the detail.");
        headerButton.Click += (_, _) => ToggleRow(evt.Id);

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(headerButton);
        if (expanded)
        {
            stack.Children.Add(BuildDetail(evt));
        }

        _rowButtons[evt.Id] = headerButton;

        return new Border
        {
            Background = expanded ? ArcUi.Brush(ArcTokens.SurfaceMuted) : null,
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(2),
            Child = stack
        };
    }

    /// <summary>
    /// Markdown body row for assistant messages: a type icon, the rendered document and a quiet metadata line.
    /// Interactive agent components inside the document report through <see cref="InteractionOccurred"/> and are
    /// re-applied from <see cref="InteractionStates"/> whenever the row is rebuilt.
    /// </summary>
    private Control BuildMessageRow(ArcAgentEvent evt)
    {
        var expanded = _expanded.Contains(evt.Id);

        var body = new ArcMarkdownView
        {
            Markdown = MarkdownBody(evt),
            Options = ResolveMarkdownOptions(),
            Margin = new Thickness(0, 0, 0, 2)
        };
        foreach (var state in _interactions.Values)
        {
            body.SetInteractionState(state);
        }

        body.InteractionOccurred += (_, args) => OnMarkdownInteraction(args);
        AutomationProperties.SetName(
            body,
            $"Message: {Shorten(evt.Title, 240)}. {StatusWord(evt.Status)}.");
        _rowBodies[evt.Id] = body;

        var right = new StackPanel { Spacing = 4 };
        right.Children.Add(body);

        var meta = ArcUi.HStack(8);
        meta.Children.Add(StatusBadge(evt.Status));
        if (evt.Timestamp != default)
        {
            meta.Children.Add(ArcUi.Caption(evt.Timestamp.ToLocalTime().ToString("MMM d, h:mm tt", DisplayCulture)));
        }

        if (!string.IsNullOrWhiteSpace(evt.Detail))
        {
            var detailToggle = ArcUi.Button(
                expanded ? "Hide detail" : "Detail",
                ArcButtonVariant.Ghost,
                ArcButtonSize.Sm,
                expanded ? "chevron-up" : "chevron-down");
            AutomationProperties.SetName(detailToggle,
                expanded ? "Collapse the extra detail" : "Show the extra detail");
            detailToggle.Click += (_, _) => ToggleMessageDetail(evt.Id);
            meta.Children.Add(detailToggle);
            _rowButtons[evt.Id] = detailToggle;
        }

        right.Children.Add(meta);
        if (expanded && !string.IsNullOrWhiteSpace(evt.Detail))
        {
            right.Children.Add(ArcUi.Body(evt.Detail, 640));
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("26,*"),
            ColumnSpacing = 8
        };
        grid.Children.Add(KindIcon(evt.Kind));
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return new Border
        {
            Background = expanded ? ArcUi.Brush(ArcTokens.SurfaceMuted) : null,
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(2),
            Child = grid
        };
    }

    private ArcMarkdownOptions ResolveMarkdownOptions()
        => MarkdownOptions ?? DefaultMarkdownOptionsFactory?.Invoke() ?? ArcMarkdownOptions.Default;

    private static string MarkdownBody(ArcAgentEvent evt)
        => string.IsNullOrWhiteSpace(evt.Title) ? evt.Detail : evt.Title;

    private static string Shorten(string text, int max)
        => string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max] + "…";

    private void ToggleMessageDetail(string id)
    {
        if (!_expanded.Remove(id))
        {
            _expanded.Add(id);
        }

        if (_rowHost is null ||
            !_rowEvents.TryGetValue(id, out var evt) ||
            !_rowContainers.TryGetValue(id, out var existing))
        {
            return;
        }

        var index = _rowHost.Children.IndexOf(existing);
        if (index < 0)
        {
            return;
        }

        _rowBodies.Remove(id);
        var replacement = BuildRow(evt);
        _rowHost.Children[index] = replacement;
        _rowContainers[id] = replacement;
    }

    private void OnMarkdownInteraction(ArcMarkdownInteractionEventArgs args)
    {
        var interaction = new ArcMarkdownInteraction(
            args.ComponentType,
            args.ComponentId,
            args.SelectedIds,
            args.Confirmed,
            args.Value);
        _interactions[interaction.ComponentId] = interaction;
        InteractionOccurred?.Invoke(this, args);
    }

    private void ApplyInteractionToBodies()
    {
        foreach (var body in _rowBodies.Values)
        {
            body.ApplyInteractionStates(_interactions.Values);
        }
    }

    private Control BuildDetail(ArcAgentEvent evt)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(34, 0, 8, 6) };
        panel.Children.Add(ArcUi.Body(
            string.IsNullOrWhiteSpace(evt.Detail) ? "No extra detail recorded for this event." : evt.Detail,
            620));

        if (evt.Status == ArcAgentEventStatus.Failed)
        {
            var retry = ArcUi.Button("Retry", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "refresh-cw");
            retry.HorizontalAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetName(retry, $"Retry {evt.Title}");
            retry.Click += (_, _) =>
            {
                RetryRequested?.Invoke(this, evt);
                RaiseEvent(new ArcAgentRetryRequestedRoutedEventArgs(RetryRequestedEvent, evt));
            };
            panel.Children.Add(retry);
        }

        var meta = ArcUi.HStack(8);
        meta.Children.Add(ArcUi.Caption($"{KindWord(evt.Kind)} · {StatusWord(evt.Status)}"));
        if (evt.Timestamp != default)
        {
            meta.Children.Add(ArcUi.Caption("·"));
            meta.Children.Add(ArcUi.Caption(evt.Timestamp.ToLocalTime().ToString("MMM d, h:mm tt", DisplayCulture)));
        }

        panel.Children.Add(meta);
        return panel;
    }

    private Control BuildEmptyState()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 32, 0, 32)
        };
        panel.Children.Add(new ArcIcon
        {
            Icon = "inbox",
            Size = 26,
            Foreground = ArcUi.Brush(ArcTokens.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var copy = ArcUi.Text(EmptyText, "arc-body-sm arc-secondary", 420);
        copy.HorizontalAlignment = HorizontalAlignment.Center;
        copy.TextAlignment = TextAlignment.Center;
        panel.Children.Add(copy);
        AutomationProperties.SetName(panel, EmptyText);
        return panel;
    }

    private void ToggleRow(string id)
    {
        if (!_expanded.Remove(id))
        {
            _expanded.Add(id);
        }

        _pendingFocus = id;
        Rebuild();
    }

    private static Control KindIcon(ArcAgentEventKind kind)
    {
        var (icon, token) = kind switch
        {
            ArcAgentEventKind.Message => ("message-circle", ArcTokens.TextSecondary),
            ArcAgentEventKind.Thinking => ("sparkles", ArcTokens.TextMuted),
            ArcAgentEventKind.Tool => ("code", ArcTokens.TextSecondary),
            ArcAgentEventKind.Plan => ("list-ordered", ArcTokens.TextSecondary),
            ArcAgentEventKind.Approval => ("shield-check", ArcTokens.Warning),
            ArcAgentEventKind.Ask => ("circle-help", ArcTokens.Accent),
            ArcAgentEventKind.Result => ("circle-check", ArcTokens.Success),
            _ => ("circle-alert", ArcTokens.Danger)
        };

        return new Border
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
                Foreground = ArcUi.Brush(token),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static ArcBadge StatusBadge(ArcAgentTranscriptStatus status) => status switch
    {
        ArcAgentTranscriptStatus.Running => ArcUi.Badge("Running", ArcBadgeTone.Accent),
        ArcAgentTranscriptStatus.AwaitingInput => ArcUi.Badge("Awaiting input", ArcBadgeTone.Warning),
        ArcAgentTranscriptStatus.Done => ArcUi.Badge("Done", ArcBadgeTone.Success),
        ArcAgentTranscriptStatus.Failed => ArcUi.Badge("Failed", ArcBadgeTone.Danger),
        ArcAgentTranscriptStatus.Stopped => ArcUi.Badge("Stopped", ArcBadgeTone.Neutral),
        _ => ArcUi.Badge("Idle", ArcBadgeTone.Neutral)
    };

    private static ArcBadge StatusBadge(ArcAgentEventStatus status) => status switch
    {
        ArcAgentEventStatus.Running => ArcUi.Badge("Running", ArcBadgeTone.Accent),
        ArcAgentEventStatus.Done => ArcUi.Badge("Done", ArcBadgeTone.Success),
        ArcAgentEventStatus.Failed => ArcUi.Badge("Failed", ArcBadgeTone.Danger),
        ArcAgentEventStatus.Skipped => ArcUi.Badge("Skipped", ArcBadgeTone.Neutral),
        _ => ArcUi.Badge("Pending", ArcBadgeTone.Neutral)
    };

    private static string KindWord(ArcAgentEventKind kind) => kind switch
    {
        ArcAgentEventKind.Message => "Message",
        ArcAgentEventKind.Thinking => "Thinking",
        ArcAgentEventKind.Tool => "Tool call",
        ArcAgentEventKind.Plan => "Plan",
        ArcAgentEventKind.Approval => "Approval",
        ArcAgentEventKind.Ask => "Question",
        ArcAgentEventKind.Result => "Result",
        _ => "Error"
    };

    private static string StatusWord(ArcAgentEventStatus status) => status switch
    {
        ArcAgentEventStatus.Running => "In progress",
        ArcAgentEventStatus.Done => "Done",
        ArcAgentEventStatus.Failed => "Failed",
        ArcAgentEventStatus.Skipped => "Skipped",
        _ => "Pending"
    };

    private static string StatusWord(ArcAgentTranscriptStatus status) => status switch
    {
        ArcAgentTranscriptStatus.Running => "Running",
        ArcAgentTranscriptStatus.AwaitingInput => "Awaiting input",
        ArcAgentTranscriptStatus.Done => "Done",
        ArcAgentTranscriptStatus.Failed => "Failed",
        ArcAgentTranscriptStatus.Stopped => "Stopped",
        _ => "Idle"
    };

    private static IEnumerable<ArcAgentEvent> SampleEvents()
    {
        var now = DateTimeOffset.Now;
        return new[]
        {
            new ArcAgentEvent
            {
                Id = "e1",
                Kind = ArcAgentEventKind.Message,
                Title = "Add rate limiting to the public API, then show me the diff.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-3),
                IsUserMessage = true
            },
            new ArcAgentEvent
            {
                Id = "e2",
                Kind = ArcAgentEventKind.Thinking,
                Title = "Checking the existing middleware and the request tests.",
                Detail = "The API already has a request logger wrapping every route, so the limiter can live beside it.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-2.8)
            },
            new ArcAgentEvent
            {
                Id = "e3",
                Kind = ArcAgentEventKind.Tool,
                Title = "Read src/middleware/limits.ts",
                Detail = "42 lines. Exports createLimiter(options) with a fixed window counter.",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-2.5)
            },
            new ArcAgentEvent
            {
                Id = "e4",
                Kind = ArcAgentEventKind.Plan,
                Title = "5 steps: limiter, Redis counters, 429 response, tests, docs",
                Status = ArcAgentEventStatus.Done,
                Timestamp = now.AddMinutes(-2.2)
            },
            new ArcAgentEvent
            {
                Id = "e5",
                Kind = ArcAgentEventKind.Tool,
                Title = "Edit src/middleware/limits.ts",
                Detail = "Replaced the fixed window counter with a sliding window over a 60 second TTL.",
                Status = ArcAgentEventStatus.Running,
                Timestamp = now.AddMinutes(-0.4)
            }
        };
    }
}



