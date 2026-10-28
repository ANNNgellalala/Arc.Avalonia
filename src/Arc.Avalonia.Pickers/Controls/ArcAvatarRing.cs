using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// The ring around the avatar during an upload. It fills from the reported progress, turns on its own while
/// no value has arrived yet, and freezes where it was when the upload fails. Under reduced motion the
/// waiting arc turns slowly instead of spinning.
/// </summary>
public sealed class ArcAvatarRing : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ArcAvatarRing, double>(nameof(Progress));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ArcAvatarRing, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcAvatarRing, double>(nameof(Thickness), 2.4);

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ArcAvatarRing, IBrush?>(nameof(Stroke));

    private DispatcherTimer? _timer;
    private double _angle;

    static ArcAvatarRing()
    {
        AffectsRender<ArcAvatarRing>(ProgressProperty, IsIndeterminateProperty, ThicknessProperty, StrokeProperty);
    }

    /// <summary>Upload progress, 0 to 1.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0, 1));
    }

    /// <summary>True while no progress value has been reported; the arc circles on its own.</summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Arc colour; defaults to the accent token.</summary>
    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty)
        {
            UpdateTimer();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
    }

    private void UpdateTimer()
    {
        if (!IsIndeterminate || VisualRoot is null)
        {
            _timer?.Stop();
            _timer = null;
            InvalidateVisual();
            return;
        }

        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + (ArcThemeManager.ReducedMotion ? 1.5 : 6)) % 360;
            InvalidateVisual();
        };
        _timer.Start();
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var thickness = Math.Max(1, Thickness);
        var radius = Math.Max(0, size / 2 - thickness / 2);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var track = new Pen(ArcResources.Brush(ArcTokens.Border), thickness);
        context.DrawEllipse(null, track, center, radius, radius);

        var stroke = Stroke ?? ArcResources.Brush(ArcTokens.Accent);
        var pen = new Pen(stroke, thickness, null, PenLineCap.Round, PenLineJoin.Round);

        if (IsIndeterminate)
        {
            DrawArc(context, center, radius, _angle - 90, 270, pen);
            return;
        }

        var sweep = Math.Clamp(Progress, 0, 1) * 360;
        if (sweep <= 0.1)
        {
            return;
        }

        DrawArc(context, center, radius, -90, sweep, pen);
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double startDegrees, double sweepDegrees, Pen pen)
    {
        if (sweepDegrees >= 359.5)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = startDegrees * Math.PI / 180;
        var sweep = Math.Clamp(sweepDegrees, 0.1, 359.9) * Math.PI / 180;
        var from = new Point(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start));
        var to = new Point(center.X + radius * Math.Cos(start + sweep), center.Y + radius * Math.Sin(start + sweep));

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(from, false);
            figure.ArcTo(to, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
