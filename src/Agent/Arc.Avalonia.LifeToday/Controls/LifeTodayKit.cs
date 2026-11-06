using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.LifeToday.Controls;
using Arc.Avalonia.Theme;

// Short alias for the shared composition helpers; the "today" surfaces are built almost entirely from them.
using Ui = Arc.Avalonia.Controls.ArcUi;

namespace Arc.Avalonia.LifeToday.Controls;

#region Shared models and state enums

/// <summary>Overall state of the daily brief aggregation.</summary>
public enum ArcDailyBriefState
{
    /// <summary>The brief is being assembled; a spinner stands in for the sections.</summary>
    Loading,

    /// <summary>Every section has data.</summary>
    Ready,

    /// <summary>Some sections are ready while another (for example weather) is still loading.</summary>
    PartiallyLoading,

    /// <summary>The last refresh failed; the previous copy stays visible.</summary>
    Error
}

/// <summary>State of the one actionable suggestion in the daily brief.</summary>
public enum ArcSuggestionState
{
    /// <summary>Waiting for a decision.</summary>
    Pending,

    /// <summary>The user accepted it; the host routes the action.</summary>
    Accepted,

    /// <summary>The user ignored it for today.</summary>
    Dismissed,

    /// <summary>The user deferred it; the host owns the reminder time.</summary>
    Snoozed
}

/// <summary>Overall state of the day agenda.</summary>
public enum ArcDayAgendaState
{
    /// <summary>The day is being loaded.</summary>
    Loading,

    /// <summary>No items for this day.</summary>
    Empty,

    /// <summary>Items are laid out on the hour grid.</summary>
    Populated,

    /// <summary>At least one pair of items overlaps; the agenda flags it and never moves data.</summary>
    Conflict,

    /// <summary>The day could not be loaded.</summary>
    Error
}

/// <summary>The kind of object placed on the agenda timeline.</summary>
public enum ArcAgendaKind
{
    Event,
    Reminder,
    Task,
    Medication
}

/// <summary>Status of one agenda item within the current day.</summary>
public enum ArcAgendaItemStatus
{
    Upcoming,
    Current,
    Done,
    Missed
}

/// <summary>Action buckets of the reminder center.</summary>
public enum ArcReminderGroup
{
    Overdue,
    Today,
    Upcoming,
    Snoozed,
    Done
}

/// <summary>Overall state of the reminder center.</summary>
public enum ArcReminderCenterState
{
    /// <summary>Grouped list of reminders.</summary>
    Normal,

    /// <summary>Nothing outstanding; the empty state gives a next step.</summary>
    Empty,

    /// <summary>Reminders are being loaded.</summary>
    Loading,

    /// <summary>The list could not be loaded.</summary>
    Error
}

/// <summary>Check-off status of one dose in the medication schedule.</summary>
public enum ArcMedicationStatus
{
    Pending,
    Taken,
    Missed,
    Snoozed
}

/// <summary>Overall state of the medication schedule.</summary>
public enum ArcMedicationScheduleState
{
    /// <summary>A plan is active; doses are expected today.</summary>
    Normal,

    /// <summary>No medication plan exists.</summary>
    Empty,

    /// <summary>The plan is paused; no doses are expected.</summary>
    Paused
}

/// <summary>One entry on the <see cref="ArcDayAgenda"/> timeline. Times are display strings owned by the host.</summary>
public sealed class ArcAgendaItem
{
    /// <summary>Host identifier used by item events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Local start time, for example "09:30".</summary>
    public string Start { get; set; } = "09:00";

    /// <summary>Local end time, for example "10:30".</summary>
    public string End { get; set; } = "10:00";

    /// <summary>Title shown on the item block.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Object type, which selects the glyph and the redundant text label.</summary>
    public ArcAgendaKind Kind { get; set; }

    /// <summary>Status of the item today.</summary>
    public ArcAgendaItemStatus Status { get; set; }

    /// <summary>True when this item overlaps another; the agenda flags it but never reorders data.</summary>
    public bool Conflict { get; set; }

    /// <summary>True for items that belong in the all-day band instead of the hour grid.</summary>
    public bool AllDay { get; set; }
}

/// <summary>One reminder in the <see cref="ArcReminderCenter"/>. The host owns due dates, recurrence and storage.</summary>
public sealed class ArcReminderItem
{
    /// <summary>Host identifier used by row events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Title of the reminder.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Relative due copy, for example "Today · 18:00".</summary>
    public string DueText { get; set; } = string.Empty;

    /// <summary>Grouping bucket.</summary>
    public ArcReminderGroup Group { get; set; }

    /// <summary>Row status, reused by the underlying reminder row.</summary>
    public ArcReminderDueState Status { get; set; }

    /// <summary>Optional repeat label; the recurrence rule itself lives in the host.</summary>
    public string? RepeatLabel { get; set; }

    /// <summary>Optional source label, for example "Bills".</summary>
    public string? SourceLabel { get; set; }
}

/// <summary>One dose in the <see cref="ArcMedicationSchedule"/>. Health data is owned and persisted by the host.</summary>
public sealed class ArcMedicationItem
{
    /// <summary>Host identifier used by dose events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Medication name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Dose, for example "500 mg".</summary>
    public string Dose { get; set; } = string.Empty;

    /// <summary>Local time, for example "08:00".</summary>
    public string TimeText { get; set; } = "08:00";

    /// <summary>Meal relation, for example "after breakfast".</summary>
    public string MealNote { get; set; } = string.Empty;

    /// <summary>Check-off status of this dose.</summary>
    public ArcMedicationStatus Status { get; set; }

    /// <summary>Optional per-dose supply note, for example "4 doses left".</summary>
    public string? StockText { get; set; }
}

#endregion

#region Daily brief

