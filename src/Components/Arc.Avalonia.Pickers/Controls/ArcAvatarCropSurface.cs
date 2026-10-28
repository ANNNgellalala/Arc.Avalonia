using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// The in-place crop stage: the picked photo under a circular mask with the overflow dimmed, pointer drag to
/// pan and wheel to zoom towards the pointer. Desktop has no pinch gesture, so two-finger zoom is replaced by
/// the wheel, the zoom slider and the plus/minus keys. Arrow keys nudge the photo, 0 resets, Enter confirms
/// and Escape cancels.
/// </summary>
public sealed class ArcAvatarCropSurface : Control
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, IImage?>(nameof(Image));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, double>(nameof(Zoom), 1d);

    public static readonly StyledProperty<double> OffsetXProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, double>(nameof(OffsetX));

    public static readonly StyledProperty<double> OffsetYProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, double>(nameof(OffsetY));

    public static readonly StyledProperty<double> StageSizeProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, double>(nameof(StageSize), 248d);

    public static readonly StyledProperty<double> CropDiameterProperty =
        AvaloniaProperty.Register<ArcAvatarCropSurface, double>(nameof(CropDiameter), 184d);

    private bool _dragging;
    private bool _clamping;
    private Point _lastPoint;

    static ArcAvatarCropSurface()
    {
        AffectsRender<ArcAvatarCropSurface>(ImageProperty, ZoomProperty, OffsetXProperty, OffsetYProperty, CropDiameterProperty);
        AffectsMeasure<ArcAvatarCropSurface>(StageSizeProperty);
        FocusableProperty.OverrideDefaultValue<ArcAvatarCropSurface>(true);
        IsTabStopProperty.OverrideDefaultValue<ArcAvatarCropSurface>(true);
    }

    public ArcAvatarCropSurface()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(
            this,
            "Crop photo. Drag to reposition, plus and minus to zoom, 0 to reset, Enter to save, Escape to cancel.");
        AutomationProperties.SetHelpText(
            this,
            "Arrow keys move the photo by two percent of the circle, hold Shift for ten percent.");
    }

    /// <summary>The photo being cropped.</summary>
    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    /// <summary>Crop scale, 1 fits the circle and 4 is the closest zoom.</summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, Math.Clamp(value, 1, 4));
    }

    /// <summary>Horizontal photo offset in stage pixels.</summary>
    public double OffsetX
    {
        get => GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    /// <summary>Vertical photo offset in stage pixels.</summary>
    public double OffsetY
    {
        get => GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    /// <summary>Side of the crop stage square.</summary>
    public double StageSize
    {
        get => GetValue(StageSizeProperty);
        set => SetValue(StageSizeProperty, value);
    }

    /// <summary>Diameter of the circular mask inside the stage.</summary>
    public double CropDiameter
    {
        get => GetValue(CropDiameterProperty);
        set => SetValue(CropDiameterProperty, value);
    }

    /// <summary>Raised when the zoom or the position changes, so a host can sync its slider.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>Enter in the crop area; the host saves and uploads.</summary>
    public event EventHandler? ConfirmRequested;

    /// <summary>Escape in the crop area; the host returns to the current photo.</summary>
    public event EventHandler? CancelRequested;

    protected override Size MeasureOverride(Size availableSize) => new(StageSize, StageSize);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomProperty || change.Property == ImageProperty)
        {
            ClampOffsets();
        }
        else if (change.Property == OffsetXProperty || change.Property == OffsetYProperty)
        {
            if (!_clamping)
            {
                ClampOffsets();
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        _dragging = true;
        _lastPoint = e.GetPosition(this);
        e.Pointer.Capture(this);
        Focus();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var point = e.GetPosition(this);
        var delta = point - _lastPoint;
        _lastPoint = point;

        SetCurrentValue(OffsetXProperty, OffsetX + delta.X);
        SetCurrentValue(OffsetYProperty, OffsetY + delta.Y);
        ClampOffsets();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Image is null)
        {
            return;
        }

        var factor = e.Delta.Y > 0 ? 1.12 : 1 / 1.12;
        ZoomTo(Math.Clamp(Zoom * factor, 1, 4), e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Enter:
                ConfirmRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case Key.Escape:
                CancelRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
            {
                var step = Math.Max(1, EffectiveDiameter() * (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.10 : 0.02));
                SetCurrentValue(OffsetXProperty, OffsetX + (e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0));
                SetCurrentValue(OffsetYProperty, OffsetY + (e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0));
                ClampOffsets();
                ViewChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            }

            case Key.OemPlus:
            case Key.Add:
                ZoomTo(Math.Clamp(Zoom * 1.12, 1, 4), Bounds.Center);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomTo(Math.Clamp(Zoom / 1.12, 1, 4), Bounds.Center);
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                ResetView();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Zooms towards a point in stage coordinates, or the stage centre when omitted.</summary>
    public void ZoomTo(double zoom, Point? anchor = null)
    {
        if (Image is null)
        {
            SetCurrentValue(ZoomProperty, Math.Clamp(zoom, 1, 4));
            return;
        }

        var target = Math.Clamp(zoom, 1, 4);
        if (Math.Abs(target - Zoom) < 0.0001)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var point = anchor ?? center;
        var diameter = EffectiveDiameter();
        var before = Metrics(Image, diameter, Zoom);
        var imageX = (point.X - center.X - OffsetX) / before.Scale;
        var imageY = (point.Y - center.Y - OffsetY) / before.Scale;
        var after = Metrics(Image, diameter, target);

        SetCurrentValue(ZoomProperty, target);
        SetCurrentValue(OffsetXProperty, point.X - center.X - imageX * after.Scale);
        SetCurrentValue(OffsetYProperty, point.Y - center.Y - imageY * after.Scale);
        ClampOffsets();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Back to zoom 1 and centre.</summary>
    public void ResetView()
    {
        SetCurrentValue(ZoomProperty, 1d);
        SetCurrentValue(OffsetXProperty, 0d);
        SetCurrentValue(OffsetYProperty, 0d);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Renders the circle's contents as an opaque square bitmap of the requested side.</summary>
    public RenderTargetBitmap? RenderCrop(int outputSize)
    {
        if (Image is null || outputSize <= 0)
        {
            return null;
        }

        var diameter = EffectiveDiameter();
        var metrics = Metrics(Image, diameter, Zoom);
        var scale = outputSize / diameter;

        var bitmap = new RenderTargetBitmap(new PixelSize(outputSize, outputSize), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(ArcResources.Brush(ArcTokens.SurfaceMuted), null, new Rect(0, 0, outputSize, outputSize));
            using (context.PushTransform(Matrix.CreateTranslation(
                       outputSize / 2.0 + OffsetX * scale,
                       outputSize / 2.0 + OffsetY * scale)))
            using (context.PushTransform(Matrix.CreateScale(metrics.Scale * scale, metrics.Scale * scale)))
            {
                var size = Image.Size;
                context.DrawImage(Image, new Rect(-size.Width / 2, -size.Height / 2, size.Width, size.Height));
            }
        }

        return bitmap;
    }

    /// <summary>Scale, pan bounds and the base fit for a photo inside a crop circle.</summary>
    public static (double BaseScale, double Scale, double MaxX, double MaxY) Metrics(IImage image, double diameter, double zoom)
    {
        var width = Math.Max(1, image.Size.Width);
        var height = Math.Max(1, image.Size.Height);
        var baseScale = Math.Max(diameter / width, diameter / height);
        var scale = baseScale * Math.Clamp(zoom, 1, 4);
        var maxX = Math.Max(0, (width * scale - diameter) / 2);
        var maxY = Math.Max(0, (height * scale - diameter) / 2);
        return (baseScale, scale, maxX, maxY);
    }

    private double EffectiveDiameter()
    {
        var available = Math.Min(Bounds.Width, Bounds.Height);
        if (available <= 0)
        {
            available = StageSize;
        }

        return Math.Max(48, Math.Min(CropDiameter, available));
    }

    private void ClampOffsets()
    {
        if (Image is null || _clamping)
        {
            return;
        }

        var metrics = Metrics(Image, EffectiveDiameter(), Zoom);
        var x = Math.Clamp(OffsetX, -metrics.MaxX, metrics.MaxX);
        var y = Math.Clamp(OffsetY, -metrics.MaxY, metrics.MaxY);
        if (Math.Abs(x - OffsetX) < 0.01 && Math.Abs(y - OffsetY) < 0.01)
        {
            return;
        }

        _clamping = true;
        try
        {
            SetCurrentValue(OffsetXProperty, x);
            SetCurrentValue(OffsetYProperty, y);
        }
        finally
        {
            _clamping = false;
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var center = bounds.Center;
        var diameter = EffectiveDiameter();
        var radius = diameter / 2;
        var circle = new Rect(center.X - radius, center.Y - radius, diameter, diameter);

        context.DrawRectangle(ArcResources.Brush(ArcTokens.SurfaceMuted), null, bounds);

        if (Image is { } image)
        {
            var metrics = Metrics(image, diameter, Zoom);
            using (context.PushTransform(Matrix.CreateTranslation(center.X + OffsetX, center.Y + OffsetY)))
            using (context.PushTransform(Matrix.CreateScale(metrics.Scale, metrics.Scale)))
            {
                var size = image.Size;
                context.DrawImage(image, new Rect(-size.Width / 2, -size.Height / 2, size.Width, size.Height));
            }
        }

        // Dim everything outside the crop circle; the overflow brightens while the photo is being dragged.
        var scrim = ArcResources.Color(ArcTokens.Scrim);
        var alpha = _dragging ? 0.62 : 1.0;
        var dimBrush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Clamp(scrim.A * alpha, 0, 255),
            scrim.R,
            scrim.G,
            scrim.B));

        var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };
        mask.Children.Add(new RectangleGeometry(bounds));
        mask.Children.Add(new EllipseGeometry(circle));
        context.DrawGeometry(dimBrush, null, mask);

        var ring = new Pen(ArcResources.Brush(ArcTokens.BorderStrong), 1);
        context.DrawEllipse(null, ring, center, radius, radius);
    }
}

