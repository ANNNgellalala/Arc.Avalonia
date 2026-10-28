using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's sticker peel: a surface of die-cut stickers. Hovering lifts the nearest edge, dragging folds the
/// sticker back along a real fold with its paper backing showing, and pulling past the middle peels it off to
/// carry around and stick down somewhere new. Keyboard users pick a sticker up, move it and stick it back.
/// </summary>
public sealed class ArcStickerPeel : ArcSpecialCanvas
{
    private sealed record StickerSpec(string Label, string Shape, double X, double Y, double Rotate, double Width, double Height, string ColorToken, string Content);

    private static readonly StickerSpec[] Specs =
    {
        new("Arc sticker", "circle", 0.16, 0.34, -8, 96, 96, ArcTokens.Accent, "A"),
        new("Ship it sticker", "pill", 0.47, 0.20, 5, 156, 62, ArcTokens.Series1, "SHIP IT"),
        new("Hello sticker", "rounded", 0.69, 0.58, -6, 112, 112, ArcTokens.Series2, "hello"),
        new("Star sticker", "circle", 0.36, 0.70, 9, 88, 88, ArcTokens.Series3, "star"),
        new("Arc club sticker", "pill", 0.62, 0.86, -4, 148, 56, ArcTokens.Series4, "ARC CLUB")
    };

    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _rotate;
    private readonly Spring[] _peel;
    private readonly double[] _peelTarget;
    private readonly Point[] _pull;
    private int _selected = 2;
    private int _hover = -1;
    private bool _carried;
    private bool _dragging;
    private bool _pressing;
    private Point _pressPoint;
    private Point _carryOffset;
    private bool _placed;
    private Rect _surface;

    public ArcStickerPeel()
    {
        _x = Specs.Select(spec => spec.X).ToArray();
        _y = Specs.Select(spec => spec.Y).ToArray();
        _rotate = Specs.Select(spec => spec.Rotate).ToArray();
        _peel = Specs.Select(_ => Spring.At(0, 190, 26)).ToArray();
        _peelTarget = new double[Specs.Length];
        _pull = Specs.Select((_, index) => SpecialUi.Normalize(new Point(0.8, index % 2 == 0 ? -0.6 : -0.9))).ToArray();

        // The demo seeds one sticker half peeled so the fold is legible in a still frame.
        _peel[2] = Spring.At(0.16, 190, 26);
        _peelTarget[2] = 0.16;

        SpecialUi.Name(
            this,
            "Sticker peel",
            "A surface of stickers. Arrow keys move between stickers, Enter picks one up or sticks it down, arrow keys move a carried sticker, and Escape puts it back.");
    }

    protected override double DefaultWidth => 900;

    protected override double DefaultHeight => 438;

    private void Layout(Size size)
    {
        _surface = new Rect(8, 8, size.Width - 16, size.Height - 16);
        if (_placed)
        {
            return;
        }

        _placed = true;
    }

    private Point Center(int index) => new(
        _surface.X + _surface.Width * _x[index],
        _surface.Y + _surface.Height * _y[index]);

    private (double Width, double Height, double Radius) SizeOf(int index)
    {
        var spec = Specs[index];
        var radius = spec.Shape switch
        {
            "circle" => spec.Width / 2,
            "pill" => spec.Height / 2,
            _ => 24
        };
        return (spec.Width, spec.Height, radius);
    }

