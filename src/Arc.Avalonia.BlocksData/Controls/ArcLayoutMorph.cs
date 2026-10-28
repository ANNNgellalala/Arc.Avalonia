using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData;

/// <summary>
/// A 24 card gallery that reflows between grid, masonry, list, fan, carousel and helix. Layouts are
/// interpolated on a short timer; the shot harness runs with reduced motion, so it paints the final layout.
/// The fan and helix are flattened approximations: cards are offset and scaled but never rotated in 3D,
/// because this is a 2D drawing surface with no perspective camera.
/// </summary>
internal sealed class ArcLayoutMorph : DataStage
{
    private static readonly string[] Titles =
    {
        "Sessions", "Revenue", "Churn", "Signups", "Latency", "Storage",
        "Invoices", "Tickets", "Seats", "Uptime", "Errors", "Deploys",
        "Trials", "Expansion", "Refunds", "Usage", "Alerts", "Webhooks",
        "Exports", "Imports", "Cohorts", "Funnels", "Budgets", "Forecasts"
    };

    private readonly List<Rect> _from = new();
    private readonly List<Rect> _to = new();
    private readonly List<(Rect Rect, int Index)> _hits = new();
    private readonly DispatcherTimer _timer;
    private string _layout = "grid";
    private double _progress = 1;
    private int _hover = -1;
    private int _selected;
    private Size _computedSize;

