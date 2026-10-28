using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>One day of activity.</summary>
public sealed class ArcHeatmapDay
{
    public DateOnly Date { get; set; }

    public int Count { get; set; }
}

/// <summary>
/// A year of activity as a seven-row calendar: series tints for the levels, month labels, a legend,
/// a tooltip with the exact count, and keyboard movement across days.
/// </summary>
public class ArcActivityHeatmap : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcHeatmapDay>?> DaysProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, IEnumerable<ArcHeatmapDay>?>(nameof(Days));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, string>(nameof(Label), "Activity");

    public static readonly StyledProperty<string> PeriodProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, string>(nameof(Period), string.Empty);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, string>(nameof(Unit), "contributions");

    public static readonly StyledProperty<int> WeekStartsOnProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, int>(nameof(WeekStartsOn));

    public static readonly StyledProperty<IEnumerable<double>?> ThresholdsProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, IEnumerable<double>?>(nameof(Thresholds));

    public static readonly StyledProperty<DateOnly?> SelectedDateProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, DateOnly?>(nameof(SelectedDate));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ArcActivityHeatmap, object?>(nameof(Actions));

    private TextBlock? _summary;
    private TextBlock? _detail;
    private ContentPresenter? _actions;
    private ArcHeatmapGrid? _grid;

    static ArcActivityHeatmap()
    {
        DaysProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        PeriodProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        UnitProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        WeekStartsOnProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        ThresholdsProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.Rebuild());
        SelectedDateProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.SyncSelection());
        ActionsProperty.Changed.AddClassHandler<ArcActivityHeatmap>((heatmap, _) => heatmap.SyncActions());
    }

    public IEnumerable<ArcHeatmapDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    /// <summary>Accessible name for the grid.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Finishes the summary, as in "1,284 contributions in {period}".</summary>
    public string Period
    {
        get => GetValue(PeriodProperty);
        set => SetValue(PeriodProperty, value);
    }

    /// <summary>Noun for the count; the summary uses it in the plural.</summary>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>0 starts weeks on Sunday, 1 on Monday.</summary>
    public int WeekStartsOn
    {
        get => GetValue(WeekStartsOnProperty);
        set => SetValue(WeekStartsOnProperty, value);
    }

    /// <summary>Upper bounds for levels one to three; defaults to quarters of the busiest day.</summary>
    public IEnumerable<double>? Thresholds
    {
        get => GetValue(ThresholdsProperty);
        set => SetValue(ThresholdsProperty, value);
    }

    public DateOnly? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>Controls beside the summary, such as a range switch.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public event EventHandler<DateOnly>? SelectedDateChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _summary = e.NameScope.Find<TextBlock>("Summary");
        _detail = e.NameScope.Find<TextBlock>("Detail");
        _actions = e.NameScope.Find<ContentPresenter>("Actions");
        _grid = e.NameScope.Find<ArcHeatmapGrid>("Grid");

        if (_grid is not null)
        {
            _grid.HoverChanged += (_, day) => UpdateDetail(day);
            _grid.DateSelected += (_, date) =>
            {
                SetCurrentValue(SelectedDateProperty, date);
                SelectedDateChanged?.Invoke(this, date);
                UpdateDetail(day: null);
            };
        }

        Rebuild();
    }

    private void SyncActions()
    {
        if (_actions is not null)
        {
            _actions.Content = Actions;
        }
    }

    private void SyncSelection()
    {
        if (_grid is not null)
        {
            _grid.SelectedDate = SelectedDate;
        }
    }

    private void UpdateDetail(ArcHeatmapDay? day)
    {
        if (_detail is null)
        {
            return;
        }

        if (day is null)
        {
            _detail.Text = "Hover or focus a day to read its count.";
            return;
        }

        var unit = day.Count == 1 ? Singular(Unit) : Unit;
        _detail.Text = $"{day.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}: {day.Count:N0} {unit}";
    }

    private static string Singular(string unit) => unit.EndsWith("s", StringComparison.Ordinal) ? unit[..^1] : unit;

    private void Rebuild()
    {
        if (_summary is null || _grid is null)
        {
            return;
        }

        var days = (Days ?? Enumerable.Empty<ArcHeatmapDay>())
            .GroupBy(day => day.Date)
            .Select(group => new ArcHeatmapDay { Date = group.Key, Count = group.Sum(item => item.Count) })
            .OrderBy(day => day.Date)
            .ToList();

        var total = days.Sum(day => day.Count);
        var max = days.Count == 0 ? 0 : days.Max(day => day.Count);
        var thresholds = Thresholds?.ToList();
        if (thresholds is null || thresholds.Count < 3)
        {
            thresholds = new List<double>
            {
                Math.Max(1, max * 0.25),
                Math.Max(1, max * 0.5),
                Math.Max(1, max * 0.75)
            };
        }

        _grid.Days = days;
        _grid.WeekStart = Math.Clamp(WeekStartsOn, 0, 1);
        _grid.Thresholds = thresholds;
        _grid.SelectedDate = SelectedDate;
        _grid.Paint(ArcHeatmapGrid.LabelBrushProperty, ArcTokens.TextMuted);
        AutomationProperties.SetName(_grid, Label);

        var unit = total == 1 ? Singular(Unit) : Unit;
        _summary.Text = string.IsNullOrWhiteSpace(Period)
            ? $"{total:N0} {unit}"
            : $"{total:N0} {unit} in {Period}";

        AutomationProperties.SetName(this, Label);
        UpdateDetail(null);
        SyncActions();
    }
}

