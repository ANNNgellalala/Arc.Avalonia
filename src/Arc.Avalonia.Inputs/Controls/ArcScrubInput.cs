using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcScrubInputSize
{
    Sm,
    Md
}

/// <summary>Where a scrub input change came from, mirroring Arc's change details.</summary>
public enum ArcScrubChangeSource
{
    Scrub,
    Type,
    Key,
    Reset
}

public sealed class ArcScrubValueEventArgs : EventArgs
{
    public ArcScrubValueEventArgs(double value, ArcScrubChangeSource source, string? unit)
    {
        Value = value;
        Source = source;
        Unit = unit;
    }

    public double Value { get; }

    public ArcScrubChangeSource Source { get; }

    public string? Unit { get; }
}

/// <summary>
/// Arc's scrub input: a compact inspector number field. Drag the label to scrub (Shift is coarse, Alt is
/// fine), click to type a value with a unit or simple arithmetic, and bounded values show a quiet track.
/// </summary>
public class ArcScrubInput : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcScrubInput, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(Value));

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(Min), double.NegativeInfinity);

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(Max), double.PositiveInfinity);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(Step), 1d);

    public static readonly StyledProperty<double> FineStepProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(FineStep), double.NaN);

    public static readonly StyledProperty<double> CoarseStepProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(CoarseStep), double.NaN);

    public static readonly StyledProperty<int> PrecisionProperty =
        AvaloniaProperty.Register<ArcScrubInput, int>(nameof(Precision), -1);

    public static readonly StyledProperty<double> PixelsPerStepProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(PixelsPerStep), 2d);

    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<ArcScrubInput, bool>(nameof(Wrap));

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ArcScrubInput, string?>(nameof(Unit));

    public static readonly StyledProperty<IList<string>?> UnitsProperty =
        AvaloniaProperty.Register<ArcScrubInput, IList<string>?>(nameof(Units));

    public static readonly StyledProperty<double> ResetValueProperty =
        AvaloniaProperty.Register<ArcScrubInput, double>(nameof(ResetValue), double.NaN);

    public static readonly StyledProperty<bool> TrackProperty =
        AvaloniaProperty.Register<ArcScrubInput, bool>(nameof(Track), true);

    public static readonly StyledProperty<ArcScrubInputSize> SizeProperty =
        AvaloniaProperty.Register<ArcScrubInput, ArcScrubInputSize>(nameof(Size));

    private Border? _shell;
    private Panel? _fillHost;
    private Border? _handle;
    private Border? _trackFill;
    private TextBox? _box;
    private TextBlock? _labelText;
    private Button? _unitButton;
    private TextBlock? _unitText;
    private TranslateTransform? _strain;

    private bool _syncing;
    private bool _scrubbing;
    private bool _pointerMoved;
    private Point _dragOrigin;
    private double _dragValue;
    private string? _activeUnit;
    private string? _textBeforeEdit;
    private ArcScrubChangeSource _lastSource = ArcScrubChangeSource.Type;

    static ArcScrubInput()
    {
        ValueProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.OnValueChanged());
        MinProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.Refresh());
        MaxProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.Refresh());
        UnitProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.OnUnitChanged());
        UnitsProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.Refresh());
        TrackProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.Refresh());
        SizeProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.UpdateClasses());
        LabelProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.UpdateLabel());
        PrecisionProperty.Changed.AddClassHandler<ArcScrubInput>((input, _) => input.Refresh());
    }

    public ArcScrubInput()
    {
        _activeUnit = Unit;
        UpdateClasses();
    }

    /// <summary>Raised for every change, including each scrub step.</summary>
    public event EventHandler<ArcScrubValueEventArgs>? ValueChanged;

    /// <summary>Raised once a change settles: a scrub release, Enter, blur, a key step or a reset.</summary>
    public event EventHandler<ArcScrubValueEventArgs>? ValueCommitted;

    /// <summary>Raised when the unit changes, from the unit button or a typed value.</summary>
    public event EventHandler? UnitChanged;

    /// <summary>Text of the scrub handle; it is the drag target, so icon labels need a name elsewhere.</summary>
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

    /// <summary>Step for Alt and fine drags; defaults to a tenth of <see cref="Step"/>.</summary>
    public double FineStep
    {
        get => GetValue(FineStepProperty);
        set => SetValue(FineStepProperty, value);
    }

    /// <summary>Step for Shift and coarse drags, snapped to the coarse grid; defaults to ten steps.</summary>
    public double CoarseStep
    {
        get => GetValue(CoarseStepProperty);
        set => SetValue(CoarseStepProperty, value);
    }

    /// <summary>Decimal places kept; -1 derives them from the step.</summary>
    public int Precision
    {
        get => GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    /// <summary>Pointer travel in px for one step while scrubbing.</summary>
    public double PixelsPerStep
    {
        get => GetValue(PixelsPerStepProperty);
        set => SetValue(PixelsPerStepProperty, value);
    }

    /// <summary>Wraps from max to min, as rotation does. Needs both bounds.</summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Units a typed value may carry; the unit button cycles them when there are several.</summary>
    public IList<string>? Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    /// <summary>Double-click the label to return here.</summary>
    public double ResetValue
    {
        get => GetValue(ResetValueProperty);
        set => SetValue(ResetValueProperty, value);
    }

    /// <summary>Shows where the value sits between the bounds as a quiet fill.</summary>
    public bool Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public ArcScrubInputSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Formats the readout; defaults to the step precision.</summary>
    public Func<double, string>? Format { get; set; }

    private bool HasMin => !double.IsNegativeInfinity(Min);

    private bool HasMax => !double.IsPositiveInfinity(Max);

    private bool HasBounds => HasMin && HasMax;

    private double EffectiveFine => double.IsNaN(FineStep) || FineStep <= 0 ? Step / 10 : FineStep;

    private double EffectiveCoarse => double.IsNaN(CoarseStep) || CoarseStep <= 0 ? Step * 10 : CoarseStep;

    private int EffectivePrecision
    {
        get
        {
            if (Precision >= 0)
            {
                return Precision;
            }

            return Math.Max(Decimals(Step), Decimals(EffectiveFine));
        }
    }

    private static int Decimals(double step)
    {
        if (step <= 0 || double.IsInfinity(step))
        {
            return 0;
        }

        var decimals = 0;
        var value = step;
        while (decimals < 4 && Math.Abs(value - Math.Round(value)) > 0.0000001)
        {
            value *= 10;
            decimals++;
        }

        return decimals;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_handle is not null)
        {
            _handle.PointerPressed -= OnHandlePressed;
            _handle.PointerMoved -= OnHandleMoved;
            _handle.PointerReleased -= OnHandleReleased;
            _handle.PointerCaptureLost -= OnHandleCaptureLost;
        }

        if (_box is not null)
        {
            _box.KeyDown -= OnBoxKeyDown;
            _box.GotFocus -= OnBoxGotFocus;
            _box.LostFocus -= OnBoxLostFocus;
            _box.TextChanged -= OnBoxTextChanged;
        }

        if (_unitButton is not null)
        {
            _unitButton.Click -= OnUnitClicked;
        }

        _shell = e.NameScope.Find<Border>("Shell");
        _fillHost = e.NameScope.Find<Panel>("FillHost");
        _handle = e.NameScope.Find<Border>("Handle");
        _trackFill = e.NameScope.Find<Border>("TrackFill");
        _box = e.NameScope.Find<TextBox>("PART_Value");
        _labelText = e.NameScope.Find<TextBlock>("LabelText");
        _unitButton = e.NameScope.Find<Button>("UnitButton");
        _unitText = e.NameScope.Find<TextBlock>("UnitText");
        _strain = _shell?.RenderTransform as TranslateTransform;

        if (_handle is not null)
        {
            _handle.PointerPressed += OnHandlePressed;
            _handle.PointerMoved += OnHandleMoved;
            _handle.PointerReleased += OnHandleReleased;
            _handle.PointerCaptureLost += OnHandleCaptureLost;
        }

        if (_fillHost is not null)
        {
            _fillHost.SizeChanged += OnFillHostSizeChanged;
        }

        if (_box is not null)
        {
            _box.KeyDown += OnBoxKeyDown;
            _box.GotFocus += OnBoxGotFocus;
            _box.LostFocus += OnBoxLostFocus;
            _box.TextChanged += OnBoxTextChanged;
            AutomationProperties.SetName(_box, Label ?? "Value");
        }

        if (_unitButton is not null)
        {
            _unitButton.Click += OnUnitClicked;
        }

        UpdateLabel();
        UpdateClasses();
        UpdateUnitVisuals();
        UpdateDisplay();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            UpdateClasses();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateTrack();
        }
    }

    private void OnFillHostSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateTrack();

    private void OnValueChanged()
    {
        if (_box is not null && !_box.IsFocused)
        {
            UpdateDisplay();
        }

        UpdateTrack();
        UpdateAutomation();
        var args = new ArcScrubValueEventArgs(Value, _lastSource, _activeUnit);
        ValueChanged?.Invoke(this, args);
    }

    private void OnUnitChanged()
    {
        SetUnit(Unit, notify: false);
        Refresh();
    }

    private void Refresh()
    {
        UpdateTrack();
        UpdateUnitVisuals();
        if (_box is not null && !_box.IsFocused)
        {
            UpdateDisplay();
        }

        UpdateAutomation();
    }

    private void UpdateClasses()
    {
        Classes.Set("sm", Size == ArcScrubInputSize.Sm);
        Classes.Set("md", Size == ArcScrubInputSize.Md);
        Classes.Set("disabled", !IsEnabled);
        Classes.Set("bounded", HasBounds);

        if (_box is not null)
        {
            _box.Foreground = ArcResources.Brush(IsEnabled ? ArcTokens.Foreground : ArcTokens.TextMuted);
            _box.Background = Brushes.Transparent;
        }
    }

    private void UpdateLabel()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !string.IsNullOrEmpty(Label);
        }

        if (_handle is not null)
        {
            _handle.IsVisible = !string.IsNullOrEmpty(Label);
        }

        if (_box is not null)
        {
            AutomationProperties.SetName(_box, Label ?? "Value");
        }

        UpdateAutomation();
    }

    private void UpdateUnitVisuals()
    {
        if (_unitText is not null)
        {
            _unitText.Text = _activeUnit ?? string.Empty;
        }

        if (_unitButton is not null)
        {
            var units = Units;
            _unitButton.IsVisible = !string.IsNullOrEmpty(_activeUnit) || (units?.Count ?? 0) > 0;
            _unitButton.IsEnabled = IsEnabled && units is { Count: > 1 };
            AutomationProperties.SetName(_unitButton, $"Unit: {_activeUnit}. Change unit.");
        }
    }

    private void UpdateDisplay()
    {
        if (_box is null)
        {
            return;
        }

        _syncing = true;
        _box.Text = FormatValue(Value);
        _syncing = false;
    }

    private string FormatValue(double value)
        => Format?.Invoke(value) ?? ArcFormat.Number(value, EffectivePrecision);

    private void UpdateTrack()
    {
        if (_trackFill is null)
        {
            return;
        }

        var visible = Track && HasBounds && Max > Min;
        _trackFill.IsVisible = visible;
        if (!visible)
        {
            return;
        }

        var width = Math.Max(0, _fillHost?.Bounds.Width ?? 0);
        var fraction = Math.Clamp((Value - Min) / (Max - Min), 0, 1);
        _trackFill.Width = Math.Max(0, width * fraction);
    }

    private void UpdateAutomation()
    {
        if (_box is null)
        {
            return;
        }

        AutomationProperties.SetName(_box, Label ?? "Value");
        AutomationProperties.SetHelpText(_box, ArcFormat.Spoken(Value, _activeUnit));
    }

    // ---------------------------------------------------------------- scrubbing

    private void OnHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _handle is null)
        {
            return;
        }

        if (e.ClickCount == 2 && !double.IsNaN(ResetValue))
        {
            e.Handled = true;
            SetValueInternal(ResetValue, ArcScrubChangeSource.Reset, commit: true);
            return;
        }

        e.Handled = true;
        _scrubbing = true;
        _pointerMoved = false;
        _dragOrigin = e.GetPosition(this);
        _dragValue = Value;
        e.Pointer.Capture(_handle);
    }

    private void OnHandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        var delta = e.GetPosition(this).X - _dragOrigin.X;
        if (!_pointerMoved && Math.Abs(delta) < 3)
        {
            return;
        }

        _pointerMoved = true;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? EffectiveCoarse
            : e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                ? EffectiveFine
                : Step;

        var steps = delta / Math.Max(1, PixelsPerStep);
        var raw = _dragValue + steps * step;
        ApplyScrub(raw);
    }

    private void OnHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        e.Pointer.Capture(null);

        if (!_pointerMoved)
        {
            FocusValueBox(selectAll: true);
            return;
        }

        SpringStrain();
        ValueCommitted?.Invoke(this, new ArcScrubValueEventArgs(Value, ArcScrubChangeSource.Scrub, _activeUnit));
    }

    private void OnHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        SpringStrain();
        ValueCommitted?.Invoke(this, new ArcScrubValueEventArgs(Value, ArcScrubChangeSource.Scrub, _activeUnit));
    }

    private void ApplyScrub(double raw)
    {
        double clamped;
        var overshoot = 0d;

        if (HasMin && raw < Min)
        {
            overshoot = Min - raw;
            clamped = Wrap && HasMax ? Max : Min;
        }
        else if (HasMax && raw > Max)
        {
            overshoot = raw - Max;
            clamped = Wrap && HasMin ? Min : Max;
        }
        else
        {
            clamped = raw;
        }

        SetValueInternal(clamped, ArcScrubChangeSource.Scrub, commit: false);
        var strain = Math.Min(12, overshoot / Math.Max(1, PixelsPerStep) * 2);
        SetStrain((raw < Min ? -strain : strain));
    }

    private void SetStrain(double x)
    {
        if (_strain is null)
        {
            return;
        }

        _strain.X = ArcThemeManager.ReducedMotion ? 0 : x;
    }

    private void SpringStrain()
    {
        if (_strain is null || Math.Abs(_strain.X) < 0.01)
        {
            SetStrain(0);
            return;
        }

        var from = _strain.X;
        ArcAnim.To(from, 0, ArcAnim.SnappyDuration, ArcAnim.Snappy, value => SetStrain(value));
    }

    private void Shake()
    {
        if (_strain is null)
        {
            return;
        }

        if (ArcThemeManager.ReducedMotion)
        {
            return;
        }

        var steps = new[] { 0d, -3, 3, -2, 0 };
        var index = 0;
        void Next()
        {
            if (index >= steps.Length || _strain is null)
            {
                return;
            }

            _strain.X = steps[index++];
            ArcAnim.Delay(TimeSpan.FromMilliseconds(45), Next);
        }

        Next();
    }

    // ---------------------------------------------------------------- typing

    private void FocusValueBox(bool selectAll)
    {
        if (_box is null)
        {
            return;
        }

        _box.Focus();
        if (selectAll)
        {
            _box.SelectAll();
        }
    }

    private void OnBoxGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _textBeforeEdit = _box?.Text;
        Classes.Set("focus", true);
        if (_box is not null)
        {
            _box.SelectAll();
        }
    }

    private void OnBoxLostFocus(object? sender, FocusChangedEventArgs e)
    {
        Classes.Set("focus", false);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin)
            {
                CommitText();
            }
        }, DispatcherPriority.Background);
    }

    private void OnBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _box is null || !_box.IsFocused)
        {
            return;
        }

        Classes.Set("typing", true);
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                CommitText();
                break;
            case Key.Escape:
                e.Handled = true;
                RestoreText();
                break;
            case Key.Up:
                e.Handled = true;
                StepValue(1, e.KeyModifiers);
                break;
            case Key.Down:
                e.Handled = true;
                StepValue(-1, e.KeyModifiers);
                break;
            case Key.Home when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                e.Handled = true;
                _box!.CaretIndex = 0;
                break;
            case Key.End when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                e.Handled = true;
                _box!.CaretIndex = _box.Text?.Length ?? 0;
                break;
        }
    }

    private void StepValue(int direction, KeyModifiers modifiers)
    {
        var step = modifiers.HasFlag(KeyModifiers.Shift)
            ? EffectiveCoarse
            : modifiers.HasFlag(KeyModifiers.Alt)
                ? EffectiveFine
                : Step;

        var next = Value + direction * step;
        if (HasBounds)
        {
            if (next > Max && !Wrap)
            {
                next = Max;
                Shake();
            }
            else if (next < Min && !Wrap)
            {
                next = Min;
                Shake();
            }
        }

        SetValueInternal(next, ArcScrubChangeSource.Key, commit: true);
        UpdateDisplay();
    }

    private void CommitText()
    {
        if (_box is null)
        {
            return;
        }

        var text = _box.Text ?? string.Empty;
        Classes.Set("typing", false);

        var allowed = AllowedUnits();
        if (!ScrubExpression.TryEvaluate(text, allowed, out var parsed, out var unit, out _))
        {
            RejectInput();
            return;
        }

        Classes.Set("invalid", false);
        if (!string.IsNullOrEmpty(unit))
        {
            SetUnit(unit, notify: true);
        }

        SetValueInternal(parsed, ArcScrubChangeSource.Type, commit: true);
        UpdateDisplay();
        _box.SelectAll();
    }

    private void RestoreText()
    {
        if (_box is null)
        {
            return;
        }

        if (_textBeforeEdit is not null)
        {
            _syncing = true;
            _box.Text = _textBeforeEdit;
            _syncing = false;
        }
        else
        {
            UpdateDisplay();
        }

        Classes.Set("typing", false);
        Classes.Set("invalid", false);
        _box.SelectAll();
    }

    private void RejectInput()
    {
        Classes.Set("invalid", true);
        Shake();
        ArcAnim.Delay(TimeSpan.FromMilliseconds(900), () =>
        {
            Classes.Set("invalid", false);
            UpdateDisplay();
            _box?.SelectAll();
        });
    }

    private IReadOnlyList<string> AllowedUnits()
    {
        var units = new List<string>();
        if (Units is { } list)
        {
            foreach (var unit in list)
            {
                if (!string.IsNullOrWhiteSpace(unit))
                {
                    units.Add(unit);
                }
            }
        }

        if (!string.IsNullOrEmpty(_activeUnit) && !units.Contains(_activeUnit))
        {
            units.Add(_activeUnit);
        }

        return units;
    }

    private void SetUnit(string? unit, bool notify = true)
    {
        if (string.Equals(_activeUnit, unit, StringComparison.Ordinal))
        {
            return;
        }

        _activeUnit = unit;
        SetCurrentValue(UnitProperty, unit);
        UpdateUnitVisuals();
        UpdateAutomation();
        if (notify)
        {
            UnitChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnUnitClicked(object? sender, RoutedEventArgs e)
    {
        var units = Units;
        if (units is null || units.Count <= 1)
        {
            return;
        }

        var index = _activeUnit is null ? -1 : IndexOf(units, _activeUnit);
        var next = units[(index + 1) % units.Count];
        SetUnit(next, notify: true);
        if (_box is not null && !_box.IsFocused)
        {
            UpdateDisplay();
        }
    }

    private static int IndexOf(IList<string> units, string value)
    {
        for (var i = 0; i < units.Count; i++)
        {
            if (string.Equals(units[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void SetValueInternal(double value, ArcScrubChangeSource source, bool commit)
    {
        value = Snap(Clamp(value));
        _lastSource = source;
        if (Math.Abs(value - Value) > 0.0000001)
        {
            SetCurrentValue(ValueProperty, value);
            ValueChanged?.Invoke(this, new ArcScrubValueEventArgs(value, source, _activeUnit));
        }

        if (commit)
        {
            ValueCommitted?.Invoke(this, new ArcScrubValueEventArgs(Value, source, _activeUnit));
        }
    }

    private double Clamp(double value)
    {
        if (HasMin)
        {
            value = Math.Max(value, Min);
        }

        if (HasMax)
        {
            value = Math.Min(value, Max);
        }

        return value;
    }

    private double Snap(double value)
    {
        var step = Step;
        if (step <= 0 || double.IsInfinity(step))
        {
            return value;
        }

        var origin = HasMin ? Min : 0;
        var steps = Math.Round((value - origin) / step);
        var snapped = origin + steps * step;
        return Math.Round(snapped, EffectivePrecision);
    }
}

/// <summary>Evaluates the typed value of a scrub input: numbers with units, + - * / and parentheses.</summary>
public static class ScrubExpression
{
    /// <summary>
    /// Parses an expression such as "(100-24)/2" or "50%". Commas read as decimal points and deg is an
    /// alias for the degree sign. Returns false for anything else or an unknown unit.
    /// </summary>
    public static bool TryEvaluate(
        string? text,
        IReadOnlyList<string>? units,
        out double value,
        out string? unit,
        out string? error)
    {
        value = 0;
        unit = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter a number";
            return false;
        }

        var parser = new Parser(text, units ?? Array.Empty<string>());
        try
        {
            value = parser.ParseExpression();
            parser.SkipSpace();
            if (!parser.AtEnd)
            {
                error = "Unexpected text";
                return false;
            }

            unit = parser.LastUnit;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                error = "Out of range";
                return false;
            }

            return true;
        }
        catch (FormatException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyList<string> _units;
        private int _index;

        public Parser(string text, IReadOnlyList<string> units)
        {
            _text = text;
            _units = units;
        }

        public string? LastUnit { get; private set; }

        public bool AtEnd => _index >= _text.Length;

        public void SkipSpace()
        {
            while (!AtEnd && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipSpace();
                if (AtEnd)
                {
                    return value;
                }

                var op = _text[_index];
                if (op != '+' && op != '-')
                {
                    return value;
                }

                _index++;
                var right = ParseTerm();
                value = op == '+' ? value + right : value - right;
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipSpace();
                if (AtEnd)
                {
                    return value;
                }

                var op = _text[_index];
                if (op != '*' && op != '/' && op != '×' && op != '÷')
                {
                    return value;
                }

                _index++;
                var right = ParseFactor();
                value = op is '*' or '×' ? value * right : right == 0 ? throw new FormatException("Can't divide by zero") : value / right;
            }
        }

        private double ParseFactor()
        {
            SkipSpace();
            if (!AtEnd && (_text[_index] == '-' || _text[_index] == '+'))
            {
                var sign = _text[_index] == '-' ? -1 : 1;
                _index++;
                return sign * ParseFactor();
            }

            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipSpace();
            if (AtEnd)
            {
                throw new FormatException("Value is incomplete");
            }

            if (_text[_index] == '(')
            {
                _index++;
                var inner = ParseExpression();
                SkipSpace();
                if (AtEnd || _text[_index] != ')')
                {
                    throw new FormatException("Missing a closing bracket");
                }

                _index++;
                return inner;
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            var start = _index;
            var seenDigit = false;
            while (!AtEnd)
            {
                var c = _text[_index];
                if (char.IsDigit(c))
                {
                    seenDigit = true;
                    _index++;
                }
                else if (c is '.' or ',')
                {
                    _index++;
                }
                else
                {
                    break;
                }
            }

            if (!seenDigit)
            {
                throw new FormatException("Enter a number");
            }

            var raw = _text.Substring(start, _index - start).Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException("Enter a number");
            }

            SkipSpace();
            var unitStart = _index;
            while (!AtEnd)
            {
                var c = _text[_index];
                if (char.IsLetter(c) || c is '%' or '°')
                {
                    _index++;
                }
                else
                {
                    break;
                }
            }

            if (_index > unitStart)
            {
                var unitText = _text.Substring(unitStart, _index - unitStart);
                var normalized = Normalize(unitText);
                if (_units.Count > 0 && !Matches(normalized))
                {
                    throw new FormatException($"Unknown unit {unitText}");
                }

                LastUnit = normalized;
            }

            return number;
        }

        private static string Normalize(string unit) =>
            unit.Equals("deg", StringComparison.OrdinalIgnoreCase) ? "°"
            : unit.Equals("percent", StringComparison.OrdinalIgnoreCase) ? "%"
            : unit;

        private bool Matches(string unit)
        {
            foreach (var candidate in _units)
            {
                if (string.Equals(Normalize(candidate), unit, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