/// <summary>
/// The "today" screen: greeting and weather, the next appointment, due reminders, health and money
/// snapshots, and exactly one suggestion from the agent.
///
/// Pure UI contract: every value is a display property, and accept, ignore and snooze raise intent
/// events for the host. The control does not aggregate data, does not call the agent and never starts a
/// continuous animation; screenshots are deterministic because nothing ticks.
/// </summary>
public class ArcDailyBrief : Panel
{
    public static readonly StyledProperty<string> GreetingProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(Greeting), "Good morning, Lin");

    public static readonly StyledProperty<string> DateTextProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(DateText), "Wednesday, 12 November");

    public static readonly StyledProperty<string> WeatherTextProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(WeatherText), "18° · light rain");

    public static readonly StyledProperty<string> NextEventProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(NextEvent), "10:30 Design review · Studio 2");

    public static readonly StyledProperty<IEnumerable<string>?> DueRemindersProperty =
        AvaloniaProperty.Register<ArcDailyBrief, IEnumerable<string>?>(nameof(DueReminders));

    public static readonly StyledProperty<string> HealthTextProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(HealthText), "6,240 steps · 7 h 20 m sleep");

    public static readonly StyledProperty<string> BudgetTextProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(BudgetText), "Groceries ¥420 of ¥800 this week");

    public static readonly StyledProperty<string> SuggestionProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(Suggestion),
            "Your 10:30 review moved to Studio 2. Leave 10 minutes early for the rain.");

    public static readonly StyledProperty<string> SuggestionSourceProperty =
        AvaloniaProperty.Register<ArcDailyBrief, string>(nameof(SuggestionSource),
            "Agent · calendar and weather");

    public static readonly StyledProperty<ArcDailyBriefState> StateProperty =
        AvaloniaProperty.Register<ArcDailyBrief, ArcDailyBriefState>(nameof(State), ArcDailyBriefState.Ready);

    public static readonly StyledProperty<ArcSuggestionState> SuggestionStateProperty =
        AvaloniaProperty.Register<ArcDailyBrief, ArcSuggestionState>(nameof(SuggestionState),
            ArcSuggestionState.Pending);

    private bool _built;

    static ArcDailyBrief()
    {
        GreetingProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        DateTextProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        WeatherTextProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        NextEventProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        DueRemindersProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        HealthTextProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        BudgetTextProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        SuggestionProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        SuggestionSourceProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
        SuggestionStateProperty.Changed.AddClassHandler<ArcDailyBrief>((brief, _) => brief.Rebuild());
    }

    public ArcDailyBrief()
    {
        ClipToBounds = false;
        SetCurrentValue(DueRemindersProperty, new[]
        {
            "Credit card payment · 2 days overdue",
            "Take the recycling out · 18:00",
            "Metformin 500 mg · 18:30"
        });
        _built = true;
        Rebuild();
    }

    /// <summary>Greeting line, for example "Good morning, Lin".</summary>
    public string Greeting
    {
        get => GetValue(GreetingProperty);
        set => SetValue(GreetingProperty, value);
    }

    /// <summary>Today's date, already formatted by the host.</summary>
    public string DateText
    {
        get => GetValue(DateTextProperty);
        set => SetValue(DateTextProperty, value);
    }

    /// <summary>Short weather summary. The host supplies it; the control never fetches weather.</summary>
    public string WeatherText
    {
        get => GetValue(WeatherTextProperty);
        set => SetValue(WeatherTextProperty, value);
    }

    /// <summary>The next appointment, for example "10:30 Design review · Studio 2".</summary>
    public string NextEvent
    {
        get => GetValue(NextEventProperty);
        set => SetValue(NextEventProperty, value);
    }

    /// <summary>Up to three due reminders, newest urgency first. Extra entries are ignored.</summary>
    public IEnumerable<string>? DueReminders
    {
        get => GetValue(DueRemindersProperty);
        set => SetValue(DueRemindersProperty, value);
    }

    /// <summary>Health snapshot line, for example steps and sleep.</summary>
    public string HealthText
    {
        get => GetValue(HealthTextProperty);
        set => SetValue(HealthTextProperty, value);
    }

    /// <summary>Money snapshot line, for example the weekly grocery envelope.</summary>
    public string BudgetText
    {
        get => GetValue(BudgetTextProperty);
        set => SetValue(BudgetTextProperty, value);
    }

    /// <summary>The single suggestion the agent surfaces today.</summary>
    public string Suggestion
    {
        get => GetValue(SuggestionProperty);
        set => SetValue(SuggestionProperty, value);
    }

    /// <summary>Where the suggestion came from, so the agent's reasoning is attributable.</summary>
    public string SuggestionSource
    {
        get => GetValue(SuggestionSourceProperty);
        set => SetValue(SuggestionSourceProperty, value);
    }

    /// <summary>Aggregation state; drives loading, partial and error presentation.</summary>
    public ArcDailyBriefState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Decision state of the suggestion; the host sets it after handling an intent.</summary>
    public ArcSuggestionState SuggestionState
    {
        get => GetValue(SuggestionStateProperty);
        set => SetValue(SuggestionStateProperty, value);
    }

    /// <summary>Raised when the user accepts the suggestion; the host performs the action.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> AcceptSuggestionRequestedEvent =
        RoutedEvent.Register<ArcDailyBrief, ArcLifeTodayRoutedEventArgs>("AcceptSuggestionRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="AcceptSuggestionRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? AcceptSuggestionRequestedRouted
    {
        add => AddHandler(AcceptSuggestionRequestedEvent, value);
        remove => RemoveHandler(AcceptSuggestionRequestedEvent, value);
    }

    public event Action? AcceptSuggestionRequested;

    /// <summary>Raised when the user ignores the suggestion for today.</summary>
    public event Action? IgnoreSuggestionRequested;

    /// <summary>Raised when the user asks to be reminded of the suggestion later.</summary>
    public event Action? SnoozeSuggestionRequested;

    /// <summary>Raised when the user opens the day agenda from the brief.</summary>
    public event Action? OpenAgendaRequested;

    /// <summary>Raised when the user opens the reminder center from the brief.</summary>
    public event Action? OpenRemindersRequested;

    /// <summary>Raised when the user asks to retry a failed brief refresh.</summary>
    public event Action? RetryRequested;

    private void Rebuild()
    {
        if (!_built)
        {
            return;
        }

        Ui.Name(this, "Daily brief",
            "Today's summary: greeting, weather, next appointment, due reminders, health, money and one suggestion.");

        Children.Clear();
        Children.Add(State switch
        {
            ArcDailyBriefState.Loading => BuildLoading(),
            ArcDailyBriefState.Error => BuildError(),
            _ => BuildBrief()
        });
    }

    private Control BuildBrief()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        header.Children.Add(LifeUi.IconDisc("sun", ArcTokens.Accent, ArcTokens.AccentSubtle, 40, 20));

        var heading = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        heading.Children.Add(Ui.Text(Greeting, "arc-subtitle"));
        heading.Children.Add(Ui.Caption(DateText));
        Grid.SetColumn(heading, 1);
        header.Children.Add(heading);

        var weather = State == ArcDailyBriefState.PartiallyLoading
            ? Ui.Pill("Weather loading", "cloud")
            : Ui.Pill(WeatherText, "cloud");
        var right = Ui.HStack(8, weather, Ui.Pill("Agent brief", "sparkles", true));
        Grid.SetColumn(right, 2);
        header.Children.Add(right);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), ColumnSpacing = 16 };

        var left = new StackPanel { Spacing = 12 };
        left.Children.Add(BuildNextEventCard());
        left.Children.Add(BuildSuggestionCard());
        Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var rightColumn = new StackPanel { Spacing = 12 };
        rightColumn.Children.Add(BuildRemindersCard());
        rightColumn.Children.Add(BuildMetrics());
        Grid.SetColumn(rightColumn, 1);
        body.Children.Add(rightColumn);

        return LifeUi.Frame(new StackPanel { Spacing = 16, Children = { header, Ui.Divider(), body } });
    }

    private Control BuildNextEventCard()
    {
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(Ui.HStack(6, Ui.Icon("calendar", 14, ArcTokens.TextSecondary), Ui.Caption("Next up")));
        text.Children.Add(Ui.Text(NextEvent, "arc-body arc-medium"));

        var open = Ui.Button("Open agenda", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        Ui.Name(open, "Open the day agenda");
        open.Click += (_, _) => OpenAgendaRequested?.Invoke();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(text);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);
        return LifeUi.Card(grid);
    }

    private Control BuildSuggestionCard()
    {
        var content = new StackPanel { Spacing = 10 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        header.Children.Add(Ui.Icon("sparkles", 16, ArcTokens.Accent));
        var label = Ui.Text("Suggestion", "arc-label");
        label.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(label, 1);
        header.Children.Add(label);

        Control? stateBadge = SuggestionState switch
        {
            ArcSuggestionState.Accepted => Ui.Badge("Accepted", ArcBadgeTone.Success),
            ArcSuggestionState.Dismissed => Ui.Badge("Dismissed", ArcBadgeTone.Neutral),
            ArcSuggestionState.Snoozed => Ui.Badge("Snoozed", ArcBadgeTone.Neutral),
            _ => null
        };
        if (stateBadge is not null)
        {
            Grid.SetColumn(stateBadge, 2);
            header.Children.Add(stateBadge);
        }

        content.Children.Add(header);
        content.Children.Add(Ui.Text(Suggestion, "arc-body-sm"));
        content.Children.Add(Ui.Caption(SuggestionSource));

        if (SuggestionState == ArcSuggestionState.Pending)
        {
            var accept = Ui.Button("Accept", ArcButtonVariant.Primary, ArcButtonSize.Sm);
            Ui.Name(accept, "Accept the suggestion");
            accept.Click += (_, _) =>
        {
            AcceptSuggestionRequested?.Invoke();
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(AcceptSuggestionRequestedEvent));
        };

            var ignore = Ui.Button("Ignore", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
            Ui.Name(ignore, "Ignore the suggestion for today");
            ignore.Click += (_, _) => IgnoreSuggestionRequested?.Invoke();

            var snooze = Ui.Button("Snooze", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
            Ui.Name(snooze, "Ask about the suggestion later");
            snooze.Click += (_, _) => SnoozeSuggestionRequested?.Invoke();

            content.Children.Add(Ui.Wrap(8, accept, ignore, snooze));
        }
        else
        {
            content.Children.Add(Ui.Caption(SuggestionState switch
            {
                ArcSuggestionState.Accepted => "Added to today's plan. The host would record the decision.",
                ArcSuggestionState.Dismissed => "The agent will not repeat this suggestion today.",
                _ => "Back at 18:00. The host schedules the reminder."
            }));
        }

        return new Border
        {
            Background = Ui.Brush(ArcTokens.AccentSubtle),
            BorderBrush = Ui.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = content
        };
    }

    private Control BuildRemindersCard()
    {
        var items = DueReminders?.Take(3).ToList() ?? new List<string>();
        var content = new StackPanel { Spacing = 8 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Ui.Caption("Due today"));
        var count = Ui.Badge(items.Count == 0 ? "None" : items.Count.ToString(CultureInfo.InvariantCulture),
            items.Count == 0 ? ArcBadgeTone.Neutral : ArcBadgeTone.Warning);
        Grid.SetColumn(count, 1);
        header.Children.Add(count);
        content.Children.Add(header);

        if (items.Count == 0)
        {
            content.Children.Add(Ui.Caption("Nothing due in the next 24 hours."));
        }
        else
        {
            foreach (var reminder in items)
            {
                content.Children.Add(Ui.HStack(8, Ui.Dot(ArcTokens.Warning, 7),
                    Ui.Text(reminder, "arc-body-sm", wrap: TextWrapping.Wrap)));
            }
        }

        var open = Ui.Button("Open reminders", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        Ui.Name(open, "Open the reminder center");
        open.Click += (_, _) => OpenRemindersRequested?.Invoke();
        content.Children.Add(open);

        return LifeUi.Card(content);
    }

    private Control BuildMetrics()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        grid.Children.Add(new ArcCard
        {
            Muted = true,
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(18),
            Content = Metric("heart", ArcTokens.Success, "Health", HealthText)
        });

        var budget = new ArcCard
        {
            Muted = true,
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(18),
            Content = Metric("credit-card", ArcTokens.Accent, "Money", BudgetText)
        };
        Grid.SetColumn(budget, 1);
        grid.Children.Add(budget);
        return grid;
    }

    private static Control Metric(string icon, string toneKey, string label, string value)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Ui.HStack(6, Ui.Icon(icon, 14, toneKey), Ui.Caption(label)));
        stack.Children.Add(Ui.Text(value, "arc-body-sm arc-medium"));
        return stack;
    }

    private Control BuildLoading()
    {
        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(Ui.Text(Greeting, "arc-subtitle"));
        content.Children.Add(Ui.Caption(DateText));
        content.Children.Add(LifeUi.Loading("Putting your brief together…"));
        return LifeUi.Frame(content);
    }

    private Control BuildError()
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Ui.HStack(8, Ui.Icon("circle-alert", 16, ArcTokens.Danger),
            Ui.Text("The brief could not refresh", "arc-body arc-medium", wrap: TextWrapping.NoWrap)));
        content.Children.Add(Ui.Body(
            "Showing the last copy from 07:00. Retry when the connection is back; the sections below stay read-only.",
            640));

        var retry = Ui.Button("Retry", ArcButtonVariant.Secondary, ArcButtonSize.Sm);
        Ui.Name(retry, "Retry the daily brief");
        retry.Click += (_, _) => RetryRequested?.Invoke();
        content.Children.Add(retry);

        return LifeUi.Frame(content);
    }
}

