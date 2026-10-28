using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.BlocksData;

internal sealed class ExplodedLayer
{
    public ExplodedLayer(string name, string note, int seriesIndex)
    {
        Name = name;
        Note = note;
        SeriesIndex = seriesIndex;
    }

    public string Name { get; }

    public string Note { get; }

    public int SeriesIndex { get; }

    public bool Visible { get; set; } = true;
}

/// <summary>
/// The exploded-view stage: a live phone screen built from five layers that separate in depth as the slider
/// moves, each with a label and a leader line that follows it. Click or arrow keys select a layer; hidden
/// layers keep their slot so the labels never jump. This is a 2D skew, not CSS 3D: layers are parallelograms
/// offset along a diagonal with no perspective divide.
/// </summary>
internal sealed class ArcExplodedLayers : DataStage
{
    private readonly IReadOnlyList<ExplodedLayer> _layers;
    private int _selected = 2;
    private double _separation;

    public ArcExplodedLayers(IReadOnlyList<ExplodedLayer> layers, double separation = 0.6)
    {
        _layers = layers;
        _separation = Math.Clamp(separation, 0, 1);
        Height = 320;
    }

    public double Separation
    {
        get => _separation;
        set
        {
            var next = Math.Clamp(value, 0, 1);
            if (Math.Abs(next - _separation) < 0.001)
            {
                return;
            }

            _separation = next;
            InvalidateVisual();
        }
    }

    public int Selected
    {
        get => _selected;
        set
        {
            var next = Math.Clamp(value, 0, _layers.Count - 1);
            if (next == _selected)
            {
                return;
            }

            _selected = next;
            SelectionChanged?.Invoke(this, next);
            InvalidateVisual();
        }
    }

    public event EventHandler<int>? SelectionChanged;

    protected override double DefaultHeight => 320;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var rects = LayerRects();
        for (var index = rects.Count - 1; index >= 0; index--)
        {
            if (rects[index].Contains(point))
            {
                Selected = index;
                Focus();
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Up:
            case Key.Left:
                Selected -= 1;
                break;
            case Key.Down:
            case Key.Right:
                Selected += 1;
                break;
            case Key.Home:
                Selected = 0;
                break;
            case Key.End:
                Selected = _layers.Count - 1;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private List<Rect> LayerRects()
    {
        var result = new List<Rect>();
        var baseLeft = Bounds.Width * 0.16;
        var baseTop = 28.0;
        var width = Math.Min(176, Bounds.Width * 0.26);
        var height = Math.Min(Bounds.Height - 52, 232);
        for (var index = 0; index < _layers.Count; index++)
        {
            var depth = index - (_layers.Count - 1) / 2.0;
            var offsetX = depth * 16 * _separation;
            var offsetY = depth * 26 * _separation;
            result.Add(new Rect(baseLeft + offsetX, baseTop + offsetY, width, height));
        }

        return result;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var rects = LayerRects();
        var labelX = Math.Min(Bounds.Width - 150, Bounds.Width * 0.56);
        var stackTop = rects[0].Top;

        for (var index = 0; index < _layers.Count; index++)
        {
            var layer = _layers[index];
            var rect = rects[index];
            if (!layer.Visible)
            {
                continue;
            }

            var skew = rect.Height * 0.06 * _separation;
            var points = new[]
            {
                new Point(rect.Left + skew, rect.Top),
                new Point(rect.Right - skew, rect.Top),
                new Point(rect.Right + skew, rect.Bottom),
                new Point(rect.Left - skew, rect.Bottom)
            };
            var geometry = new StreamGeometry();
            using (var shape = geometry.Open())
            {
                shape.BeginFigure(points[0], true);
                for (var i = 1; i < points.Length; i++)
                {
                    shape.LineTo(points[i]);
                }

                shape.EndFigure(true);
            }

            var selected = index == _selected;
            var fill = Ink.Alpha(Ink.Series(layer.SeriesIndex), selected ? 0.28 : 0.16);
            context.DrawGeometry(fill, Ink.Pen(selected ? Ink.Accent : Ink.Alpha(Ink.Series(layer.SeriesIndex), 0.55),
                selected ? 1.6 : 1), geometry);

            // A faint content band so a layer reads as a screen rather than a card.
            var inner = new Rect(rect.Left + 14, rect.Top + 16, rect.Width - 28, rect.Height * 0.46);
            context.DrawRectangle(Ink.Alpha(Ink.SurfaceRaised, selected ? 0.55 : 0.30), null, inner, 8, 8);
            for (var line = 0; line < 3; line++)
            {
                context.DrawRectangle(Ink.Alpha(Ink.Secondary, 0.22), null,
                    new Rect(inner.Left + 10, inner.Top + 12 + line * 12, Math.Max(10, inner.Width - 24 - line * 18), 4),
                    2, 2);
            }

            var labelY = stackTop + index * 34;
            var from = new Point(rect.Right + 6, rect.Top + rect.Height * 0.3);
            var to = new Point(labelX - 10, labelY);
            context.DrawLine(Ink.Pen(Ink.Alpha(Ink.BorderStrong, 0.55), 1, true), from, to);
            context.DrawEllipse(Ink.Series(layer.SeriesIndex), null, from, 2.6, 2.6);
            Text(context, layer.Name, labelX, labelY - 6, 11, selected ? Ink.Foreground : Ink.Secondary,
                selected ? FontWeight.Medium : FontWeight.Normal);
            Text(context, layer.Note, labelX, labelY + 8, 9, Ink.Muted, maxWidth: Bounds.Width - labelX - 8);
        }

        Text(context, $"{_layers.Count(layer => layer.Visible)} of {_layers.Count} layers shown · {_separation * 100:0}% apart",
            8, Bounds.Height - 10, 9, Ink.Muted);
    }
}
