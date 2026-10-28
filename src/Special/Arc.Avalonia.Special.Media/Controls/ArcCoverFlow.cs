using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// Arc's cover flow: a depth rail of images. The front card stands forward, the rest recede, scale, dim and
/// turn toward the centre while their image drifts inside the frame; each card casts a grounded shadow and a
/// quiet reflection. Drag to move the rail, throw it with inertia, or scroll, arrow and click.
/// </summary>
public sealed class ArcCoverFlow : ArcSpecialCanvas
{
    private sealed record Cover(string Title, string Subtitle, string Meta, int Seed);

    private static readonly Cover[] Covers =
    {
        new("Ridge loop", "Mount Tam", "8 km", 5),
        new("Low tide", "Bolinas", "3 km", 1),
        new("Old town", "Porto", "City walk", 2),
        new("Pine shelter", "Sierra", "12 km", 7),
        new("Salt flats", "Badwater", "6 km", 0),
        new("Dusk road", "Big Sur", "Drive", 8),
        new("Glass house", "Palm Springs", "Studio", 6),
        new("Wildflower", "Antelope", "Photo set", 3)
    };

    private const double CardWidth = 224;
    private const double CardHeight = 220;
    private const double Step = 138;

    private Spring _progress = Spring.At(3, 150, 24);
    private double _targetIndex = 3;
    private int _index = 3;
    private bool _pointerDown;
    private bool _dragging;
    private bool _moved;
    private Point _pressPoint;
    private double _pressProgress;
    private double _lastX;
    private double _velocity;
    private double _wheel;
    private Rect _frontRect;

    public ArcCoverFlow()
    {
        SpecialUi.Name(
            this,
            "Cover flow",
            "A depth rail of images. Drag or throw it sideways, scroll, click a card, or use the arrow keys and Home and End.");
    }

    protected override double DefaultWidth => 980;

    protected override double DefaultHeight => 430;

    private void GoTo(int index)
    {
        _index = Math.Clamp(index, 0, Covers.Length - 1);
        _targetIndex = _index;
        EnsureFrame();
    }

    protected override void OnFrame(double dt)
    {
        _progress.Step(_targetIndex, dt);
        if (!_pointerDown && Math.Abs(_progress.Value - _targetIndex) < 0.01)
        {
            _progress.Jump(_targetIndex);
        }
    }

    protected override bool NeedsFrame => Math.Abs(_progress.Value - _targetIndex) > 0.002;

    private (Rect Card, double Delta) CardRect(int index, Size size, double progress)
    {
        var delta = index - progress;
        var distance = Math.Min(1, Math.Abs(delta));
        var scale = 1 - distance * 0.22;
        var width = CardWidth * scale * (1 - distance * 0.20);
        var height = CardHeight * (1 - distance * 0.10);
        var x = size.Width / 2 + delta * Step - width / 2;
        var y = 42 + distance * 12 - (height - CardHeight) / 2;
        return (new Rect(x, y, width, height), delta);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        _pointerDown = true;
        _dragging = false;
        _moved = false;
        _pressPoint = e.GetPosition(this);
        _lastX = _pressPoint.X;
        _pressProgress = _progress.Value;
        _velocity = 0;
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
        if (!_dragging && Math.Abs(dx) < 5)
        {
            return;
        }

        _dragging = true;
        _moved = true;
        var raw = _pressProgress - dx / Step;
        var max = Covers.Length - 1;
        var clamped = raw < 0 ? raw * 0.30 : raw > max ? max + (raw - max) * 0.30 : raw;
        _velocity = (point.X - _lastX) * 0.35;
        _lastX = point.X;
        _progress.Jump(clamped);
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
        _dragging = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_moved)
        {
            var point = e.GetPosition(this);
            for (var index = 0; index < Covers.Length; index++)
            {
                var (card, _) = CardRect(index, Bounds.Size, _progress.Value);
                if (card.Contains(point))
                {
                    GoTo(index);
                    break;
                }
            }

            e.Handled = true;
            return;
        }

        var projected = _progress.Value + _velocity / Step;
        var target = (int)Math.Round(projected);
        if (Math.Abs(_velocity) > Step * 1.6)
        {
            var direction = Math.Sign(_velocity);
            target = _index + direction;
            if (direction > 0)
            {
                target = Math.Max(target, (int)Math.Ceiling(_progress.Value));
            }
            else
            {
                target = Math.Min(target, (int)Math.Floor(_progress.Value));
            }
        }

        GoTo(target);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _wheel += Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X * 30 : e.Delta.Y * 30;
        if (Math.Abs(_wheel) < 46)
        {
            return;
        }