#endregion

#region Day agenda

/// <summary>
/// A single day on one timeline: all-day items in a band, then an hour grid that holds events,
/// reminders, tasks and medication. A thin accent line marks the current time and overlapping items
/// carry an explicit overlap badge.
///
/// Pure UI contract: the host supplies local display times and decides what "now" is; the control never
/// refreshes a clock, never reorders conflicting data and raises navigation and activation intents.
/// </summary>
public class ArcDayAgenda : Panel
{
    public static readonly StyledProperty<string> DateTextProperty =
        AvaloniaProperty.Register<ArcDayAgenda, string>(nameof(DateText), "Wednesday, 12 November");

    public static readonly StyledProperty<string> NowTextProperty =
        AvaloniaProperty.Register<ArcDayAgenda, string>(nameof(NowText), "14:05");

    public static readonly StyledProperty<string> TimeZoneTextProperty =
        AvaloniaProperty.Register<ArcDayAgenda, string>(nameof(TimeZoneText), "Local time · UTC+08:00");

    public static readonly StyledProperty<IEnumerable<ArcAgendaItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcDayAgenda, IEnumerable<ArcAgendaItem>?>(nameof(Items));

    public static readonly StyledProperty<ArcDayAgendaState> StateProperty =
        AvaloniaProperty.Register<ArcDayAgenda, ArcDayAgendaState>(nameof(State), ArcDayAgendaState.Populated);

    private bool _built;

