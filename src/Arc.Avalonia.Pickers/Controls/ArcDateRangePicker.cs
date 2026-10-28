using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>An inclusive whole-day span; both ends are normal dates without a time of day.</summary>
public readonly record struct ArcDateRange(DateTime Start, DateTime End)
{
    /// <summary>Number of days the range covers, counting both ends.</summary>
    public int Days => (End.Date - Start.Date).Days + 1;
}

/// <summary>A shortcut in the range picker rail: a label plus a range built from today.</summary>
public sealed record ArcDateRangePreset(string Label, Func<DateTime, ArcDateRange> Create)
{
    /// <summary>Arc's default rail: Today, Last 7 days, Last 30 days, This month.</summary>
    public static IReadOnlyList<ArcDateRangePreset> Defaults { get; } = new[]
    {
        new ArcDateRangePreset("Today", today => new ArcDateRange(today, today)),
        new ArcDateRangePreset("Last 7 days", today => new ArcDateRange(today.AddDays(-6), today)),
        new ArcDateRangePreset("Last 30 days", today => new ArcDateRange(today.AddDays(-29), today)),
        new ArcDateRangePreset("This month", today => new ArcDateRange(new DateTime(today.Year, today.Month, 1), today))
    };
}

/// <summary>How many months the range panel shows.</summary>
public enum ArcRangeMonths
{
    Auto,
    One,
    Two
}

/// <summary>
/// Arc's date range picker: a compact field that opens a panel with one or two months and a preset
/// rail. Selection is a draft until Apply; Escape or Cancel discards it.
/// </summary>
public class ArcDateRangePicker : ArcPickerBase
{
    public static readonly StyledProperty<ArcDateRange?> RangeProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, ArcDateRange?>(nameof(Range));

    public static readonly StyledProperty<ArcDateRange?> DefaultRangeProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, ArcDateRange?>(nameof(DefaultRange));

    public static readonly StyledProperty<IEnumerable<ArcDateRangePreset>?> PresetsProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, IEnumerable<ArcDateRangePreset>?>(nameof(Presets));

    public static readonly StyledProperty<DateTime?> MinDateProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, DateTime?>(nameof(MinDate));

    public static readonly StyledProperty<DateTime?> MaxDateProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, DateTime?>(nameof(MaxDate));

    public static readonly StyledProperty<DayOfWeek> WeekStartsOnProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, DayOfWeek>(nameof(WeekStartsOn));

    public static readonly StyledProperty<ArcRangeMonths> MonthsProperty =
        AvaloniaProperty.Register<ArcDateRangePicker, ArcRangeMonths>(nameof(Months));

    private readonly List<(ArcButton Button, ArcDateRangePreset Preset)> _presetButtons = new();
    private TextBlock? _value;
    private TextBlock? _label;
    private TextBlock? _description;
    private TextBlock? _footer;
    private Button? _apply;
    private Button? _cancel;
    private StackPanel? _presetHost;
    private ArcCalendar? _left;
    private ArcCalendar? _right;
    private DateTime? _draftStart;
    private DateTime? _draftEnd;
    private DateTime? _hover;
    private bool _syncing;

