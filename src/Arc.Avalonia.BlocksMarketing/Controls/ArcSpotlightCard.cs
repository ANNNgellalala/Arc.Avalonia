using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksMarketing.Controls;

/// <summary>
/// A card whose light follows the pointer: a soft radial glow in the accent colour tracks the last pointer
/// position and fades out when the pointer leaves. The glow is drawn under the card's content, so text keeps
/// its contrast, and it is a gradient brush rather than a blur filter, which keeps the frame cheap and
/// deterministic. Keyboard focus draws a 2 px accent border instead of the pointer glow.
/// </summary>
public sealed class ArcSpotlightCard : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, Thickness>(nameof(BorderThickness), new Thickness(1));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, CornerRadius>(nameof(CornerRadius), new CornerRadius(20));

    public static readonly StyledProperty<double> SpotRadiusProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, double>(nameof(SpotRadius), 150d);

    public static readonly StyledProperty<double> SpotOpacityProperty =
        AvaloniaProperty.Register<ArcSpotlightCard, double>(nameof(SpotOpacity), 0.3d);

    private Point? _spot;
    private bool _pressed;

    static ArcSpotlightCard()
    {
        AffectsRender<ArcSpotlightCard>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty,
            CornerRadiusProperty, SpotRadiusProperty, SpotOpacityProperty);
        AffectsMeasure<ArcSpotlightCard>(BorderThicknessProperty);
    }

    public ArcSpotlightCard()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Focusable = true;
        Background = ArcResources.Brush(ArcTokens.Surface);
        BorderBrush = ArcResources.Brush(ArcTokens.Border);
        PointerMoved += OnPointerMoved;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        GotFocus += (_, _) => BorderBrush = ArcResources.Brush(ArcTokens.Accent);
        LostFocus += (_, _) => BorderBrush = ArcResources.Brush(ArcTokens.Border);
    }

    /// <summary>Invoked by a click, Enter or Space.</summary>
    public Action? Invoke { get; set; }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double SpotRadius
    {
        get => GetValue(SpotRadiusProperty);
        set => SetValue(SpotRadiusProperty, value);
    }

    public double SpotOpacity
    {
        get => GetValue(SpotOpacityProperty);
        set => SetValue(SpotOpacityProperty, value);
    }

    /// <summary>Accessible name of the card.</summary>
    public string? Label
    {
        get => AutomationProperties.GetName(this);
        set => AutomationProperties.SetName(this, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        if (Child is null)
        {
            return new Size(padding.Left + padding.Right, padding.Top + padding.Bottom);
        }

        Child.Measure(new Size(
            Math.Max(0, availableSize.Width - padding.Left - padding.Right),
            Math.Max(0, availableSize.Height - padding.Top - padding.Bottom)));

        return new Size(
            Child.DesiredSize.Width + padding.Left + padding.Right,
            Child.DesiredSize.Height + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not null)
        {
            var padding = Padding;
            Child.Arrange(new Rect(
                padding.Left,
                padding.Top,
                Math.Max(0, finalSize.Width - padding.Left - padding.Right),
                Math.Max(0, finalSize.Height - padding.Top - padding.Bottom)));
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            if (Background is { } background)
            {
                context.DrawRectangle(background, null, bounds, CornerRadius.TopLeft, CornerRadius.TopLeft);
            }

            if (_spot is { } spot)
            {
                var accent = ArcResources.Color(ArcTokens.Accent);
                var glow = new RadialGradientBrush
                {
                    Center = new RelativePoint(spot.X / Bounds.Width, spot.Y / Bounds.Height, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(spot.X / Bounds.Width, spot.Y / Bounds.Height, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(SpotRadius, RelativeUnit.Absolute),
                    RadiusY = new RelativeScalar(SpotRadius, RelativeUnit.Absolute),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)Math.Clamp(SpotOpacity * 255, 0, 255), accent.R, accent.G, accent.B), 0),
                        new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1)
                    }
                };
                context.DrawRectangle(glow, null, bounds);
            }

            if (BorderBrush is { } border && BorderThickness.Left > 0)
            {
                var pen = new Pen(border, BorderThickness.Left);
                context.DrawRectangle(null, pen, Deflate(bounds, BorderThickness.Left / 2), CornerRadius.TopLeft,
                    CornerRadius.TopLeft);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space)
        {
            Invoke?.Invoke();
            e.Handled = true;
        }
    }

    private static Rect Deflate(Rect rect, double amount) =>
        new(rect.X + amount, rect.Y + amount, Math.Max(0, rect.Width - (amount * 2)),
            Math.Max(0, rect.Height - (amount * 2)));

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        _spot = e.GetPosition(this);
        InvalidateVisual();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        BorderBrush = ArcResources.Brush(ArcTokens.BorderStrong);
        _spot = e.GetPosition(this);
        InvalidateVisual();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        BorderBrush = ArcResources.Brush(ArcTokens.Border);
        _spot = null;
        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressed && Bounds.Contains(e.GetPosition(this)))
        {
            Invoke?.Invoke();
        }

        _pressed = false;
    }
}

