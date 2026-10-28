using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData;

/// <summary>One state or region placed on a tile cartogram grid.</summary>
internal sealed class MapRegion
{
    public MapRegion(string code, string name, int column, int row)
    {
        Code = code;
        Name = name;
        Column = column;
        Row = row;
    }

    public string Code { get; }

    public string Name { get; }

    public int Column { get; }

    public int Row { get; }
}

/// <summary>
/// A tile cartogram of a country: every state or region is an equal-area tile placed near its real position,
/// shaded on a four-step scale, with a camera zoom, hover readout and selection. This is deliberately not a
/// geographic polygon map: equal-area tiles keep small states readable and drop the projection entirely.
/// </summary>
internal sealed class ArcChoroplethMap : DataStage
{
    private readonly IReadOnlyList<MapRegion> _regions;
    private readonly Dictionary<string, double> _values;
    private readonly string _metric;
    private readonly Func<double, string> _format;
    private readonly Dictionary<(int Column, int Row), Rect> _tiles = new();
    private readonly List<(string Code, Rect Rect)> _hits = new();
    private string? _hover;

    public ArcChoroplethMap(IReadOnlyList<MapRegion> regions, IReadOnlyDictionary<string, double> values,
        string metric, Func<double, string> format, double zoom = 1)
    {
        _regions = regions;
        _values = new Dictionary<string, double>(values);
        _metric = metric;
        _format = format;
        Zoom = zoom;
        Height = 300;
    }

    public double Zoom { get; set; }

    public string? Selected { get; private set; }

    public event EventHandler<string?>? SelectionChanged;

    protected override double DefaultHeight => 300;

    private void Layout()
    {
        _tiles.Clear();
        var columns = _regions.Max(region => region.Column) + 1;
        var rows = _regions.Max(region => region.Row) + 1;
        var area = new Rect(18, 14, Math.Max(10, Bounds.Width - 36), Math.Max(10, Bounds.Height - 44));
        var cell = Math.Min(area.Width / columns, area.Height / rows) * Zoom;
        var width = cell * columns;
        var height = cell * rows;
        var left = area.Left + (area.Width - width) / 2;
        var top = area.Top + (area.Height - height) / 2;
        foreach (var region in _regions)
        {
            _tiles[(region.Column, region.Row)] = new Rect(
                left + region.Column * cell + 1.5,
                top + region.Row * cell + 1.5,
                Math.Max(2, cell - 3),
                Math.Max(2, cell - 3));
        }
    }

    private IReadOnlyList<double> SortedValues => _values.Values.OrderBy(value => value).ToList();

    private int Step(double value)
    {
        var values = SortedValues;
        if (values.Count == 0)
        {
            return 0;
        }

        // Rank based, so a country with a narrow value range still uses all four steps.
        var rank = values.Count(candidate => candidate < value);
        var step = (int)Math.Floor(rank / (double)Math.Max(1, values.Count) * 4);
        return Math.Clamp(step, 0, 3);
    }

    private IBrush Fill(int step)
    {
        var alpha = step switch
        {
            0 => 0.16,
            1 => 0.34,
            2 => 0.56,
            _ => 0.82
        };
        return Ink.Alpha(Ink.Series(0), alpha);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Layout();
        var point = e.GetPosition(this);
        var hover = _hits.FirstOrDefault(hit => hit.Rect.Contains(point)).Code;
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var hit = _hits.FirstOrDefault(item => item.Rect.Contains(point)).Code;
        if (hit is not null)
        {
            Selected = hit == Selected ? null : hit;
            SelectionChanged?.Invoke(this, Selected);
            Focus();
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ordered = _regions.OrderByDescending(region => _values.TryGetValue(region.Code, out var value) ? value : 0)
            .ToList();
        var index = ordered.FindIndex(region => region.Code == (Selected ?? _hover));
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                index = index <= 0 ? ordered.Count - 1 : index - 1;
                break;
            case Key.Right:
            case Key.Down:
                index = index + 1 >= ordered.Count ? 0 : index + 1;
                break;
            case Key.Add:
            case Key.OemPlus:
                Zoom = Math.Min(2.2, Zoom + 0.15);
                InvalidateVisual();
                e.Handled = true;
                return;
            case Key.Subtract:
            case Key.OemMinus:
                Zoom = Math.Max(0.7, Zoom - 0.15);
                InvalidateVisual();
                e.Handled = true;
                return;
            case Key.Escape:
                Selected = null;
                SelectionChanged?.Invoke(this, null);
                InvalidateVisual();
                e.Handled = true;
                return;
            default:
                return;
        }

        Selected = ordered[index].Code;
        SelectionChanged?.Invoke(this, Selected);
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Layout();
        _hits.Clear();
        if (_tiles.Count == 0)
        {
            return;
        }

        foreach (var region in _regions)
        {
            if (!_tiles.TryGetValue((region.Column, region.Row), out var rect))
            {
                continue;
            }

            _hits.Add((region.Code, rect));
            var value = _values.TryGetValue(region.Code, out var amount) ? amount : 0;
            var selected = Selected == region.Code;
            var hovered = _hover == region.Code;
            var border = selected ? Ink.Pen(Ink.Foreground, 1.6) : hovered ? Ink.Pen(Ink.BorderStrong, 1.2) : null;
            context.DrawRectangle(Fill(Step(value)), border, rect, 5, 5);
            if (rect.Width > 26 && rect.Height > 18)
            {
                Text(context, region.Code, rect.Center.X, rect.Center.Y - (rect.Height > 34 ? 5 : 0), 10,
                    Step(value) >= 2 ? Ink.OnFill(Fill(Step(value))) : Ink.Secondary, FontWeight.Medium,
                    TextAlignment.Center);
                if (rect.Height > 34)
                {
                    Text(context, _format(value), rect.Center.X, rect.Center.Y + 9, 9,
                        Step(value) >= 2 ? Ink.OnFill(Fill(Step(value))) : Ink.Muted, alignment: TextAlignment.Center);
                }
            }
        }

        Text(context, "Equal-area tiles · not a geographic projection", 4, Bounds.Height - 10, 9, Ink.Muted);

        if (_hover is { Length: > 0 })
        {
            var region = _regions.First(item => item.Code == _hover);
            if (_tiles.TryGetValue((region.Column, region.Row), out var rect))
            {
                Tooltip(context, Bounds.Size, rect.Center, region.Name, new[]
                {
                    (_metric, _format(_values.TryGetValue(region.Code, out var value) ? value : 0), (IBrush?)null)
                });
            }
        }
    }
}
