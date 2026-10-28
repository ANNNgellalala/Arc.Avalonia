using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

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

    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcButton> _rowButtons = new(StringComparer.Ordinal);
    private bool _seedExpanded = true;
    private string? _pendingFocus;

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

    /// <summary>Raised when a failed event's Retry action is pressed; the host decides whether to re-run it.</summary>
    public event EventHandler<ArcAgentEvent>? RetryRequested;

    private void Rebuild()
    {
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

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 10
        };

        root.Children.Add(BuildHeader(events.Count));

        var divider = ArcUi.Divider();
        Grid.SetRow(divider, 1);
        root.Children.Add(divider);

        var body = new Control();
        if (events.Count == 0)
        {
            body = BuildEmptyState();
        }
        else
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var evt in events)
            {
                list.Children.Add(BuildRow(evt));
            }

            body = new ScrollViewer
            {
                Content = list,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        Grid.SetRow(body, 2);
        root.Children.Add(body);

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
        titles.Children.Add(ArcUi.Caption(Summary(count)));
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
        var expanded = _expanded.Contains(evt.Id);

        var headerContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("26,*,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        headerContent.Children.Add(KindIcon(evt.Kind));

        var title = ArcUi.Text(evt.Title, "arc-body-sm");
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
            retry.Click += (_, _) => RetryRequested?.Invoke(this, evt);
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
                Timestamp = now.AddMinutes(-3)
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
