using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksCommerce.Controls;

/// <summary>
/// The empty state illustration: six simple token shapes that interpolate between four scenes (no results,
/// offline, inbox zero, missing page). It is drawn rather than composed so the shapes can move continuously;
/// with reduced motion the scene changes jump straight to the target. All colours come from the palette.
/// </summary>
public sealed class ArcEmptyScene : Control
{
    public static readonly StyledProperty<int> SceneProperty =
        AvaloniaProperty.Register<ArcEmptyScene, int>(nameof(Scene));

    public static readonly StyledProperty<double> HeightHintProperty =
        AvaloniaProperty.Register<ArcEmptyScene, double>(nameof(HeightHint), 176);

    private static readonly SceneShape[][] Scenes =
    {
        // No results: a magnifier over dimmed rows.
        new[]
        {
            new SceneShape(20, 12, 280, 152, 20, 1),
            new SceneShape(46, 42, 96, 10, 5, 0.55),
            new SceneShape(46, 62, 148, 10, 5, 0.4),
            new SceneShape(46, 82, 72, 10, 5, 0.28),
            new SceneShape(214, 46, 46, 46, 23, 1),
            new SceneShape(250, 84, 30, 7, 3, 1)
        },
        // Offline: the same row block sinks and becomes a cloud with a slash.
        new[]
        {
            new SceneShape(20, 12, 280, 152, 20, 1),
            new SceneShape(46, 104, 72, 10, 5, 0.3),
            new SceneShape(46, 122, 112, 10, 5, 0.22),
            new SceneShape(46, 140, 56, 10, 5, 0.16),
            new SceneShape(190, 40, 72, 38, 19, 1),
            new SceneShape(236, 46, 7, 66, 3, 0.9)
        },
        // Inbox zero: rows become one wide list and the glyph becomes a tray.
        new[]
        {
            new SceneShape(20, 12, 280, 152, 20, 1),
            new SceneShape(46, 44, 118, 10, 5, 0.45),
            new SceneShape(46, 62, 118, 10, 5, 0.3),
            new SceneShape(46, 80, 82, 10, 5, 0.2),
            new SceneShape(196, 40, 92, 60, 14, 1),
            new SceneShape(208, 78, 56, 6, 3, 1)
        },
        // Missing page: a dot over two posts, the row block faded out.
        new[]
        {
            new SceneShape(20, 12, 280, 152, 20, 1),
            new SceneShape(46, 44, 70, 10, 5, 0.22),
            new SceneShape(46, 62, 96, 10, 5, 0.16),
            new SceneShape(46, 80, 54, 10, 5, 0.12),
            new SceneShape(150, 34, 48, 48, 24, 1),
            new SceneShape(122, 96, 104, 7, 3, 0.9)
        }
    };

    private int _from;
    private int _to;
    private double _progress = 1;
    private DispatcherTimer? _timer;

    public int Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double HeightHint
    {
        get => GetValue(HeightHintProperty);
        set => SetValue(HeightHintProperty, value);
    }

    public ArcEmptyScene()
    {
        ClipToBounds = false;
        SceneProperty.Changed.AddClassHandler<ArcEmptyScene>((scene, _) => scene.StartMorph());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        return new Size(width, HeightHint);
    }

    private void StartMorph()
    {
        _from = _to;
        _to = Math.Clamp(Scene, 0, Scenes.Length - 1);

        if (ArcThemeManager.ReducedMotion || _from == _to)
        {
            _progress = 1;
            InvalidateVisual();
            return;
        }

        _progress = 0;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _progress = Math.Min(1, _progress + 0.06);
            if (_progress >= 1)
            {
                _timer?.Stop();
            }

            InvalidateVisual();
        };
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(bounds.Width / 320.0, bounds.Height / 176.0);
        var offsetX = (bounds.Width - 320 * scale) / 2;
        var offsetY = (bounds.Height - 176 * scale) / 2;
        var progress = Ease(_progress);

        var from = Scenes[Math.Clamp(_from, 0, Scenes.Length - 1)];
        var to = Scenes[Math.Clamp(_to, 0, Scenes.Length - 1)];

        var plateFill = ArcResources.Brush(ArcTokens.SurfaceMuted);
        var platePen = new Pen(ArcResources.Brush(ArcTokens.Border), 1);
        var rowFill = ArcResources.Brush(ArcTokens.TextMuted);
        var glyphFill = new SolidColorBrush(Color.FromArgb(58, ArcResources.Color(ArcTokens.Accent).R,
            ArcResources.Color(ArcTokens.Accent).G, ArcResources.Color(ArcTokens.Accent).B));
        var glyphPen = new Pen(new SolidColorBrush(Color.FromArgb(205, ArcResources.Color(ArcTokens.Accent).R,
            ArcResources.Color(ArcTokens.Accent).G, ArcResources.Color(ArcTokens.Accent).B)), 1.5);

        for (var index = 0; index < from.Length; index++)
        {
            var a = from[index];
            var b = to[index];
            var x = Lerp(a.X, b.X, progress);
            var y = Lerp(a.Y, b.Y, progress);
            var width = Lerp(a.W, b.W, progress);
            var height = Lerp(a.H, b.H, progress);
            var radius = Lerp(a.R, b.R, progress);
            var alpha = Lerp(a.Alpha, b.Alpha, progress);

            var rect = new Rect(
                offsetX + x * scale,
                offsetY + y * scale,
                Math.Max(0, width * scale),
                Math.Max(0, height * scale));

            if (index == 0)
            {
                context.DrawRectangle(plateFill, platePen, new RoundedRect(rect, radius * scale));
                continue;
            }

            var isGlyph = index >= 4;
            var fill = isGlyph ? glyphFill : rowFill;
            var pen = isGlyph ? glyphPen : null;

            using (context.PushOpacity(alpha))
            {
                if (Math.Abs(width - height) < 0.5 && Math.Abs(radius - width / 2) < 0.5)
                {
                    context.DrawEllipse(fill, pen, rect.Center, rect.Width / 2, rect.Height / 2);
                }
                else
                {
                    context.DrawRectangle(fill, pen, new RoundedRect(rect, radius * scale));
                }
            }
        }
    }

    private static double Lerp(double from, double to, double progress) => from + (to - from) * progress;

    private static double Ease(double t) => t * t * (3 - 2 * t);

    private readonly record struct SceneShape(double X, double Y, double W, double H, double R, double Alpha);
}
