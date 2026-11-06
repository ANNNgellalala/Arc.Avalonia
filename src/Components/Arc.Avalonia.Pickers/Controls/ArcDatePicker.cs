using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// Arc's date picker: a field that opens a calendar in a floating panel and shows the formatted date.
/// Typed keys stay in the calendar; Enter opens the panel, Escape closes it.
/// </summary>
[TemplatePart("PART_Value", typeof(TextBlock))]
[TemplatePart("PART_Label", typeof(TextBlock))]
[TemplatePart("PART_Description", typeof(TextBlock))]
[TemplatePart("PART_Clear", typeof(Button))]
[TemplatePart("PART_Calendar", typeof(ArcCalendar))]
public class ArcDatePicker : ArcPickerBase
{
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<ArcDatePicker, DateTime?>(nameof(SelectedDate));

    public static readonly StyledProperty<DateTime?> MinDateProperty =
        AvaloniaProperty.Register<ArcDatePicker, DateTime?>(nameof(MinDate));

    public static readonly StyledProperty<DateTime?> MaxDateProperty =
        AvaloniaProperty.Register<ArcDatePicker, DateTime?>(nameof(MaxDate));

    public static readonly StyledProperty<Func<DateTime, bool>?> DisabledDatesProperty =
        AvaloniaProperty.Register<ArcDatePicker, Func<DateTime, bool>?>(nameof(DisabledDates));

    public static readonly StyledProperty<bool> ShowTodayProperty =
        AvaloniaProperty.Register<ArcDatePicker, bool>(nameof(ShowToday));

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<ArcDatePicker, string>(nameof(Format), "MMM d, yyyy");

    private TextBlock? _value;
    private TextBlock? _label;
    private TextBlock? _description;
    private Button? _clear;
    private ArcCalendar? _calendar;
    private bool _syncing;

    static ArcDatePicker()
    {
        SelectedDateProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.OnSelectionChanged());
        FormatProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.OnSelectionChanged());
        MinDateProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.SyncCalendar());
        MaxDateProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.SyncCalendar());
        DisabledDatesProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.SyncCalendar());
        ShowTodayProperty.Changed.AddClassHandler<ArcDatePicker>((picker, _) => picker.SyncCalendar());
    }

    /// <summary>Selected day, or null. Arc's controlled value.</summary>
    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
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

    /// <summary>Returns true to block a day.</summary>
    public Func<DateTime, bool>? DisabledDates
    {
        get => GetValue(DisabledDatesProperty);
        set => SetValue(DisabledDatesProperty, value);
    }

    /// <summary>Adds the Today action to the calendar header.</summary>
    public bool ShowToday
    {
        get => GetValue(ShowTodayProperty);
        set => SetValue(ShowTodayProperty, value);
    }

    /// <summary>Date format used by the field, in .NET format syntax; Arc uses Intl options.</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Raised on a pick and with null from the Clear action.</summary>
    public event EventHandler<DateTime?>? SelectedDateChanged;

    /// <summary>Bubbled copy of <see cref="SelectedDateChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcDatePickerSelectedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<ArcDatePicker, ArcDatePickerSelectedEventArgs>("SelectedDateChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="SelectedDateChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcDatePickerSelectedEventArgs>? SelectedDateChangedRouted
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    private void RaiseSelectedDateChanged(DateTime? date)
    {
        SelectedDateChanged?.Invoke(this, date);
        RaiseEvent(new ArcDatePickerSelectedEventArgs(SelectedDateChangedEvent) { Date = date });
    }

    protected override string AccessibleRole => "Date";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }

        if (_calendar is not null)
        {
            _calendar.SelectedDateChanged -= OnCalendarSelected;
        }

        _value = e.NameScope.Find<TextBlock>("PART_Value");
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _clear = e.NameScope.Find<Button>("PART_Clear");
        _calendar = e.NameScope.Find<ArcCalendar>("PART_Calendar");

        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
            AutomationProperties.SetName(_clear, "Clear date");
        }

        if (_calendar is not null)
        {
            _calendar.SelectedDateChanged += OnCalendarSelected;
        }

        OnContentChanged();
        SyncCalendar();
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
            var hasValue = SelectedDate.HasValue;
            _value.Text = hasValue
                ? ArcDateMath.FieldDate(SelectedDate!.Value, Format)
                : Placeholder ?? "Select a date";
            _value.Classes.Set("picker-placeholder", !hasValue);
            _value.Classes.Set("picker-value", hasValue);
        }

        if (_clear is not null)
        {
            _clear.IsVisible = SelectedDate.HasValue;
        }

        if (TriggerPart is not null)
        {
            TriggerPart.Padding = SelectedDate.HasValue ? new Thickness(14, 0, 40, 0) : new Thickness(14, 0, 14, 0);
        }

        SetTriggerName(SelectedDate.HasValue ? ArcDateMath.FieldDate(SelectedDate!.Value, Format) : null);
    }

    protected override void OnOpened()
    {
        SyncCalendar();
        var focus = SelectedDate?.Date ?? DateTime.Today;
        Dispatcher.UIThread.Post(() => _calendar?.FocusDay(focus), DispatcherPriority.Loaded);
    }

    private void OnSelectionChanged()
    {
        if (_syncing)
        {
            return;
        }

        OnContentChanged();
        SyncCalendar();
    }

    private void SyncCalendar()
    {
        if (_calendar is null || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            _calendar.SelectedDate = SelectedDate;
            _calendar.MinDate = MinDate;
            _calendar.MaxDate = MaxDate;
            _calendar.DisabledDates = DisabledDates;
            _calendar.ShowToday = ShowToday;
            if (SelectedDate is { } date)
            {
                _calendar.DisplayMonth = ArcDateMath.StartOfMonth(date);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnCalendarSelected(object? sender, DateTime date)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            SelectedDate = date;
            RaiseSelectedDateChanged(date);
        }
        finally
        {
            _syncing = false;
        }

        OnContentChanged();
        CloseAndFocus();
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        SelectedDate = null;
        RaiseSelectedDateChanged(null);
        OnContentChanged();
        TriggerPart?.Focus();
        e.Handled = true;
    }
}
