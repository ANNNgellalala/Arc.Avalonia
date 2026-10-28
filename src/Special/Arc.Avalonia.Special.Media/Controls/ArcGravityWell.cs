using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// Arc's gravity well: a wall of tiles that bends toward the pointer like spacetime, with a click that sends
/// a shockwave through it. Tiles near the well lean in, shrink and dim while keeping their grid slot. Under
/// reduced motion the wall rests at a fixed, authored bend so the effect still reads in a still frame, and
/// clicks do not pulse.
/// </summary>
public sealed class ArcGravityWell : ArcSpecialCanvas
{
    private const int Columns = 9;
    private const int Rows = 4;
    private const double Gap = 8;
    private const double Reach = 205;

    private readonly List<(Point Origin, double Age)> _pulses = new();
    private Spring _wellX = Spring.At(0, 210, 28);
    private Spring _wellY = Spring.At(0, 210, 28);
    private Point _target;
    private bool _placed;
    private int _focus = Columns * Rows / 2 - Columns / 2;

    public ArcGravityWell()
    {
        SpecialUi.Name(
            this,
            "Gravity well",
            "A wall of tiles that leans toward the pointer. Arrow keys move the well from tile to tile; Space or Enter sends a shockwave.");
    }

    protected override double DefaultWidth => 930;

    protected override double DefaultHeight => 420;

    private Point Well => new(_wellX.Value, _wellY.Value);

    private void Place(Size size)
    {
        if (_placed)
        {
            return;
        }

        _placed = true;
        _target = TileCenter(23, size) + new Point(4, 3);
        _wellX.Jump(_target.X);
        _wellY.Jump(_target.Y);
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
        var point = e.GetPosition(this);
        _target = point;
        Pulse(point);
        e.Handled = true;
    }

    private void Pulse(Point origin)
    {
        if (SpecialUi.Reduced)
        {
            return;
        }

        if (_pulses.Count >= 4)
        {
            _pulses.RemoveAt(0);
        }

        _pulses.Add((origin, 0));
        EnsureFrame();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var handled = true;
        switch (e.Key)
        {
            case Key.Left:
                _focus = Math.Max(0, _focus - 1);
                break;
            case Key.Right:
                _focus = Math.Min(Columns * Rows - 1, _focus + 1);
                break;
            case Key.Up:
                _focus = Math.Max(0, _focus - Columns);
                break;
            case Key.Down:
                _focus = Math.Min(Columns * Rows - 1, _focus + Columns);
                break;
            case Key.Home:
                _focus = 0;
                break;
            case Key.End:
                _focus = Columns * Rows - 1;
                break;
            case Key.Space:
            case Key.Enter:
                Pulse(TileCenter(_focus, Bounds.Size));
                handled = true;
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            var center = TileCenter(_focus, Bounds.Size);
            _target = center;
            EnsureFrame();
            e.Handled = true;
        }
    }

    private Rect TileRect(int index, Size size)
    {
        var tile = Math.Min(
            (size.Width - 16 - (Columns - 1) * Gap) / Columns,
            (size.Height - 16 - (Rows - 1) * Gap) / Rows);
        var gridWidth = Columns * tile + (Columns - 1) * Gap;
        var gridHeight = Rows * tile + (Rows - 1) * Gap;
        var left = (size.Width - gridWidth) / 2;
        var top = (size.Height - gridHeight) / 2;
        var column = index % Columns;
        var row = index / Columns;
        return new Rect(left + column * (tile + Gap), top + row * (tile + Gap), tile, tile);
    }

    private Point TileCenter(int index, Size size)
    {
        var rect = TileRect(index, size);
        return rect.Center;
    }

    protected override void OnFrame(double dt)
    {
        _wellX.Step(_target.X, dt);
        _wellY.Step(_target.Y, dt);
        for (var index = _pulses.Count - 1; index >= 0; index--)
        {
            var pulse = _pulses[index];
            pulse.Age += dt;
            if (pulse.Age > 1.4)
            {
                _pulses.RemoveAt(index);
            }
            else
            {
                _pulses[index] = pulse;
            }
        }
    }

