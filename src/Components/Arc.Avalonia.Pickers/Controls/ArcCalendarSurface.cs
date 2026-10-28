using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// The day grid of <see cref="ArcCalendar"/>. It is a pure renderer: the calendar owns focus, keyboard
/// and selection, this control draws the weekday rail, the range band, the selection pill, the today
/// marker and the keyboard cursor. Kept separate so the month header can stay a styled template.
/// </summary>
public sealed class ArcCalendarSurface : Control
{
    public static readonly StyledProperty<DateTime> MonthProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime>(nameof(Month));

    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(SelectedDate));

    public static readonly StyledProperty<DateTime?> RangeStartProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(RangeStart));

    public static readonly StyledProperty<DateTime?> RangeEndProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(RangeEnd));

    public static readonly StyledProperty<DateTime?> MinDateProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(MinDate));

    public static readonly StyledProperty<DateTime?> MaxDateProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(MaxDate));

    public static readonly StyledProperty<Func<DateTime, bool>?> DisabledDatesProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, Func<DateTime, bool>?>(nameof(DisabledDates));

    public static readonly StyledProperty<DayOfWeek> WeekStartsOnProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DayOfWeek>(nameof(WeekStartsOn));

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, double>(nameof(CellSize), 42);

    public static readonly StyledProperty<DateTime?> HoverDateProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(HoverDate));

    public static readonly StyledProperty<DateTime?> CursorDateProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, DateTime?>(nameof(CursorDate));

    public static readonly StyledProperty<bool> ShowCursorProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, bool>(nameof(ShowCursor));

    public static readonly StyledProperty<bool> ShowFocusRingProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, bool>(nameof(ShowFocusRing));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<ArcCalendarSurface, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public const double WeekdayHeight = 24;

    static ArcCalendarSurface()
    {
        AffectsMeasure<ArcCalendarSurface>(MonthProperty, WeekStartsOnProperty, CellSizeProperty);
        AffectsRender<ArcCalendarSurface>(
            MonthProperty,
            SelectedDateProperty,
            RangeStartProperty,
            RangeEndProperty,
            MinDateProperty,
            MaxDateProperty,
            DisabledDatesProperty,
            WeekStartsOnProperty,
            HoverDateProperty,
            CursorDateProperty,
            ShowCursorProperty,
            ShowFocusRingProperty,
            FontFamilyProperty);
    }

    public DateTime Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTime? RangeStart
    {
        get => GetValue(RangeStartProperty);
        set => SetValue(RangeStartProperty, value);
    }

    public DateTime? RangeEnd
    {
        get => GetValue(RangeEndProperty);
        set => SetValue(RangeEndProperty, value);
    }

    public DateTime? MinDate
    {
        get => GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    public DateTime? MaxDate
    {
        get => GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    public Func<DateTime, bool>? DisabledDates
    {
        get => GetValue(DisabledDatesProperty);
        set => SetValue(DisabledDatesProperty, value);
    }

    public DayOfWeek WeekStartsOn
    {
        get => GetValue(WeekStartsOnProperty);
        set => SetValue(WeekStartsOnProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public DateTime? HoverDate
    {
        get => GetValue(HoverDateProperty);
        set => SetValue(HoverDateProperty, value);
    }

    public DateTime? CursorDate
    {
        get => GetValue(CursorDateProperty);
        set => SetValue(CursorDateProperty, value);
    }

    public bool ShowCursor
    {
        get => GetValue(ShowCursorProperty);
        set => SetValue(ShowCursorProperty, value);
    }

    public bool ShowFocusRing
    {
        get => GetValue(ShowFocusRingProperty);
        set => SetValue(ShowFocusRingProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    internal int Weeks => ArcDateMath.MonthGrid(Month, WeekStartsOn).Weeks;

    internal DateTime FirstVisible => ArcDateMath.MonthGrid(Month, WeekStartsOn).FirstVisible;

    protected override Size MeasureOverride(Size availableSize)
    {
        var cell = CellSize;
        return new Size(cell * 7, WeekdayHeight + Weeks * cell);
    }

    internal bool IsSelectable(DateTime date)
    {
        if (MinDate is { } min && date.Date < min.Date)
        {
            return false;
        }

        if (MaxDate is { } max && date.Date > max.Date)
        {
            return false;
        }

        return DisabledDates is not { } blocked || !blocked(date.Date);
    }

    /// <summary>Maps a point in surface coordinates to the day under it.</summary>
    internal DateTime? DateAt(Point point)
    {
        var width = Bounds.Width;
        if (width <= 0 || point.Y < WeekdayHeight)
        {
            return null;
        }

        var cellWidth = width / 7.0;
        var column = (int)Math.Floor(point.X / cellWidth);
        var row = (int)Math.Floor((point.Y - WeekdayHeight) / CellSize);
        if (column is < 0 or > 6 || row < 0 || row >= Weeks)
        {
            return null;
        }

        return FirstVisible.AddDays(row * 7 + column);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var cellWidth = width / 7.0;
        var cellHeight = CellSize;
        var weeks = Weeks;
        var first = FirstVisible;
        var month = ArcDateMath.StartOfMonth(Month);

        var foreground = ArcResources.Brush(ArcTokens.Foreground);
        var muted = ArcResources.Brush(ArcTokens.TextMuted);
        var accent = ArcResources.Brush(ArcTokens.Accent);
        var accentForeground = ArcResources.Brush(ArcTokens.AccentForeground);
        var accentSubtle = ArcResources.Brush(ArcTokens.AccentSubtle);
        var hover = ArcResources.Brush(ArcTokens.SurfaceMuted);
        var focus = ArcResources.Brush(ArcTokens.FocusOutline);

        var typeface = new Typeface(FontFamily);
        var mediumTypeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);

        // Weekday rail.
        for (var column = 0; column < 7; column++)
        {
            var day = (DayOfWeek)(((int)WeekStartsOn + column) % 7);
            var label = ArcDateMath.WeekdayShort(day);
            var text = Text(label, 12, muted, typeface, cellWidth);
            context.DrawText(text, new Point(column * cellWidth, (WeekdayHeight - text.Height) / 2));
        }

        var rangeStart = RangeStart?.Date;
        var rangeEnd = RangeEnd?.Date;
        if (rangeStart is { } rs && rangeEnd is { } re && re < rs)
        {
            (rangeStart, rangeEnd) = (re, rs);
        }

        var hasRange = rangeStart.HasValue && rangeEnd.HasValue && rangeStart.Value != rangeEnd.Value;
        var hovered = HoverDate?.Date;

        for (var index = 0; index < weeks * 7; index++)
        {
            var date = first.AddDays(index);
            var column = index % 7;
            var row = index / 7;
            var cell = new Rect(column * cellWidth, WeekdayHeight + row * cellHeight, cellWidth, cellHeight);
            var pillSize = cellHeight - 8;
            var pill = new Rect(
                cell.X + (cellWidth - pillSize) / 2,
                cell.Y + (cellHeight - pillSize) / 2,
                pillSize,
                pillSize);
            var radius = Math.Min(12, pillSize / 2);

            var outside = date.Month != month.Month || date.Year != month.Year;
            var enabled = IsSelectable(date);
            var isStart = ArcDateMath.SameDay(rangeStart, date);
            var isEnd = ArcDateMath.SameDay(rangeEnd, date);
            var inRange = hasRange && date >= rangeStart!.Value && date <= rangeEnd!.Value;
            var isSelected = !hasRange && ArcDateMath.SameDay(SelectedDate, date);
            var isToday = date.Date == DateTime.Today;

            // Range band: full-width tint that meets the pill at each end.
            if (inRange)
            {
                var band = new Rect(cell.X, cell.Y + 4, cellWidth, cellHeight - 8);
                if (isStart)
                {
                    band = new Rect(cell.X + cellWidth / 2, band.Y, cellWidth / 2, band.Height);
                }
                else if (isEnd)
                {
                    band = new Rect(cell.X, band.Y, cellWidth / 2, band.Height);
                }

                context.DrawRectangle(accentSubtle, null, band, 0, 0);
            }

            // Hover wash for an idle day.
            if (hovered is { } hoverDate && ArcDateMath.SameDay(hoverDate, date) && enabled && !inRange && !isSelected)
            {
                context.DrawRectangle(hover, null, pill, radius, radius);
            }

            using (context.PushOpacity(outside ? 0.45 : enabled ? 1.0 : 0.35))
            {
                if (isStart || isEnd || isSelected)
                {
                    context.DrawRectangle(accent, null, pill, radius, radius);
                }

                var brush = isSelected || isStart || isEnd ? accentForeground : isToday ? accent : outside ? muted : foreground;
                var face = isSelected || isStart || isEnd || isToday ? mediumTypeface : typeface;
                var text = Text(date.Day.ToString(ArcDateMath.Culture), 14, brush, face, cellWidth);
                context.DrawText(text, new Point(cell.X, cell.Y + (cellHeight - text.Height) / 2));

                if (isToday && !isSelected && !isStart && !isEnd)
                {
                    context.DrawEllipse(accent, null, new Point(cell.X + cellWidth / 2, cell.Y + cellHeight / 2 + 12), 1.75, 1.75);
                }
                else if (isToday && (isSelected || isStart || isEnd))
                {
                    context.DrawEllipse(accentForeground, null, new Point(cell.X + cellWidth / 2, cell.Y + cellHeight / 2 + 12), 1.75, 1.75);
                }
            }

            // Keyboard cursor: the focus ring itself, drawn only while the calendar has keyboard focus.
            if (ShowCursor && ShowFocusRing && ArcDateMath.SameDay(CursorDate, date))
            {
                var ring = new Rect(pill.X - 2, pill.Y - 2, pill.Width + 4, pill.Height + 4);
                context.DrawRectangle(null, new Pen(focus, 2), ring, radius + 2, radius + 2);
            }
        }
    }

    private static FormattedText Text(string value, double size, IBrush brush, Typeface typeface, double width) =>
        new(value, ArcDateMath.Culture, FlowDirection.LeftToRight, typeface, size, brush)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth = width
        };
}
