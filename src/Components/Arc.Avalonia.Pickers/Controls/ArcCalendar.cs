using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// Arc's calendar: a compact month grid with sliding month navigation, a gliding selection pill and a
/// tinted range band. One tab stop; arrows, Home/End and PageUp/PageDown move a day cursor inside it.
/// </summary>
[TemplatePart("PART_Surface", typeof(ArcCalendarSurface))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_Previous", typeof(Button))]
[TemplatePart("PART_Next", typeof(Button))]
[TemplatePart("PART_Today", typeof(Button))]
public class ArcCalendar : TemplatedControl
{
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(SelectedDate));

    public static readonly StyledProperty<DateTime?> RangeStartProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(RangeStart));

    public static readonly StyledProperty<DateTime?> RangeEndProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(RangeEnd));

    public static readonly StyledProperty<DateTime?> DisplayMonthProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(DisplayMonth));

    public static readonly StyledProperty<DateTime?> MinDateProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(MinDate));

    public static readonly StyledProperty<DateTime?> MaxDateProperty =
        AvaloniaProperty.Register<ArcCalendar, DateTime?>(nameof(MaxDate));

    public static readonly StyledProperty<Func<DateTime, bool>?> DisabledDatesProperty =
        AvaloniaProperty.Register<ArcCalendar, Func<DateTime, bool>?>(nameof(DisabledDates));

    public static readonly StyledProperty<bool> ShowTodayProperty =
        AvaloniaProperty.Register<ArcCalendar, bool>(nameof(ShowToday));

    public static readonly StyledProperty<DayOfWeek> WeekStartsOnProperty =
        AvaloniaProperty.Register<ArcCalendar, DayOfWeek>(nameof(WeekStartsOn), DayOfWeek.Sunday);

    private ArcCalendarSurface? _surface;
    private TextBlock? _title;
    private Button? _previous;
    private Button? _next;
    private Button? _today;
    private DateTime? _cursor;
    private DateTime? _hover;
    private bool _focusVisible;
    private bool _pointerFocus;

    static ArcCalendar()
    {
        SelectedDateProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        RangeStartProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        RangeEndProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        DisplayMonthProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        MinDateProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        MaxDateProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        DisabledDatesProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        WeekStartsOnProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        ShowTodayProperty.Changed.AddClassHandler<ArcCalendar>((calendar, _) => calendar.OnStateChanged());
        FocusableProperty.OverrideDefaultValue<ArcCalendar>(true);
        IsTabStopProperty.OverrideDefaultValue<ArcCalendar>(true);
    }

    /// <summary>Selected day. Controlled: set it, or listen to <see cref="SelectedDateChanged"/>.</summary>
    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>First day of a highlighted range. Together with <see cref="RangeEnd"/> the grid tints the span.</summary>
    public DateTime? RangeStart
    {
        get => GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }

    /// <summary>Last day of the highlighted range.</summary>
    public DateTime? RangeEnd
    {
        get => GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
    }

    /// <summary>Visible month; the first of the month wins. Navigation updates this property.</summary>
    public DateTime? DisplayMonth
    {
        get => GetValue(DisplayMonthProperty);
        set => SetValue(DisplayMonthProperty, value);
    }

    /// <summary>Earliest selectable day; earlier days render disabled.</summary>
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

    /// <summary>Returns true to block a day, for example weekends.</summary>
    public Func<DateTime, bool>? DisabledDates
    {
        get => GetValue(DisabledDatesProperty);
        set => SetValue(DisabledDatesProperty, value);
    }

    /// <summary>Shows the Today action that returns to the current month and selects today when allowed.</summary>
    public bool ShowToday
    {
        get => GetValue(ShowTodayProperty);
        set => SetValue(ShowTodayProperty, value);
    }

    /// <summary>First column of the week; Sunday by default, Monday for ISO weeks.</summary>
    public DayOfWeek WeekStartsOn
    {
        get => GetValue(WeekStartsOnProperty);
        set => SetValue(WeekStartsOnProperty, value);
    }

    public event EventHandler<DateTime>? SelectedDateChanged;

    /// <summary>Bubbled copy of <see cref="SelectedDateChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcCalendarDateChangedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<ArcCalendar, ArcCalendarDateChangedEventArgs>("SelectedDateChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="SelectedDateChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcCalendarDateChangedEventArgs>? SelectedDateChangedRouted
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    private void RaiseSelectedDateChanged(DateTime date)
    {
        SelectedDateChanged?.Invoke(this, date);
        RaiseEvent(new ArcCalendarDateChangedEventArgs(SelectedDateChangedEvent) { Date = date });
    }

    public event EventHandler<DateTime>? DisplayMonthChanged;

    /// <summary>Bubbled copy of <see cref="DisplayMonthChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcCalendarDisplayMonthChangedEventArgs> DisplayMonthChangedEvent =
        RoutedEvent.Register<ArcCalendar, ArcCalendarDisplayMonthChangedEventArgs>("DisplayMonthChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="DisplayMonthChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcCalendarDisplayMonthChangedEventArgs>? DisplayMonthChangedRouted
    {
        add => AddHandler(DisplayMonthChangedEvent, value);
        remove => RemoveHandler(DisplayMonthChangedEvent, value);
    }

    private void RaiseDisplayMonthChanged(DateTime month)
    {
        DisplayMonthChanged?.Invoke(this, month);
        RaiseEvent(new ArcCalendarDisplayMonthChangedEventArgs(DisplayMonthChangedEvent) { Month = month });
    }

    /// <summary>Raised while the pointer moves across days, used for a live range preview.</summary>
    internal event EventHandler<DateTime?>? HoverDateChanged;

    internal DateTime CurrentMonth => ArcDateMath.StartOfMonth(DisplayMonth ?? SelectedDate ?? DateTime.Today);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }

        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }

        if (_today is not null)
        {
            _today.Click -= OnTodayClick;
        }

        if (_surface is not null)
        {
            _surface.PointerPressed -= OnSurfacePressed;
            _surface.PointerMoved -= OnSurfaceMoved;
            _surface.PointerExited -= OnSurfaceExited;
        }

        _surface = e.NameScope.Find<ArcCalendarSurface>("PART_Surface");
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _previous = e.NameScope.Find<Button>("PART_Previous");
        _next = e.NameScope.Find<Button>("PART_Next");
        _today = e.NameScope.Find<Button>("PART_Today");

        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
            AutomationProperties.SetName(_previous, "Previous month");
        }

        if (_next is not null)
        {
            _next.Click += OnNextClick;
            AutomationProperties.SetName(_next, "Next month");
        }

        if (_today is not null)
        {
            _today.Click += OnTodayClick;
        }

        if (_surface is not null)
        {
            _surface.PointerPressed += OnSurfacePressed;
            _surface.PointerMoved += OnSurfaceMoved;
            _surface.PointerExited += OnSurfaceExited;
        }

        if (DisplayMonth is null)
        {
            DisplayMonth = CurrentMonth;
        }

        UpdateSurface();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty)
        {
            UpdateSurface();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _cursor ??= SelectedDate?.Date ?? DateTime.Today;
        _focusVisible = !_pointerFocus;
        UpdateSurface();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        UpdateSurface();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var cursor = (_cursor ?? SelectedDate?.Date ?? DateTime.Today).Date;
        var handled = true;

        switch (e.Key)
        {
            case Key.Left:
                cursor = Move(cursor, -1);
                break;
            case Key.Right:
                cursor = Move(cursor, 1);
                break;
            case Key.Up:
                cursor = Move(cursor, -7);
                break;
            case Key.Down:
                cursor = Move(cursor, 7);
                break;
            case Key.Home:
                cursor = Move(cursor, -OffsetInWeek(cursor));
                break;
            case Key.End:
                cursor = Move(cursor, 6 - OffsetInWeek(cursor));
                break;
            case Key.PageUp:
                cursor = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                    ? ArcDateMath.AddMonthsClamped(cursor, -12)
                    : ArcDateMath.AddMonthsClamped(cursor, -1);
                break;
            case Key.PageDown:
                cursor = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                    ? ArcDateMath.AddMonthsClamped(cursor, 12)
                    : ArcDateMath.AddMonthsClamped(cursor, 1);
                break;
            case Key.Enter:
            case Key.Space:
                Select(cursor);
                e.Handled = true;
                return;
            default:
                handled = false;
                break;
        }

        if (!handled)
        {
            return;
        }

        _cursor = cursor;
        _focusVisible = true;
        if (!IsInCurrentMonth(cursor))
        {
            SetMonth(ArcDateMath.StartOfMonth(cursor));
        }

        UpdateSurface();
        e.Handled = true;
    }

    /// <summary>Moves the keyboard cursor to a day and focuses the grid, used when a popup opens.</summary>
    internal void FocusDay(DateTime date)
    {
        _cursor = date.Date;
        if (!IsInCurrentMonth(date))
        {
            SetMonth(ArcDateMath.StartOfMonth(date));
        }

        UpdateSurface();
        _pointerFocus = true;
        Focus();
        Dispatcher.UIThread.Post(() => _pointerFocus = false);
    }

    private int OffsetInWeek(DateTime date) => ((int)date.DayOfWeek - (int)WeekStartsOn + 7) % 7;

    private bool IsInCurrentMonth(DateTime date) =>
        date.Year == CurrentMonth.Year && date.Month == CurrentMonth.Month;

    private bool IsSelectable(DateTime date) =>
        _surface?.IsSelectable(date) ?? true;

    private DateTime Move(DateTime from, int days)
    {
        if (days == 0)
        {
            return from;
        }

        var step = Math.Sign(days);
        var target = from;
        for (var i = 0; i < Math.Abs(days); i++)
        {
            var next = target.AddDays(step);
            var guard = 0;
            while (!IsSelectable(next) && guard++ < 400)
            {
                next = next.AddDays(step);
            }

            target = next;
        }

        return target;
    }

    private void Select(DateTime date)
    {
        if (!IsSelectable(date))
        {
            return;
        }

        _cursor = date.Date;
        SelectedDate = date.Date;
        RaiseSelectedDateChanged(date.Date);
    }

    private void SetMonth(DateTime month)
    {
        var normalized = ArcDateMath.StartOfMonth(month);
        if (DisplayMonth is { } current && ArcDateMath.StartOfMonth(current) == normalized)
        {
            return;
        }

        DisplayMonth = normalized;
        RaiseDisplayMonthChanged(normalized);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => SetMonth(CurrentMonth.AddMonths(-1));

    private void OnNextClick(object? sender, RoutedEventArgs e) => SetMonth(CurrentMonth.AddMonths(1));

    private void OnTodayClick(object? sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        SetMonth(ArcDateMath.StartOfMonth(today));
        FocusDay(today);
        if (IsSelectable(today) && (MinDate is null || today >= MinDate.Value.Date) && (MaxDate is null || today <= MaxDate.Value.Date))
        {
            SelectedDate = today;
            _cursor = today;
            RaiseSelectedDateChanged(today);
        }
    }

    private void OnSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var date = _surface?.DateAt(e.GetPosition(_surface!));
        if (date is null)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        Dispatcher.UIThread.Post(() => _pointerFocus = false);
        if (DisplayMonth is null || !IsInCurrentMonth(date.Value))
        {
            SetMonth(ArcDateMath.StartOfMonth(date.Value));
        }

        _cursor = date.Value.Date;
        Select(date.Value);
        UpdateSurface();
        e.Handled = true;
    }

    private void OnSurfaceMoved(object? sender, PointerEventArgs e)
    {
        if (_surface is null)
        {
            return;
        }

        var date = _surface.DateAt(e.GetPosition(_surface));
        var next = date?.Date;
        SetCursorShape(next.HasValue && IsSelectable(next.Value));
        if (_hover == next)
        {
            return;
        }

        _hover = next;
        _surface.HoverDate = next;
        HoverDateChanged?.Invoke(this, next);
    }

    private void OnSurfaceExited(object? sender, PointerEventArgs e)
    {
        _hover = null;
        if (_surface is not null)
        {
            _surface.HoverDate = null;
        }

        Cursor = Cursor.Default;
        HoverDateChanged?.Invoke(this, null);
    }

    private void SetCursorShape(bool hand) =>
        Cursor = hand ? new Cursor(StandardCursorType.Hand) : Cursor.Default;

    private void OnStateChanged()
    {
        if (DisplayMonth is { } month && ArcDateMath.StartOfMonth(month) != month)
        {
            DisplayMonth = ArcDateMath.StartOfMonth(month);
            return;
        }

        UpdateSurface();
    }

    private void UpdateSurface()
    {
        if (_surface is null)
        {
            return;
        }

        var month = CurrentMonth;
        _surface.Month = month;
        _surface.SelectedDate = SelectedDate;
        _surface.RangeStart = RangeStart;
        _surface.RangeEnd = RangeEnd;
        _surface.MinDate = MinDate;
        _surface.MaxDate = MaxDate;
        _surface.DisabledDates = DisabledDates;
        _surface.WeekStartsOn = WeekStartsOn;
        _surface.CursorDate = _cursor;
        _surface.ShowCursor = _cursor.HasValue;
        _surface.ShowFocusRing = _focusVisible;
        _surface.FontFamily = FontFamily;

        if (_title is not null)
        {
            _title.Text = ArcDateMath.MonthTitle(month);
        }

        if (_previous is not null)
        {
            _previous.IsEnabled = MinDate is not { } min || month > ArcDateMath.StartOfMonth(min);
        }

        if (_next is not null)
        {
            _next.IsEnabled = MaxDate is not { } max || month < ArcDateMath.StartOfMonth(max);
        }

        if (_today is not null)
        {
            _today.IsVisible = ShowToday;
        }

        AutomationProperties.SetName(this, $"{ArcDateMath.MonthTitle(month)} calendar");
        AutomationProperties.SetHelpText(
            this,
            "Arrow keys move by day or week, Home and End by week edge, PageUp and PageDown by month, Enter selects.");
    }
}