    static ArcDayAgenda()
    {
        DateTextProperty.Changed.AddClassHandler<ArcDayAgenda>((agenda, _) => agenda.Rebuild());
        NowTextProperty.Changed.AddClassHandler<ArcDayAgenda>((agenda, _) => agenda.Rebuild());
        TimeZoneTextProperty.Changed.AddClassHandler<ArcDayAgenda>((agenda, _) => agenda.Rebuild());
        ItemsProperty.Changed.AddClassHandler<ArcDayAgenda>((agenda, _) => agenda.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcDayAgenda>((agenda, _) => agenda.Rebuild());
    }

    public ArcDayAgenda()
    {
        ClipToBounds = false;
        SetCurrentValue(ItemsProperty, DefaultItems());
        _built = true;
        Rebuild();
    }

    /// <summary>Date heading, already formatted by the host.</summary>
    public string DateText
    {
        get => GetValue(DateTextProperty);
        set => SetValue(DateTextProperty, value);
    }

    /// <summary>Current local time used by the now line, for example "14:05".</summary>
    public string NowText
    {
        get => GetValue(NowTextProperty);
        set => SetValue(NowTextProperty, value);
    }

    /// <summary>Time zone note shown under the date; display only.</summary>
    public string TimeZoneText
    {
        get => GetValue(TimeZoneTextProperty);
        set => SetValue(TimeZoneTextProperty, value);
    }

    /// <summary>Items for the day. All-day entries render in the band, the rest on the hour grid.</summary>
    public IEnumerable<ArcAgendaItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Overall agenda state.</summary>
    public ArcDayAgendaState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Raised when the user asks for the previous day.</summary>
    public event Action? PreviousDayRequested;

    /// <summary>Raised when the user asks for the next day.</summary>
    public event Action? NextDayRequested;

    /// <summary>Raised when the user jumps back to today.</summary>
    public event Action? TodayRequested;

    /// <summary>Raised when an item is activated; the argument is the item id.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> ItemActivatedEvent =
        RoutedEvent.Register<ArcDayAgenda, ArcLifeTodayRoutedEventArgs>("ItemActivated", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="ItemActivated"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? ItemActivatedRouted
    {
        add => AddHandler(ItemActivatedEvent, value);
        remove => RemoveHandler(ItemActivatedEvent, value);
    }

    public event Action<string>? ItemActivated;

    /// <summary>Raised when the user asks to add the first item of an empty day.</summary>
    public event Action? AddRequested;

    /// <summary>Raised when the user retries a failed load.</summary>
    public event Action? RetryRequested;

    private void Rebuild()
    {
        if (!_built)
        {
            return;
        }

        Ui.Name(this, "Day agenda", "Events, reminders, tasks and medication on one timeline.");

        Children.Clear();
        Children.Add(State switch
        {
            ArcDayAgendaState.Loading => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children = { BuildHeader(), LifeUi.Loading("Loading the day…") }
            }),
            ArcDayAgendaState.Empty => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    LifeUi.Empty("calendar", "Nothing scheduled",
                        "No events, reminders, tasks or doses for this day yet.", "Add the first item",
                        () => AddRequested?.Invoke())
                }
            }),
            ArcDayAgendaState.Error => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    LifeUi.Failure("The day could not load",
                        "The host keeps the previous copy; retry when the calendar sync is back.",
                        () => RetryRequested?.Invoke())
                }
            }),
            _ => BuildAgenda()
        });
    }

    private Control BuildAgenda()
    {
        var items = Items?.ToList() ?? new List<ArcAgendaItem>();
        var allDay = items.Where(item => item.AllDay).ToList();
        var timed = items.Where(item => !item.AllDay).ToList();

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(BuildHeader());
        if (allDay.Count > 0)
        {
            stack.Children.Add(BuildAllDay(allDay));
        }

        if (State == ArcDayAgendaState.Conflict || items.Any(item => item.Conflict))
        {
            stack.Children.Add(BuildConflictBand(items));
        }

        stack.Children.Add(BuildTimeline(timed));
        stack.Children.Add(Ui.Caption(
            $"{items.Count} items · Local times · Conflicts are flagged, never moved.", 720));

        return LifeUi.Frame(stack, 16);
    }

    private Control BuildHeader()
    {
        var previous = LifeUi.IconButton("chevron-left", "Previous day", () => PreviousDayRequested?.Invoke());
        var next = LifeUi.IconButton("chevron-right", "Next day", () => NextDayRequested?.Invoke());

        var today = Ui.Button("Today", ArcButtonVariant.Secondary, ArcButtonSize.Sm);
        Ui.Name(today, "Jump to today");
        today.Click += (_, _) => TodayRequested?.Invoke();

        var date = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        date.Children.Add(Ui.Text(DateText, "arc-heading"));
        date.Children.Add(Ui.Caption(TimeZoneText));

        var left = Ui.HStack(4, previous, date, next);
        var right = Ui.HStack(8, today, Ui.Pill("Day view", "calendar", true));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private static Control BuildAllDay(IReadOnlyList<ArcAgendaItem> allDay)
    {
        var label = Ui.Caption("All day");
        label.Width = 52;

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var item in allDay)
        {
            var pill = Ui.Pill(item.Title, KindVisual(item.Kind).Icon);
            pill.Margin = new Thickness(0, 0, 6, 4);
            wrap.Children.Add(pill);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("52,*") };
        grid.Children.Add(label);
        Grid.SetColumn(wrap, 1);
        grid.Children.Add(wrap);

        return new Border
        {
            Background = Ui.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Child = grid
        };
    }

    private static Control BuildConflictBand(IReadOnlyList<ArcAgendaItem> items)
    {
        var count = items.Count(item => item.Conflict);
        var copy = count > 1
            ? $"{count} items overlap in this day. Nothing was moved."
            : "One item overlaps another. Nothing was moved.";
        var content = Ui.HStack(8, Ui.Icon("triangle-alert", 15, ArcTokens.Warning),
            Ui.Text(copy, "arc-body-sm"));
        return new Border
        {
            Background = Ui.Brush(ArcTokens.WarningSubtle),
            BorderBrush = Ui.Brush(ArcTokens.WarningSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Child = content
        };
    }

    private Control BuildTimeline(IReadOnlyList<ArcAgendaItem> timed)
    {
        var byHour = new Dictionary<int, List<ArcAgendaItem>>();
        foreach (var item in timed)
        {
            var start = TimeOf(item.Start);
            var hour = Math.Clamp(start.Hours, 8, 18);
            if (!byHour.TryGetValue(hour, out var bucket))
            {
                bucket = new List<ArcAgendaItem>();
                byHour[hour] = bucket;
            }

            bucket.Add(item);
        }

        var timeline = new StackPanel();
        var now = TimeOf(NowText);
        for (var hour = 8; hour <= 18; hour++)
        {
            timeline.Children.Add(BuildHourRow(hour, byHour.TryGetValue(hour, out var bucket) ? bucket : null));
            if (now.Hours == hour)
            {
                timeline.Children.Add(BuildNowRow());
            }
        }

        if (now.Hours is < 8 or > 18)
        {
            timeline.Children.Insert(0, BuildNowRow());
        }

        return timeline;
    }

    private Control BuildHourRow(int hour, IReadOnlyList<ArcAgendaItem>? items)
    {
        var label = Ui.Text($"{hour:00}:00", "arc-caption arc-mono", wrap: TextWrapping.NoWrap);
        label.VerticalAlignment = VerticalAlignment.Top;
        label.Margin = new Thickness(0, 1, 0, 0);

        var content = new WrapPanel { Orientation = Orientation.Horizontal };
        if (items is not null)
        {
            foreach (var item in items)
            {
                content.Children.Add(BuildAgendaTile(item));
            }
        }

        var cell = new Border
        {
            Padding = new Thickness(4, 1, 0, 2),
            MinHeight = items is { Count: > 0 } ? 44 : 18,
            Child = content
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("52,*") };
        grid.Children.Add(label);
        Grid.SetColumn(cell, 1);
        grid.Children.Add(cell);

        return new Border
        {
            BorderBrush = Ui.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 2, 0, 2),
            Child = grid
        };
    }

    private Control BuildNowRow()
    {
        var tag = Ui.Text("Now", "arc-caption arc-medium", wrap: TextWrapping.NoWrap);
        tag.Foreground = Ui.Brush(ArcTokens.Accent);
        tag.VerticalAlignment = VerticalAlignment.Center;

        var rule = new Border
        {
            Height = 1,
            Background = Ui.Brush(ArcTokens.Accent),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0)
        };

        var time = Ui.Text(NowText, "arc-caption arc-mono", wrap: TextWrapping.NoWrap);
        time.Foreground = Ui.Brush(ArcTokens.Accent);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("52,*,Auto") };
        grid.Children.Add(tag);
        Grid.SetColumn(rule, 1);
        grid.Children.Add(rule);
        Grid.SetColumn(time, 2);
        grid.Children.Add(time);

        return new Border { Padding = new Thickness(0, 2, 0, 2), Child = grid };
    }

    private Control BuildAgendaTile(ArcAgendaItem item)
    {
        var visual = KindVisual(item.Kind);

        var meta = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        meta.Children.Add(Ui.Dot(visual.ToneKey, 7));
        meta.Children.Add(Ui.Text($"{item.Start} – {item.End}", "arc-caption", wrap: TextWrapping.NoWrap));
        var kind = Ui.Text(visual.Label, "arc-caption", wrap: TextWrapping.NoWrap);
        kind.Foreground = Ui.Brush(visual.ToneKey);
        meta.Children.Add(kind);
        if (item.Conflict)
        {
            meta.Children.Add(Ui.Badge("Overlap", ArcBadgeTone.Danger));
        }

        var status = StatusBadge(item.Status);
        if (status is not null)
        {
            meta.Children.Add(status);
        }

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(meta);
        content.Children.Add(Ui.Text(item.Title, "arc-body-sm arc-medium"));

        var tile = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            MinHeight = 0,
            Padding = new Thickness(10, 5, 10, 5),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MaxWidth = 340,
            Margin = new Thickness(0, 0, 8, 4),
            Background = Ui.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = Ui.Brush(item.Conflict ? ArcTokens.Warning : ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Content = content
        };
        Ui.Name(tile, $"{item.Title}, {visual.Label}, {item.Start} to {item.End}"
            + (item.Conflict ? ", overlaps another item" : string.Empty));
        tile.Click += (_, _) =>
        {
            ItemActivated?.Invoke(item.Id);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(ItemActivatedEvent) { Text = item.Id });
        };
        return tile;
    }

    private static Control? StatusBadge(ArcAgendaItemStatus status) => status switch
    {
        ArcAgendaItemStatus.Current => Ui.Badge("Now", ArcBadgeTone.Accent),
        ArcAgendaItemStatus.Done => Ui.Badge("Done", ArcBadgeTone.Success),
        ArcAgendaItemStatus.Missed => Ui.Badge("Missed", ArcBadgeTone.Danger),
        _ => null
    };

    private static (string ToneKey, string Label, string Icon) KindVisual(ArcAgendaKind kind) => kind switch
    {
        ArcAgendaKind.Event => (ArcTokens.Accent, "Event", "calendar"),
        ArcAgendaKind.Reminder => (ArcTokens.Warning, "Reminder", "bell"),
        ArcAgendaKind.Task => (ArcTokens.TextSecondary, "Task", "check-check"),
        _ => (ArcTokens.Success, "Medication", "heart")
    };

    private static TimeSpan TimeOf(string text) =>
        TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value) ? value : TimeSpan.FromHours(8);

    private static IEnumerable<ArcAgendaItem> DefaultItems() => new[]
    {
        new ArcAgendaItem { Id = "trip", Title = "Partner offsite", Kind = ArcAgendaKind.Event, AllDay = true },
        new ArcAgendaItem { Id = "birthday", Title = "Mum's birthday", Kind = ArcAgendaKind.Reminder, AllDay = true },
        new ArcAgendaItem { Id = "med-am", Start = "08:00", End = "08:15", Title = "Metformin 500 mg", Kind = ArcAgendaKind.Medication, Status = ArcAgendaItemStatus.Done },
        new ArcAgendaItem { Id = "review", Start = "09:30", End = "10:30", Title = "Design review", Kind = ArcAgendaKind.Event, Status = ArcAgendaItemStatus.Done, Conflict = true },
        new ArcAgendaItem { Id = "call", Start = "09:45", End = "10:15", Title = "Call with Sam", Kind = ArcAgendaKind.Event, Status = ArcAgendaItemStatus.Done, Conflict = true },
        new ArcAgendaItem { Id = "bill", Start = "12:30", End = "12:45", Title = "Pay the credit card", Kind = ArcAgendaKind.Reminder, Status = ArcAgendaItemStatus.Missed },
        new ArcAgendaItem { Id = "lunch", Start = "13:00", End = "13:30", Title = "Lunch with Ana", Kind = ArcAgendaKind.Event, Status = ArcAgendaItemStatus.Done },
        new ArcAgendaItem { Id = "parcel", Start = "15:00", End = "15:20", Title = "Pick up the parcels", Kind = ArcAgendaKind.Task, Status = ArcAgendaItemStatus.Upcoming },
        new ArcAgendaItem { Id = "recycling", Start = "18:00", End = "18:15", Title = "Recycling out", Kind = ArcAgendaKind.Reminder, Status = ArcAgendaItemStatus.Upcoming },
        new ArcAgendaItem { Id = "med-pm", Start = "18:30", End = "18:45", Title = "Metformin 500 mg", Kind = ArcAgendaKind.Medication, Status = ArcAgendaItemStatus.Upcoming }
    };
}

#endregion

#region Composition helpers

/// <summary>
/// Small token-bound building blocks for the four "today" surfaces. Every colour comes from
/// <see cref="ArcTokens"/>, every text from the arc-* type classes; no hex or font size is written here.
/// </summary>
internal static class LifeUi
{
    public static IBrush Brush(string key) => ArcUi.Brush(key);

    /// <summary>The outer surface of a block: hairline border, panel radius, 1 px border.</summary>
    public static Border Frame(Control content, double padding = 20) => new()
    {
        Background = Brush(ArcTokens.Surface),
        BorderBrush = Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(26),
        Padding = new Thickness(padding),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Child = content
    };