    private List<Point> Polygon(int index, Point center, double grow = 0)
    {
        var (width, height, radius) = SizeOf(index);
        var rect = new Rect(center.X - width / 2 - grow, center.Y - height / 2 - grow, width + grow * 2, height + grow * 2);
        var points = SpecialUi.RoundedPolygon(rect, radius + grow, index % 5 == 0 ? 8 : 4);
        var angle = _rotate[index] * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        for (var i = 0; i < points.Count; i++)
        {
            var dx = points[i].X - center.X;
            var dy = points[i].Y - center.Y;
            points[i] = new Point(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
        }

        return points;
    }

    private bool HitTest(int index, Point point)
    {
        var center = Center(index);
        var (width, height, _) = SizeOf(index);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var angle = -_rotate[index] * Math.PI / 180;
        var lx = dx * Math.Cos(angle) - dy * Math.Sin(angle);
        var ly = dx * Math.Sin(angle) + dy * Math.Cos(angle);
        return Math.Abs(lx) <= width / 2 + 8 && Math.Abs(ly) <= height / 2 + 8;
    }

    private int Pick(Point point)
    {
        for (var index = Specs.Length - 1; index >= 0; index--)
        {
            if (HitTest(index, point))
            {
                return index;
            }
        }

        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        Layout(Bounds.Size);

        if (_dragging && _pressing)
        {
            var center = Center(_selected);
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 4)
            {
                _pull[_selected] = SpecialUi.Normalize(new Point(dx, dy));
                if (!_carried)
                {
                    var (width, height, _) = SizeOf(_selected);
                    var extent = Math.Max(width, height);
                    _peelTarget[_selected] = Math.Clamp(distance / (extent * 1.15), 0.06, 1);
                    if (_peelTarget[_selected] > 0.74)
                    {
                        _carried = true;
                        _carryOffset = new Point(center.X - point.X, center.Y - point.Y);
                        _peelTarget[_selected] = 0;
                    }
                }
            }

            if (_carried)
            {
                _x[_selected] = Math.Clamp((point.X + _carryOffset.X - _surface.X) / _surface.Width, 0.06, 0.94);
                _y[_selected] = Math.Clamp((point.Y + _carryOffset.Y - _surface.Y) / _surface.Height, 0.08, 0.92);
            }

            EnsureFrame();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_pressing)
        {
            return;
        }

        var hover = Pick(point);
        if (hover == _hover)
        {
            return;
        }

        if (_hover >= 0)
        {
            _peelTarget[_hover] = 0;
            EnsureFrame();
        }

        _hover = hover;
        if (_hover >= 0)
        {
            _selected = _hover;
            var center = Center(_hover);
            _pull[_hover] = SpecialUi.Normalize(new Point(point.X - center.X, point.Y - center.Y));
            if (!SpecialUi.Reduced && !_carried)
            {
                _peelTarget[_hover] = 0.12;
                EnsureFrame();
            }
        }

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        Layout(Bounds.Size);
        var point = e.GetPosition(this);
        var hit = Pick(point);
        if (hit < 0)
        {
            e.Handled = true;
            return;
        }

        _selected = hit;
        _hover = hit;
        _pressPoint = point;
        _pressing = true;
        _dragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_pressing)
        {
            return;
        }

        _pressing = false;
        if (_carried)
        {
            _carried = false;
            _rotate[_selected] += 2;
            _peelTarget[_selected] = 0;
        }
        else
        {
            _peelTarget[_selected] = 0;
        }

