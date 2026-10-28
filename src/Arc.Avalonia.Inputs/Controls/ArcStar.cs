using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// One star in the rating row. It fills left to right so half stars read accurately, and resolves its
/// brushes from the shared tokens on every render so theme and accent swaps are reflected.
/// </summary>
internal sealed class ArcStar : Control
{
    public static readonly StyledProperty<double> FillProperty =
        AvaloniaProperty.Register<ArcStar, double>(nameof(Fill));

    private static Geometry? _geometry;

    static ArcStar()
    {
        AffectsRender<ArcStar>(FillProperty);
    }

    public double Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(24, 24);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var geometry = _geometry ??= BuildGeometry();
        var scale = size / 24.0 * 0.92;
        var offset = (size - 24 * scale) / 2;
        var fill = Math.Clamp(Fill, 0, 1);
        var accent = ArcResources.Brush(ArcTokens.Accent);
        var outline = ArcResources.Brush(fill > 0 ? ArcTokens.Accent : ArcTokens.BorderStrong);
        var pen = new Pen(outline, 1.75 / scale, null, PenLineCap.Round, PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateTranslation(offset, offset) * Matrix.CreateScale(scale, scale)))
        {
            if (fill > 0)
            {
                using (context.PushClip(new Rect(0, 0, 24 * fill, 24)))
                {
                    context.DrawGeometry(accent, null, geometry);
                }
            }

            context.DrawGeometry(null, pen, geometry);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is { } app)
        {
            app.ResourcesChanged += OnResourcesChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } app)
        {
            app.ResourcesChanged -= OnResourcesChanged;
        }
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();

    private static Geometry BuildGeometry()
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        for (var i = 0; i < 10; i++)
        {
            var radius = i % 2 == 0 ? 10.2 : 4.3;
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var point = new Point(12 + radius * Math.Cos(angle), 12 + radius * Math.Sin(angle));
            if (i == 0)
            {
                context.BeginFigure(point, isFilled: true);
            }
            else
            {
                context.LineTo(point);
            }
        }

        context.EndFigure(true);
        return geometry;
    }
}
