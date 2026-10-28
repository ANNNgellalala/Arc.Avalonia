using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// Arc's lightbox gallery: a masonry grid of photos that open in a viewer. The tapped photo flies out of its
/// slot into the stage, the viewer swaps photos with arrows or a swipe, drags down to dismiss with
/// resistance, and flies back to the slot of whichever photo is showing when it closes.
/// </summary>
public sealed class ArcLightboxGallery : ArcSpecialCanvas
{
    private sealed record Shot(int Seed, string Title, string Caption, double Ratio);

    private static readonly Shot[] Shots =
    {
        new(1, "Harbor", "Boats at dawn, May 2026", 1.30),
        new(2, "Old town", "Lanterns in the alley", 0.70),
        new(4, "Cliffs", "Wind off the headland", 1.02),
        new(0, "Dunes", "Late light on the ridge", 1.24),
        new(6, "Studio", "Drape study", 0.68),
        new(3, "Bloom", "Garden macro", 1.46),
        new(7, "Night field", "Stars over the valley", 0.82),
        new(5, "Salt", "Badwater basin", 1.12),
        new(9, "Paper fold", "Editorial still", 1.24),
        new(8, "Ferry", "Last crossing", 0.74),
        new(2, "Courtyard", "Tile and shadow", 0.98),
        new(6, "Orchard", "Rows in the fog", 0.72)
    };

    private readonly Rect[] _slots = new Rect[Shots.Length];
    private readonly Rect[] _columnSlots = new Rect[Shots.Length];
    private Size _layoutSize;
    private Spring _viewer = Spring.At(0, 200, 28);
    private bool _open;
    private int _focus;
    private int _index;
    private Rect _origin;
    private Rect _stage;
    private Rect _close;
    private double _dragY;
    private double _zoom = 1;
    private double _dragX;
    private bool _pointerDown;
    private bool _dragging;
    private Point _pressPoint;
    private double _velocity;

    public ArcLightboxGallery()
    {
        SpecialUi.Name(
            this,
            "Lightbox gallery",
            "A masonry photo grid. Enter opens the focused photo; arrow keys move through the viewer, plus and minus zoom, Escape closes.");
    }

    protected override double DefaultWidth => 980;

    protected override double DefaultHeight => 460;

    private void BuildLayout(Size size)
    {
        if (_layoutSize == size)
        {
            return;
        }

        _layoutSize = size;
        const double gap = 10;
        var columns = Math.Max(2, (int)Math.Floor((size.Width + gap) / (150 + gap)));
        var width = (size.Width - (columns - 1) * gap) / columns;
        var heights = new double[columns];
        for (var index = 0; index < Shots.Length; index++)
        {
            var best = 0;
            for (var column = 1; column < columns; column++)
            {
                if (heights[column] < heights[best] - 0.5)
                {
                    best = column;
                }
            }

            var height = width * Shots[index].Ratio;
            var rect = new Rect(best * (width + gap), heights[best], width, height);
            _columnSlots[index] = rect;
            heights[best] += height + gap;
        }

        var tallest = 0.0;
        for (var column = 0; column < columns; column++)
        {
            tallest = Math.Max(tallest, heights[column] - gap);
        }

        var offset = Math.Max(0, (size.Height - tallest) / 2);
        for (var index = 0; index < Shots.Length; index++)
        {
            var rect = _columnSlots[index];
            _slots[index] = new Rect(rect.X, rect.Y + offset, rect.Width, rect.Height);
        }

        UpdateStage();
    }

    private void UpdateStage()
    {
        var shot = Shots[Math.Clamp(_index, 0, Shots.Length - 1)];
        var maxWidth = Math.Min(Bounds.Width - 220, 600);
        var maxHeight = Math.Max(180, Bounds.Height - 128);
        var height = Math.Min(maxHeight, maxWidth * shot.Ratio);
        var width = height / shot.Ratio;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = width * shot.Ratio;
        }