        EnsureFrame();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_carried)
        {
            var stepX = (shift ? 40 : 10) / Math.Max(1, _surface.Width);
            var stepY = (shift ? 40 : 10) / Math.Max(1, _surface.Height);
            switch (e.Key)
            {
                case Key.Left:
                    _x[_selected] = Math.Clamp(_x[_selected] - stepX, 0.06, 0.94);
                    break;
                case Key.Right:
                    _x[_selected] = Math.Clamp(_x[_selected] + stepX, 0.06, 0.94);
                    break;
                case Key.Up:
                    _y[_selected] = Math.Clamp(_y[_selected] - stepY, 0.08, 0.92);
                    break;
                case Key.Down:
                    _y[_selected] = Math.Clamp(_y[_selected] + stepY, 0.08, 0.92);
                    break;
                case Key.Enter:
                case Key.Space:
                    _carried = false;
                    _rotate[_selected] += 2;
                    break;
                case Key.Escape:
                    _carried = false;
                    Restore(_selected);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            InvalidateVisual();
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _selected = (_selected - 1 + Specs.Length) % Specs.Length;
                _hover = _selected;
                e.Handled = true;
                break;
            case Key.Right:
                _selected = (_selected + 1) % Specs.Length;
                _hover = _selected;
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                _carried = true;
                _peelTarget[_selected] = 0;
                _peel[_selected].Jump(0);
                e.Handled = true;
                break;
            case Key.Escape:
                Restore(_selected);
                e.Handled = true;
                break;
        }

        EnsureFrame();
        InvalidateVisual();
    }

    private void Restore(int index)
    {
        _x[index] = Specs[index].X;
        _y[index] = Specs[index].Y;
        _rotate[index] = Specs[index].Rotate;
        _peelTarget[index] = 0;
        _peel[index].Jump(0);
        InvalidateVisual();
    }
    protected override void OnFrame(double dt)
    {
        for (var index = 0; index < Specs.Length; index++)
        {
            if (!_dragging || index != _selected || !_carried)
            {
                _peel[index].Step(_peelTarget[index], dt);
            }
        }
    }

    protected override bool NeedsFrame
    {
        get
        {
            for (var index = 0; index < Specs.Length; index++)
            {
                if (Math.Abs(_peel[index].Value - _peelTarget[index]) > 0.002)
                {
                    return true;
                }
            }

            return false;
        }
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        Layout(size);
        var surface = SpecialUi.Brush(ArcTokens.SurfaceMuted);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);

        SpecialUi.RoundRect(context, _surface, 30, surface);
        using (context.PushClip(new RoundedRect(_surface, 30)))
        {
            for (var y = 14.0; y < size.Height; y += 18)
            {
                for (var x = 14.0; x < size.Width; x += 18)
                {
                    context.DrawEllipse(SpecialUi.Alpha(ArcTokens.BorderStrong, 0.35), null, new Point(x, y), 0.8, 0.8);
                }
            }

            context.DrawRectangle(
                SpecialUi.Fade(ArcColor.WithAlpha(foreground, 0.05)),
                null,
                _surface);

            var order = Enumerable.Range(0, Specs.Length)
                .OrderBy(index => index == _selected ? 1 : 0)
                .ToList();
            foreach (var index in order)
            {
                DrawSticker(context, index, foreground);
            }
        }

        context.DrawRectangle(null, new Pen(border, 1), _surface.Deflate(0.5), 30, 30);

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, BoundsOf(_selected, 15), 18);
        }
    }

    private Rect BoundsOf(int index, double inflate)
    {
        var center = Center(index);
        var (width, height, _) = SizeOf(index);
        return new Rect(center.X - width / 2 - inflate, center.Y - height / 2 - inflate, width + inflate * 2, height + inflate * 2);
    }

    private void DrawSticker(DrawingContext context, int index, Color foreground)
    {
        var spec = Specs[index];
        var center = Center(index);
        var color = SpecialUi.Color(spec.ColorToken);
        var (width, height, _) = SizeOf(index);
        var extent = Math.Max(width, height) / 2.0;
        var peel = _carried ? 0 : Math.Clamp(_peel[index].Value, 0, 1);

        if (_carried && index == _selected)
        {
            SpecialUi.Lift(context, BoundsOf(index, 6), 22, 1.6);
            DrawFace(context, index, center, color, 1.0);
            return;
        }

        if (_carried)
        {
            context.DrawEllipse(SpecialUi.ShadowBrush(0.10), null, new Point(center.X, center.Y + height * 0.55), width * 0.5, 6);
            DrawFace(context, index, center, color, 1.0);
            return;
        }

        if (peel <= 0.02)
        {
            // Stuck down: a soft contact shadow and a crisp die cut.
            context.DrawEllipse(SpecialUi.ShadowBrush(0.10), null, new Point(center.X, center.Y + height * 0.52), width * 0.46, 5);
            DrawFace(context, index, center, color, 1.0);
            return;
        }

        var normal = _pull[index];
        var reach = extent * (Math.Abs(normal.X) * width + Math.Abs(normal.Y) * height) / Math.Max(1, Math.Max(width, height));
        var offset = SpecialUi.Dot(center, normal) + reach * (1 - 2 * peel * 0.98);
        var outer = Polygon(index, center, 7);
        var remaining = SpecialUi.ClipHalfPlane(outer, normal, offset);
        var folded = SpecialUi.ClipHalfPlane(outer, new Point(-normal.X, -normal.Y), -offset);
        if (remaining.Count < 3 || folded.Count < 3)
        {
            DrawFace(context, index, center, color, 1.0);
            return;
        }

        var flap = folded.Select(point => SpecialUi.Reflect(point, normal, offset)).ToList();
        var shadow = flap.Select(point => new Point(point.X + 3, point.Y + 5)).ToList();
        context.DrawGeometry(SpecialUi.ShadowBrush(0.20), null, SpecialUi.Polygon(shadow));

        using (context.PushGeometryClip(SpecialUi.Polygon(remaining)))
        {
            DrawFace(context, index, center, color, 1.0);
        }

        var back = ArcColor.Mix(SpecialUi.Color(ArcTokens.Surface), SpecialUi.Color(ArcTokens.Background), 0.16);
        context.DrawGeometry(SpecialUi.Gradient(ArcColor.Mix(back, SpecialUi.Color(ArcTokens.Foreground), 0.05), back, false), null, SpecialUi.Polygon(flap));
        context.DrawGeometry(
            null,
            new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.22)), 1),
            SpecialUi.Polygon(flap));

        var crease = CreasePoints(BoundsOf(index, 9), normal, offset);
        if (crease is not null)
        {
            context.DrawLine(
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(SpecialUi.Color(ArcTokens.Background), 0.85)), 2, null, PenLineCap.Round, PenLineJoin.Round),
                crease.Value.A,
                crease.Value.B);
            context.DrawLine(
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.16)), 1),
                crease.Value.A,
                crease.Value.B);
        }
    }

    private void DrawFace(DrawingContext context, int index, Point center, Color color, double opacity)
    {
        var spec = Specs[index];
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            var outer = Polygon(index, center, 7);
            context.DrawGeometry(SpecialUi.Brush(ArcTokens.Surface), null, SpecialUi.Polygon(outer));
            var face = Polygon(index, center, 0);
            var light = ArcColor.Mix(color, SpecialUi.Color(ArcTokens.Background), 0.22);
            context.DrawGeometry(SpecialUi.Gradient(light, color), null, SpecialUi.Polygon(face));
            context.DrawGeometry(null, new Pen(SpecialUi.Solid(ArcColor.WithAlpha(color, 0.7)), 1), SpecialUi.Polygon(face));

            var (lightness, _, _) = ArcColor.ToOklch(color);
            var ink = ContrastInk(color, lightness);
            var angle = _rotate[index] * Math.PI / 180;
            using (context.PushTransform(
                       Matrix.CreateTranslation(-center.X, -center.Y) *
                       Matrix.CreateRotation(angle) *
                       Matrix.CreateTranslation(center.X, center.Y)))
            {
                if (spec.Content == "star")
                {
                    DrawStar(context, center, Math.Min(spec.Width, spec.Height) * 0.26, SpecialUi.Solid(ink));
                }
                else
                {
                    var size = spec.Content.Length > 5 ? 15.0 : spec.Content.Length > 2 ? 21.0 : 26.0;
                    var text = SpecialUi.Text(spec.Content, size, SpecialUi.Solid(ink), FontWeight.Medium);
                    SpecialUi.DrawTextCentered(context, text, new Rect(center.X - spec.Width / 2, center.Y - spec.Height / 2, spec.Width, spec.Height));
                }
            }
        }
    }

    private static Color ContrastInk(Color color, double lightness)
    {
        var background = SpecialUi.Color(ArcTokens.Background);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var againstBackground = Math.Abs(lightness - ArcColor.ToOklch(background).Lightness);
        var againstForeground = Math.Abs(lightness - ArcColor.ToOklch(foreground).Lightness);
        return againstBackground >= againstForeground ? background : foreground;
    }

    private static void DrawStar(DrawingContext context, Point center, double radius, IBrush brush)
    {
        var points = new List<Point>();
        for (var index = 0; index < 10; index++)
        {
            var angle = -Math.PI / 2 + index * Math.PI / 5;
            var length = index % 2 == 0 ? radius : radius * 0.45;
            points.Add(new Point(center.X + Math.Cos(angle) * length, center.Y + Math.Sin(angle) * length));
        }

        context.DrawGeometry(brush, null, SpecialUi.Polygon(points));
    }

    private static (Point A, Point B)? CreasePoints(Rect rect, Point normal, double offset)
    {
        var points = new List<Point>();
        if (Math.Abs(normal.Y) > 0.0001)
        {
            foreach (var x in new[] { rect.X, rect.Right })
            {
                var y = (offset - x * normal.X) / normal.Y;
                if (y >= rect.Y - 0.5 && y <= rect.Bottom + 0.5)
                {
                    points.Add(new Point(x, Math.Clamp(y, rect.Y, rect.Bottom)));
                }
            }
        }

        if (Math.Abs(normal.X) > 0.0001)
        {
            foreach (var y in new[] { rect.Y, rect.Bottom })
            {
                var x = (offset - y * normal.Y) / normal.X;
                if (x >= rect.X - 0.5 && x <= rect.Right + 0.5)
                {
                    points.Add(new Point(Math.Clamp(x, rect.X, rect.Right), y));
                }
            }
        }

        return points.Count < 2 ? null : (points[0], points[^1]);
    }
}