    protected override bool NeedsFrame =>
        Math.Abs(_wellX.Value - _target.X) > 0.2 ||
        Math.Abs(_wellY.Value - _target.Y) > 0.2 ||
        _pulses.Count > 0;

    protected override void OnRender(DrawingContext context, Size size)
    {
        Place(size);
        var well = Well;
        var count = Columns * Rows;
        var layers = new List<(int Index, double Pull, Rect Rect, double Opacity, double Scale)>(count);

        for (var index = 0; index < count; index++)
        {
            var rect = TileRect(index, size);
            var dx = well.X - rect.Center.X;
            var dy = well.Y - rect.Center.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var normalized = Math.Min(1, distance / Reach);
            var pull = Math.Pow(1 - normalized, 1.55);
            var offsetX = dx * 0.42 * pull;
            var offsetY = dy * 0.42 * pull;
            var extra = 0.0;

            foreach (var pulse in _pulses)
            {
                var px = rect.Center.X - pulse.Origin.X;
                var py = rect.Center.Y - pulse.Origin.Y;
                var radius = Math.Sqrt(px * px + py * py);
                var wave = radius - pulse.Age * 900;
                if (wave < -20 || wave > 46)
                {
                    continue;
                }

                var band = 1 - Math.Abs(wave) / 46;
                var fade = Math.Max(0, 1 - pulse.Age / 1.3);
                extra = Math.Max(extra, band * fade);
                if (radius > 1)
                {
                    offsetX += px / radius * 12 * extra;
                    offsetY += py / radius * 12 * extra;
                }
            }

            var scale = 1 - 0.30 * pull - 0.07 * extra;
            var opacity = Math.Clamp(1 - 0.36 * pull - 0.14 * extra, 0.25, 1);
            var moved = new Rect(
                rect.Center.X + offsetX - rect.Width * scale / 2,
                rect.Center.Y + offsetY - rect.Height * scale / 2,
                rect.Width * scale,
                rect.Height * scale);
            layers.Add((index, pull + extra, moved, opacity, scale));
        }

        // A faint well of light sits under the tiles so the bend reads even in a still frame.
        var halo = new RadialGradientBrush
        {
            Center = new RelativePoint(well.X / size.Width, well.Y / size.Height, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(well.X / size.Width, well.Y / size.Height, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.30, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.34, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ArcColor.WithAlpha(SpecialUi.Color(ArcTokens.Accent), 0.16), 0),
                new GradientStop(ArcColor.WithAlpha(SpecialUi.Color(ArcTokens.Accent), 0.0), 1)
            }
        };
        context.DrawRectangle(halo, null, new Rect(0, 0, size.Width, size.Height));

        // Deeper tiles paint first so the ones around the well overlap them.
        layers.Sort((a, b) => b.Pull.CompareTo(a.Pull));

        foreach (var layer in layers)
        {
            using (context.PushOpacity(layer.Opacity))
            {
                var rect = layer.Rect;
                SampleArt.Draw(context, new Rect(rect.X, rect.Y, rect.Width, rect.Height), layer.Index + 2, 12, null, false);
                context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Background, 0.5), 1), rect.Deflate(0.5), 12, 12);
            }
        }

        // Shockwave rings travel over the wall.
        foreach (var pulse in _pulses)
        {
            var radius = pulse.Age * 900;
            var fade = Math.Max(0, 1 - pulse.Age / 1.3);
            var alpha = 0.55 * fade;
            context.DrawEllipse(null, new Pen(SpecialUi.Alpha(ArcTokens.Accent, alpha), 2), pulse.Origin, radius, radius);
        }

        if (ShowFocusRing)
        {
            var focus = TileRect(_focus, size);
            SpecialUi.FocusRing(context, focus, 14);
        }
    }
}