    /// <summary>A nested card: raised surface, 18 px radius so it stays concentric inside the frame.</summary>
    public static Border Card(Control content, double padding = 14, bool muted = false) => new()
    {
        Background = Brush(muted ? ArcTokens.SurfaceMuted : ArcTokens.SurfaceRaised),
        BorderBrush = Brush(muted ? ArcTokens.BorderSubtle : ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(18),
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>A round icon disc used as a product glyph.</summary>
    public static Border IconDisc(string icon, string foregroundKey, string backgroundKey, double size = 32,
        double iconSize = 15) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = Brush(backgroundKey),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new ArcIcon
        {
            Icon = icon,
            Size = iconSize,
            Foreground = Brush(foregroundKey),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    /// <summary>A small square header button with an accessible name.</summary>
    public static ArcButton IconButton(string icon, string label, Action onClick)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 32,
            Height = 32,
            MinHeight = 32,
            Content = new ArcIcon { Icon = icon, Size = 16, Foreground = Brush(ArcTokens.TextSecondary) }
        };
        Ui.Name(button, label);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A filter chip; the active chip takes the control fill, not a second hue.</summary>
    public static ArcButton Chip(string label, bool active, Action onClick)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = label,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(999),
            Background = Brush(active ? ArcTokens.ControlFill : ArcTokens.SurfaceMuted),
            BorderBrush = Brush(active ? ArcTokens.BorderStrong : ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            Foreground = Brush(active ? ArcTokens.ControlGlyph : ArcTokens.TextSecondary)
        };
        Ui.Name(button, label);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>An action button with an explicit accessible name.</summary>
    public static ArcButton Action(string label, ArcButtonVariant variant, string name, Action onClick)
    {
        var button = Ui.Button(label, variant, ArcButtonSize.Sm);
        Ui.Name(button, name);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A centered empty state with a next step.</summary>
    public static Control Empty(string icon, string title, string body, string? actionLabel = null,
        Action? action = null)
    {
        var content = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(IconDisc(icon, ArcTokens.TextSecondary, ArcTokens.SurfaceMuted, 40, 18));

        var heading = Ui.Text(title, "arc-body arc-medium");
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        content.Children.Add(heading);

        var copy = Ui.Caption(body, 420);
        copy.HorizontalAlignment = HorizontalAlignment.Center;
        copy.TextAlignment = TextAlignment.Center;
        content.Children.Add(copy);

        if (!string.IsNullOrWhiteSpace(actionLabel) && action is not null)
        {
            var button = Ui.Button(actionLabel!, ArcButtonVariant.Secondary, ArcButtonSize.Sm);
            Ui.Name(button, actionLabel!);
            button.Click += (_, _) => action();
            button.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(button);
        }

        return content;
    }

    /// <summary>A loading row with the shared Arc spinner.</summary>
    public static Control Loading(string label)
    {
        var row = Ui.HStack(10,
            new ArcSpinner
            {
                Size = 18,
                StrokeWidth = 2,
                Stroke = Brush(ArcTokens.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            },
            Ui.Text(label, "arc-body-sm arc-secondary", wrap: TextWrapping.NoWrap));
        row.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Padding = new Thickness(12), Child = row };
    }

    /// <summary>A failure block with a retry action.</summary>
    public static Control Failure(string title, string body, Action retry)
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Ui.HStack(8, new ArcIcon { Icon = "circle-alert", Size = 16, Foreground = Brush(ArcTokens.Danger) },
            Ui.Text(title, "arc-body arc-medium", wrap: TextWrapping.NoWrap)));
        content.Children.Add(Ui.Body(body, 620));
        var button = Ui.Button("Retry", ArcButtonVariant.Secondary, ArcButtonSize.Sm);
        Ui.Name(button, "Retry");
        button.Click += (_, _) => retry();
        content.Children.Add(button);
        return new Border { Padding = new Thickness(12), Child = content };
    }
}

/// <summary>
/// A row of state buttons for the pages of this file: exactly one is active, the others stay quiet.
/// Pages use it to show two or three states of one control without rebuilding the page.
/// </summary>
internal sealed class TodayStateSwitch : StackPanel
{
    private readonly ArcButton[] _buttons;
    private readonly string[] _labels;

    public TodayStateSwitch(params string[] labels)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        _labels = labels;
        _buttons = new ArcButton[labels.Length];
        for (var index = 0; index < labels.Length; index++)
        {
            var target = index;
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = labels[index],
                Height = 30,
                MinHeight = 30,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(999),
                Background = LifeUi.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = LifeUi.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                Foreground = LifeUi.Brush(ArcTokens.TextSecondary)
            };
            Ui.Name(button, $"Show {labels[index]}");
            button.Click += (_, _) => Select(target);
            _buttons[index] = button;
            Children.Add(button);
        }
    }

    /// <summary>Raised after the active state changes; the argument is the button index.</summary>
    public event Action<int>? Selected;

    /// <summary>Display label of one state button.</summary>
    public string Label(int index) => index >= 0 && index < _labels.Length ? _labels[index] : string.Empty;

    /// <summary>Selects one state and updates every button's emphasis.</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= _buttons.Length)
        {
            return;
        }

        for (var i = 0; i < _buttons.Length; i++)
        {
            var active = i == index;
            var button = _buttons[i];
            button.Variant = active ? ArcButtonVariant.Secondary : ArcButtonVariant.Ghost;
            button.Background = LifeUi.Brush(active ? ArcTokens.ControlFill : ArcTokens.SurfaceMuted);
            button.BorderBrush = LifeUi.Brush(active ? ArcTokens.BorderStrong : ArcTokens.BorderSubtle);
            button.Foreground = LifeUi.Brush(active ? ArcTokens.ControlGlyph : ArcTokens.TextSecondary);
        }

        Selected?.Invoke(index);
    }
}

/// <summary>Page-level helpers shared by the four "today" pages.</summary>
internal static class TodayPageUi
{
    /// <summary>A polite live region that reports the last intent event of a page.</summary>
    public static TextBlock StatusLine()
    {
        var line = Ui.Caption(string.Empty, 860);
        line.IsVisible = false;
        AutomationProperties.SetLiveSetting(line, AutomationLiveSetting.Polite);
        return line;
    }

    /// <summary>Replaces the status line and reveals it.</summary>
    public static void Say(TextBlock line, string text)
    {
        line.Text = text;
        line.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>A quiet implementation note under the demo.</summary>
    public static TextBlock Hint(string text) => Ui.Caption(text, 820);
}

#endregion

// --- Reminder center and medication schedule follow below. ---

#region Reminder center

/// <summary>
/// The life action queue: overdue, today, upcoming and snoozed reminders, grouped with counts and a
/// filter row. Each row is an <see cref="ArcReminderRow"/>, so delay, reschedule, skip and completion
/// are the same primitives as everywhere else.
///
/// Pure UI contract: the control renders the reminders it is given, groups them by the host's local day
/// boundaries and raises per-item and bulk intents. It never recalculates due dates, never persists a
/// snooze and never sends a notification.
/// </summary>
public class ArcReminderCenter : Panel
{
    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<ArcReminderCenter, string>(nameof(Heading), "Reminders");

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ArcReminderCenter, string>(nameof(Subtitle),
            "Grouped by urgency. Actions are intent events; the host applies them.");

    public static readonly StyledProperty<string> QuietHoursTextProperty =
        AvaloniaProperty.Register<ArcReminderCenter, string>(nameof(QuietHoursText), "Quiet hours 22:00 – 07:00");

    public static readonly StyledProperty<IEnumerable<ArcReminderItem>?> RemindersProperty =
        AvaloniaProperty.Register<ArcReminderCenter, IEnumerable<ArcReminderItem>?>(nameof(Reminders));

    public static readonly StyledProperty<ArcReminderCenterState> StateProperty =
        AvaloniaProperty.Register<ArcReminderCenter, ArcReminderCenterState>(nameof(State),
            ArcReminderCenterState.Normal);

    private static readonly ArcReminderGroup[] GroupOrder =
    {
        ArcReminderGroup.Overdue,
        ArcReminderGroup.Today,
        ArcReminderGroup.Upcoming,
        ArcReminderGroup.Snoozed,
        ArcReminderGroup.Done
    };

    private bool _built;
    private ArcReminderGroup? _filter;

