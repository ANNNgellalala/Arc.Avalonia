using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>How often a routine runs. Custom is a weekday set the host extends with its own interval.</summary>
public enum ArcRoutineRepeat
{
    Daily,
    Weekdays,
    Weekly,
    Monthly,
    Custom
}

/// <summary>What should happen when a scheduled run was missed, for example while the machine slept.</summary>
public enum ArcRoutineMisfirePolicy
{
    /// <summary>Run once on wake, then continue with the normal cadence.</summary>
    RunOnce,

    /// <summary>Skip the missed run.</summary>
    Skip,

    /// <summary>Ask the visitor before doing anything.</summary>
    Ask
}

/// <summary>What should happen when the previous run is still going at the next scheduled time.</summary>
public enum ArcRoutineOverlapPolicy
{
    /// <summary>Skip the new run.</summary>
    Skip,

    /// <summary>Queue the new run and start it when the current one ends.</summary>
    Queue,

    /// <summary>Start the new run immediately, side by side.</summary>
    Allow
}

/// <summary>The editor's display state. The host drives it; the control words it and validates input.</summary>
public enum ArcRoutineEditorState
{
    /// <summary>Read only: the rule card is being browsed.</summary>
    Viewing,

    /// <summary>The visitor is changing the rule.</summary>
    Editing,

    /// <summary>The host is checking or saving the rule.</summary>
    Validating,

    /// <summary>The host accepted the rule.</summary>
    Saved,

    /// <summary>The rule does not validate, or the host rejected it.</summary>
    Error
}

/// <summary>
/// The structured contract the editor raises on save. It is display data only: the host owns the scheduler,
/// the real time zone database, persistence and the missed-run recovery. The RRULE string is a readable
/// subset (FREQ / BYDAY / BYMONTHDAY / INTERVAL) built from the current fields.
/// </summary>
public sealed class ArcScheduleSpec
{
    public ArcRoutineRepeat Repeat { get; set; }

    /// <summary>24-hour "HH:mm" text.</summary>
    public string TimeText { get; set; } = "08:00";

    /// <summary>IANA zone name as supplied by the host, for example "Asia/Shanghai".</summary>
    public string TimeZone { get; set; } = "Asia/Shanghai";

    /// <summary>Selected weekdays, ordered Monday to Sunday; empty for daily and monthly rules.</summary>
    public IReadOnlyList<DayOfWeek> Days { get; set; } = Array.Empty<DayOfWeek>();

    /// <summary>Day of the month for a monthly rule, 1 to 31.</summary>
    public int MonthlyDay { get; set; } = 1;

    public ArcRoutineMisfirePolicy MisfirePolicy { get; set; }

    public ArcRoutineOverlapPolicy OverlapPolicy { get; set; }

    /// <summary>Readable RRULE subset; the host may store a fuller rule.</summary>
    public string Rrule { get; set; } = string.Empty;
}

/// <summary>
/// Arc's routine schedule editor: repeat, days, time, time zone, missed-run and overlap policies, a live
/// next-run preview and the RRULE the host will store. It is a pure UI contract. The editor validates what it
/// shows and raises <see cref="SaveRequested"/> with an <see cref="ArcScheduleSpec"/>; it never schedules,
/// never keeps a timer and never runs the routine. Time zone database, daylight saving resolution, persistence
/// and misfire recovery all stay with the host.
/// </summary>
public class ArcRoutineScheduleEditor : Panel
{
    /// <summary>Repeat cadence. Weekly and Custom use <see cref="SelectedDays"/>.</summary>
    public static readonly StyledProperty<ArcRoutineRepeat> RepeatProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, ArcRoutineRepeat>(nameof(Repeat));

    /// <summary>24-hour "HH:mm" time text; the time picker and the host share it.</summary>
    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, string>(nameof(TimeText), "08:00");

    /// <summary>IANA time zone as a display string, such as "Asia/Shanghai (UTC+8)".</summary>
    public static readonly StyledProperty<string> TimeZoneTextProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, string>(nameof(TimeZoneText), "Asia/Shanghai (UTC+8)");

    /// <summary>Weekdays for a weekly or custom rule; the editor copies the list before changing it.</summary>
    public static readonly StyledProperty<IList<DayOfWeek>?> SelectedDaysProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, IList<DayOfWeek>?>(nameof(SelectedDays));