/// <summary>The drawn calendar. Kept separate so the summary row can stay real text.</summary>
public sealed class ArcHeatmapGrid : Control
{
    public static readonly StyledProperty<IReadOnlyList<ArcHeatmapDay>?> DaysProperty =
        AvaloniaProperty.Register<ArcHeatmapGrid, IReadOnlyList<ArcHeatmapDay>?>(nameof(Days));

    public static readonly StyledProperty<int> WeekStartProperty =
        AvaloniaProperty.Register<ArcHeatmapGrid, int>(nameof(WeekStart));

    public static readonly StyledProperty<IReadOnlyList<double>?> ThresholdsProperty =
        AvaloniaProperty.Register<ArcHeatmapGrid, IReadOnlyList<double>?>(nameof(Thresholds));

    public static readonly StyledProperty<DateOnly?> SelectedDateProperty =
        AvaloniaProperty.Register<ArcHeatmapGrid, DateOnly?>(nameof(SelectedDate));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<ArcHeatmapGrid, IBrush?>(nameof(LabelBrush));

    private const double Cell = 12;
    private const double Gap = 3;
    private const double Step = Cell + Gap;
    private const double Left = 30;
    private const double Top = 20;
    private const double Legend = 26;

    private readonly Dictionary<DateOnly, int> _counts = new();
    private DateOnly _minDate;
    private DateOnly _first;
    private DateOnly _last;
    private int _weeks;
    private int _hover = -1;
    private int _focused = -1;
    private bool _keyboard;

    public event EventHandler<ArcHeatmapDay?>? HoverChanged;

    public event EventHandler<DateOnly>? DateSelected;

    static ArcHeatmapGrid()
    {
        AffectsRender<ArcHeatmapGrid>(DaysProperty, WeekStartProperty, ThresholdsProperty, SelectedDateProperty, LabelBrushProperty);
        AffectsMeasure<ArcHeatmapGrid>(DaysProperty, WeekStartProperty);
    }

    public ArcHeatmapGrid()
    {
        Focusable = true;
    }

    public IReadOnlyList<ArcHeatmapDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public int WeekStart
    {
        get => GetValue(WeekStartProperty);
        set => SetValue(WeekStartProperty, value);
    }

    public IReadOnlyList<double>? Thresholds
    {
        get => GetValue(ThresholdsProperty);
        set => SetValue(ThresholdsProperty, value);
    }

