using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksMarketing.Controls;

/// <summary>
/// A static approximation of Arc's mesh gradient: a token base wash with three soft radial blooms and no
/// external renderer. The web original uses WebGL points with grain; this draws the same colour story with
/// gradient brushes, which is honest about what a desktop XAML port can do. <see cref="Drift"/> moves the
/// blooms on a slow loop and is switched off entirely under reduced motion, where the wash draws one still
/// frame.
/// </summary>
public sealed class ArcMeshWash : Control
{
    public static readonly StyledProperty<bool> DriftProperty =
        AvaloniaProperty.Register<ArcMeshWash, bool>(nameof(Drift));

    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<ArcMeshWash, double>(nameof(Intensity), 1d);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ArcMeshWash, CornerRadius>(nameof(CornerRadius), new CornerRadius(0));

    private readonly DispatcherTimer _timer;
    private double _phase;

    static ArcMeshWash()
    {
        AffectsRender<ArcMeshWash>(DriftProperty, IntensityProperty, CornerRadiusProperty);
    }

    public ArcMeshWash()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(48) };
        _timer.Tick += (_, _) =>
        {
            if (!Drift || ArcThemeManager.ReducedMotion)
            {
                return;
            }

            _phase += 0.012;
            if (_phase > Math.PI * 2)
            {
                _phase -= Math.PI * 2;
            }

            InvalidateVisual();
        };
    }

    /// <summary>Slowly moves the blooms; ignored under reduced motion.</summary>
    public bool Drift
    {
        get => GetValue(DriftProperty);
        set => SetValue(DriftProperty, value);
    }

    /// <summary>Overall opacity of the wash, 0 to 1.</summary>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Drift && !ArcThemeManager.ReducedMotion)
        {
            _timer.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var baseWash = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(WithAlpha(ArcResources.Color(ArcTokens.GradientFrom), 0.55), 0),
                new GradientStop(WithAlpha(ArcResources.Color(ArcTokens.GradientTo), 0.42), 1)
            }
        };

        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            context.DrawRectangle(baseWash, null, bounds);

            var drift = Drift && !ArcThemeManager.ReducedMotion;
            var phase = drift ? _phase : 0;

            Bloom(context, bounds,
                new RelativePoint(0.24 + (drift ? Math.Sin(phase) * 0.05 : 0), 0.18 + (drift ? Math.Cos(phase) * 0.04 : 0), RelativeUnit.Relative),
                0.62, ArcTokens.Accent, 0.30);
            Bloom(context, bounds,
                new RelativePoint(0.82 + (drift ? Math.Cos(phase * 0.8) * 0.05 : 0), 0.24 + (drift ? Math.Sin(phase * 0.7) * 0.05 : 0), RelativeUnit.Relative),
                0.55, ArcTokens.GradientTo, 0.36);
            Bloom(context, bounds,
                new RelativePoint(0.55 + (drift ? Math.Sin(phase * 0.6) * 0.07 : 0), 0.92 + (drift ? Math.Cos(phase * 0.5) * 0.03 : 0), RelativeUnit.Relative),
                0.7, ArcTokens.GradientFrom, 0.32);
        }
    }

    private void Bloom(DrawingContext context, Rect bounds, RelativePoint center, double radius, string token, double alpha)
    {
        var color = ArcResources.Color(token);
        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(WithAlpha(color, alpha * Intensity), 0),
                new GradientStop(WithAlpha(color, alpha * Intensity * 0.45), 0.55),
                new GradientStop(WithAlpha(color, 0), 1)
            }
        };

        context.DrawRectangle(brush, null, bounds);
    }

    private static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);
}