    static ArcReminderCenter()
    {
        HeadingProperty.Changed.AddClassHandler<ArcReminderCenter>((center, _) => center.Rebuild());
        SubtitleProperty.Changed.AddClassHandler<ArcReminderCenter>((center, _) => center.Rebuild());
        QuietHoursTextProperty.Changed.AddClassHandler<ArcReminderCenter>((center, _) => center.Rebuild());
        RemindersProperty.Changed.AddClassHandler<ArcReminderCenter>((center, _) => center.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcReminderCenter>((center, _) => center.Rebuild());
    }

    public ArcReminderCenter()
    {
        ClipToBounds = false;
        SetCurrentValue(RemindersProperty, DefaultReminders());
        _built = true;
        Rebuild();
    }

    /// <summary>Heading of the surface.</summary>
    public string Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>One-line explanation under the heading.</summary>
    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Quiet-hours reminder rendered beside the counts; the delivery policy itself is the host's.</summary>
    public string QuietHoursText
    {
        get => GetValue(QuietHoursTextProperty);
        set => SetValue(QuietHoursTextProperty, value);
    }

    /// <summary>The reminders to show. Grouping and due copy come from the host.</summary>
    public IEnumerable<ArcReminderItem>? Reminders
    {
        get => GetValue(RemindersProperty);
        set => SetValue(RemindersProperty, value);
    }

    /// <summary>Overall state of the center.</summary>
    public ArcReminderCenterState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Raised when a row's completion control is toggled; the host persists the timestamp.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> CompleteRequestedEvent =
        RoutedEvent.Register<ArcReminderCenter, ArcLifeTodayRoutedEventArgs>("CompleteRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="CompleteRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? CompleteRequestedRouted
    {
        add => AddHandler(CompleteRequestedEvent, value);
        remove => RemoveHandler(CompleteRequestedEvent, value);
    }

    public event Action<string, bool>? CompleteRequested;

    /// <summary>Raised when a row asks to be delayed by the given span.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> SnoozeRequestedEvent =
        RoutedEvent.Register<ArcReminderCenter, ArcLifeTodayRoutedEventArgs>("SnoozeRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="SnoozeRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? SnoozeRequestedRouted
    {
        add => AddHandler(SnoozeRequestedEvent, value);
        remove => RemoveHandler(SnoozeRequestedEvent, value);
    }

    public event Action<string, TimeSpan>? SnoozeRequested;

    /// <summary>Raised when a row asks to be rescheduled.</summary>
    public event Action<string>? RescheduleRequested;

    /// <summary>Raised when a row asks to skip this occurrence.</summary>
    public event Action<string>? SkipRequested;

    /// <summary>Raised when the user asks to delay every overdue reminder by the given span.</summary>
    public event Action<TimeSpan>? SnoozeAllRequested;

    /// <summary>Raised when the empty state asks for a first reminder.</summary>
    public event Action? AddRequested;

    /// <summary>Raised when the user retries a failed load.</summary>
    public event Action? RetryRequested;

    private void Rebuild()
    {
        if (!_built)
        {
            return;
        }

        Ui.Name(this, "Reminder center", "Overdue, today, upcoming and snoozed life reminders with row actions.");

        Children.Clear();
        Children.Add(State switch
        {
            ArcReminderCenterState.Loading => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children = { BuildHeader(new List<ArcReminderItem>()), LifeUi.Loading("Loading reminders…") }
            }),
            ArcReminderCenterState.Empty => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    BuildHeader(new List<ArcReminderItem>()),
                    LifeUi.Empty("check-check", "All clear",
                        "Nothing is overdue and nothing is due today. The next reminder is tomorrow at 08:00.",
                        "Add a reminder", () => AddRequested?.Invoke())
                }
            }),
            ArcReminderCenterState.Error => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    BuildHeader(new List<ArcReminderItem>()),
                    LifeUi.Failure("Reminders could not load",
                        "The host keeps the last copy. Retry when the reminder store is reachable.",
                        () => RetryRequested?.Invoke())
                }
            }),
            _ => BuildNormal()
        });
    }

    private Control BuildNormal()
    {
        var reminders = Reminders?.ToList() ?? new List<ArcReminderItem>();
        var visible = _filter is null
            ? reminders
            : reminders.Where(item => item.Group == _filter).ToList();

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(BuildHeader(reminders));
        stack.Children.Add(BuildFilters());

        if (visible.Count == 0)
        {
            var empty = new StackPanel { Spacing = 8 };
            empty.Children.Add(Ui.Caption("Nothing in this view."));
            var showAll = LifeUi.Action("Show all", ArcButtonVariant.Ghost, "Show all reminders", () =>
            {
                _filter = null;
                Rebuild();
            });
            empty.Children.Add(showAll);
            stack.Children.Add(LifeUi.Card(empty, muted: true));
        }
        else
        {
            foreach (var group in GroupOrder)
            {
                var items = visible.Where(item => item.Group == group).ToList();
                if (items.Count == 0)
                {
                    continue;
                }

                stack.Children.Add(BuildGroupHeader(group, items.Count));

                var rows = new StackPanel { Spacing = 8 };
                foreach (var item in items)
                {
                    rows.Children.Add(BuildRow(item));
                }

                stack.Children.Add(rows);
            }
        }

        stack.Children.Add(BuildFooter(reminders));
        return LifeUi.Frame(stack);
    }

    private Control BuildHeader(IReadOnlyList<ArcReminderItem> reminders)
    {
        var overdue = reminders.Count(item => item.Group == ArcReminderGroup.Overdue);
        var today = reminders.Count(item => item.Group == ArcReminderGroup.Today);
        var upcoming = reminders.Count(item => item.Group == ArcReminderGroup.Upcoming);

        var title = new StackPanel { Spacing = 2 };
        title.Children.Add(Ui.Text(Heading, "arc-heading"));
        title.Children.Add(Ui.Caption(Subtitle, 540));

        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        badges.Children.Add(Ui.Badge($"{overdue} overdue", ArcBadgeTone.Danger));
        badges.Children.Add(Ui.Badge($"{today} today", ArcBadgeTone.Warning));
        badges.Children.Add(Ui.Badge($"{upcoming} upcoming", ArcBadgeTone.Neutral));

        var quiet = Ui.HStack(6, Ui.Icon("moon", 13, ArcTokens.TextMuted), Ui.Caption(QuietHoursText));
        quiet.HorizontalAlignment = HorizontalAlignment.Right;

        var right = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(badges);
        right.Children.Add(quiet);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(title);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private Control BuildFilters()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        void Add(string label, ArcReminderGroup? group)
        {
            var chip = LifeUi.Chip(label, _filter == group, () =>
            {
                _filter = group;
                Rebuild();
            });
            chip.Margin = new Thickness(0, 0, 8, 0);
            wrap.Children.Add(chip);
        }

        Add("All", null);
        Add("Overdue", ArcReminderGroup.Overdue);
        Add("Today", ArcReminderGroup.Today);
        Add("Upcoming", ArcReminderGroup.Upcoming);
        Add("Snoozed", ArcReminderGroup.Snoozed);
        return wrap;
    }

    private static Control BuildGroupHeader(ArcReminderGroup group, int count)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Ui.Text(GroupLabel(group), "arc-label"));

        var caption = Ui.Caption(count == 1 ? "1 item" : $"{count} items");
        Grid.SetColumn(caption, 1);
        header.Children.Add(caption);

        return header;
    }

    private Control BuildRow(ArcReminderItem item)
    {
        var row = new ArcReminderRow
        {
            Title = item.Title,
            DueText = item.DueText,
            DueState = item.Status,
            RepeatLabel = item.RepeatLabel,
            SourceLabel = item.SourceLabel,
            IsChecked = item.Status == ArcReminderDueState.Done
        };

        var id = item.Id;
        row.CompletedChanged += value =>
        {
            CompleteRequested?.Invoke(id, value);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(CompleteRequestedEvent) { Text = id, Flag = value });
        };
        row.SnoozeRequested += span =>
        {
            SnoozeRequested?.Invoke(id, span);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(SnoozeRequestedEvent) { Text = id, Span = span });
        };
        row.RescheduleRequested += () => RescheduleRequested?.Invoke(id);
        row.SkipRequested += () => SkipRequested?.Invoke(id);
        return row;
    }

    private Control BuildFooter(IReadOnlyList<ArcReminderItem> reminders)
    {
        var done = reminders.Count(item => item.Status == ArcReminderDueState.Done);
        var summary = Ui.Caption($"{reminders.Count} reminders · {done} done");
        var snooze = LifeUi.Action("Delay overdue by 1 hour", ArcButtonVariant.Ghost,
            "Delay every overdue reminder by 1 hour", () => SnoozeAllRequested?.Invoke(TimeSpan.FromHours(1)));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(summary);
        Grid.SetColumn(snooze, 1);
        grid.Children.Add(snooze);
        return grid;
    }

    private static string GroupLabel(ArcReminderGroup group) => group switch
    {
        ArcReminderGroup.Overdue => "Overdue",
        ArcReminderGroup.Today => "Today",
        ArcReminderGroup.Upcoming => "Upcoming",
        ArcReminderGroup.Snoozed => "Snoozed",
        _ => "Done"
    };

    private static IEnumerable<ArcReminderItem> DefaultReminders() => new[]
    {
        new ArcReminderItem
        {
            Id = "card", Title = "Pay the credit card", DueText = "2 days overdue · ¥1,240",
            Group = ArcReminderGroup.Overdue, Status = ArcReminderDueState.Overdue,
            RepeatLabel = "Monthly", SourceLabel = "Bills"
        },
        new ArcReminderItem
        {
            Id = "recycling", Title = "Take the recycling out", DueText = "Today · 18:00",
            Group = ArcReminderGroup.Today, Status = ArcReminderDueState.Due,
            RepeatLabel = "Every Wednesday", SourceLabel = "Home"
        },
        new ArcReminderItem
        {
            Id = "metformin", Title = "Metformin 500 mg", DueText = "Today · 18:30",
            Group = ArcReminderGroup.Today, Status = ArcReminderDueState.Due,
            RepeatLabel = "Daily", SourceLabel = "Health"
        },
        new ArcReminderItem
        {
            Id = "passport", Title = "Renew the passport", DueText = "In 30 days",
            Group = ArcReminderGroup.Upcoming, Status = ArcReminderDueState.Upcoming,
            RepeatLabel = "Every 10 years", SourceLabel = "ID"
        },
        new ArcReminderItem
        {
            Id = "plants", Title = "Water the plants", DueText = "Snoozed to 20:00",
            Group = ArcReminderGroup.Snoozed, Status = ArcReminderDueState.Snoozed,
            RepeatLabel = "Every 3 days", SourceLabel = "Home"
        }
    };
}

#endregion

#region Medication schedule

/// <summary>
/// Today's medication plan: doses by time with check-off, a refill warning, weekly adherence and the
/// remaining prescription supply.
///
/// Pure UI contract: medication names, doses and stock are display data owned by the host. The control
/// raises take, snooze, skip, undo, refill, pause and edit intents; it never writes a dose log, never
/// schedules a reminder and gives no medical advice. A paused plan renders doses without action buttons.
/// </summary>
public class ArcMedicationSchedule : Panel
{
    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, string>(nameof(Heading), "Medications");

