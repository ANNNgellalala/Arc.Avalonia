using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A squarified treemap for flat parts of a whole. Tiles are tinted series colours, labels appear
/// only where they fit, and hovering a tile quiets the others and reveals its exact share.
/// </summary>
public class ArcTreemap : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ChartValue>?> ItemsProperty =
        AvaloniaProperty.Register<ArcTreemap, IReadOnlyList<ChartValue>?>(nameof(Items));

    public static readonly StyledProperty<bool> ShowValuesProperty =
        AvaloniaProperty.Register<ArcTreemap, bool>(nameof(ShowValues), true);

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcTreemap, Func<double, string>?>(nameof(ValueFormat));

    private readonly List<(ChartValue Item, Rect Bounds)> _tiles = new();
    private int _activeIndex = -1;

    static ArcTreemap()
    {
        AffectsRender<ArcTreemap>(ItemsProperty, ShowValuesProperty, ValueFormatProperty);
        ItemsProperty.Changed.AddClassHandler<ArcTreemap>((chart, _) => chart._activeIndex = -1);
    }

    public ArcTreemap()
    {
        Focusable = true;
    }

    public IReadOnlyList<ChartValue>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Shows the formatted size under each tile's name.</summary>
    public bool ShowValues
    {
        get => GetValue(ShowValuesProperty);
        set => SetValue(ShowValuesProperty, value);
    }

    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 220;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        for (var i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].Bounds.Contains(point))
            {
                SetActive(ItemIndex(_tiles[i].Item));
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
        var count = _tiles.Count;
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
        _tiles.Clear();
        var items = Items;
        if (items is null || items.Count == 0 || items.Sum(item => Math.Max(0, item.Value)) <= 0)
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

        var bounds = new Rect(1, 1, Math.Max(1, Bounds.Width - 2), Math.Max(1, Bounds.Height - 2));
        var layout = TreemapLayout.Compute(items, bounds);
        var total = items.Sum(item => Math.Max(0, item.Value));
        foreach (var (item, tile) in layout)
        {
            var inset = tile.Deflate(1.5);
            if (inset.Width < 1 || inset.Height < 1)
            {
                continue;
            }

            var index = ItemIndex(item);
            var brush = item.Brush ?? ChartPalette.Series(index);
            var active = index == _activeIndex;
            var alpha = active ? 0.38 : _activeIndex >= 0 ? 0.09 : 0.24;
            context.DrawRectangle(
                ChartPalette.Alpha(brush, alpha),
                new Pen(ChartPalette.Alpha(brush, active ? 0.9 : 0.5), active ? 1.5 : 1),
                inset,
                5,
                5);

            if (inset.Width < 34 || inset.Height < 22)
            {
                _tiles.Add((item, inset));
                continue;
            }

            var pad = Math.Min(8, inset.Width * 0.08);
            var textWidth = Math.Max(12, inset.Width - pad * 2);
            ChartText.Draw(
                context,
                item.Name,
                inset.Left + pad,
                inset.Top + pad + 6,
                12,
                ChartPalette.Foreground,
                FontWeight.Medium,
                maxWidth: textWidth);
            if (ShowValues && inset.Height >= 40)
            {
                ChartText.Draw(
                    context,
                    FormatValue(item.Value),
                    inset.Left + pad,
                    inset.Top + pad + 23,
                    12,
                    ChartPalette.Secondary,
                    maxWidth: textWidth);
            }

            _tiles.Add((item, inset));
        }

        if (_activeIndex >= 0 && _activeIndex < items.Count)
        {
            var item = items[_activeIndex];
            var match = _tiles.FirstOrDefault(tile => ReferenceEquals(tile.Item, item));
            if (match.Bounds.Width > 0)
            {
                ChartTooltip.Draw(
                    context,
                    Bounds,
                    match.Bounds.Center,
                    item.Name,
                    new[]
                    {
                        ("Value", FormatValue(item.Value), (IBrush?)(item.Brush ?? ChartPalette.Series(_activeIndex))),
                        ("Share", ChartFormat.Percent(item.Value / total, 1), null)
                    });
            }
        }
    }

    private string FormatValue(double value)
        => ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 0);

    private int ItemIndex(ChartValue item)
    {
        var source = Items;
        if (source is not null)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (ReferenceEquals(source[i], item) || string.Equals(source[i].Name, item.Name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return 0;
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