    /// <summary>Day of the month for a monthly rule, 1 to 31.</summary>
    public static readonly StyledProperty<int> MonthlyDayProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, int>(nameof(MonthlyDay), 1);

    /// <summary>Overrides the computed next-run line. Leave empty to let the editor preview it.</summary>
    public static readonly StyledProperty<string> NextRunTextProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, string>(nameof(NextRunText), string.Empty);

    public static readonly StyledProperty<ArcRoutineMisfirePolicy> MisfirePolicyProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, ArcRoutineMisfirePolicy>(
            nameof(MisfirePolicy), ArcRoutineMisfirePolicy.RunOnce);

    public static readonly StyledProperty<ArcRoutineOverlapPolicy> OverlapPolicyProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, ArcRoutineOverlapPolicy>(
            nameof(OverlapPolicy), ArcRoutineOverlapPolicy.Skip);

    public static readonly StyledProperty<ArcRoutineEditorState> StateProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, ArcRoutineEditorState>(
            nameof(State), ArcRoutineEditorState.Editing);

    /// <summary>Validation copy shown when <see cref="State"/> is Error; empty falls back to local checks.</summary>
    public static readonly StyledProperty<string> ValidationMessageProperty =
        AvaloniaProperty.Register<ArcRoutineScheduleEditor, string>(nameof(ValidationMessage), string.Empty);

    private static readonly (DayOfWeek Day, string Short, string Full, string Code)[] Week =
    {
        (DayOfWeek.Monday, "Mon", "Monday", "MO"),
        (DayOfWeek.Tuesday, "Tue", "Tuesday", "TU"),
        (DayOfWeek.Wednesday, "Wed", "Wednesday", "WE"),
        (DayOfWeek.Thursday, "Thu", "Thursday", "TH"),
        (DayOfWeek.Friday, "Fri", "Friday", "FR"),
        (DayOfWeek.Saturday, "Sat", "Saturday", "SA"),
        (DayOfWeek.Sunday, "Sun", "Sunday", "SU")
    };

    private readonly TextBlock _summary;
    private readonly Border _stateHost;
    private readonly WrapPanel _repeatRow;
    private readonly WrapPanel _daysRow;
    private readonly StackPanel _daysSection;
    private readonly ArcTimePicker _timePicker;
    private readonly StackPanel _monthlySection;
    private readonly TextBox _monthlyDay;
    private readonly WrapPanel _misfireRow;
    private readonly WrapPanel _overlapRow;
    private readonly TextBlock _nextRun;
    private readonly TextBlock _rrule;
    private readonly TextBlock _validation;
    private readonly ArcButton _save;
    private bool _syncing;

