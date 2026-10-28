using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's dot grid: a field of dots that bulges away from the pointer like a lens and ripples outward from
/// every click. The field is decoration, so the control is one focus stop with an internal lens that the
/// arrow keys drive. Reduced motion keeps a single curated still: a lens bulge at a fixed point with no
/// ripples and no spring motion.
/// </summary>
public sealed class ArcDotGrid : ArcSpecialCanvas
{
    private readonly List<(Point Origin, double Age)> _ripples = new();
    private Spring _lensX = Spring.At(0);
    private Spring _lensY = Spring.At(0);
    private Point _target;
    private bool _placed;

    private const double Gap = 22;
    private const double DotSize = 1.5;
    private const double LensRadius = 150;
    private const double Strength = 17;
    private const double RippleSpeed = 620;

    public ArcDotGrid()
    {
        SpecialUi.Name(
            this,
            "Dot grid",
            "A decorative dot field. Move the pointer or use the arrow keys to move the lens; press Space to send a ripple.");
    }

    protected override double DefaultWidth => 960;

    protected override double DefaultHeight => 440;

    private Point LensPoint => new(_lensX.Value, _lensY.Value);

    private void PlaceLensIfNeeded(Size size)
    {
        if (_placed)
        {
            return;
        }

        _placed = true;
        _target = new Point(size.Width * 0.60, size.Height * 0.44);
        _lensX.Jump(_target.X);
        _lensY.Jump(_target.Y);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _target = e.GetPosition(this);
        EnsureFrame();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        Ripple(e.GetPosition(this));
        e.Handled = true;
    }

    private void Ripple(Point origin)
    {
        if (SpecialUi.Reduced)
        {
            // Reduced motion: clicks do not ripple, the field only settles.
            return;
        }

        if (_ripples.Count >= 6)
        {
            _ripples.RemoveAt(0);
        }

        _ripples.Add((origin, 0));
        EnsureFrame();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 48 : 16;
        var handled = true;
        switch (e.Key)
        {
            case Key.Left:
                _target = new Point(Math.Max(0, _target.X - step), _target.Y);
                break;
            case Key.Right:
                _target = new Point(_target.X + step, _target.Y);
                break;
            case Key.Up:
                _target = new Point(_target.X, Math.Max(0, _target.Y - step));
                break;
            case Key.Down:
                _target = new Point(_target.X, _target.Y + step);
                break;
            case Key.Space:
            case Key.Enter:
                Ripple(LensPoint);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            EnsureFrame();
            e.Handled = true;
        }
    }

    protected override void OnFrame(double dt)
    {
        _lensX.Step(_target.X, dt);
        _lensY.Step(_target.Y, dt);

        for (var index = _ripples.Count - 1; index >= 0; index--)
        {
            var ripple = _ripples[index];
            ripple.Age += dt;
            if (ripple.Age > 1.6)
            {
                _ripples.RemoveAt(index);
            }
            else
            {
                _ripples[index] = ripple;
            }
        }
    }

    protected override bool NeedsFrame =>
        Math.Abs(_lensX.Value - _target.X) > 0.2 ||
        Math.Abs(_lensY.Value - _target.Y) > 0.2 ||
        _ripples.Count > 0;

    protected override void OnRender(DrawingContext context, Size size)
    {
        PlaceLensIfNeeded(size);
        var outer = new Rect(0, 0, size.Width, size.Height);
        var surface = SpecialUi.Brush(ArcTokens.Background);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var baseDot = SpecialUi.Color(ArcTokens.BorderStrong);
        var accent = SpecialUi.Color(ArcTokens.Accent);

        context.DrawRectangle(surface, null, outer, 26, 26);
        context.DrawRectangle(null, new Pen(border, 1), outer.Deflate(0.5), 26, 26);

        var lens = LensPoint;
        var inset = 14.0;
        var usable = new Rect(inset, inset, Math.Max(1, size.Width - inset * 2), Math.Max(1, size.Height - inset * 2));
        var columns = (int)Math.Floor(usable.Width / Gap);
        var rows = (int)Math.Floor(usable.Height / Gap);
        var startX = usable.X + (usable.Width - (columns - 1) * Gap) / 2;
        var startY = usable.Y + (usable.Height - (rows - 1) * Gap) / 2;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var rest = new Point(startX + column * Gap, startY + row * Gap);
                var dx = rest.X - lens.X;
                var dy = rest.Y - lens.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var normalized = Math.Min(1, distance / LensRadius);
                var profile = normalized >= 1 ? 0 : Math.Sin(Math.PI * Math.Pow(normalized, 0.75));

                var offsetX = 0.0;
                var offsetY = 0.0;
                var tint = profile;

                if (distance > 0.5)
                {
                    var direction = new Point(dx / distance, dy / distance);
                    offsetX = direction.X * Strength * profile;
                    offsetY = direction.Y * Strength * profile;
                }

                foreach (var ripple in _ripples)
                {
                    var rx = rest.X - ripple.Origin.X;
                    var ry = rest.Y - ripple.Origin.Y;
                    var radius = Math.Sqrt(rx * rx + ry * ry);
                    var wave = radius - ripple.Age * RippleSpeed;
                    if (wave < 0 || wave > 30)
                    {
                        continue;
                    }

                    var band = 1 - Math.Abs(wave) / 30;
                    var fade = Math.Max(0, 1 - ripple.Age / 1.5);
                    var push = band * fade;
                    if (radius > 0.5)
                    {
                        offsetX += rx / radius * 9 * push;
                        offsetY += ry / radius * 9 * push;
                    }

                    tint = Math.Min(1, tint + push * 0.9);
                }

                var point = new Point(rest.X + offsetX, rest.Y + offsetY);
                var radiusAtPoint = DotSize * (1 + 0.85 * tint);
                var color = tint <= 0.01 ? baseDot : ArcColor.Mix(baseDot, accent, Math.Clamp(tint, 0, 1));
                context.DrawEllipse(SpecialUi.Solid(color), null, point, radiusAtPoint, radiusAtPoint);
            }
        }

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, outer.Deflate(1), 25);
        }
    }
}