        var steps = (int)(_wheel / 46);
        _wheel -= steps * 46;
        GoTo(_index + Math.Sign(steps));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                GoTo(_index - 1);
                e.Handled = true;
                break;
            case Key.Right:
                GoTo(_index + 1);
                e.Handled = true;
                break;
            case Key.Home:
                GoTo(0);
                e.Handled = true;
                break;
            case Key.End:
                GoTo(Covers.Length - 1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        var progress = _progress.Value;
        var surface = SpecialUi.Brush(ArcTokens.Surface);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var muted = SpecialUi.Brush(ArcTokens.TextMuted);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var background = SpecialUi.Color(ArcTokens.Background);

        // Ground plane: a soft horizon line keeps the cards from floating.
        context.DrawLine(
            new Pen(SpecialUi.Alpha(ArcTokens.Border, 0.9), 1),
            new Point(0, 330),
            new Point(size.Width, 330));

        var order = new List<(int Index, Rect Card, double Delta)>();
        for (var index = 0; index < Covers.Length; index++)
        {
            var (card, delta) = CardRect(index, size, progress);
            if (Math.Abs(delta) > 4.2)
            {
                continue;
            }

            order.Add((index, card, delta));
        }

        order.Sort((a, b) => Math.Abs(b.Delta).CompareTo(Math.Abs(a.Delta)));
        var edgeFade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ArcColor.WithAlpha(foreground, 0), 0),
                new GradientStop(ArcColor.WithAlpha(foreground, 1), 0.06),
                new GradientStop(ArcColor.WithAlpha(foreground, 1), 0.94),
                new GradientStop(ArcColor.WithAlpha(foreground, 0), 1)
            }
        };
        using var edge = context.PushOpacityMask(edgeFade, new Rect(0, 0, size.Width, size.Height));
        foreach (var (index, card, delta) in order)
        {
            var distance = Math.Min(1, Math.Abs(delta));
            var front = Math.Abs(delta) < 0.5;
            if (front)
            {
                _frontRect = card;
            }

            using (context.PushOpacity(1 - distance * 0.34))
            {
                // Grounded shadow: an ellipse whose weight falls away with depth.
                context.DrawEllipse(
                    SpecialUi.ShadowBrush(0.22 - distance * 0.09),
                    null,
                    new Point(card.Center.X, card.Bottom + 14),
                    card.Width * 0.46,
                    12);

                using (context.PushClip(new RoundedRect(card, 18)))
                {
                    // The image drifts inside the frame as the rail moves.
                    var drift = -delta * 9;
                    var art = new Rect(card.X - 12 + drift, card.Y - 10, card.Width + 24, card.Height + 26);
                    SampleArt.Draw(context, art, Covers[index].Seed, 0, null, false);
                    context.DrawRectangle(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.10)), null, card);
                }

                context.DrawRectangle(null, new Pen(front ? border : SpecialUi.Alpha(ArcTokens.Border, 0.7), front ? 1.5 : 1), card, 18, 18);

                // Reflection, masked to fade away from the card.
                var mirror = new Rect(card.X, card.Bottom + 4, card.Width, 48);
                var mask = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(ArcColor.WithAlpha(foreground, 0.55), 0),
                        new GradientStop(ArcColor.WithAlpha(foreground, 0), 1)
                    }
                };
                using (context.PushOpacityMask(mask, mirror))
                using (context.PushClip(mirror))
                using (context.PushTransform(
                           Matrix.CreateTranslation(0, -mirror.Top) *
                           Matrix.CreateScale(1, -1) *
                           Matrix.CreateTranslation(0, mirror.Top)))
                {
                    SampleArt.Draw(context, new Rect(card.X, card.Y, card.Width, card.Height), Covers[index].Seed, 0, null, false);
                }

                // A wash of the page colour over the reflection ties it to the surface.
                context.DrawRectangle(
                    new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(ArcColor.WithAlpha(background, 0), 0),
                            new GradientStop(ArcColor.WithAlpha(background, 0.85), 1)
                        }
                    },
                    null,
                    mirror);
            }
        }

        // Caption under the rail, always for the front card.
        var current = Covers[Math.Clamp(_index, 0, Covers.Length - 1)];
        var title = SpecialUi.Text(current.Title, 22, SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium);
        SpecialUi.DrawTextCentered(context, title, new Rect(0, 344, size.Width, title.Height));
        var meta = SpecialUi.Text($"{current.Subtitle}  ·  {current.Meta}", 13, muted);
        SpecialUi.DrawTextCentered(context, meta, new Rect(0, 344 + title.Height + 2, size.Width, meta.Height));

        // Position ticks.
        var tickWidth = 16.0;
        var tickGap = 7.0;
        var total = Covers.Length * tickWidth + (Covers.Length - 1) * tickGap;
        var tickX = (size.Width - total) / 2;
        for (var index = 0; index < Covers.Length; index++)
        {
            var active = index == _index;
            var rect = new Rect(tickX + index * (tickWidth + tickGap), 414, active ? 22 : 8, 4);
            context.DrawRectangle(
                active ? SpecialUi.Brush(ArcTokens.Accent) : SpecialUi.Alpha(ArcTokens.BorderStrong, 0.8),
                null,
                rect,
                2,
                2);
        }

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, _frontRect, 20);
        }
    }
}