    public DateOnly? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Prepare();
        return new Size(Left + Math.Max(1, _weeks) * Step + 2, Top + 7 * Step + Legend);
    }

    private void Prepare()
    {
        _counts.Clear();
        var days = Days;
        if (days is null || days.Count == 0)
        {
            _first = DateOnly.FromDateTime(DateTime.Today.AddDays(-364));
            _last = DateOnly.FromDateTime(DateTime.Today);
        }
        else
        {
            _first = days.Min(day => day.Date);
            _last = days.Max(day => day.Date);
            foreach (var day in days)
            {
                _counts[day.Date] = day.Count;
            }
        }

        var shift = ((int)_first.DayOfWeek - Math.Clamp(WeekStart, 0, 1) + 7) % 7;
        _minDate = _first;
        _first = _first.AddDays(-shift);
        _weeks = Math.Max(1, ((_last.DayNumber - _first.DayNumber) / 7) + 1);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        Prepare();
        var labelBrush = LabelBrush ?? ArcResources.Brush(ArcTokens.TextMuted);
        var accent = ArcResources.Brush(ArcTokens.Series1);
        var background = ArcResources.Color(ArcTokens.Background);
        var dark = background.R + background.G + background.B < 384;
        var baseCell = ArcResources.Brush(dark ? ArcTokens.BorderSubtle : ArcTokens.ControlTrack);
        var typeface = new Typeface(FeedbackUi.Font(ArcTokens.FontBody));

        var level = Math.Max(1, Thresholds is { Count: >= 3 } thresholds ? thresholds[2] : 1);

        // Month labels above the columns, spaced so two labels never collide.
        var lastLabelX = double.NegativeInfinity;
        var lastMonth = -1;
        for (var week = 0; week < _weeks; week++)
        {
            var weekStart = _first.AddDays(week * 7);
            var date = weekStart < _minDate ? _minDate : weekStart;
            if (date.Month == lastMonth)
            {
                continue;
            }

            lastMonth = date.Month;
            var x = Left + week * Step;
            if (x - lastLabelX < 34)
            {
                continue;
            }

            lastLabelX = x;
            DrawText(context, date.ToString("MMM", CultureInfo.InvariantCulture), x, 2, 10, labelBrush, typeface);
        }

        // Weekday labels on the Monday, Wednesday, Friday rows.
        var startDow = Math.Clamp(WeekStart, 0, 1);
        for (var row = 0; row < 7; row++)
        {
            var dow = (row + startDow) % 7;
            if (dow is not (1 or 3 or 5))
            {
                continue;
            }

            var name = dow switch { 1 => "Mon", 3 => "Wed", _ => "Fri" };
            var y = Top + row * Step + 1;
            DrawText(context, name, 0, y, 9.5, labelBrush, typeface);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var week = 0; week < _weeks; week++)
        {
            for (var row = 0; row < 7; row++)
            {
                var date = _first.AddDays(week * 7 + row);
                if (date > _last)
                {
                    continue;
                }

                var count = _counts.TryGetValue(date, out var value) ? value : 0;
                var levelIndex = LevelFor(count, level);
                var x = Left + week * Step;
                var y = Top + row * Step;
                var cell = new Rect(x, y, Cell, Cell);

                if (levelIndex == 0)
                {
                    context.DrawRectangle(baseCell, null, cell, 3, 3);
                }
                else
                {
                    using (context.PushOpacity(LevelOpacity(levelIndex, dark)))
                    {
                        context.DrawRectangle(accent, null, cell, 3, 3);
                    }
                }

                if (SelectedDate == date)
                {
                    context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.AccentStrong), 2), cell.Inflate(1.5), 4.5, 4.5);
                }
                else if (date == today && levelIndex == 0)
                {
                    context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.BorderStrong), 1), cell, 3, 3);
                }
            }
        }

        DrawLegend(context, labelBrush, accent, baseCell, dark, typeface);

        var hover = _hover >= 0 ? _hover : _keyboard ? _focused : -1;
        if (hover >= 0 && hover < _weeks * 7 && DayAt(hover) is not null)
        {
            DrawTooltip(context, hover, labelBrush, typeface, level);
        }
    }

    private void DrawLegend(DrawingContext context, IBrush labelBrush, IBrush accent, IBrush baseCell, bool dark, Typeface typeface)
    {
        var y = Top + 7 * Step + 8;
        var x = Left;
        DrawText(context, "Less", x, y + 1, 10, labelBrush, typeface);
        x += 30;
        for (var level = 0; level <= 4; level++)
        {
            var cell = new Rect(x, y, 10, 10);
            if (level == 0)
            {
                context.DrawRectangle(baseCell, null, cell, 2.5, 2.5);
            }
            else
            {
                using (context.PushOpacity(LevelOpacity(level, dark)))
                {
                    context.DrawRectangle(accent, null, cell, 2.5, 2.5);
                }
            }

            x += 14;
        }

        DrawText(context, "More", x + 4, y + 1, 10, labelBrush, typeface);
    }

    private static double LevelOpacity(int level, bool dark) => dark
        ? level switch { 1 => 0.38, 2 => 0.58, 3 => 0.78, _ => 1.0 }
        : level switch { 1 => 0.28, 2 => 0.5, 3 => 0.72, _ => 1.0 };

    private void DrawTooltip(DrawingContext context, int index, IBrush labelBrush, Typeface typeface, double maxLevel)
    {
        var date = _first.AddDays(index);
        var count = _counts.TryGetValue(date, out var value) ? value : 0;
        var unit = count == 1 ? "contribution" : "contributions";
        var text = $"{date.ToString("MMM d", CultureInfo.InvariantCulture)}: {count:N0} {unit}";
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10.5, ArcResources.Brush(ArcTokens.Foreground));
        var width = formatted.Width + 16;
        var height = formatted.Height + 10;
        var column = index / 7;
        var row = index % 7;
        var x = Left + column * Step + Cell / 2 - width / 2;
        x = Math.Clamp(x, 2, Math.Max(2, Bounds.Width - width - 2));
        var y = Top + row * Step - height - 6;
        if (y < 18)
        {
            y = Top + row * Step + Cell + 6;
        }

        var rect = new RoundedRect(new Rect(x, y, width, height), 7);
        context.DrawRectangle(ArcResources.Brush(ArcTokens.SurfaceRaised), new Pen(ArcResources.Brush(ArcTokens.Border), 1), rect);
        context.DrawText(formatted, new Point(x + 8, y + 5));
    }

    private int LevelFor(int count, double max)
    {
        if (count <= 0)
        {
            return 0;
        }

        var thresholds = Thresholds;
        if (thresholds is { Count: >= 3 })
        {
            if (count <= thresholds[0])
            {
                return 1;
            }

            if (count <= thresholds[1])
            {
                return 2;
            }

            return count <= thresholds[2] ? 3 : 4;
        }

        return 4;
    }

    private static void DrawText(DrawingContext context, string text, double x, double y, double size, IBrush brush, Typeface typeface)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
        context.DrawText(formatted, new Point(x, y));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        var index = HitTest(position);
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
            HoverChanged?.Invoke(this, DayAt(index));
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            InvalidateVisual();
            HoverChanged?.Invoke(this, null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = HitTest(e.GetPosition(this));
        if (index < 0)
        {
            return;
        }

        Focus();
        _keyboard = false;
        _focused = index;
        Select(index);
        e.Handled = true;
    }

    private ArcHeatmapDay? DayAt(int index)
    {
        if (index < 0 || index >= _weeks * 7)
        {
            return null;
        }

        var date = _first.AddDays(index);
        if (date > _last)
        {
            return null;
        }

        return new ArcHeatmapDay
        {
            Date = date,
            Count = _counts.TryGetValue(date, out var value) ? value : 0
        };
    }

    private int HitTest(Point position)
    {
        if (position.X < Left || position.Y < Top || position.Y > Top + 7 * Step)
        {
            return -1;
        }

        var column = (int)((position.X - Left) / Step);
        var row = (int)((position.Y - Top) / Step);
        if (column < 0 || column >= _weeks || row < 0 || row > 6)
        {
            return -1;
        }

        var index = column * 7 + row;
        return _first.AddDays(index) <= _last ? index : -1;
    }

    private void Select(int index)
    {
        var day = DayAt(index);
        if (day is null)
        {
            return;
        }

        SelectedDate = day.Date;
        InvalidateVisual();
        DateSelected?.Invoke(this, day.Date);
        HoverChanged?.Invoke(this, day);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_weeks == 0)
        {
            return;
        }

        if (_focused < 0)
        {
            _focused = 0;
        }

        var max = _weeks * 7 - 1;
        switch (e.Key)
        {
            case Key.Left:
                _focused = Math.Max(0, _focused - 7);
                KeyMoved();
                e.Handled = true;
                break;
            case Key.Right:
                _focused = Math.Min(max, _focused + 7);
                KeyMoved();
                e.Handled = true;
                break;
            case Key.Up:
                _focused = Math.Max(0, _focused - 1);
                KeyMoved();
                e.Handled = true;
                break;
            case Key.Down:
                _focused = Math.Min(max, _focused + 1);
                KeyMoved();
                e.Handled = true;
                break;
            case Key.Home:
                _focused = 0;
                KeyMoved();
                e.Handled = true;
                break;
            case Key.End:
                _focused = max;
                KeyMoved();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Select(_focused);
                e.Handled = true;
                break;
            case Key.Escape:
                _hover = -1;
                InvalidateVisual();
                HoverChanged?.Invoke(this, null);
                e.Handled = true;
                break;
        }
    }

    private void KeyMoved()
    {
        _keyboard = true;
        InvalidateVisual();
        HoverChanged?.Invoke(this, DayAt(_focused));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboard = true;
        if (_focused < 0)
        {
            _focused = Math.Max(0, _weeks * 7 - 1);
        }

        InvalidateVisual();
        HoverChanged?.Invoke(this, DayAt(_focused));
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard = false;
        InvalidateVisual();
    }
}

