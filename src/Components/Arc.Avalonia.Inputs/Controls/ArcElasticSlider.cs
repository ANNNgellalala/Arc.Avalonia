using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// Arc's elastic slider: rubber-band travel past either end, a value tag that sways with the drag,
/// detents the thumb clicks into, and arrow, Page and Home/End support.
/// </summary>
public class ArcElasticSlider : Control
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcElasticSlider, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcElasticSlider, double>(nameof(Value), 50d);

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<ArcElasticSlider, double>(nameof(Min), 0d);

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcElasticSlider, double>(nameof(Max), 100d);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcElasticSlider, double>(nameof(Step), 1d);

    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<ArcElasticSlider, double>(nameof(LargeStep), double.NaN);

    public static readonly StyledProperty<IList<ArcSliderMark>?> MarksProperty =
        AvaloniaProperty.Register<ArcElasticSlider, IList<ArcSliderMark>?>(nameof(Marks));

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<ArcElasticSlider, bool>(nameof(ShowValue), true);

    public static readonly StyledProperty<string?> StartIconProperty =
        AvaloniaProperty.Register<ArcElasticSlider, string?>(nameof(StartIcon));

    public static readonly StyledProperty<string?> EndIconProperty =
        AvaloniaProperty.Register<ArcElasticSlider, string?>(nameof(EndIcon));

    private const double HeaderHeight = 22;
    private const double TrackCenter = 44;
    private const double ThumbRadius = 10;

    private double _display;
    private bool _dragging;
    private bool _pointerFocus;
    private double _strain;
    private double _tagAngle;
    private double _velocity;
    private double _lastPointerX;

    static ArcElasticSlider()
    {
        AffectsRender<ArcElasticSlider>(
            LabelProperty, ValueProperty, MinProperty, MaxProperty, StepProperty,
            MarksProperty, ShowValueProperty, StartIconProperty, EndIconProperty);
        AffectsMeasure<ArcElasticSlider>(MarksProperty);
        ValueProperty.Changed.AddClassHandler<ArcElasticSlider>((slider, _) => slider.OnValueChanged());
        LabelProperty.Changed.AddClassHandler<ArcElasticSlider>((slider, _) => slider.UpdateAutomation());
        MarksProperty.Changed.AddClassHandler<ArcElasticSlider>((slider, _) => slider.InvalidateMeasure());
    }

    public ArcElasticSlider()
    {
        Focusable = true;
        _display = Value;
    }

    /// <summary>Raised on every change while dragging or pressing keys.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Raised once a drag is released or a key changes the value.</summary>
    public event EventHandler? ValueCommitted;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Min
    {
        get => GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public double Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>Distance for Page keys and Shift with an arrow; defaults to a tenth of the range.</summary>
    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    /// <summary>Detents the thumb clicks into; labelled marks print under the track.</summary>
    public IList<ArcSliderMark>? Marks
    {
        get => GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>Lucide icon drawn before the track; it is pushed outward past the start.</summary>
    public string? StartIcon
    {
        get => GetValue(StartIconProperty);
        set => SetValue(StartIconProperty, value);
    }

    /// <summary>Lucide icon drawn after the track; it is pushed outward past the end.</summary>
    public string? EndIcon
    {
        get => GetValue(EndIconProperty);
        set => SetValue(EndIconProperty, value);
    }

    /// <summary>Formats the readout, the tag and the spoken value.</summary>
    public Func<double, string>? Format { get; set; }

    private double EffectiveLargeStep =>
        double.IsNaN(LargeStep) || LargeStep <= 0 ? (Max - Min) / 10 : LargeStep;

    private bool HasLabels => Marks?.Any(mark => !string.IsNullOrEmpty(mark.Label)) == true;

    private double TotalHeight => (HasLabels ? 68 : 58) + 20;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
        return new Size(width, TotalHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var foreground = ArcResources.Brush(ArcTokens.Foreground);
        var secondary = ArcResources.Brush(ArcTokens.TextSecondary);
        var muted = ArcResources.Brush(ArcTokens.TextMuted);
        var track = ArcResources.Brush(ArcTokens.ControlTrack);
        var accent = ArcResources.Brush(ArcTokens.Accent);
        var surface = ArcResources.Brush(ArcTokens.Surface);
        var border = ArcResources.Brush(ArcTokens.BorderStrong);

        if (!string.IsNullOrEmpty(Label))
        {
            var label = ArcText.Create(Label!, 14, foreground, FontWeight.Medium);
            ArcDraw.Text(context, label, 0, HeaderHeight / 2 + 1);
        }

        if (ShowValue)
        {
            var readout = ArcText.Create(FormatValue(Value), 14, secondary);
            ArcDraw.Text(context, readout, Bounds.Width, HeaderHeight / 2 + 1, alignRight: true);
        }

        var startInset = 12d + (string.IsNullOrEmpty(StartIcon) ? 0 : 24);
        var endInset = 12d + (string.IsNullOrEmpty(EndIcon) ? 0 : 24);
        var x0 = startInset;
        var x1 = Math.Max(x0 + 1, Bounds.Width - endInset);
        var usable = x1 - x0;

        if (!string.IsNullOrEmpty(StartIcon))
        {
            ArcDraw.Icon(context, StartIcon, new Rect(-_strain, TrackCenter - 8, 16, 16), muted);
        }

        if (!string.IsNullOrEmpty(EndIcon))
        {
            ArcDraw.Icon(context, EndIcon, new Rect(Bounds.Width - 16 + _strain, TrackCenter - 8, 16, 16), muted);
        }

        var trackRect = new Rect(x0 - Math.Max(0, -_strain), TrackCenter - 3, usable + Math.Abs(_strain), 6);
        ArcDraw.Pill(context, trackRect, track);

        var fraction = Fraction(Clamp(_display));
        var thumbX = x0 + usable * fraction + _strain;

        if (thumbX > x0 + 0.5)
        {
            ArcDraw.Pill(context, new Rect(x0, TrackCenter - 3, Math.Max(0, thumbX - x0), 6), accent);
        }

        DrawMarks(context, x0, usable, track, muted);

        // The thumb swells under the finger and keeps a hairline edge on every theme.
        var radius = _dragging ? ThumbRadius * 1.28 : ThumbRadius;
        context.DrawEllipse(surface, new Pen(border, 1.5), new Point(thumbX, TrackCenter), radius, radius);

        if (_dragging || IsFocused)
        {
            DrawTag(context, thumbX, FormatValue(_display), foreground, ArcResources.Brush(ArcTokens.Background));
        }

        if (IsFocused && !_pointerFocus)
        {
            ArcDraw.Stroke(context, new Rect(x0 - 8, TrackCenter - 18, usable + 16, 36), 12,
                ArcResources.Brush(ArcTokens.FocusOutline), 2);
        }
    }

    private void DrawMarks(DrawingContext context, double x0, double usable, IBrush track, IBrush muted)
    {
        var marks = Marks;
        if (marks is null || marks.Count == 0)
        {
            return;
        }

        foreach (var mark in marks)
        {
            var x = x0 + usable * Fraction(Clamp(mark.Value));
            context.DrawEllipse(track, null, new Point(x, TrackCenter), 2, 2);
            if (!string.IsNullOrEmpty(mark.Label))
            {
                var text = ArcText.Create(mark.Label!, 12, muted);
                var offset = x < 24 ? 0 : x > Bounds.Width - 24 ? -text.Width : -text.Width / 2;
                ArcDraw.Text(context, text, x + offset, TrackCenter + 22);
                context.DrawEllipse(track, null, new Point(x, TrackCenter), 2.5, 2.5);
            }
        }
    }

    private void DrawTag(DrawingContext context, double thumbX, string text, IBrush fill, IBrush ink)
    {
        var formatted = ArcText.Create(text, 12, ink);
        var width = formatted.Width + 16;
        var height = 22d;
        var x = Math.Clamp(thumbX - width / 2, 0, Math.Max(0, Bounds.Width - width));
        var y = TrackCenter - ThumbRadius - height - 6;
        var rect = new Rect(x, y, width, height);

        var centerX = x + width / 2;
        var centerY = y + height / 2;
        using (context.PushTransform(Matrix.CreateRotation(_tagAngle * Math.PI / 180) *
                                     Matrix.CreateTranslation(centerX, centerY) *
                                     Matrix.CreateTranslation(-centerX, -centerY)))
        {
            ArcDraw.Pill(context, rect, fill);
            ArcDraw.Text(context, formatted, x + 8, centerY);
        }
    }

    private double Fraction(double value)
    {
        var range = Math.Max(0.0000001, Max - Min);
        return Math.Clamp((value - Min) / range, 0, 1);
    }

    private double Clamp(double value) => Math.Clamp(value, Min, Max);

    private string FormatValue(double value)
        => Format?.Invoke(value) ?? ArcFormat.Number(value, Decimals(Step));

    private static int Decimals(double step)
    {
        var decimals = 0;
        var value = Math.Abs(step);
        while (decimals < 3 && value > 0 && Math.Abs(value - Math.Round(value)) > 0.000001)
        {
            value *= 10;
            decimals++;
        }

        return decimals;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        _dragging = true;
        e.Pointer.Capture(this);
        _lastPointerX = e.GetPosition(this).X;
        SetFromPointer(_lastPointerX);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        _velocity = x - _lastPointerX;
        _lastPointerX = x;
        SetFromPointer(x);

        var target = Math.Clamp(-_velocity * 0.8, -8, 8);
        _tagAngle += (target - _tagAngle) * 0.5;
        InvalidateVisual();
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
        SpringStrain();
        SpringTag();
        ValueCommitted?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        SpringStrain();
        SpringTag();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled)
        {
            return;
        }

        _pointerFocus = false;
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = large ? EffectiveLargeStep : Step;

        switch (e.Key)
        {
            case Key.Right:
            case Key.Up:
                e.Handled = true;
                SetValueAnimated(Value + step);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
            case Key.Left:
            case Key.Down:
                e.Handled = true;
                SetValueAnimated(Value - step);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
            case Key.PageUp:
                e.Handled = true;
                SetValueAnimated(Value + EffectiveLargeStep);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
            case Key.PageDown:
                e.Handled = true;
                SetValueAnimated(Value - EffectiveLargeStep);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
            case Key.Home:
                e.Handled = true;
                SetValueAnimated(Min);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
            case Key.End:
                e.Handled = true;
                SetValueAnimated(Max);
                ValueCommitted?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == IsEnabledProperty)
        {
            Opacity = IsEnabled ? 1 : 0.5;
        }
    }

    private void OnValueChanged()
    {
        UpdateAutomation();
        if (_dragging)
        {
            _display = Value;
            InvalidateVisual();
            return;
        }

        SetDisplayAnimated(Value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetValueAnimated(double value)
    {
        value = Snap(Clamp(value));
        if (Math.Abs(value - Value) < 0.0000001)
        {
            return;
        }

        SetCurrentValue(ValueProperty, value);
        SetDisplayAnimated(value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetDisplayAnimated(double value)
    {
        var from = _display;
        ArcAnim.To(from, value, ArcAnim.SnappyDuration, ArcAnim.Snappy, step =>
        {
            _display = step;
            InvalidateVisual();
        });
    }

    private double Snap(double value)
    {
        if (Step <= 0)
        {
            return value;
        }

        var steps = Math.Round((value - Min) / Step);
        return Math.Round(Min + steps * Step, 6);
    }

    private void SetFromPointer(double x)
    {
        var startInset = 12d + (string.IsNullOrEmpty(StartIcon) ? 0 : 24);
        var endInset = 12d + (string.IsNullOrEmpty(EndIcon) ? 0 : 24);
        var x0 = startInset;
        var x1 = Math.Max(x0 + 1, Bounds.Width - endInset);
        var usable = x1 - x0;

        var fraction = (x - x0) / usable;
        var raw = Min + fraction * (Max - Min);

        // Past an end the value holds and the track strains; the pull has resistance.
        if (raw < Min)
        {
            _strain = -Math.Min(14, (Min - raw) / Math.Max(0.0001, Max - Min) * usable * 0.35);
            raw = Min;
        }
        else if (raw > Max)
        {
            _strain = Math.Min(14, (raw - Max) / Math.Max(0.0001, Max - Min) * usable * 0.35);
            raw = Max;
        }
        else
        {
            _strain = 0;
        }

        var value = Snap(raw);

        // Detents: inside seven pixels of a mark the thumb clicks into it and holds.
        if (Marks is { } marks)
        {
            foreach (var mark in marks)
            {
                var markX = x0 + usable * Fraction(Clamp(mark.Value));
                if (Math.Abs(x - markX) <= 7)
                {
                    value = Snap(Clamp(mark.Value));
                    break;
                }
            }
        }

        if (Math.Abs(value - Value) > 0.0000001)
        {
            SetCurrentValue(ValueProperty, value);
            _display = value;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        InvalidateVisual();
    }

    private void SpringStrain()
    {
        if (Math.Abs(_strain) < 0.01)
        {
            _strain = 0;
            return;
        }

        var from = _strain;
        ArcAnim.To(from, 0, ArcAnim.SnappyDuration, ArcAnim.Snappy, value =>
        {
            _strain = value;
            InvalidateVisual();
        });
    }

    private void SpringTag()
    {
        if (Math.Abs(_tagAngle) < 0.01)
        {
            _tagAngle = 0;
            return;
        }

        var from = _tagAngle;
        ArcAnim.To(from, 0, TimeSpan.FromMilliseconds(240), ArcAnim.Smooth, value =>
        {
            _tagAngle = value;
            InvalidateVisual();
        });
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "Slider");
        AutomationProperties.SetHelpText(this, FormatValue(Value));
    }
}