    static ArcDateRangePicker()
    {
        RangeProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.OnRangeChanged());
        DefaultRangeProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.OnRangeChanged());
        PresetsProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.BuildPresets());
        MonthsProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.UpdateCalendarVisibility());
        MinDateProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.SyncCalendars());
        MaxDateProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.SyncCalendars());
        WeekStartsOnProperty.Changed.AddClassHandler<ArcDateRangePicker>((picker, _) => picker.SyncCalendars());
    }

    /// <summary>Committed range, or null. Selection inside the panel is a draft until Apply.</summary>
    public ArcDateRange? Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    /// <summary>Starting range when uncontrolled.</summary>
    public ArcDateRange? DefaultRange
    {
        get => GetValue(DefaultRangeProperty);
        set => SetValue(DefaultRangeProperty, value);
    }

    /// <summary>Shortcut rail; defaults to <see cref="ArcDateRangePreset.Defaults"/>.</summary>
    public IEnumerable<ArcDateRangePreset>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    /// <summary>Earliest selectable day.</summary>
    public DateTime? MinDate
    {
        get => GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    /// <summary>Latest selectable day.</summary>
    public DateTime? MaxDate
    {
        get => GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    /// <summary>First column of the week; Sunday by default.</summary>
    public DayOfWeek WeekStartsOn
    {
        get => GetValue(WeekStartsOnProperty);
        set => SetValue(WeekStartsOnProperty, value);
    }

    /// <summary>Force one or two months. Auto shows two beside the rail, as Arc does on wide boundaries.</summary>
    public ArcRangeMonths Months
    {
        get => GetValue(MonthsProperty);
        set => SetValue(MonthsProperty, value);
    }

    /// <summary>Raised with the applied range.</summary>
    public event EventHandler<ArcDateRange>? RangeChanged;

    protected override string AccessibleRole => "Date range";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_apply is not null)
        {
            _apply.Click -= OnApplyClick;
        }

        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }

        if (_left is not null)
        {
            _left.SelectedDateChanged -= OnLeftSelected;
            _left.DisplayMonthChanged -= OnLeftMonthChanged;
            _left.HoverDateChanged -= OnCalendarHover;
        }

        if (_right is not null)
        {
            _right.SelectedDateChanged -= OnRightSelected;
            _right.DisplayMonthChanged -= OnRightMonthChanged;
            _right.HoverDateChanged -= OnCalendarHover;
        }

        _value = e.NameScope.Find<TextBlock>("PART_Value");
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _footer = e.NameScope.Find<TextBlock>("PART_Footer");
        _apply = e.NameScope.Find<Button>("PART_Apply");
        _cancel = e.NameScope.Find<Button>("PART_Cancel");
        _presetHost = e.NameScope.Find<StackPanel>("PART_Presets");
        _left = e.NameScope.Find<ArcCalendar>("PART_CalendarLeft");
        _right = e.NameScope.Find<ArcCalendar>("PART_CalendarRight");

        if (_apply is not null)
        {
            _apply.Click += OnApplyClick;
            AutomationProperties.SetName(_apply, "Apply range");
        }

        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
            AutomationProperties.SetName(_cancel, "Cancel range");
        }

        if (_left is not null)
        {
            _left.SelectedDateChanged += OnLeftSelected;
            _left.DisplayMonthChanged += OnLeftMonthChanged;
            _left.HoverDateChanged += OnCalendarHover;
            AutomationProperties.SetName(_left, "Range calendar, first month");
        }

        if (_right is not null)
        {
            _right.SelectedDateChanged += OnRightSelected;
            _right.DisplayMonthChanged += OnRightMonthChanged;
            _right.HoverDateChanged += OnCalendarHover;
            AutomationProperties.SetName(_right, "Range calendar, second month");
        }

        if (Range is null && DefaultRange is { } fallback)
        {
            Range = fallback;
        }

        BuildPresets();
        UpdateCalendarVisibility();
        OnContentChanged();
        SyncCalendars();
    }

    protected override void OnContentChanged()
    {
        if (_label is not null)
        {
            _label.Text = Label;
            _label.IsVisible = !string.IsNullOrWhiteSpace(Label);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_value is not null)
        {
            var hasRange = Range is { } range;
            _value.Text = hasRange
                ? ArcDateMath.Range(Range!.Value.Start, Range!.Value.End)
                : Placeholder ?? "Select dates";
            _value.Classes.Set("picker-placeholder", !hasRange);
            _value.Classes.Set("picker-value", hasRange);
        }

        SetTriggerName(Range is { } r ? ArcDateMath.Range(r.Start, r.End) : null);
    }

    protected override void OnOpened()
    {
        _hover = null;
        _draftStart = Range?.Start.Date;
        _draftEnd = Range?.End.Date;
        SyncCalendars();
        UpdateDraft();
        Dispatcher.UIThread.Post(() => _left?.FocusDay(_draftStart ?? DateTime.Today), DispatcherPriority.Loaded);
    }

    protected override void OnClosed()
    {
        _hover = null;
        _draftStart = null;
        _draftEnd = null;
        SyncCalendars();
        UpdateDraft();
    }

    private void OnRangeChanged()
    {
        OnContentChanged();
        if (!IsDropDownOpen)
        {
            SyncCalendars();
        }
    }

    private void BuildPresets()
    {
        if (_presetHost is null)
        {
            return;
        }

        _presetHost.Children.Clear();
        _presetButtons.Clear();

        foreach (var preset in Presets ?? ArcDateRangePreset.Defaults)
        {
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Content = preset.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontWeight = FontWeight.Normal
            };
            button.Classes.Add("picker-option");
            AutomationProperties.SetName(button, preset.Label);
            button.Click += (_, _) => ApplyPreset(preset);
            button.KeyDown += OnPresetKeyDown;
            _presetButtons.Add((button, preset));
            _presetHost.Children.Add(button);
        }

        UpdateDraft();
    }

    private void ApplyPreset(ArcDateRangePreset preset)    {
        var today = DateTime.Today;
        var range = preset.Create(today);
        _draftStart = range.Start.Date;
        _draftEnd = range.End.Date;
        _hover = null;

        if (_left is not null && _right is not null)
        {
            SetMonths(_draftStart.Value);
        }

        UpdateDraft();
    }

    private void OnPresetKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ArcButton button)
        {
            return;
        }

        var index = _presetButtons.FindIndex(entry => ReferenceEquals(entry.Button, button));
        if (index < 0 || _presetButtons.Count == 0)
        {
            return;
        }

        var target = e.Key switch
        {
            Key.Down => (index + 1) % _presetButtons.Count,
            Key.Up => (index - 1 + _presetButtons.Count) % _presetButtons.Count,
            _ => -1
        };

        if (target < 0)
        {
            return;
        }

        _presetButtons[target].Button.Focus();
        e.Handled = true;
    }

    private void UpdateDraft()
    {
        var previewEnd = _draftEnd;
        if (_draftStart is { } start && _draftEnd is null && _hover is { } hover && hover >= start)
        {
            previewEnd = hover;
        }

        foreach (var calendar in new[] { _left, _right })
        {
            if (calendar is null)
            {
                continue;
            }

            calendar.RangeStart = _draftStart;
            calendar.RangeEnd = previewEnd;
            calendar.SelectedDate = null;
        }

        var complete = _draftStart.HasValue && _draftEnd.HasValue;
        var normalized = Normalize(_draftStart, _draftEnd);
        if (_footer is not null)
        {
            _footer.Text = normalized is { } shown
                ? $"{ArcDateMath.Range(shown.Start, shown.End)} \u00b7 {shown.Days} days"
                : _draftStart is null
                    ? "Pick a start date"
                    : "Pick an end date";
        }

        if (_apply is not null)
        {
            _apply.IsEnabled = complete;
        }

        foreach (var (button, preset) in _presetButtons)
        {
            var presetRange = preset.Create(DateTime.Today);
            var selected = normalized is { } current
                           && current.Start.Date == presetRange.Start.Date
                           && current.End.Date == presetRange.End.Date;
            button.Classes.Set("selected", selected);
        }
    }

    private static ArcDateRange? Normalize(DateTime? start, DateTime? end)
    {
        if (start is not { } s || end is not { } e)
        {
            return null;
        }

        return s <= e ? new ArcDateRange(s.Date, e.Date) : new ArcDateRange(e.Date, s.Date);
    }

    private void SyncCalendars()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var month = ArcDateMath.StartOfMonth(_draftStart ?? Range?.Start ?? DateTime.Today);
            var second = month.AddMonths(1);

            foreach (var calendar in new[] { _left, _right })
            {
                if (calendar is null)
                {
                    continue;
                }

                calendar.MinDate = MinDate;
                calendar.MaxDate = MaxDate;
                calendar.WeekStartsOn = WeekStartsOn;
            }

            if (_left is not null)
            {
                _left.DisplayMonth = month;
            }

            if (_right is not null)
            {
                _right.DisplayMonth = second;
            }
        }
        finally
        {
            _syncing = false;
        }

        UpdateDraft();
    }

    private void SetMonths(DateTime start)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var month = ArcDateMath.StartOfMonth(start);
            if (_left is not null)
            {
                _left.DisplayMonth = month;
            }

            if (_right is not null)
            {
                _right.DisplayMonth = month.AddMonths(1);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateCalendarVisibility()
    {
        if (_right is not null)
        {
            _right.IsVisible = Months != ArcRangeMonths.One;
        }
    }

    private void OnLeftSelected(object? sender, DateTime date) => Pick(date);

    private void OnRightSelected(object? sender, DateTime date) => Pick(date);

    private void Pick(DateTime date)
    {
        date = date.Date;
        if (_draftStart is null || _draftEnd is not null)
        {
            _draftStart = date;
            _draftEnd = null;
        }
        else if (date == _draftStart.Value)
        {
            _draftEnd = date;
        }
        else if (date < _draftStart.Value)
        {
            _draftStart = date;
            _draftEnd = null;
        }
        else
        {
            _draftEnd = date;
        }

        UpdateDraft();
    }

    private void OnCalendarHover(object? sender, DateTime? date)
    {
        _hover = date?.Date;
        UpdateDraft();
    }

    private void OnLeftMonthChanged(object? sender, DateTime month)
    {
        if (_syncing || _right is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            _right.DisplayMonth = month.AddMonths(1);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnRightMonthChanged(object? sender, DateTime month)
    {
        if (_syncing || _left is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            _left.DisplayMonth = month.AddMonths(-1);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (Normalize(_draftStart, _draftEnd) is not { } range)
        {
            return;
        }

        SetCurrentValue(RangeProperty, range);
        OnContentChanged();
        RangeChanged?.Invoke(this, range);
        CloseAndFocus();
        e.Handled = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CloseAndFocus();
        e.Handled = true;
    }
}