    public ArcLayoutMorph()
    {
        Height = 316;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _progress = Math.Min(1, _progress + 0.06);
            if (_progress >= 1)
            {
                _timer.Stop();
            }

            InvalidateVisual();
        };
        for (var index = 0; index < Titles.Length; index++)
        {
            _from.Add(new Rect());
            _to.Add(new Rect());
        }
    }

    public string Layout
    {
        get => _layout;
        set
        {
            if (value == _layout)
            {
                return;
            }

            var previous = new List<Rect>(_to);
            _layout = value;
            Compute(_to);
            for (var index = 0; index < _from.Count; index++)
            {
                _from[index] = previous[index];
            }

            if (ArcThemeManager.ReducedMotion)
            {
                _progress = 1;
                _timer.Stop();
            }
            else
            {
                _progress = 0;
                _timer.Start();
            }

            InvalidateVisual();
        }
    }

    protected override double DefaultHeight => 330;

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (_computedSize.Width < 20)
        {
            Compute(_to);
            Compute(_from);
        }

        return size;
    }

    private void Compute(List<Rect> target)
    {
        var width = Math.Max(200, Bounds.Width > 20 ? Bounds.Width : 940);
        var height = Math.Max(200, Bounds.Height > 20 ? Bounds.Height : 300);
        var area = new Rect(8, 8, width - 16, height - 28);
        switch (_layout)
        {
            case "list":
                var rowHeight = Math.Min(30, area.Height / Titles.Length);
                for (var index = 0; index < Titles.Length; index++)
                {
                    target[index] = new Rect(area.Left, area.Top + index * rowHeight, area.Width, rowHeight - 3);
                }

                break;
            case "masonry":
                var columns = 5;
                var columnWidth = (area.Width - 8 * (columns - 1)) / columns;
                var heights = new[] { 66.0, 44.0, 56.0, 40.0 };
                var columnY = new double[columns];
                var offsets = new double[columns];
                for (var index = 0; index < Titles.Length; index++)
                {
                    var column = index % columns;
                    var tileHeight = heights[(index * 3 + column) % heights.Length];
                    var y = area.Top + columnY[column];
                    target[index] = new Rect(area.Left + column * (columnWidth + 8), y, columnWidth, tileHeight);
                    columnY[column] += tileHeight + 8;
                    offsets[column] = y;
                }

                break;
            case "fan":
                var centerX = area.Center.X;
                var centerY = area.Bottom - 40;
                for (var index = 0; index < Titles.Length; index++)
                {
                    var angle = -0.9 + 1.8 * (index / (double)(Titles.Length - 1));
                    var x = centerX + Math.Sin(angle) * area.Width * 0.38;
                    var y = centerY - Math.Cos(angle) * area.Height * 0.62;
                    const double cardWidth = 112;
                    const double cardHeight = 72;
                    target[index] = new Rect(x - cardWidth / 2, y - cardHeight / 2, cardWidth, cardHeight);
                }

                break;
            case "carousel":
                var carouselY = area.Center.Y - 80;
                for (var index = 0; index < Titles.Length; index++)
                {
                    var offset = index - (Titles.Length - 1) / 2.0;
                    var scale = Math.Max(0.45, 1 - Math.Abs(offset) * 0.12);
                    const double cardWidth = 168;
                    const double cardHeight = 180;
                    var x = area.Center.X + offset * 54 - cardWidth * scale / 2;
                    var y = carouselY + (1 - scale) * 40;
                    target[index] = new Rect(x, y, cardWidth * scale, cardHeight * scale);
                }

                break;
            case "helix":
                for (var index = 0; index < Titles.Length; index++)
                {
                    var t = index / (double)Titles.Length * Math.PI * 2;
                    var depth = (Math.Cos(t) + 1) / 2;
                    var scale = 0.5 + depth * 0.5;
                    const double cardWidth = 104;
                    const double cardHeight = 62;
                    var x = area.Center.X + Math.Sin(t) * area.Width * 0.36 - cardWidth * scale / 2;
                    var y = area.Top + 18 + index * (area.Height - 50) / Titles.Length + (1 - scale) * 10;
                    target[index] = new Rect(x, y, cardWidth * scale, cardHeight * scale);
                }

                break;
            default:
                var gridColumns = 6;
                var cellWidth = (area.Width - 8 * (gridColumns - 1)) / gridColumns;
                var cellHeight = (area.Height - 8 * 3) / 4;
                for (var index = 0; index < Titles.Length; index++)
                {
                    var column = index % gridColumns;
                    var row = index / gridColumns;
                    target[index] = new Rect(
                        area.Left + column * (cellWidth + 8),
                        area.Top + row * (cellHeight + 8),
                        cellWidth,
                        cellHeight);
                }

                break;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var hover = -1;
        for (var index = _hits.Count - 1; index >= 0; index--)
        {
            if (_hits[index].Rect.Contains(point))
            {
                hover = _hits[index].Index;
                break;
            }
        }

        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover >= 0 ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_hover >= 0)
        {
            _selected = _hover;
            Focus();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                _selected = (_selected + Titles.Length - 1) % Titles.Length;
                break;
            case Key.Right:
                _selected = (_selected + 1) % Titles.Length;
                break;
            case Key.Up:
                _selected = (_selected + Titles.Length - 6) % Titles.Length;
                break;
            case Key.Down:
                _selected = (_selected + 6) % Titles.Length;
                break;
            case Key.Home:
                _selected = 0;
                break;
            case Key.End:
                _selected = Titles.Length - 1;
                break;
            default:
                return;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_computedSize != Bounds.Size)
        {
            _computedSize = Bounds.Size;
            Compute(_to);
            Compute(_from);
            _progress = 1;
        }

        _hits.Clear();
        var ease = DataMath.Ease(_progress);
        for (var index = 0; index < Titles.Length; index++)
        {
            var from = _from[index];
            var to = _to[index];
            var rect = new Rect(
                from.X + (to.X - from.X) * ease,
                from.Y + (to.Y - from.Y) * ease,
                from.Width + (to.Width - from.Width) * ease,
                from.Height + (to.Height - from.Height) * ease);
            if (rect.Width < 4 || rect.Height < 4)
            {
                continue;
            }

            _hits.Add((rect, index));
            var selected = index == _selected;
            var hovered = index == _hover;
            var fill = selected ? Ink.Alpha(Ink.Accent, 0.12) : Ink.SurfaceMuted;
            var border = selected ? Ink.Pen(Ink.Accent, 1.4) : hovered ? Ink.Pen(Ink.BorderStrong, 1) : Ink.Pen(Ink.BorderSubtle, 1);
            context.DrawRectangle(fill, border, rect, 10, 10);

            if (rect.Width < 60 || rect.Height < 34)
            {
                continue;
            }

            var bar = new Rect(rect.Left + 10, rect.Top + 10, Math.Min(28, rect.Width - 20), 3);
            context.DrawRectangle(Ink.Series(index % 4), null, bar, 2, 2);
            var size = rect.Height < 52 ? 10 : 12;
            Text(context, Titles[index], rect.Left + 10, rect.Top + (rect.Height < 52 ? rect.Height / 2 : 32), size,
                Ink.Foreground, selected ? FontWeight.Medium : FontWeight.Normal, maxWidth: rect.Width - 20);
            if (rect.Height >= 60)
            {
                Text(context, "24 cards · " + _layout, rect.Left + 10, rect.Bottom - 14, 9, Ink.Muted,
                    maxWidth: rect.Width - 20);
            }
        }
    }
}