        _stage = new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2 - 8, width, height);
        _close = new Rect(_stage.Right - 2, _stage.Y - 40, 32, 32);
    }

    private void Open(int index)
    {
        _index = Math.Clamp(index, 0, Shots.Length - 1);
        _focus = _index;
        UpdateStage();
        _origin = _slots[_index];
        _open = true;
        _dragY = 0;
        _dragX = 0;
        _zoom = 1;
        _viewer.Jump(SpecialUi.Reduced ? 1 : 0);
        EnsureFrame();
    }

    private void Close()
    {
        _open = false;
        _viewer.Jump(0);
        _zoom = 1;
        EnsureFrame();
        InvalidateVisual();
    }

    private void Move(int direction)
    {
        if (!_open)
        {
            return;
        }

        _index = (_index + direction + Shots.Length) % Shots.Length;
        _focus = _index;
        UpdateStage();
        _origin = _slots[_index];
        _zoom = 1;
        _dragX = 0;
        EnsureFrame();
    }

    protected override void OnFrame(double dt)
    {
        _viewer.Step(_open ? 1 : 0, dt);
    }

    protected override bool NeedsFrame => Math.Abs(_viewer.Value - (_open ? 1 : 0)) > 0.002;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        BuildLayout(Bounds.Size);
        var point = e.GetPosition(this);
        _pressPoint = point;
        _velocity = 0;

        if (!_open)
        {
            for (var index = 0; index < Shots.Length; index++)
            {
                if (_slots[index].Contains(point))
                {
                    Open(index);
                    break;
                }
            }

            e.Handled = true;
            return;
        }

        if (_close.Contains(point))
        {
            Close();
            e.Handled = true;
            return;
        }

        _pointerDown = true;
        _dragging = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerDown || e.Pointer.Captured != this)
        {
            return;
        }

        var point = e.GetPosition(this);
        var dx = point.X - _pressPoint.X;
        var dy = point.Y - _pressPoint.Y;
        if (!_dragging && Math.Abs(dx) < 5 && Math.Abs(dy) < 5)
        {
            return;
        }

        _dragging = true;
        _dragY = dy > 0 ? dy : dy * 0.25;
        _dragX = dx;
        _velocity = dx * 0.4;
        EnsureFrame();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (_dragY > 110)
        {
            Close();
        }
        else if (Math.Abs(_velocity) > 120 && Math.Abs(_dragY) < 60)
        {
            Move(_velocity > 0 ? -1 : 1);
        }
        else if (Math.Abs(_dragY) < 30 && Math.Abs(_dragX) > 60)
        {
            Move(_dragX < 0 ? 1 : -1);
        }

        _dragY = 0;
        _dragX = 0;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_open)
        {
            switch (e.Key)
            {
                case Key.Left:
                    Move(-1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    Move(1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
                case Key.Add:
                case Key.OemPlus:
                    _zoom = Math.Min(2.4, _zoom + 0.3);
                    e.Handled = true;
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    _zoom = Math.Max(1, _zoom - 0.3);
                    e.Handled = true;
                    break;
                case Key.D0:
                    _zoom = 1;
                    e.Handled = true;
                    break;
            }

            InvalidateVisual();
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _focus = Math.Max(0, _focus - 1);
                e.Handled = true;
                break;
            case Key.Right:
                _focus = Math.Min(Shots.Length - 1, _focus + 1);
                e.Handled = true;
                break;
            case Key.Home:
                _focus = 0;
                e.Handled = true;
                break;
            case Key.End:
                _focus = Shots.Length - 1;
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Open(_focus);
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        BuildLayout(size);
        var background = SpecialUi.Color(ArcTokens.Background);

        for (var index = 0; index < Shots.Length; index++)
        {
            var rect = _slots[index];
            using (context.PushClip(new RoundedRect(rect, 12)))
            {
                SampleArt.Draw(context, rect, Shots[index].Seed, 12, null, false);
            }

            context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Border, 0.6), 1), rect.Deflate(0.5), 12, 12);

            if (Hovering && !_open && rect.Contains(Pointer))
            {
                var ink = SpecialUi.Color(ArcTokens.Scrim);
                var strip = new Rect(rect.X, rect.Bottom - 34, rect.Width, 34);
                context.DrawRectangle(
                    new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(ArcColor.WithAlpha(ink, 0), 0),
                            new GradientStop(ArcColor.WithAlpha(ink, 0.55), 1)
                        }
                    },
                    null,
                    strip);
                context.DrawText(
                    SpecialUi.Text(Shots[index].Title, 12, SpecialUi.Brush(ArcTokens.Background), FontWeight.Medium),
                    new Point(rect.X + 10, rect.Bottom - 22));
            }
        }

        if (ShowFocusRing && !_open)
        {
            SpecialUi.FocusRing(context, _slots[Math.Clamp(_focus, 0, Shots.Length - 1)], 14);
        }

        if (_viewer.Value > 0.01)
        {
            DrawViewer(context, size, background);
        }
    }

    private void DrawViewer(DrawingContext context, Size size, Color background)
    {
        var t = Math.Clamp(_viewer.Value, 0, 1);
        using (context.PushOpacity(t))
        {
            SpecialUi.RoundRect(context, new Rect(0, 0, size.Width, size.Height), 0, SpecialUi.Brush(ArcTokens.Scrim));
        }

        var origin = _open ? _origin : _slots[Math.Clamp(_index, 0, Shots.Length - 1)];
        var stage = _stage;
        var rect = new Rect(
            SpecialUi.Lerp(origin.X, stage.X, t),
            SpecialUi.Lerp(origin.Y, stage.Y + _dragY, t),
            SpecialUi.Lerp(origin.Width, stage.Width, t),
            SpecialUi.Lerp(origin.Height, stage.Height, t));
        var radius = SpecialUi.Lerp(12, 20, t);
        var shot = Shots[Math.Clamp(_index, 0, Shots.Length - 1)];

        if (t > 0.6)
        {
            SpecialUi.Lift(context, rect, radius, 1.4);
        }

        using (context.PushClip(new RoundedRect(rect, radius)))
        {
            if (_zoom > 1.01)
            {
                using (context.PushTransform(
                           Matrix.CreateTranslation(-rect.Center.X, -rect.Center.Y) *
                           Matrix.CreateScale(_zoom, _zoom) *
                           Matrix.CreateTranslation(rect.Center.X, rect.Center.Y)))
                {
                    SampleArt.Draw(context, rect, shot.Seed, radius);
                }
            }
            else
            {
                SampleArt.Draw(context, rect, shot.Seed, radius);
            }
        }

        context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Surface, 0.5), 1), rect, radius, radius);

        using (context.PushOpacity(t))
        {
            var close = new Rect(rect.Right - 44, rect.Y + 12, 32, 32);
            _close = close;
            SpecialUi.RoundRect(context, close, 16, SpecialUi.Brush(ArcTokens.Surface));
            SpecialIcons.Draw(context, "x", close.Deflate(8), SpecialUi.Brush(ArcTokens.Foreground), 2);

            var counter = SpecialUi.Text($"{_index + 1} of {Shots.Length}", 12, SpecialUi.Brush(ArcTokens.Surface), FontWeight.Medium);
            var counterRect = new Rect(rect.X + 12, rect.Y + 12, counter.Width + 18, 24);
            SpecialUi.RoundRect(context, counterRect, 12, SpecialUi.Alpha(ArcTokens.Scrim, 0.7));
            SpecialUi.DrawTextCentered(context, counter, counterRect);

            var title = SpecialUi.Text(shot.Title, 18, SpecialUi.Brush(ArcTokens.Surface), FontWeight.Medium);
            var caption = SpecialUi.Text(shot.Caption, 12, SpecialUi.Alpha(ArcTokens.Surface, 0.75));
            var captionY = Math.Min(rect.Bottom + 14, size.Height - 52);
            context.DrawText(title, new Point(rect.X, captionY));
            context.DrawText(caption, new Point(rect.X, captionY + title.Height + 2));

            if (_zoom > 1.01)
            {
                var zoomLabel = SpecialUi.Text($"{_zoom:0.0}×", 11, SpecialUi.Brush(ArcTokens.Surface), FontWeight.Medium);
                var zoomRect = new Rect(rect.Right - 58, rect.Bottom - 36, 46, 24);
                SpecialUi.RoundRect(context, zoomRect, 12, SpecialUi.Alpha(ArcTokens.Scrim, 0.65));
                SpecialUi.DrawTextCentered(context, zoomLabel, zoomRect);
            }

            var previous = new Rect(rect.X - 52, rect.Center.Y - 20, 40, 40);
            if (previous.X > 8)
            {
                SpecialUi.RoundRect(context, previous, 20, SpecialUi.Alpha(ArcTokens.Surface, 0.92));
                SpecialIcons.Draw(context, "chevron-left", previous.Deflate(10), SpecialUi.Brush(ArcTokens.Foreground), 2);
            }

            var next = new Rect(rect.Right + 12, rect.Center.Y - 20, 40, 40);
            if (next.Right < size.Width - 8)
            {
                SpecialUi.RoundRect(context, next, 20, SpecialUi.Alpha(ArcTokens.Surface, 0.92));
                SpecialIcons.Draw(context, "chevron-right", next.Deflate(10), SpecialUi.Brush(ArcTokens.Foreground), 2);
            }

            // Hint at the drag gesture under the stage.
            var hint = SpecialUi.Text("Drag down to close  ·  + and − zoom", 11, SpecialUi.Alpha(ArcTokens.Surface, 0.6));
            SpecialUi.DrawTextCentered(context, hint, new Rect(0, size.Height - 24, size.Width, hint.Height));
        }

        // Keep the background colour referenced so the vignette respects the theme.
        _ = background;
    }
}
