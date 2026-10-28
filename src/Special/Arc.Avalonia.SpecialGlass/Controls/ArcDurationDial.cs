using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>
/// The focus session dial: a draggable knob counts minutes as it travels, keeps counting across twelve
/// o'clock, resists past the ends and snaps to five minute steps on release. Keys move by five minutes,
/// Page keys by fifteen, and Home and End jump to five and one hundred and twenty.
///
/// Avalonia has no conic vector dial primitive, so the ring, the progress arc and the knob are painted with
/// arcs and ellipses in one Render pass, and the centre readout uses FormattedText. No Pro source was used.
/// </summary>
public sealed class ArcDurationDial : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcDurationDial, double>(nameof(Value), 45);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ArcDurationDial, double>(nameof(Minimum), 5);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ArcDurationDial, double>(nameof(Maximum), 120);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcDurationDial, double>(nameof(Step), 5);

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<ArcDurationDial, string>(nameof(Caption), "minutes");

    private bool _dragging;
    private bool _pointerFocus;
    private double _lastAngle;

    static ArcDurationDial()
    {
        AffectsRender<ArcDurationDial>(ValueProperty, MinimumProperty, MaximumProperty, CaptionProperty);
        ValueProperty.Changed.AddClassHandler<ArcDurationDial>((dial, _) => dial.UpdateAutomation());
    }

    public ArcDurationDial()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        Width = 164;
        Height = 164;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        KeyDown += OnKeyDown;
        GotFocus += (_, _) =>
        {
            _pointerFocus = false;
            InvalidateVisual();
        };
        LostFocus += (_, _) => InvalidateVisual();
        UpdateAutomation();
    }

    public event EventHandler<double>? ValueChanged;

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, $"Session length, {Math.Round(Value)} minutes");
        AutomationProperties.SetHelpText(this, "Dial. Drag or use the arrow keys; Page Up adds fifteen minutes.");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Min(
            double.IsInfinity(availableSize.Width) ? 164 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 164 : availableSize.Height);
        size = Math.Clamp(size, 120, 180);
        return new Size(size, size);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 10)
        {
            return;
        }

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size / 2) - 16;
        var range = Math.Max(1, Maximum - Minimum);
        var fraction = Math.Clamp((Value - Minimum) / range, 0, 1);

        var track = ArcGlass.Solid(ArcGlass.Color(ArcTokens.ControlTrack));
        var accent = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong));
        var ink = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground));
        var muted = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted));

        context.DrawEllipse(null, new Pen(track, 9), centre, radius, radius);

        var startAngle = -90.0;
        var sweep = fraction * 360.0;
        if (sweep > 0.5)
        {
            var geometry = new StreamGeometry();
            using (var stream = geometry.Open())
            {
                Point PointAt(double degrees)
                {
                    var radians = degrees * Math.PI / 180.0;
                    return new Point(centre.X + (radius * Math.Cos(radians)), centre.Y + (radius * Math.Sin(radians)));
                }

                var start = PointAt(startAngle);
                var end = PointAt(startAngle + sweep);
                stream.BeginFigure(start, false);
                stream.ArcTo(
                    end,
                    new Size(radius, radius),
                    0,
                    sweep > 180,
                    SweepDirection.Clockwise);
                stream.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(accent, 9, null, PenLineCap.Round, PenLineJoin.Round), geometry);
        }

        var knobAngle = (startAngle + sweep) * Math.PI / 180.0;
        var knob = new Point(
            centre.X + (radius * Math.Cos(knobAngle)),
            centre.Y + (radius * Math.Sin(knobAngle)));
        context.DrawEllipse(ArcGlass.Solid(ArcGlass.Color(ArcTokens.ControlThumb)), new Pen(accent, 2), knob, 9, 9);

        // A faint inner disc separates the readout from the ring.
        context.DrawEllipse(ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.16)), null, centre, radius - 12, radius - 12);

        var minutes = new FormattedText(
            $"{Math.Round(Value)}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(ArcGlass.Font, FontStyle.Normal, FontWeight.Medium),
            Math.Max(22, size * 0.20),
            ink);
        context.DrawText(minutes, new Point(centre.X - (minutes.Width / 2), centre.Y - (minutes.Height * 0.72)));

        var caption = new FormattedText(
            Caption,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(ArcGlass.Font, FontStyle.Normal, FontWeight.Normal),
            11,
            muted);
        context.DrawText(caption, new Point(centre.X - (caption.Width / 2), centre.Y + (minutes.Height * 0.30)));

        if (IsFocused && !_pointerFocus)
        {
            var ring = ArcGlass.Solid(ArcGlass.Color(ArcTokens.FocusOutline));
            context.DrawEllipse(null, new Pen(ring, 2), centre, radius + 8, radius + 8);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            InvalidateVisual();
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        _dragging = true;
        _lastAngle = AngleOf(e.GetPosition(this));
        e.Pointer.Capture(this);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var angle = AngleOf(e.GetPosition(this));
        var delta = angle - _lastAngle;
        if (delta > 180)
        {
            delta -= 360;
        }
        else if (delta < -180)
        {
            delta += 360;
        }

        _lastAngle = angle;
        var range = Math.Max(1, Maximum - Minimum);
        var candidate = Value + ((delta / 360.0) * range);
        if (candidate > Maximum)
        {
            candidate = Maximum + ((candidate - Maximum) * 0.25);
        }
        else if (candidate < Minimum)
        {
            candidate = Minimum - ((Minimum - candidate) * 0.25);
        }

        SetCurrentValue(ValueProperty, Math.Clamp(candidate, Minimum - 4, Maximum + 4));
        InvalidateVisual();
        ValueChanged?.Invoke(this, Value);
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        Snap();
    }

    private void Snap()
    {
        var step = Math.Max(1, Step);
        var snapped = Math.Round(Value / step) * step;
        snapped = Math.Clamp(snapped, Minimum, Maximum);
        SetCurrentValue(ValueProperty, snapped);
        InvalidateVisual();
        ValueChanged?.Invoke(this, snapped);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var next = Value;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                next -= Step;
                break;
            case Key.Right:
            case Key.Up:
                next += Step;
                break;
            case Key.PageDown:
                next -= Step * 3;
                break;
            case Key.PageUp:
                next += Step * 3;
                break;
            case Key.Home:
                next = Minimum;
                break;
            case Key.End:
                next = Maximum;
                break;
            default:
                return;
        }

        e.Handled = true;
        SetCurrentValue(ValueProperty, Math.Clamp(next, Minimum, Maximum));
        InvalidateVisual();
        ValueChanged?.Invoke(this, Value);
    }

    private double AngleOf(Point point)
    {
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var degrees = Math.Atan2(point.Y - centre.Y, point.X - centre.X) * 180.0 / Math.PI;
        return (degrees + 90 + 360) % 360;
    }
}

