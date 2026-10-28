using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A single-value slider with a quiet track, an accent fill, optional ticks, a value bubble while
/// dragging and arrow, Home, End and PageUp/PageDown support.
/// </summary>
public class ArcSlider : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcSlider, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcSlider, string?>(nameof(Description));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcSlider, double>(nameof(Value), 50d);

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<ArcSlider, double>(nameof(Min));

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcSlider, double>(nameof(Max), 100d);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcSlider, double>(nameof(Step), 1d);

    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<ArcSlider, double>(nameof(LargeStep), double.NaN);

    public static readonly StyledProperty<IReadOnlyList<ArcSliderMark>?> MarksProperty =
        AvaloniaProperty.Register<ArcSlider, IReadOnlyList<ArcSliderMark>?>(nameof(Marks));

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<ArcSlider, bool>(nameof(ShowValue), true);

    private Panel? _trackArea;
    private Border? _track;
    private Border? _fill;
    private Border? _thumb;
    private Border? _bubble;
    private TextBlock? _bubbleText;
    private TextBlock? _readout;
    private TextBlock? _label;
    private TextBlock? _description;
    private Canvas? _marksLayer;
    private TranslateTransform? _thumbTransform;
    private TranslateTransform? _bubbleTransform;
    private bool _dragging;

    static ArcSlider()
    {
        ValueProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateVisuals());
        MinProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateVisuals());
        MaxProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateVisuals());
        StepProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateVisuals());
        MarksProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.RebuildMarks());
        ShowValueProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateReadout());
        LabelProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateLabels());
        DescriptionProperty.Changed.AddClassHandler<ArcSlider>((slider, _) => slider.UpdateLabels());
    }

    public ArcSlider()
    {
        Focusable = true;
    }

    public event EventHandler? ValueChanged;

    public event EventHandler? ValueCommitted;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
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

    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    public IReadOnlyList<ArcSliderMark>? Marks
    {
        get => GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    /// <summary>Formats the readout, bubble and spoken value; defaults to a trimmed number.</summary>
    public Func<double, string>? Format { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_trackArea is not null)
        {
            _trackArea.PointerPressed -= OnTrackPressed;
            _trackArea.PointerMoved -= OnTrackMoved;
            _trackArea.PointerReleased -= OnTrackReleased;
            _trackArea.SizeChanged -= OnTrackSizeChanged;
        }

        _trackArea = e.NameScope.Find<Panel>("TrackArea");
        _track = e.NameScope.Find<Border>("Track");
        _fill = e.NameScope.Find<Border>("Fill");
        _thumb = e.NameScope.Find<Border>("Thumb");
        _bubble = e.NameScope.Find<Border>("Bubble");
        _bubbleText = e.NameScope.Find<TextBlock>("BubbleText");
        _readout = e.NameScope.Find<TextBlock>("Readout");
        _label = e.NameScope.Find<TextBlock>("LabelText");
        _description = e.NameScope.Find<TextBlock>("Description");
        _marksLayer = e.NameScope.Find<Canvas>("Marks");
        _thumbTransform = _thumb?.RenderTransform as TranslateTransform;
        _bubbleTransform = _bubble?.RenderTransform as TranslateTransform;

        if (_trackArea is not null)
        {
            _trackArea.PointerPressed += OnTrackPressed;
            _trackArea.PointerMoved += OnTrackMoved;
            _trackArea.PointerReleased += OnTrackReleased;
            _trackArea.SizeChanged += OnTrackSizeChanged;
        }

        UpdateLabels();
        UpdateVisuals();
        RebuildMarks();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = EffectiveLargeStep;

        switch (e.Key)
        {
            case Key.Right:
            case Key.Up:
                SetValueInternal(Value + (large ? step : Step), announce: true);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Down:
                SetValueInternal(Value - (large ? step : Step), announce: true);
                e.Handled = true;
                break;
            case Key.PageUp:
                SetValueInternal(Value + step, announce: true);
                e.Handled = true;
                break;
            case Key.PageDown:
                SetValueInternal(Value - step, announce: true);
                e.Handled = true;
                break;
            case Key.Home:
                SetValueInternal(Min, announce: true);
                e.Handled = true;
                break;
            case Key.End:
                SetValueInternal(Max, announce: true);
                e.Handled = true;
                break;
        }
    }

    private double EffectiveLargeStep => double.IsNaN(LargeStep) || LargeStep <= 0 ? (Max - Min) / 10 : LargeStep;

    private void OnTrackPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _trackArea is null)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(_trackArea);
        Focus();
        SetFromPointer(e);
        UpdateBubble();
        e.Handled = true;
    }

    private void OnTrackMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging)
        {
            SetFromPointer(e);
        }
    }

    private void OnTrackReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        UpdateBubble();
        ValueCommitted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void SetFromPointer(PointerEventArgs e)
    {
        if (_trackArea is null)
        {
            return;
        }

        var width = Math.Max(1, _trackArea.Bounds.Width - 20);
        var x = Math.Clamp(e.GetPosition(_trackArea).X - 10, 0, width);
        var fraction = x / width;
        var raw = Min + fraction * (Max - Min);
        SetValueInternal(raw, announce: false);
    }

    private void SetValueInternal(double value, bool announce)
    {
        value = Snap(Math.Clamp(value, Min, Max));
        if (Math.Abs(value - Value) > 0.0000001)
        {
            SetCurrentValue(ValueProperty, value);
            ValueChanged?.Invoke(this, EventArgs.Empty);
            if (announce)
            {
                ValueCommitted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private double Snap(double value)
    {
        if (Step <= 0)
        {
            return value;
        }

        var steps = Math.Round((value - Min) / Step);
        return Math.Clamp(Min + steps * Step, Min, Max);
    }

    private void OnTrackSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateVisuals();
        RebuildMarks();
    }

    private void UpdateVisuals()
    {
        if (_thumbTransform is null || _trackArea is null)
        {
            return;
        }

        var width = _trackArea.Bounds.Width;
        var usable = Math.Max(1, width - 20);
        var range = Math.Max(0.0000001, Max - Min);
        var fraction = Math.Clamp((Value - Min) / range, 0, 1);
        var x = fraction * usable;

        _thumbTransform.X = x;
        if (_fill is not null)
        {
            _fill.Width = x + 10;
        }

        if (_bubbleTransform is not null)
        {
            var bubbleWidth = _bubble?.Bounds.Width ?? 44;
            _bubbleTransform.X = Math.Clamp(x + 10 - bubbleWidth / 2, 0, Math.Max(0, width - bubbleWidth));
        }

        UpdateReadout();
        UpdateAutomation();
    }

    private void UpdateReadout()
    {
        if (_readout is not null)
        {
            _readout.Text = ShowValue ? FormatValue(Value) : null;
            _readout.IsVisible = ShowValue;
        }

        if (_bubbleText is not null)
        {
            _bubbleText.Text = FormatValue(Value);
        }
    }

    private void UpdateBubble()
    {
        if (_bubble is not null)
        {
            _bubble.IsVisible = _dragging || IsFocused;
        }
    }

    private string FormatValue(double value) =>
        Format?.Invoke(value) ?? value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    private void UpdateLabels()
    {
        if (_label is not null)
        {
            _label.Text = Label;
            _label.IsVisible = !string.IsNullOrEmpty(Label);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "Slider");
        AutomationProperties.SetHelpText(this, FormatValue(Value));
    }

    private void RebuildMarks()
    {
        if (_marksLayer is null)
        {
            return;
        }

        _marksLayer.Children.Clear();
        var marks = Marks;
        if (marks is null || marks.Count == 0 || _trackArea is null)
        {
            return;
        }

        var width = Math.Max(1, _trackArea.Bounds.Width);
        var usable = Math.Max(1, width - 20);
        var range = Math.Max(0.0000001, Max - Min);

        foreach (var mark in marks)
        {
            var fraction = Math.Clamp((mark.Value - Min) / range, 0, 1);
            var dot = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = ArcResources.Brush(ArcTokens.ControlTrackHover)
            };
            Canvas.SetLeft(dot, fraction * usable + 8);
            Canvas.SetTop(dot, 1);
            _marksLayer.Children.Add(dot);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsFocusedProperty)
        {
            UpdateBubble();
        }
    }
}
