using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts;

/// <summary>
/// A ten by ten unit chart where each cell is one percent. Groups fill from the bottom-left in
/// column order, and hovering a cell highlights its group in both the grid and the legend.
/// </summary>
public class ArcWaffleChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartValue>?> SegmentsProperty =
        AvaloniaProperty.Register<ArcWaffleChart, IReadOnlyList<ChartValue>?>(nameof(Segments));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcWaffleChart, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcWaffleChart, string?>(nameof(Unit));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcWaffleChart, Func<double, string>?>(nameof(ValueFormat));

    private readonly List<(Rect Bounds, int Index)> _legendHits = new();
    private int[] _owners = Array.Empty<int>();
    private Rect _grid;
    private double _cell;
    private double _step;
    private int _activeIndex = -1;

    static ArcWaffleChart()
    {
        AffectsRender<ArcWaffleChart>(
            SegmentsProperty,
            ShowLegendProperty,
            UnitProperty,
            ValueFormatProperty);
        SegmentsProperty.Changed.AddClassHandler<ArcWaffleChart>((chart, _) => chart._activeIndex = -1);
    }

    public ArcWaffleChart()
    {
        Focusable = true;
    }

    public IReadOnlyList<ChartValue>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>Shows each group name and share beside the grid.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 210;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                SetActive(hit.Index);
                return;
            }
        }

        if (_grid.Contains(point) && _step > 0)
        {
            var col = (int)((point.X - _grid.Left) / _step);
            var rowFromBottom = (int)((_grid.Bottom - point.Y) / _step);
            col = Math.Clamp(col, 0, 9);
            rowFromBottom = Math.Clamp(rowFromBottom, 0, 9);
            var index = col * 10 + rowFromBottom;
            if (index >= 0 && index < _owners.Length)
            {
                SetActive(_owners[index]);
                return;
            }
        }

        SetActive(-1);
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
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Visible().Count;
        if (count == 0)
        {
            return;
        }

        var next = _activeIndex < 0 ? 0 : _activeIndex;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                next = (next - 1 + count) % count;
                break;
            case Key.Right:
            case Key.Down:
                next = (next + 1) % count;
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
        _legendHits.Clear();
        var segments = Visible();
        var total = segments.Sum(item => item.Value);
        if (segments.Count == 0 || total <= 0)
        {
            ChartText.Draw(
                context,
                "No data",
                Bounds.Width / 2,
                Bounds.Height / 2,
                13,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        var showLegend = ShowLegend && Bounds.Width >= 258;
        var availableGrid = showLegend ? Math.Min(Bounds.Width * 0.54, Bounds.Height - 16) : Math.Min(Bounds.Width, Bounds.Height) - 16;
        availableGrid = Math.Max(80, availableGrid);
        _grid = new Rect(4, (Bounds.Height - availableGrid) / 2, availableGrid, availableGrid);
        const int gap = 2;
        _cell = (_grid.Width - gap * 9) / 10;
        _step = _cell + gap;

        _owners = BuildOwners(segments, total);
        for (var index = 0; index < _owners.Length; index++)
        {
            var owner = _owners[index];
            var col = index / 10;
            var row = index % 10;
            var x = _grid.Left + col * _step;
            var y = _grid.Bottom - _cell - row * _step;
            var rect = new Rect(x, y, _cell, _cell);
            var brush = segments[owner].Brush ?? ChartPalette.Series(owner);
            if (_activeIndex >= 0 && owner != _activeIndex)
            {
                brush = ChartPalette.Alpha(brush, 0.28);
            }

            context.DrawRectangle(brush, null, rect, 2, 2);
        }

        if (showLegend)
        {
            DrawLegend(context, segments, total);
        }
    }

    private void DrawLegend(DrawingContext context, IReadOnlyList<ChartValue> segments, double total)
    {
        var x = _grid.Right + 16;
        var available = Math.Max(20, Bounds.Width - x - 4);
        var rowHeight = Math.Min(26, (Bounds.Height - 20) / Math.Max(1, segments.Count));
        var y = (Bounds.Height - rowHeight * segments.Count) / 2;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var row = new Rect(x - 4, y, available + 4, rowHeight);
            if (i == _activeIndex)
            {
                context.DrawRectangle(ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.7), null, row, 5, 5);
            }

            var brush = segment.Brush ?? ChartPalette.Series(i);
            context.DrawRectangle(brush, null, new Rect(x, y + 6, 8, 8), 2, 2);
            ChartText.Draw(
                context,
                segment.Name,
                x + 14,
                y + 10,
                12,
                i == _activeIndex ? ChartPalette.Foreground : ChartPalette.Secondary,
                i == _activeIndex ? FontWeight.Medium : FontWeight.Normal,
                maxWidth: Math.Max(24, available - 36));
            ChartText.Draw(
                context,
                ChartFormat.Percent(segment.Value / total, 0),
                x + available,
                y + 10,
                12,
                ChartPalette.Muted,
                alignment: TextAlignment.Right);
            _legendHits.Add((row, i));
            y += rowHeight;
        }
    }

    private int[] BuildOwners(IReadOnlyList<ChartValue> segments, double total)
    {
        var cells = new int[100];
        var raw = segments.Select(item => item.Value / total * 100).ToArray();
        var counts = raw.Select(value => (int)Math.Floor(value)).ToArray();
        var remainder = 100 - counts.Sum();
        var order = Enumerable.Range(0, segments.Count)
            .OrderByDescending(index => raw[index] - counts[index])
            .ToArray();
        for (var i = 0; i < remainder; i++)
        {
            counts[order[i % order.Length]]++;
        }

        var cursor = 0;
        for (var group = 0; group < counts.Length; group++)
        {
            for (var i = 0; i < counts[group] && cursor < 100; i++)
            {
                cells[cursor++] = group;
            }
        }

        while (cursor < 100)
        {
            cells[cursor++] = counts.Length - 1;
        }

        return cells;
    }

    private List<ChartValue> Visible()
        => Segments?.Where(item => item.Value > 0).ToList() ?? new List<ChartValue>();

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