    public static readonly StyledProperty<string> DateTextProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, string>(nameof(DateText), "Today, 12 November");

    public static readonly StyledProperty<string> CourseTextProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, string>(nameof(CourseText), "Day 12 of 30");

    public static readonly StyledProperty<double> CourseProgressProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, double>(nameof(CourseProgress), 0.4);

    public static readonly StyledProperty<string> StockTextProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, string>(nameof(StockText),
            "Supply covers 8 of 10 days");

    public static readonly StyledProperty<double> StockProgressProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, double>(nameof(StockProgress), 0.8);

    public static readonly StyledProperty<string> RefillTextProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, string>(nameof(RefillText),
            "A refill is needed in 2 days. The pharmacy can deliver Thursday.");

    public static readonly StyledProperty<bool> ShowRefillPromptProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, bool>(nameof(ShowRefillPrompt), true);

    public static readonly StyledProperty<IEnumerable<ArcMedicationItem>?> MedicationsProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, IEnumerable<ArcMedicationItem>?>(nameof(Medications));

    public static readonly StyledProperty<IEnumerable<ArcMedicationStatus>?> WeekProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, IEnumerable<ArcMedicationStatus>?>(nameof(Week));

    public static readonly StyledProperty<ArcMedicationScheduleState> StateProperty =
        AvaloniaProperty.Register<ArcMedicationSchedule, ArcMedicationScheduleState>(nameof(State),
            ArcMedicationScheduleState.Normal);

    private bool _built;

    static ArcMedicationSchedule()
    {
        HeadingProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        DateTextProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        CourseTextProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        CourseProgressProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        StockTextProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        StockProgressProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        RefillTextProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        ShowRefillPromptProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        MedicationsProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        WeekProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcMedicationSchedule>((schedule, _) => schedule.Rebuild());
    }

    public ArcMedicationSchedule()
    {
        ClipToBounds = false;
        SetCurrentValue(MedicationsProperty, DefaultMedications());
        SetCurrentValue(WeekProperty, new[]
        {
            ArcMedicationStatus.Taken,
            ArcMedicationStatus.Taken,
            ArcMedicationStatus.Taken,
            ArcMedicationStatus.Missed,
            ArcMedicationStatus.Taken,
            ArcMedicationStatus.Taken,
            ArcMedicationStatus.Pending
        });
        _built = true;
        Rebuild();
    }

    /// <summary>Heading of the surface.</summary>
    public string Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>Today's date, already formatted by the host.</summary>
    public string DateText
    {
        get => GetValue(DateTextProperty);
        set => SetValue(DateTextProperty, value);
    }

    /// <summary>Course position, for example "Day 12 of 30".</summary>
    public string CourseText
    {
        get => GetValue(CourseTextProperty);
        set => SetValue(CourseTextProperty, value);
    }

    /// <summary>Course progress from 0 to 1; display only.</summary>
    public double CourseProgress
    {
        get => GetValue(CourseProgressProperty);
        set => SetValue(CourseProgressProperty, value);
    }

    /// <summary>Remaining supply copy, for example "Supply covers 8 of 10 days".</summary>
    public string StockText
    {
        get => GetValue(StockTextProperty);
        set => SetValue(StockTextProperty, value);
    }

    /// <summary>Remaining supply from 0 to 1; below 0.35 the progress bar turns warning.</summary>
    public double StockProgress
    {
        get => GetValue(StockProgressProperty);
        set => SetValue(StockProgressProperty, value);
    }

    /// <summary>Refill prompt copy; shown only when <see cref="ShowRefillPrompt"/> is true.</summary>
    public string RefillText
    {
        get => GetValue(RefillTextProperty);
        set => SetValue(RefillTextProperty, value);
    }

    /// <summary>Shows the refill warning band.</summary>
    public bool ShowRefillPrompt
    {
        get => GetValue(ShowRefillPromptProperty);
        set => SetValue(ShowRefillPromptProperty, value);
    }

    /// <summary>The doses to show, ordered by time when possible.</summary>
    public IEnumerable<ArcMedicationItem>? Medications
    {
        get => GetValue(MedicationsProperty);
        set => SetValue(MedicationsProperty, value);
    }

    /// <summary>Seven adherence marks, Monday first; missing entries render as pending.</summary>
    public IEnumerable<ArcMedicationStatus>? Week
    {
        get => GetValue(WeekProperty);
        set => SetValue(WeekProperty, value);
    }

    /// <summary>Overall state of the plan.</summary>
    public ArcMedicationScheduleState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Raised when a dose is checked off; the host writes the log entry.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> TakeRequestedEvent =
        RoutedEvent.Register<ArcMedicationSchedule, ArcLifeTodayRoutedEventArgs>("TakeRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="TakeRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? TakeRequestedRouted
    {
        add => AddHandler(TakeRequestedEvent, value);
        remove => RemoveHandler(TakeRequestedEvent, value);
    }

    public event Action<string>? TakeRequested;

    /// <summary>Raised when a dose is postponed by the given span.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> SnoozeRequestedEvent =
        RoutedEvent.Register<ArcMedicationSchedule, ArcLifeTodayRoutedEventArgs>("SnoozeRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="SnoozeRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? SnoozeRequestedRouted
    {
        add => AddHandler(SnoozeRequestedEvent, value);
        remove => RemoveHandler(SnoozeRequestedEvent, value);
    }

    public event Action<string, TimeSpan>? SnoozeRequested;

    /// <summary>Raised when a dose is skipped for today.</summary>
    public event Action<string>? SkipRequested;

    /// <summary>Raised when a taken dose is undone; the host reverses the log entry.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> UndoTakenRequestedEvent =
        RoutedEvent.Register<ArcMedicationSchedule, ArcLifeTodayRoutedEventArgs>("UndoTakenRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="UndoTakenRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? UndoTakenRequestedRouted
    {
        add => AddHandler(UndoTakenRequestedEvent, value);
        remove => RemoveHandler(UndoTakenRequestedEvent, value);
    }

    public event Action<string>? UndoTakenRequested;

    /// <summary>Raised when the user asks to request a refill.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> RefillRequestedEvent =
        RoutedEvent.Register<ArcMedicationSchedule, ArcLifeTodayRoutedEventArgs>("RefillRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="RefillRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? RefillRequestedRouted
    {
        add => AddHandler(RefillRequestedEvent, value);
        remove => RemoveHandler(RefillRequestedEvent, value);
    }

    public event Action? RefillRequested;

    /// <summary>Raised when the user pauses the plan.</summary>
    public event Action? PauseRequested;

    /// <summary>Raised when the user resumes a paused plan.</summary>
    public event Action? ResumeRequested;

    /// <summary>Raised when the user opens the plan editor.</summary>
    public event Action? EditPlanRequested;

    /// <summary>Raised when the empty state asks for a first medication.</summary>
    public event Action? AddRequested;

    private void Rebuild()
    {
        if (!_built)
        {
            return;
        }

        Ui.Name(this, "Medication schedule",
            "Today's doses with check-off, weekly adherence and refill supply. No medical advice.");

        Children.Clear();
        Children.Add(State switch
        {
            ArcMedicationScheduleState.Empty => LifeUi.Frame(new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    BuildHeader(),
                    LifeUi.Empty("heart",
                        "No medications scheduled",
                        "Add a medication to see today's doses, check-off and refill supply.",
                        "Add a medication", () => AddRequested?.Invoke())
                }
            }),
            ArcMedicationScheduleState.Paused => LifeUi.Frame(BuildPaused()),
            _ => LifeUi.Frame(BuildNormal())
        });
    }

    private Control BuildNormal()
    {
        var medications = OrderedMedications();
        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(BuildHeader());
        if (ShowRefillPrompt)
        {
            stack.Children.Add(BuildRefillBand());
        }

        stack.Children.Add(BuildDoses(medications, paused: false));
        stack.Children.Add(BuildFooter());
        return stack;
    }

    private Control BuildPaused()
    {
        var medications = OrderedMedications();
        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(BuildHeader());
        stack.Children.Add(BuildPausedBand());
        stack.Children.Add(BuildDoses(medications, paused: true));
        stack.Children.Add(BuildFooter());
        return stack;
    }

    private Control BuildHeader()
    {
        var disc = LifeUi.IconDisc("heart", ArcTokens.Success, ArcTokens.SuccessSubtle, 38, 18);

        var text = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        text.Children.Add(Ui.Text(Heading, "arc-heading"));
        text.Children.Add(Ui.Caption($"{DateText} · {CourseText}"));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (State != ArcMedicationScheduleState.Empty)
        {
            var paused = State == ArcMedicationScheduleState.Paused;
            actions.Children.Add(LifeUi.Action(paused ? "Resume plan" : "Pause plan", ArcButtonVariant.Ghost,
                paused ? "Resume the medication plan" : "Pause the medication plan", () =>
                {
                    if (paused)
                    {
                        ResumeRequested?.Invoke();
                    }
                    else
                    {
                        PauseRequested?.Invoke();
                    }
                }));
            actions.Children.Add(LifeUi.Action("Edit plan", ArcButtonVariant.Ghost,
                "Edit the medication plan", () => EditPlanRequested?.Invoke()));
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(disc);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        return grid;
    }

    private Control BuildRefillBand()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        grid.Children.Add(new ArcIcon
        {
            Icon = "triangle-alert",
            Size = 15,
            Foreground = LifeUi.Brush(ArcTokens.Warning),
            VerticalAlignment = VerticalAlignment.Center
        });

        var text = Ui.Text(RefillText, "arc-body-sm", wrap: TextWrapping.Wrap);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var request = LifeUi.Action("Request refill", ArcButtonVariant.Secondary,
            "Request a medication refill", () =>
        {
            RefillRequested?.Invoke();
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(RefillRequestedEvent));
        });
        Grid.SetColumn(request, 2);
        grid.Children.Add(request);

        return new Border
        {
            Background = LifeUi.Brush(ArcTokens.WarningSubtle),
            BorderBrush = LifeUi.Brush(ArcTokens.WarningSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = grid
        };
    }

    private Control BuildPausedBand()
    {
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(Ui.HStack(8, Ui.Icon("pause", 15, ArcTokens.TextSecondary),
            Ui.Text("This plan is paused. No doses are expected until you resume.", "arc-body-sm")));
        content.Children.Add(LifeUi.Action("Resume plan", ArcButtonVariant.Secondary,
            "Resume the medication plan", () => ResumeRequested?.Invoke()));

        return new Border
        {
            Background = LifeUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = content
        };
    }

    private Control BuildDoses(IReadOnlyList<ArcMedicationItem> medications, bool paused)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var medication in medications)
        {
            stack.Children.Add(BuildDoseRow(medication, paused));
        }

        return stack;
    }

