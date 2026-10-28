using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// The saturation and brightness square of the colour picker: a 2D slider that follows the pointer and
/// the arrow keys. Drawn rather than composed so the hue wash stays exact at every size.
/// </summary>
public sealed class ArcColorArea : Control
{
    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ArcColorArea, double>(nameof(Hue), 210);

    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<ArcColorArea, double>(nameof(Saturation), 1);

    public static readonly StyledProperty<double> BrightnessProperty =
        AvaloniaProperty.Register<ArcColorArea, double>(nameof(Brightness), 1);

    static ArcColorArea()
    {
        AffectsRender<ArcColorArea>(HueProperty, SaturationProperty, BrightnessProperty);
        FocusableProperty.OverrideDefaultValue<ArcColorArea>(true);
        IsTabStopProperty.OverrideDefaultValue<ArcColorArea>(true);
    }

    public ArcColorArea()
    {
        AutomationProperties.SetName(this, "Saturation and brightness");
        AutomationProperties.SetHelpText(this, "Arrow keys move the colour thumb; Shift steps ten times further.");
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double Brightness
    {
        get => GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    public event EventHandler? ValueChanged;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var radius = Math.Min(14, bounds.Height / 2);
        var rounded = new RoundedRect(bounds, radius);
        var hue = new SolidColorBrush(new ArcHsv(Hue, 1, 1, 1).ToColor());

        using (context.PushClip(rounded))
        {
            context.DrawRectangle(hue, null, bounds);

            var white = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(255, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                }
            };
            context.DrawRectangle(white, null, bounds);

            var black = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(255, 0, 0, 0), 1)
                }
            };
            context.DrawRectangle(black, null, bounds);
        }

        context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.Border), 1), rounded);

        var x = Math.Clamp(Saturation, 0, 1) * bounds.Width;
        var y = (1 - Math.Clamp(Brightness, 0, 1)) * bounds.Height;
        var center = new Point(Math.Clamp(x, 8, Math.Max(8, bounds.Width - 8)), Math.Clamp(y, 8, Math.Max(8, bounds.Height - 8)));
        context.DrawEllipse(null, new Pen(ArcResources.Brush(ArcTokens.BorderStrong), 3.5), center, 8, 8);
        context.DrawEllipse(null, new Pen(ArcResources.Brush(ArcTokens.Surface), 2), center, 8, 8);

        if (ShowFocusRing)
        {
            var ring = new Rect(1, 1, bounds.Width - 2, bounds.Height - 2);
            context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2), ring, Math.Max(0, radius - 1));
        }
    }

    internal bool ShowFocusRing { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        _pointerFocus = true;
        Dispatcher.UIThread.Post(() => _pointerFocus = false);
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured != this)
        {
            return;
        }

        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
    }

    private bool _pointerFocus;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ShowFocusRing = true;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.1 : 0.01;
        var saturation = Saturation;
        var brightness = Brightness;

        switch (e.Key)
        {
            case Key.Left:
                saturation -= step;
                break;
            case Key.Right:
                saturation += step;
                break;
            case Key.Up:
                brightness += step;
                break;
            case Key.Down:
                brightness -= step;
                break;
            case Key.PageUp:
                brightness += 0.1;
                break;
            case Key.PageDown:
                brightness -= 0.1;
                break;
            case Key.Home:
                saturation = 0;
                break;
            case Key.End:
                saturation = 1;
                break;
            default:
                return;
        }

        SetCurrentValue(SaturationProperty, Math.Clamp(saturation, 0, 1));
        SetCurrentValue(BrightnessProperty, Math.Clamp(brightness, 0, 1));
        ValueChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        ShowFocusRing = !_pointerFocus;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ShowFocusRing = false;
        InvalidateVisual();
    }

    private void UpdateFromPoint(Point point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var saturation = Math.Clamp(point.X / Bounds.Width, 0, 1);
        var brightness = 1 - Math.Clamp(point.Y / Bounds.Height, 0, 1);
        if (Math.Abs(saturation - Saturation) < 0.0005 && Math.Abs(brightness - Brightness) < 0.0005)
        {
            return;
        }

        SetCurrentValue(SaturationProperty, saturation);
        SetCurrentValue(BrightnessProperty, brightness);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }
}

/// <summary>Which ramp a <see cref="ArcColorRamp"/> draws.</summary>
public enum ArcColorRampMode
{
    Hue,
    Alpha
}

/// <summary>A hue or opacity ramp: a thin gradient track with a draggable thumb and keyboard stepping.</summary>
public sealed class ArcColorRamp : Control
{
    public static readonly StyledProperty<ArcColorRampMode> ModeProperty =
        AvaloniaProperty.Register<ArcColorRamp, ArcColorRampMode>(nameof(Mode));

    public static readonly StyledProperty<double> HueProperty =
        AvaloniaProperty.Register<ArcColorRamp, double>(nameof(Hue), 210);

    public static readonly StyledProperty<double> AlphaProperty =
        AvaloniaProperty.Register<ArcColorRamp, double>(nameof(Alpha), 1);

