using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Controls;

/// <summary>
/// A meter with a rubber-banded edge: the value follows the pointer one to one, resists past its limits,
/// snaps back on release, and jumps to a tap. Keys move it by the configured small step, Page keys by four
/// steps and Home and End jump to the limits.
/// </summary>
public sealed class ArcRubberMeter : Panel
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcRubberMeter, string>(nameof(Label), "Alert volume");

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcRubberMeter, double>(nameof(Value), 60);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ArcRubberMeter, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ArcRubberMeter, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcRubberMeter, double>(nameof(Step), 1);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<ArcRubberMeter, string>(nameof(Suffix), "%");

    private readonly TextBlock _label;
    private readonly TextBlock _readout;
    private readonly Border _track;
    private readonly Border _fill;
    private readonly Border _knob;
    private readonly Border _ring;
    private readonly Panel _lane;
    private bool _dragging;
    private bool _pointerFocus;

    static ArcRubberMeter()
    {
        ValueProperty.Changed.AddClassHandler<ArcRubberMeter>((meter, _) => meter.UpdateVisuals());
        LabelProperty.Changed.AddClassHandler<ArcRubberMeter>((meter, _) => meter.UpdateVisuals());
        MinimumProperty.Changed.AddClassHandler<ArcRubberMeter>((meter, _) => meter.UpdateVisuals());
        MaximumProperty.Changed.AddClassHandler<ArcRubberMeter>((meter, _) => meter.UpdateVisuals());
        SuffixProperty.Changed.AddClassHandler<ArcRubberMeter>((meter, _) => meter.UpdateVisuals());
    }

    public ArcRubberMeter()
    {
        ClipToBounds = false;
        Focusable = true;
        IsTabStop = true;
        Height = 54;

        _label = new TextBlock
        {
            Text = Label,
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };
        _readout = new TextBlock
        {
            Text = "60%",
            Classes = { "arc-caption", "arc-medium" },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _label, _readout } };
        Grid.SetColumn(_readout, 1);

        _fill = new Border
        {
            Height = 8,
            CornerRadius = new CornerRadius(9999),
            Background = ArcGlass.Gradient(
                90,
                new GradientStop(ArcGlass.Color(ArcTokens.Accent), 0),
                new GradientStop(ArcGlass.Color(ArcTokens.AccentStrong), 1)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        _knob = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.ControlThumb)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.BorderStrong)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ControlThumbShadow),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        _track = new Border
        {
            Height = 8,
            CornerRadius = new CornerRadius(9999),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.ControlTrack)),
            VerticalAlignment = VerticalAlignment.Center
        };
        _lane = new Panel { Height = 18, Children = { _track, _fill, _knob } };
        _lane.PointerPressed += OnLanePressed;
        _lane.PointerMoved += OnLaneMoved;
        _lane.PointerReleased += OnLaneReleased;

        _ring = new Border
        {
            Margin = new Thickness(-3),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(2),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.FocusOutline)),
            IsVisible = false,
            IsHitTestVisible = false
        };

        var stack = new StackPanel { Spacing = 6, Children = { header, _lane } };
        Children.Add(stack);
        Children.Add(_ring);

        GotFocus += (_, _) =>
        {
            _ring.IsVisible = !_pointerFocus;
            _pointerFocus = false;
        };
        LostFocus += (_, _) => _ring.IsVisible = false;
        KeyDown += OnKeyDown;
        UpdateVisuals();
    }

    public event EventHandler<double>? ValueChanged;

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

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

    public string Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
        return new Size(width, 54);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        UpdateVisuals();
        return finalSize;
    }

    private void UpdateVisuals()
    {
        _label.Text = Label;
        _readout.Text = $"{Math.Round(Value)}{Suffix}";
        AutomationProperties.SetName(this, $"{Label}, {Math.Round(Value)}{Suffix}");
        AutomationProperties.SetHelpText(this, "Slider. Use the arrow keys, Page Up and Page Down, or Home and End.");

        var width = Math.Max(1, _lane.Bounds.Width > 0 ? _lane.Bounds.Width : Bounds.Width);
        var range = Math.Max(1, Maximum - Minimum);
        var fraction = Math.Clamp((Value - Minimum) / range, 0, 1);
        _fill.Width = Math.Max(0, width * fraction);
        _knob.Margin = new Thickness(Math.Clamp((width * fraction) - 8, -8, Math.Max(0, width - 16)), 0, 0, 0);
    }

    private void OnLanePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_lane).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        _dragging = true;
        e.Pointer.Capture(_lane);
        SetFromPointer(e.GetPosition(_lane).X);
        e.Handled = true;
    }

    private void OnLaneMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        SetFromPointer(e.GetPosition(_lane).X);
        e.Handled = true;
    }

    private void OnLaneReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        SetCurrentValue(ValueProperty, Math.Clamp(Value, Minimum, Maximum));
        ValueChanged?.Invoke(this, Value);
        UpdateVisuals();
    }

    private void SetFromPointer(double x)
    {
        var width = Math.Max(1, _lane.Bounds.Width);
        var range = Math.Max(1, Maximum - Minimum);
        var raw = Minimum + ((x / width) * range);
        var value = raw > Maximum
            ? Maximum + ((raw - Maximum) * 0.25)
            : raw < Minimum
                ? Minimum - ((Minimum - raw) * 0.25)
                : raw;

        SetCurrentValue(ValueProperty, value);
        UpdateVisuals();
        ValueChanged?.Invoke(this, Value);
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
                next -= Step * 4;
                break;
            case Key.PageUp:
                next += Step * 4;
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
        ValueChanged?.Invoke(this, Value);
        UpdateVisuals();
    }
}
