using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Controls;

/// <summary>
/// The Arc loading spinner: a 270 degree arc on a 24 unit grid. Deterministic when reduced motion is on.
/// </summary>
public class ArcSpinner : Control
{
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcSpinner, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<ArcSpinner, double>(nameof(StrokeWidth), 2);

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ArcSpinner, IBrush?>(nameof(Stroke));

    private DispatcherTimer? _timer;
    private double _angle;

    static ArcSpinner()
    {
        AffectsRender<ArcSpinner>(SizeProperty, StrokeWidthProperty, StrokeProperty);
        AffectsMeasure<ArcSpinner>(SizeProperty);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ArcThemeManager.ReducedMotion)
        {
            _angle = 0;
            return;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 7.5) % 360;
            InvalidateVisual();
        };
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    public override void Render(DrawingContext context)
    {
        var stroke = Stroke ?? ArcResources.Brush(ArcTokens.TextSecondary);
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var thickness = StrokeWidth;
        var radius = Math.Max(0, size / 2 - thickness / 2);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var startAngle = (_angle - 90) * Math.PI / 180;
        var sweep = 270 * Math.PI / 180;

        var start = new Point(center.X + radius * Math.Cos(startAngle), center.Y + radius * Math.Sin(startAngle));
        var end = new Point(center.X + radius * Math.Cos(startAngle + sweep), center.Y + radius * Math.Sin(startAngle + sweep));

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(start, false);
            figure.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(stroke, thickness, null, PenLineCap.Round, PenLineJoin.Round), geometry);
    }
}