    public static readonly StyledProperty<Color> RampColorProperty =
        AvaloniaProperty.Register<ArcColorRamp, Color>(nameof(RampColor), Colors.Blue);

    static ArcColorRamp()
    {
        AffectsRender<ArcColorRamp>(ModeProperty, HueProperty, AlphaProperty, RampColorProperty);
        FocusableProperty.OverrideDefaultValue<ArcColorRamp>(true);
        IsTabStopProperty.OverrideDefaultValue<ArcColorRamp>(true);
    }

    public ArcColorRamp()
    {
        Height = 20;
        AutomationProperties.SetName(this, "Hue");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            AutomationProperties.SetName(this, Mode == ArcColorRampMode.Hue ? "Hue" : "Opacity");
        }
    }

    public ArcColorRampMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Hue
    {
        get => GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public double Alpha
    {
        get => GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public Color RampColor
    {
        get => GetValue(RampColorProperty);
        set => SetValue(RampColorProperty, value);
    }

    public event EventHandler? ValueChanged;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var track = new Rect(0, (Bounds.Height - 12) / 2, width, 12);
        var rounded = new RoundedRect(track, 6);

        using (context.PushClip(rounded))
        {
            if (Mode == ArcColorRampMode.Hue)
            {
                var hue = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
                };
                for (var stop = 0; stop <= 6; stop++)
                {
                    var angle = stop * 60;
                    hue.GradientStops.Add(new GradientStop(new ArcHsv(angle, 1, 1, 1).ToColor(), stop / 6.0));
                }

                context.DrawRectangle(hue, null, track);
            }
            else
            {
                var surface = ArcResources.Brush(ArcTokens.Surface);
                var checker = ArcResources.Brush(ArcTokens.SurfaceMuted);
                context.DrawRectangle(surface, null, track);
                const double cell = 6;
                for (var row = 0; row * cell < track.Height; row++)
                {
                    for (var column = 0; column * cell < track.Width; column++)
                    {
                        if ((row + column) % 2 == 0)
                        {
                            continue;
                        }

                        var square = new Rect(
                            track.X + column * cell,
                            track.Y + row * cell,
                            Math.Min(cell, track.Width - column * cell),
                            Math.Min(cell, track.Height - row * cell));
                        context.DrawRectangle(checker, null, square);
                    }
                }

                var opaque = Color.FromArgb(255, RampColor.R, RampColor.G, RampColor.B);
                var transparent = Color.FromArgb(0, RampColor.R, RampColor.G, RampColor.B);
                var alpha = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(transparent, 0), new GradientStop(opaque, 1) }
                };
                context.DrawRectangle(alpha, null, track);
            }
        }

        context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.Border), 1), rounded);

        var position = Mode == ArcColorRampMode.Hue
            ? Math.Clamp(Hue, 0, 360) / 360
            : Math.Clamp(Alpha, 0, 1);
        var x = Math.Clamp(position * width, 8, Math.Max(8, width - 8));
        var center = new Point(x, Bounds.Height / 2);
        context.DrawEllipse(ArcResources.Brush(ArcTokens.Surface), new Pen(ArcResources.Brush(ArcTokens.BorderStrong), 1), center, 8, 8);

        if (ShowFocusRing)
        {
            context.DrawEllipse(null, new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2), center, 11, 11);
        }
    }

    internal bool ShowFocusRing { get; set; }

    private bool _pointerFocus;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        Dispatcher.UIThread.Post(() => _pointerFocus = false);
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured != this)
        {
            return;
        }

        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ShowFocusRing = true;
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = Mode == ArcColorRampMode.Hue ? (large ? 10 : 1) : (large ? 0.1 : 0.01);
        var value = Mode == ArcColorRampMode.Hue ? Hue : Alpha;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                value -= step;
                break;
            case Key.Right:
            case Key.Up:
                value += step;
                break;
            case Key.PageDown:
                value -= Mode == ArcColorRampMode.Hue ? 10 : 0.1;
                break;
            case Key.PageUp:
                value += Mode == ArcColorRampMode.Hue ? 10 : 0.1;
                break;
            case Key.Home:
                value = 0;
                break;
            case Key.End:
                value = Mode == ArcColorRampMode.Hue ? 360 : 1;
                break;
            default:
                return;
        }

        if (Mode == ArcColorRampMode.Hue)
        {
            SetCurrentValue(HueProperty, Math.Clamp(value, 0, 360));
        }
        else
        {
            SetCurrentValue(AlphaProperty, Math.Clamp(value, 0, 1));
        }

        ValueChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        ShowFocusRing = !_pointerFocus;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ShowFocusRing = false;
        InvalidateVisual();
    }

    private void UpdateFromPoint(Point point)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var position = Math.Clamp(point.X / Bounds.Width, 0, 1);
        if (Mode == ArcColorRampMode.Hue)
        {
            SetCurrentValue(HueProperty, position * 360);
        }
        else
        {
            SetCurrentValue(AlphaProperty, position);
        }

        ValueChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }
}