    private Control BuildDoseRow(ArcMedicationItem item, bool paused)
    {
        var time = Ui.Text(item.TimeText, "arc-body arc-medium", wrap: TextWrapping.NoWrap);
        var timeCell = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        timeCell.Children.Add(time);

        var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Text(item.Name, "arc-body-sm arc-medium"));
        var detail = string.Join(" · ", new[] { item.Dose, item.MealNote }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(detail))
        {
            info.Children.Add(Ui.Caption(detail));
        }

        if (!string.IsNullOrWhiteSpace(item.StockText))
        {
            info.Children.Add(Ui.Caption(item.StockText!));
        }

        var status = MedicationBadge(item.Status);
        var actions = BuildDoseActions(item, paused);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("56,*,Auto,Auto"),
            ColumnSpacing = 10
        };
        grid.Children.Add(timeCell);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        Grid.SetColumn(status, 2);
        grid.Children.Add(status);
        Grid.SetColumn(actions, 3);
        grid.Children.Add(actions);

        return new Border
        {
            Background = LifeUi.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = LifeUi.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = grid
        };
    }

    private Control BuildDoseActions(ArcMedicationItem item, bool paused)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (paused)
        {
            return row;
        }

        switch (item.Status)
        {
            case ArcMedicationStatus.Pending:
                row.Children.Add(LifeUi.Action("Take", ArcButtonVariant.Secondary,
                    $"Mark {item.Name} as taken", () =>
        {
            TakeRequested?.Invoke(item.Id);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(TakeRequestedEvent) { Text = item.Id });
        }));
                row.Children.Add(LifeUi.Action("Snooze", ArcButtonVariant.Ghost,
                    $"Delay {item.Name} by 30 minutes",
                    () =>
        {
            SnoozeRequested?.Invoke(item.Id, TimeSpan.FromMinutes(30));
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(SnoozeRequestedEvent) { Text = item.Id, Span = TimeSpan.FromMinutes(30) });
        }));
                row.Children.Add(LifeUi.Action("Skip", ArcButtonVariant.Ghost,
                    $"Skip {item.Name}", () => SkipRequested?.Invoke(item.Id)));
                break;
            case ArcMedicationStatus.Taken:
                row.Children.Add(LifeUi.Action("Undo", ArcButtonVariant.Ghost,
                    $"Undo {item.Name}", () =>
        {
            UndoTakenRequested?.Invoke(item.Id);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(UndoTakenRequestedEvent) { Text = item.Id });
        }));
                break;
            case ArcMedicationStatus.Missed:
                row.Children.Add(LifeUi.Action("Log as taken", ArcButtonVariant.Secondary,
                    $"Log {item.Name} as taken now", () =>
        {
            TakeRequested?.Invoke(item.Id);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(TakeRequestedEvent) { Text = item.Id });
        }));
                break;
            default:
                row.Children.Add(LifeUi.Action("Take", ArcButtonVariant.Secondary,
                    $"Mark {item.Name} as taken", () =>
        {
            TakeRequested?.Invoke(item.Id);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(TakeRequestedEvent) { Text = item.Id });
        }));
                break;
        }

        return row;
    }

    private Control BuildFooter()
    {
        var week = BuildWeekCard();
        var stock = BuildStockCard();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        grid.Children.Add(week);
        Grid.SetColumn(stock, 1);
        grid.Children.Add(stock);
        return grid;
    }

    private Control BuildWeekCard()
    {
        var statuses = Week?.ToList() ?? new List<ArcMedicationStatus>();
        var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var taken = statuses.Count(status => status == ArcMedicationStatus.Taken);

        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var index = 0; index < days.Length; index++)
        {
            var status = index < statuses.Count ? statuses[index] : ArcMedicationStatus.Pending;
            var key = status switch
            {
                ArcMedicationStatus.Taken => ArcTokens.Success,
                ArcMedicationStatus.Missed => ArcTokens.Danger,
                ArcMedicationStatus.Snoozed => ArcTokens.Warning,
                _ => ArcTokens.BorderStrong
            };
            var dot = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = LifeUi.Brush(key),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(dot, $"{days[index]} · {MedicationWord(status)}");
            dots.Children.Add(dot);
        }

        Ui.Name(dots, "Weekly adherence", $"{taken} of {statuses.Count} doses taken this week");

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(Ui.Caption("This week"));
        content.Children.Add(dots);
        content.Children.Add(Ui.Caption($"{taken} of {statuses.Count} doses taken · course {Math.Round(CourseProgress * 100)}%"));
        return LifeUi.Card(content, muted: true);
    }

    private Control BuildStockCard()
    {
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(Ui.Caption("Refill supply"));
        content.Children.Add(new ArcProgress
        {
            Value = Math.Clamp(StockProgress, 0, 1) * 100,
            Max = 100,
            Label = "Supply",
            ShowValue = true,
            Size = ArcProgressSize.Sm,
            Tone = StockProgress <= 0.35 ? ArcProgressTone.Warning : ArcProgressTone.Accent
        });
        content.Children.Add(Ui.Caption(StockText, 320));
        return LifeUi.Card(content, muted: true);
    }

    private IReadOnlyList<ArcMedicationItem> OrderedMedications()
    {
        var medications = Medications?.ToList() ?? new List<ArcMedicationItem>();
        return medications
            .OrderBy(item => TimeSpan.TryParse(item.TimeText, CultureInfo.InvariantCulture, out var time)
                ? time
                : TimeSpan.MaxValue)
            .ToList();
    }

    private static Control MedicationBadge(ArcMedicationStatus status) => status switch
    {
        ArcMedicationStatus.Taken => Ui.Badge("Taken", ArcBadgeTone.Success),
        ArcMedicationStatus.Missed => Ui.Badge("Missed", ArcBadgeTone.Danger),
        ArcMedicationStatus.Snoozed => Ui.Badge("Snoozed", ArcBadgeTone.Neutral),
        _ => Ui.Badge("Pending", ArcBadgeTone.Neutral)
    };

    private static string MedicationWord(ArcMedicationStatus status) => status switch
    {
        ArcMedicationStatus.Taken => "Taken",
        ArcMedicationStatus.Missed => "Missed",
        ArcMedicationStatus.Snoozed => "Snoozed",
        _ => "Pending"
    };

    private static IEnumerable<ArcMedicationItem> DefaultMedications() => new[]
    {
        new ArcMedicationItem
        {
            Id = "metformin-am", Name = "Metformin", Dose = "500 mg", TimeText = "08:00",
            MealNote = "after breakfast", Status = ArcMedicationStatus.Taken
        },
        new ArcMedicationItem
        {
            Id = "iron", Name = "Iron supplement", Dose = "1 tablet", TimeText = "10:00",
            MealNote = "with water", Status = ArcMedicationStatus.Missed, StockText = "4 doses left"
        },
        new ArcMedicationItem
        {
            Id = "vitamin-d", Name = "Vitamin D", Dose = "1,000 IU", TimeText = "12:30",
            MealNote = "with lunch", Status = ArcMedicationStatus.Taken
        },
        new ArcMedicationItem
        {
            Id = "metformin-pm", Name = "Metformin", Dose = "500 mg", TimeText = "18:00",
            MealNote = "after dinner", Status = ArcMedicationStatus.Pending
        },
        new ArcMedicationItem
        {
            Id = "atorvastatin", Name = "Atorvastatin", Dose = "10 mg", TimeText = "21:30",
            MealNote = "before bed", Status = ArcMedicationStatus.Pending, StockText = "8 doses left"
        }
    };
}

#endregion

