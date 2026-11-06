using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

// The Feedback assembly also has an internal FeedbackUi helper for its older components, so the shared P0
// composition helpers from the foundation are aliased explicitly.
using Ui = Arc.Avalonia.Controls.ArcUi;

namespace Arc.Avalonia.LifeToday.Controls;

/// <summary>
/// The due state of a reminder row. The words are always rendered beside the due time, so colour is
/// never the only signal.
/// </summary>
public enum ArcReminderDueState
{
    /// <summary>Not due yet.</summary>
    Upcoming,

    /// <summary>Due now or within the current reminder window.</summary>
    Due,

    /// <summary>The due time has passed and the item is still open.</summary>
    Overdue,

    /// <summary>Deferred to a later time; the host owns the new due time.</summary>
    Snoozed,

    /// <summary>Completed; the host owns the completion timestamp.</summary>
    Done,

    /// <summary>The due window was missed and the item is still open.</summary>
    Missed
}

/// <summary>
/// The atomic row for a life action: a reminder, bill, dose, chore or renewal. It shows a leading
/// completion control, a due badge with relative copy, repeat and source metadata, and a trailing
/// expandable action strip (delay 10 minutes, delay 1 hour, tomorrow, reschedule, skip).
///
/// Pure UI contract: the row only renders the state it is given and raises intent events. It never reads
/// or writes reminder storage, never schedules a timer and never sends a system notification; the host
/// applies every action and then feeds the new state back in. The completion control toggles its own
/// <see cref="IsChecked"/> display and raises <see cref="CompletedChanged"/> so the host can persist the
/// timestamp and offer undo.
/// </summary>
public class ArcReminderRow : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcReminderRow, string>(nameof(Title), "Take the recycling out");

    public static readonly StyledProperty<string> DueTextProperty =
        AvaloniaProperty.Register<ArcReminderRow, string>(nameof(DueText), "Today · 18:00");

    public static readonly StyledProperty<ArcReminderDueState> DueStateProperty =
        AvaloniaProperty.Register<ArcReminderRow, ArcReminderDueState>(nameof(DueState), ArcReminderDueState.Due);

    public static readonly StyledProperty<string?> RepeatLabelProperty =
        AvaloniaProperty.Register<ArcReminderRow, string?>(nameof(RepeatLabel), "Every Wednesday");

    public static readonly StyledProperty<string?> SourceLabelProperty =
        AvaloniaProperty.Register<ArcReminderRow, string?>(nameof(SourceLabel), "Home");

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ArcReminderRow, string?>(nameof(Note),
            "Created by the agent from the household routine. The host owns the source link.");

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ArcReminderRow, bool>(nameof(IsChecked));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcReminderRow, bool>(nameof(IsExpanded));

    private bool _built;
    private bool _actionsOpen;
    private Border? _surface;

    static ArcReminderRow()
    {
        TitleProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        DueTextProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        DueStateProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        RepeatLabelProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        SourceLabelProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        NoteProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        IsCheckedProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) => row.Rebuild());
        IsExpandedProperty.Changed.AddClassHandler<ArcReminderRow>((row, _) =>
        {
            row._actionsOpen = row.IsExpanded;
            row.Rebuild();
        });
    }

    public ArcReminderRow()
    {
        Focusable = true;
        ClipToBounds = false;
        AddHandler(KeyDownEvent, OnRowKeyDown, RoutingStrategies.Tunnel);
        GotFocus += (_, _) => UpdateFocusVisual();
        LostFocus += (_, _) => UpdateFocusVisual();
        _built = true;
        Rebuild();
    }

    /// <summary>Title of the action, for example "Take the recycling out".</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Relative due copy with its basis, for example "Today · 18:00" or "2 days overdue".</summary>
    public string DueText
    {
        get => GetValue(DueTextProperty);
        set => SetValue(DueTextProperty, value);
    }

    /// <summary>Due state; drives the badge word and tone. Never the only signal: the word is always rendered.</summary>
    public ArcReminderDueState DueState
    {
        get => GetValue(DueStateProperty);
        set => SetValue(DueStateProperty, value);
    }

    /// <summary>Optional repeat label, for example "Every Wednesday". No recurrence rule is evaluated here.</summary>
    public string? RepeatLabel
    {
        get => GetValue(RepeatLabelProperty);
        set => SetValue(RepeatLabelProperty, value);
    }

    /// <summary>Optional source label, for example "Bills", "Health" or "Home".</summary>
    public string? SourceLabel
    {
        get => GetValue(SourceLabelProperty);
        set => SetValue(SourceLabelProperty, value);
    }

    /// <summary>Optional note shown when the row is expanded.</summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>
    /// Checked display of the leading completion control. The row toggles it locally for immediate
    /// feedback and raises <see cref="CompletedChanged"/>; the host persists the result.
    /// </summary>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Expands the note and the inline action strip.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Raised when the completion control is toggled; the argument is the new checked state.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> CompletedChangedEvent =
        RoutedEvent.Register<ArcReminderRow, ArcLifeTodayRoutedEventArgs>("CompletedChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="CompletedChanged"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? CompletedChangedRouted
    {
        add => AddHandler(CompletedChangedEvent, value);
        remove => RemoveHandler(CompletedChangedEvent, value);
    }

    public event Action<bool>? CompletedChanged;

    /// <summary>Raised when the user asks to delay the reminder by the given span.</summary>
    public event Action<TimeSpan>? SnoozeRequested;

    /// <summary>Raised when the user asks to reschedule the reminder to a new date or time.</summary>
    public event Action? RescheduleRequested;

    /// <summary>Raised when the user asks to skip this occurrence.</summary>
    public event Action? SkipRequested;

    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (ReferenceEquals(e.Source, this) && e.Key is Key.Enter or Key.Space)
        {
            IsChecked = !IsChecked;
            CompletedChanged?.Invoke(IsChecked);
        RaiseEvent(new ArcLifeTodayRoutedEventArgs(CompletedChangedEvent) { Flag = IsChecked });
            e.Handled = true;
            return;
        }

        if (e.Key != Key.M)
        {
            return;
        }

        _actionsOpen = !_actionsOpen;
        IsExpanded = _actionsOpen;
        e.Handled = true;
    }

    /// <summary>Keeps a visible focus cue on the row when the row itself (not one of its buttons) has focus.</summary>
    private void UpdateFocusVisual()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.BorderBrush = Ui.Brush(IsFocused ? ArcTokens.BorderStrong : ArcTokens.BorderSubtle);
    }

    private void Rebuild()
    {
        if (!_built)
        {
            return;
        }

        Children.Clear();
        Children.Add(BuildRow());
    }

    private Control BuildRow()
    {
        var done = IsChecked || DueState == ArcReminderDueState.Done;
        var (stateWord, tone) = StateVisual(DueState);
        var stateKey = StateKey();

        Ui.Name(this, $"{Title}, {stateWord}, {DueText}",
            "Reminder row. Enter toggles completion, M opens the inline actions.");

        var check = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Width = 32,
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Top,
            Background = Ui.Brush(done ? ArcTokens.SuccessSubtle : ArcTokens.SurfaceMuted),
            BorderBrush = Ui.Brush(done ? ArcTokens.Success : ArcTokens.BorderStrong),
            BorderThickness = new Thickness(1),
            Content = done
                ? Ui.Icon("check", 16, ArcTokens.Success)
                : Ui.Icon("circle", 14, ArcTokens.TextMuted)
        };
        Ui.Name(check, done ? $"Mark {Title} as not done" : $"Mark {Title} as done");
        check.Click += (_, _) =>
        {
            IsChecked = !IsChecked;
            CompletedChanged?.Invoke(IsChecked);
        RaiseEvent(new ArcLifeTodayRoutedEventArgs(CompletedChangedEvent) { Flag = IsChecked });
        };

        var title = Ui.Text(Title, "arc-body arc-medium");
        if (done || DueState == ArcReminderDueState.Missed)
        {
            title = Ui.Text(Title, "arc-body arc-medium arc-secondary");
        }

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        meta.Children.Add(Ui.Badge(stateWord, tone));
        meta.Children.Add(Ui.Text(DueText, "arc-caption", wrap: TextWrapping.NoWrap));
        if (!string.IsNullOrWhiteSpace(RepeatLabel))
        {
            meta.Children.Add(Ui.Pill(RepeatLabel!, "refresh-cw"));
        }

        if (!string.IsNullOrWhiteSpace(SourceLabel))
        {
            meta.Children.Add(Ui.Pill(SourceLabel!, "tag"));
        }

        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(meta);

        var more = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 30,
            Height = 30,
            MinHeight = 30,
            VerticalAlignment = VerticalAlignment.Top,
            Content = Ui.Icon(_actionsOpen ? "chevron-up" : "more-horizontal", 16, ArcTokens.TextSecondary)
        };
        Ui.Name(more, _actionsOpen ? "Hide reminder actions" : "Show reminder actions");
        more.Click += (_, _) =>
        {
            _actionsOpen = !_actionsOpen;
            IsExpanded = _actionsOpen;
        };

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10
        };
        top.Children.Add(check);
        Grid.SetColumn(text, 1);
        top.Children.Add(text);
        Grid.SetColumn(more, 2);
        top.Children.Add(more);

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(top);

        if (_actionsOpen && !string.IsNullOrWhiteSpace(Note))
        {
            body.Children.Add(Ui.Divider());
            body.Children.Add(Ui.Body(Note!, 640));
        }

        if (_actionsOpen)
        {
            body.Children.Add(Ui.Divider());
            body.Children.Add(BuildActions());
        }

        _surface = new Border
        {
            Background = Ui.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = Ui.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = body
        };
        UpdateFocusVisual();
        return _surface;
    }

    private Control BuildActions()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        void Add(string label, string icon, string name, Action run)
        {
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Height = 28,
                MinHeight = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 6, 6),
                Content = Ui.HStack(6, Ui.Icon(icon, 13, ArcTokens.TextSecondary), Ui.Text(label, "arc-body-sm", wrap: TextWrapping.NoWrap))
            };
            Ui.Name(button, name);
            button.Click += (_, _) => run();
            wrap.Children.Add(button);
        }

        Add("Delay 10 min", "clock", $"Delay {Title} by 10 minutes", () => SnoozeRequested?.Invoke(TimeSpan.FromMinutes(10)));
        Add("Delay 1 hour", "clock", $"Delay {Title} by 1 hour", () => SnoozeRequested?.Invoke(TimeSpan.FromHours(1)));
        Add("Tomorrow", "clock", $"Delay {Title} until tomorrow", () => SnoozeRequested?.Invoke(TimeSpan.FromDays(1)));
        Add("Reschedule", "calendar", $"Reschedule {Title}", () => RescheduleRequested?.Invoke());
        Add("Skip", "ban", $"Skip {Title}", () => SkipRequested?.Invoke());

        return wrap;
    }

    private string StateKey() => DueState switch
    {
        ArcReminderDueState.Overdue or ArcReminderDueState.Missed => ArcTokens.Danger,
        ArcReminderDueState.Due => ArcTokens.Warning,
        ArcReminderDueState.Done => ArcTokens.Success,
        _ => ArcTokens.TextSecondary
    };

    private static (string Word, ArcBadgeTone Tone) StateVisual(ArcReminderDueState state) => state switch
    {
        ArcReminderDueState.Upcoming => ("Upcoming", ArcBadgeTone.Neutral),
        ArcReminderDueState.Due => ("Due now", ArcBadgeTone.Warning),
        ArcReminderDueState.Overdue => ("Overdue", ArcBadgeTone.Danger),
        ArcReminderDueState.Snoozed => ("Snoozed", ArcBadgeTone.Neutral),
        ArcReminderDueState.Done => ("Done", ArcBadgeTone.Success),
        _ => ("Missed", ArcBadgeTone.Danger)
    };
}


