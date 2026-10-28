using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksTeam.Controls;

/// <summary>
/// The onboarding line illustration. One set of seven shared shapes interpolates between a folder, a stack of
/// files, a team and a badge, so the scenes morph instead of swapping images. Geometry lives in a 260 x 160
/// design box and scales to the control bounds; strokes and fills resolve from Arc tokens at render time.
/// Under <see cref="ArcThemeManager.ReducedMotion"/> the scenes swap instantly, which is the documented
/// reduced-motion branch.
/// </summary>
public sealed class ArcMorphIllustration : Control
{
    private sealed record Shape(double X, double Y, double W, double H, double Radius, double Opacity, bool Ellipse = false);

    private sealed record Scene(IReadOnlyList<Shape> Shapes, double CheckOpacity);

    private static readonly Scene[] Scenes =
    {
        // Folder.
        new(new[]
        {
            new Shape(52, 54, 156, 84, 14, 1),
            new Shape(52, 36, 66, 22, 8, 1),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(78, 78, 104, 8, 4, 0),
            new Shape(78, 92, 78, 8, 4, 0),
            new Shape(78, 106, 56, 8, 4, 0)
        }, 0),
        // Files.
        new(new[]
        {
            new Shape(62, 44, 124, 92, 12, 1),
            new Shape(82, 56, 124, 88, 12, 1),
            new Shape(102, 68, 124, 84, 12, 1),
            new Shape(102, 68, 124, 84, 12, 0),
            new Shape(116, 86, 62, 7, 3.5, 1),
            new Shape(116, 100, 54, 7, 3.5, 1),
            new Shape(116, 114, 40, 7, 3.5, 1)
        }, 0),
        // Team.
        new(new[]
        {
            new Shape(72, 102, 148, 38, 19, 1),
            new Shape(102, 110, 118, 30, 15, 1),
            new Shape(104, 54, 28, 28, 14, 1, true),
            new Shape(138, 46, 28, 28, 14, 1, true),
            new Shape(172, 54, 28, 28, 14, 1, true),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(52, 54, 156, 84, 14, 0)
        }, 0),
        // Badge.
        new(new[]
        {
            new Shape(106, 36, 78, 78, 39, 1, true),
            new Shape(114, 108, 16, 36, 4, 1),
            new Shape(160, 108, 16, 36, 4, 1),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(52, 54, 156, 84, 14, 0),
            new Shape(52, 54, 156, 84, 14, 0)
        }, 1)
    };

    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<ArcMorphIllustration, int>(nameof(Step));

    private readonly DispatcherTimer _timer;
    private Scene _from = Scenes[0];
    private Scene _to = Scenes[0];
    private double _progress = 1;
    private DateTime _started;

    static ArcMorphIllustration()
    {
        AffectsRender<ArcMorphIllustration>(StepProperty);
        StepProperty.Changed.AddClassHandler<ArcMorphIllustration>((control, _) => control.OnStepChanged());
    }

    public ArcMorphIllustration()
    {
        Focusable = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _timer.Tick += (_, _) => Advance();
    }

    /// <summary>Scene index: 0 folder, 1 files, 2 team, 3 badge.</summary>
    public int Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, Math.Clamp(value, 0, Scenes.Length - 1));
    }

    protected override Size MeasureOverride(Size availableSize) => new(260, 160);

    private void OnStepChanged()
    {
        var target = Math.Clamp(Step, 0, Scenes.Length - 1);
        if (target == Array.IndexOf(Scenes, _to))
        {
            return;
        }

        _from = _progress >= 1 ? _to : Snapshot();
        _to = Scenes[target];

        if (ArcThemeManager.ReducedMotion)
        {
            _progress = 1;
            InvalidateVisual();
            return;
        }

        _progress = 0;
        _started = DateTime.UtcNow;
        _timer.Start();
        InvalidateVisual();
    }

    private Scene Snapshot()
    {
        var shapes = new Shape[_to.Shapes.Count];
        for (var index = 0; index < shapes.Length; index++)
        {
            shapes[index] = Lerp(_from.Shapes[index], _to.Shapes[index], Ease(_progress));
        }

        return new Scene(shapes, _from.CheckOpacity + (_to.CheckOpacity - _from.CheckOpacity) * Ease(_progress));
    }

    private void Advance()
    {
        var elapsed = (DateTime.UtcNow - _started).TotalMilliseconds;
        _progress = Math.Clamp(elapsed / 420d, 0, 1);
        InvalidateVisual();
        if (_progress >= 1)
        {
            _timer.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var from = _from;
        var to = _to;
        var ease = Ease(_progress);
        var scale = Math.Min(width / 260d, height / 160d);
        var offsetX = (width - 260 * scale) / 2;
        var offsetY = (height - 160 * scale) / 2;

        var accent = ArcResources.Brush(ArcTokens.Accent);

        // A quiet plate behind the scene gives the line work something to sit on.
        var plate = new Rect(offsetX + 12 * scale, offsetY + 8 * scale, 236 * scale, 144 * scale);
        context.DrawRectangle(new SolidColorBrush(ArcResources.Color(ArcTokens.SurfaceMuted), 0.7), null, plate, 18 * scale, 18 * scale);

        for (var index = 0; index < to.Shapes.Count; index++)
        {
            var shape = Lerp(from.Shapes[index], to.Shapes[index], ease);
            if (shape.Opacity <= 0.01)
            {
                continue;
            }

            var rect = new Rect(
                offsetX + shape.X * scale,
                offsetY + shape.Y * scale,
                Math.Max(0, shape.W * scale),
                Math.Max(0, shape.H * scale));

            var fill = new SolidColorBrush(ArcResources.Color(ArcTokens.Accent), 0.05 * shape.Opacity);
            var shapePen = new Pen(accent, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

            if (shape.Ellipse)
            {
                context.DrawEllipse(fill, shapePen, rect.Center, rect.Width / 2, rect.Height / 2);
            }
            else
            {
                context.DrawRectangle(fill, shapePen, rect, shape.Radius * scale, shape.Radius * scale);
            }
        }

        // Check mark for the badge scene.
        var checkOpacity = from.CheckOpacity + (to.CheckOpacity - from.CheckOpacity) * ease;
        if (checkOpacity > 0.01)
        {
            var checkPen = new Pen(new SolidColorBrush(ArcResources.Color(ArcTokens.Accent), checkOpacity), 2.4,
                lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var geometry = new StreamGeometry();
            using var stream = geometry.Open();
            stream.BeginFigure(new Point(offsetX + 132 * scale, offsetY + 76 * scale), false);
            stream.LineTo(new Point(offsetX + 144 * scale, offsetY + 88 * scale));
            stream.LineTo(new Point(offsetX + 164 * scale, offsetY + 66 * scale));
            stream.EndFigure(false);
            context.DrawGeometry(null, checkPen, geometry);
        }
    }

    private static double Ease(double value) => value * value * (3 - 2 * value);

    private static Shape Lerp(Shape from, Shape to, double t) => new(
        from.X + (to.X - from.X) * t,
        from.Y + (to.Y - from.Y) * t,
        from.W + (to.W - from.W) * t,
        from.H + (to.H - from.H) * t,
        from.Radius + (to.Radius - from.Radius) * t,
        from.Opacity + (to.Opacity - from.Opacity) * t,
        from.Ellipse || to.Ellipse);
}