    static ArcRoutineScheduleEditor()
    {
        RepeatProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        TimeTextProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.OnTimeTextChanged());
        TimeZoneTextProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        SelectedDaysProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        MonthlyDayProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        NextRunTextProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        MisfirePolicyProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        OverlapPolicyProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        StateProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
        ValidationMessageProperty.Changed.AddClassHandler<ArcRoutineScheduleEditor>((editor, _) => editor.Refresh());
    }

    public ArcRoutineScheduleEditor()
    {
        Width = 720;
        HorizontalAlignment = HorizontalAlignment.Left;

        _summary = ArcUi.Text("Every Mon, Wed and Fri at 8:00 am", "arc-body arc-medium");
        _stateHost = new Border { VerticalAlignment = VerticalAlignment.Center };

        _repeatRow = new WrapPanel { Orientation = Orientation.Horizontal };
        _daysRow = new WrapPanel { Orientation = Orientation.Horizontal };
        _daysSection = ArcUi.Stack(6, ArcUi.Label("On days"), _daysRow);

        _timePicker = new ArcTimePicker
        {
            Width = 200,
            Value = new TimeSpan(8, 0, 0),
            MinuteStep = 15,
            Placeholder = "Choose a time"
        };
        ArcUi.Name(_timePicker, "Run time", "Choose the time of day the routine should run.");
        _timePicker.TimeChanged += OnTimePickerChanged;

        _monthlyDay = new TextBox { Width = 72, Text = "1", VerticalContentAlignment = VerticalAlignment.Center };
        ArcUi.Name(_monthlyDay, "Day of month", "A number from 1 to 31.");
        _monthlyDay.TextChanged += OnMonthlyDayChanged;
        _monthlySection = ArcUi.Stack(
            6,
            ArcUi.Label("Day of month"),
            ArcUi.HStack(8, _monthlyDay, ArcUi.Caption("1\u201331")));

        _misfireRow = new WrapPanel { Orientation = Orientation.Horizontal };
        _overlapRow = new WrapPanel { Orientation = Orientation.Horizontal };

        _nextRun = ArcUi.Text(string.Empty, "arc-body arc-medium");
        _rrule = ArcUi.Mono(string.Empty);
        _validation = ArcUi.Caption(string.Empty);
        _validation.TextWrapping = TextWrapping.Wrap;

        _save = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = ArcUi.HStack(6, ArcUi.Icon("check", 14), ArcUi.Text("Save schedule", "arc-body-sm"))
        };
        ArcUi.Name(_save, "Save schedule", "Raise the schedule spec for the host to store.");
        _save.Click += (_, _) => OnSave();

        var cancel = ArcUi.Button("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "x");
        ArcUi.Name(cancel, "Cancel schedule", "Discard the edit and raise CancelRequested.");
        cancel.Click += (_, _) => OnCancel();

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        footer.Children.Add(_validation);
        Grid.SetColumn(cancel, 1);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        footer.Children.Add(cancel);
        Grid.SetColumn(_save, 2);
        footer.Children.Add(_save);

        Build(footer);
        ArcUi.Name(
            this,
            "Routine schedule editor",
            "Edit how often a routine runs, its time and policies. Save raises a schedule spec; the host schedules it.");

        // A per-instance list: the property default is shared between controls, so it must stay immutable.
        SetCurrentValue(
            SelectedDaysProperty,
            new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday });
        Refresh();
    }

    /// <summary>Raised with the structured rule when the visitor saves; the host validates and persists it.</summary>
    public event Action<ArcScheduleSpec>? SaveRequested;

    /// <summary>Raised when the visitor discards the edit.</summary>
    public event EventHandler? CancelRequested;

    public ArcRoutineRepeat Repeat
    {
        get => GetValue(RepeatProperty);
        set => SetValue(RepeatProperty, value);
    }

    public string TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public string TimeZoneText
    {
        get => GetValue(TimeZoneTextProperty);
        set => SetValue(TimeZoneTextProperty, value);
    }

    public IList<DayOfWeek>? SelectedDays
    {
        get => GetValue(SelectedDaysProperty);
        set => SetValue(SelectedDaysProperty, value);
    }

    public int MonthlyDay
    {
        get => GetValue(MonthlyDayProperty);
        set => SetValue(MonthlyDayProperty, value);
    }

    public string NextRunText
    {
        get => GetValue(NextRunTextProperty);
        set => SetValue(NextRunTextProperty, value);
    }

    public ArcRoutineMisfirePolicy MisfirePolicy
    {
        get => GetValue(MisfirePolicyProperty);
        set => SetValue(MisfirePolicyProperty, value);
    }

    public ArcRoutineOverlapPolicy OverlapPolicy
    {
        get => GetValue(OverlapPolicyProperty);
        set => SetValue(OverlapPolicyProperty, value);
    }

    public ArcRoutineEditorState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string ValidationMessage
    {
        get => GetValue(ValidationMessageProperty);
        set => SetValue(ValidationMessageProperty, value);
    }

    /// <summary>Builds the current rule without saving it, for a host that previews the contract.</summary>
    public ArcScheduleSpec CreateSpec() => new()
    {
        Repeat = Repeat,
        TimeText = TimeText,
        TimeZone = TimeZoneText,
        Days = SelectedDays?.OrderBy(day => Array.FindIndex(Week, entry => entry.Day == day)).ToList()
               ?? new List<DayOfWeek>(),
        MonthlyDay = MonthlyDay,
        MisfirePolicy = MisfirePolicy,
        OverlapPolicy = OverlapPolicy,
        Rrule = BuildRrule()
    };

    private void Build(Control footer)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_summary);
        Grid.SetColumn(_stateHost, 1);
        header.Children.Add(_stateHost);

        var timeRow = new WrapPanel { Orientation = Orientation.Horizontal };
        timeRow.Children.Add(ArcUi.Stack(6, ArcUi.Label("Time"), _timePicker));
        _monthlySection.Margin = new Thickness(16, 0, 16, 0);
        timeRow.Children.Add(_monthlySection);
        timeRow.Children.Add(ArcUi.Stack(
            6,
            ArcUi.Label("Time zone"),
            ArcUi.HStack(8, ArcUi.Pill(TimeZoneText, "globe"), ArcUi.Caption("Resolved by the host"))));

        var policies = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
        var misfire = ArcUi.Stack(6, ArcUi.Label("When a run is missed"), _misfireRow);
        var overlap = ArcUi.Stack(6, ArcUi.Label("When a run is still going"), _overlapRow);
        policies.Children.Add(misfire);
        Grid.SetColumn(overlap, 1);
        policies.Children.Add(overlap);

        var preview = ArcUi.Surface(
            ArcUi.Stack(4, ArcUi.Label("Next run"), _nextRun, _rrule),
            padding: 14,
            backgroundKey: ArcTokens.SurfaceMuted);

        var stack = ArcUi.Stack(
            16,
            header,
            ArcUi.Divider(),
            ArcUi.Label("Repeat"),
            _repeatRow,
            _daysSection,
            timeRow,
            policies,
            preview,
            footer);

        Children.Clear();
        Children.Add(ArcUi.Surface(stack, padding: 20));
    }

    private void OnTimeTextChanged()
    {
        SyncTimePicker();
        Refresh();
    }

    private void SyncTimePicker()
    {
        if (!TryReadTime(out var time) || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            _timePicker.Value = time;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnTimePickerChanged(object? sender, TimeSpan time)
    {
        if (_syncing)
        {
            return;
        }

        SetCurrentValue(TimeTextProperty, $"{time.Hours:00}:{time.Minutes:00}");
        MarkEditing();
        Refresh();
    }

    private void OnMonthlyDayChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        if (int.TryParse(_monthlyDay.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) &&
            day is >= 1 and <= 31)
        {
            SetCurrentValue(MonthlyDayProperty, day);
        }

        MarkEditing();
        Refresh();
    }

    private void Refresh()
    {
        _syncing = true;
        try
        {
            _summary.Text = Summary();
            _stateHost.Child = ArcUi.Badge(StateWord(State), StateTone(State));
            _nextRun.Text = string.IsNullOrWhiteSpace(NextRunText) ? ComputeNextRun() : NextRunText;
            _rrule.Text = $"{BuildRrule()} \u00b7 {TimeZoneText}";
            _monthlyDay.Text = MonthlyDay.ToString(CultureInfo.InvariantCulture);
            SyncTimePicker();
            _save.IsEnabled = State != ArcRoutineEditorState.Validating;
        }
        finally
        {
            _syncing = false;
        }

        BuildRepeatButtons();
        BuildDayButtons();
        BuildMisfireButtons();
        BuildOverlapButtons();

        _daysSection.IsVisible = Repeat is ArcRoutineRepeat.Weekly or ArcRoutineRepeat.Custom;
        _monthlySection.IsVisible = Repeat == ArcRoutineRepeat.Monthly;

        var message = ValidationText();
        _validation.Text = message;
        _validation.IsVisible = message.Length > 0;
        _validation.Foreground = State == ArcRoutineEditorState.Error
            ? ArcUi.Brush(ArcTokens.Danger)
            : ArcUi.Brush(ArcTokens.TextSecondary);

        AutomationProperties.SetHelpText(this, $"{StateWord(State)}. {Summary()}. Next run {_nextRun.Text}.");
    }

    private void BuildRepeatButtons()
    {
        _repeatRow.Children.Clear();
        foreach (var (value, label) in new (ArcRoutineRepeat, string)[]
                 {
                     (ArcRoutineRepeat.Daily, "Daily"),
                     (ArcRoutineRepeat.Weekdays, "Weekdays"),
                     (ArcRoutineRepeat.Weekly, "Weekly"),
                     (ArcRoutineRepeat.Monthly, "Monthly"),
                     (ArcRoutineRepeat.Custom, "Custom")
                 })
        {
            var button = new ArcButton
            {
                Variant = value == Repeat ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                Content = label,
                Margin = new Thickness(0, 0, 6, 6)
            };
            ArcUi.Name(button, $"Repeat {label.ToLowerInvariant()}");
            button.Click += (_, _) =>
            {
                SetCurrentValue(RepeatProperty, value);
                MarkEditing();
            };
            _repeatRow.Children.Add(button);
        }
    }

    private void BuildDayButtons()
    {
        _daysRow.Children.Clear();
        var selected = SelectedDays ?? Array.Empty<DayOfWeek>();
        foreach (var (day, shortLabel, full, _) in Week)
        {
            var button = new ArcButton
            {
                Variant = selected.Contains(day) ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = shortLabel,
                MinWidth = 48,
                Margin = new Thickness(0, 0, 6, 6)
            };
            ArcUi.Name(button, full, $"Toggle {full} for the routine.");
            button.Click += (_, _) => ToggleDay(day);
            _daysRow.Children.Add(button);
        }
    }

    private void ToggleDay(DayOfWeek day)
    {
        var days = (SelectedDays ?? Array.Empty<DayOfWeek>()).ToList();
        if (!days.Remove(day))
        {
            days.Add(day);
        }

        SetCurrentValue(
            SelectedDaysProperty,
            days.OrderBy(value => Array.FindIndex(Week, entry => entry.Day == value)).ToList());
        MarkEditing();
    }

    private void BuildMisfireButtons()
    {
        _misfireRow.Children.Clear();
        foreach (var (value, label) in new (ArcRoutineMisfirePolicy, string)[]
                 {
                     (ArcRoutineMisfirePolicy.RunOnce, "Run once"),
                     (ArcRoutineMisfirePolicy.Skip, "Skip"),
                     (ArcRoutineMisfirePolicy.Ask, "Ask me")
                 })
        {
            var button = new ArcButton
            {
                Variant = value == MisfirePolicy ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                Content = label,
                Margin = new Thickness(0, 0, 6, 6)
            };
            ArcUi.Name(button, $"Missed run: {label.ToLowerInvariant()}");
            button.Click += (_, _) =>
            {
                SetCurrentValue(MisfirePolicyProperty, value);
                MarkEditing();
            };
            _misfireRow.Children.Add(button);
        }
    }

    private void BuildOverlapButtons()
    {
        _overlapRow.Children.Clear();
        foreach (var (value, label) in new (ArcRoutineOverlapPolicy, string)[]
                 {
                     (ArcRoutineOverlapPolicy.Skip, "Skip"),
                     (ArcRoutineOverlapPolicy.Queue, "Queue"),
                     (ArcRoutineOverlapPolicy.Allow, "Allow")
                 })
        {
            var button = new ArcButton
            {
                Variant = value == OverlapPolicy ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                Content = label,
                Margin = new Thickness(0, 0, 6, 6)
            };
            ArcUi.Name(button, $"Overlap: {label.ToLowerInvariant()}");
            button.Click += (_, _) =>
            {
                SetCurrentValue(OverlapPolicyProperty, value);
                MarkEditing();
            };
            _overlapRow.Children.Add(button);
        }
    }

    private void MarkEditing()
    {
        if (_syncing)
        {
            return;
        }

        if (IsValid && !string.IsNullOrEmpty(ValidationMessage))
        {
            SetCurrentValue(ValidationMessageProperty, string.Empty);
        }

        SetCurrentValue(StateProperty, IsValid ? ArcRoutineEditorState.Editing : ArcRoutineEditorState.Error);
    }

    private void OnSave()
    {
        if (!IsValid)
        {
            SetCurrentValue(StateProperty, ArcRoutineEditorState.Error);
            Refresh();
            return;
        }

        SetCurrentValue(StateProperty, ArcRoutineEditorState.Saved);
        SaveRequested?.Invoke(CreateSpec());
        Refresh();
    }

    private void OnCancel()
    {
        SetCurrentValue(StateProperty, ArcRoutineEditorState.Viewing);
        CancelRequested?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    private bool IsValid => TryReadTime(out _) && (Repeat != ArcRoutineRepeat.Monthly || MonthlyDay is >= 1 and <= 31);

    private bool TryReadTime(out TimeSpan time)
    {
        time = default;
        return TimeSpan.TryParse(TimeText, CultureInfo.InvariantCulture, out time) &&
               time >= TimeSpan.Zero &&
               time < TimeSpan.FromDays(1);
    }

    private string ValidationText()
    {
        if (!string.IsNullOrWhiteSpace(ValidationMessage))
        {
            return ValidationMessage;
        }

        if (!TryReadTime(out _))
        {
            return "Enter the run time as HH:mm, such as 08:00.";
        }

        if (Repeat == ArcRoutineRepeat.Monthly && MonthlyDay is < 1 or > 31)
        {
            return "Choose a day of the month from 1 to 31.";
        }

        return string.Empty;
    }

    private string Summary()
    {
        var time = TryReadTime(out var parsed)
            ? new DateTime(2000, 1, 1).Add(parsed).ToString("h:mm tt", ArcDateMath.Culture)
            : TimeText;

        return Repeat switch
        {
            ArcRoutineRepeat.Daily => $"Every day at {time}",
            ArcRoutineRepeat.Weekdays => $"Every weekday at {time}",
            ArcRoutineRepeat.Weekly => $"Every {DayList(requireAll: true)} at {time}",
            ArcRoutineRepeat.Monthly => $"Monthly on day {MonthlyDay} at {time}",
            _ => $"Custom: every {DayList(requireAll: false)} at {time}"
        };
    }

    private string DayList(bool requireAll)
    {
        var selected = SelectedDays ?? Array.Empty<DayOfWeek>();
        var labels = Week.Where(entry => selected.Contains(entry.Day)).Select(entry => entry.Short).ToList();
        if (labels.Count == 0)
        {
            return requireAll ? "day" : "day (none chosen)";
        }

        return labels.Count == 1 ? labels[0] : string.Join(", ", labels);
    }

    private string BuildRrule()
    {
        var codes = string.Join(
            ",",
            Week.Where(entry => (SelectedDays ?? Array.Empty<DayOfWeek>()).Contains(entry.Day)).Select(entry => entry.Code));

        return Repeat switch
        {
            ArcRoutineRepeat.Daily => "FREQ=DAILY",
            ArcRoutineRepeat.Weekdays => "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
            ArcRoutineRepeat.Weekly => codes.Length == 0 ? "FREQ=WEEKLY" : $"FREQ=WEEKLY;BYDAY={codes}",
            ArcRoutineRepeat.Monthly => $"FREQ=MONTHLY;BYMONTHDAY={MonthlyDay}",
            _ => codes.Length == 0 ? "FREQ=WEEKLY;INTERVAL=1" : $"FREQ=WEEKLY;INTERVAL=1;BYDAY={codes}"
        };
    }

    private string ComputeNextRun()
    {
        if (!TryReadTime(out var time))
        {
            return "Add a valid time to see the next run";
        }

        var now = DateTime.Now;
        for (var offset = 0; offset < 370; offset++)
        {
            var day = now.Date.AddDays(offset);
            if (!RunsOn(day))
            {
                continue;
            }

            var candidate = day + time;
            if (candidate <= now)
            {
                continue;
            }

            var relative = candidate.Date == now.Date
                ? "today"
                : candidate.Date == now.Date.AddDays(1)
                    ? "tomorrow"
                    : $"in {(candidate.Date - now.Date).Days} days";
            return $"{candidate.ToString("ddd d MMM", ArcDateMath.Culture)} \u00b7 " +
                   $"{candidate.ToString("HH:mm", CultureInfo.InvariantCulture)} ({relative})";
        }

        return "No upcoming run with this rule";
    }

    private bool RunsOn(DateTime day)
    {
        var selected = SelectedDays ?? Array.Empty<DayOfWeek>();
        return Repeat switch
        {
            ArcRoutineRepeat.Daily => true,
            ArcRoutineRepeat.Weekdays => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
            ArcRoutineRepeat.Weekly or ArcRoutineRepeat.Custom => selected.Contains(day.DayOfWeek),
            ArcRoutineRepeat.Monthly => day.Day == MonthlyDay,
            _ => false
        };
    }

    private static string StateWord(ArcRoutineEditorState state) => state switch
    {
        ArcRoutineEditorState.Viewing => "Viewing",
        ArcRoutineEditorState.Validating => "Validating",
        ArcRoutineEditorState.Saved => "Saved",
        ArcRoutineEditorState.Error => "Error",
        _ => "Editing"
    };

    private static ArcBadgeTone StateTone(ArcRoutineEditorState state) => state switch
    {
        ArcRoutineEditorState.Validating => ArcBadgeTone.Accent,
        ArcRoutineEditorState.Saved => ArcBadgeTone.Success,
        ArcRoutineEditorState.Error => ArcBadgeTone.Danger,
        _ => ArcBadgeTone.Neutral
    };
}
