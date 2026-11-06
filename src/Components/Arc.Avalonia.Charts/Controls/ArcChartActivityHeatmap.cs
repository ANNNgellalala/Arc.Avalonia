using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A contribution-style calendar: seven rows and one column per week, five tint steps derived
/// from the accent series, month labels, weekday labels, and a date/count tooltip.
/// </summary>
public class ArcChartActivityHeatmap : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<int>?> ValuesProperty =
        AvaloniaProperty.Register<ArcChartActivityHeatmap, IReadOnlyList<int>?>(nameof(Values));

    public static readonly StyledProperty<DateTime> StartDateProperty =
        AvaloniaProperty.Register<ArcChartActivityHeatmap, DateTime>(nameof(StartDate), DateTime.Today);

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcChartActivityHeatmap, string?>(nameof(Unit), "activity");

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private Rect _grid;
    private double _cell;
    private double _step;
    private int _weeks;
    private int _activeIndex = -1;

    static ArcChartActivityHeatmap()
    {
        AffectsRender<ArcChartActivityHeatmap>(ValuesProperty, StartDateProperty, UnitProperty);
        ValuesProperty.Changed.AddClassHandler<ArcChartActivityHeatmap>((chart, _) => chart._activeIndex = -1);
    }

    public ArcChartActivityHeatmap()
    {
        Focusable = true;
        Height = 190;
    }

    /// <summary>One count per day, oldest first, read left to right then top to bottom.</summary>
    public IReadOnlyList<int>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Date of the top-left cell. Set this to a week boundary for stable month labels.</summary>
    public DateTime StartDate
    {
        get => GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    /// <summary>Noun used in the tooltip.</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    protected override double DefaultHeight => 190;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (!_grid.Contains(point) || _step <= 0)
        {
            SetActive(-1);
            return;
        }

        var column = (int)((point.X - _grid.Left) / _step);
        var row = (int)((point.Y - _grid.Top) / _step);
        column = Math.Clamp(column, 0, Math.Max(0, _weeks - 1));
        row = Math.Clamp(row, 0, 6);
        SetActive(column * 7 + row);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetPosition(this);
        if (!_grid.Contains(point) || _step <= 0)
        {
            return;
        }

        var column = Math.Clamp((int)((point.X - _grid.Left) / _step), 0, Math.Max(0, _weeks - 1));
        var row = Math.Clamp((int)((point.Y - _grid.Top) / _step), 0, 6);
        var index = column * 7 + row;
        var values = Values;
        RaiseItemClick(index, string.Empty, values is not null && index < values.Count ? values[index] : 0, point);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Values?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? count - 1 : _activeIndex;
        switch (e.Key)
        {
            case Key.Left:
                next = Math.Max(0, next - 7);
                break;
            case Key.Right:
                next = Math.Min(count - 1, next + 7);
                break;
            case Key.Up:
                next = Math.Max(0, next - 1);
                break;
            case Key.Down:
                next = Math.Min(count - 1, next + 1);
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = count - 1;
                break;
            case Key.Escape:
                SetActive(-1);
                e.Handled = true;
                return;
            default:
                return;
        }

        SetActive(next);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var values = Values;
        if (values is null || values.Count == 0)
        {
            ChartText.Draw(
                context,
                "No activity",
                Bounds.Width / 2,
                Bounds.Height / 2,
                ChartMetrics.TitleFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        _weeks = Math.Max(1, (values.Count + 6) / 7);
        const double gap = 3;
        const double left = 34;
        const double top = 24;
        var availableWidth = Bounds.Width - left - 6;
        var availableHeight = Bounds.Height - top - 8;
        _cell = Math.Min(16, Math.Min((availableWidth - gap * (_weeks - 1)) / _weeks, (availableHeight - gap * 6) / 7));
        _cell = Math.Max(6, _cell);
        _step = _cell + gap;
        var gridWidth = _weeks * _cell + (_weeks - 1) * gap;
        var gridHeight = 7 * _cell + 6 * gap;
        _grid = new Rect(left + Math.Max(0, (availableWidth - gridWidth) / 2), top + Math.Max(0, (availableHeight - gridHeight) / 2), gridWidth, gridHeight);

        var max = Math.Max(1, values.Max());
        var baseBrush = ChartPalette.Series(0);
        var levelBrushes = new[]
        {
            ChartPalette.BorderSubtle,
            ChartPalette.Alpha(baseBrush, 0.16),
            ChartPalette.Alpha(baseBrush, 0.34),
            ChartPalette.Alpha(baseBrush, 0.58),
            baseBrush
        };

        for (var i = 0; i < _weeks * 7; i++)
        {
            var col = i / 7;
            var row = i % 7;
            var x = _grid.Left + col * _step;
            var y = _grid.Top + row * _step;
            var count = i < values.Count ? Math.Max(0, values[i]) : 0;
            var level = Level(count, max);
            var brush = levelBrushes[level];
            if (_activeIndex >= 0 && i != _activeIndex)
            {
                brush = ChartPalette.Alpha(brush, 0.48);
            }

            context.DrawRectangle(brush, null, new Rect(x, y, _cell, _cell), 2, 2);
            if (i == _activeIndex)
            {
                context.DrawRectangle(
                    null,
                    new Pen(ChartPalette.Foreground, 1.5),
                    new Rect(x - 0.75, y - 0.75, _cell + 1.5, _cell + 1.5),
                    2.5,
                    2.5);
            }
        }

        DrawMonthLabels(context, values.Count);
        DrawWeekdayLabels(context);

        if (_activeIndex >= 0 && _activeIndex < values.Count)
        {
            var date = StartDate.Date.AddDays(_activeIndex);
            var col = _activeIndex / 7;
            var row = _activeIndex % 7;
            var anchor = new Point(_grid.Left + col * _step + _cell / 2, _grid.Top + row * _step + _cell / 2);
            ChartTooltip.Draw(
                context,
                Bounds,
                anchor,
                date.ToString("ddd, MMM d", Culture),
                new[] { ("Count", values[_activeIndex].ToString(Culture) + " " + (Unit ?? "activity"), (IBrush?)null) });
        }
    }

    private void DrawMonthLabels(DrawingContext context, int count)
    {
        var start = StartDate.Date;
        var lastMonth = -1;
        var lastLabelX = double.NegativeInfinity;
        for (var week = 0; week < _weeks; week++)
        {
            var date = start.AddDays(week * 7);
            if (date.Month == lastMonth)
            {
                continue;
            }

            lastMonth = date.Month;
            if (week == 0 && date.Day > 7)
            {
                continue;
            }

            var x = _grid.Left + week * _step;
            if (x - lastLabelX < 28)
            {
                continue;
            }

            lastLabelX = x;
            ChartText.Draw(
                context,
                date.ToString("MMM", Culture),
                x,
                _grid.Top - 9,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Left,
                maxWidth: Math.Max(26, _step * 4));
        }
    }

    private void DrawWeekdayLabels(DrawingContext context)
    {
        var start = StartDate.Date;
        for (var row = 0; row < 7; row++)
        {
            var day = start.AddDays(row).DayOfWeek;
            if (day is not (DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday))
            {
                continue;
            }

            ChartText.Draw(
                context,
                day.ToString()[..3],
                _grid.Left - 8,
                _grid.Top + row * _step + _cell / 2,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Right);
        }
    }

    private static int Level(int count, int max)
    {
        if (count <= 0)
        {
            return 0;
        }

        var step = max / 4.0;
        if (count <= step)
        {
            return 1;
        }

        if (count <= step * 2)
        {
            return 2;
        }

        return count <= step * 3 ? 3 : 4;
    }

    private void SetActive(int index)
    {
        if (_activeIndex == index)
        {
            return;
        }

        _activeIndex = index;
        InvalidateVisual();
    }
}
