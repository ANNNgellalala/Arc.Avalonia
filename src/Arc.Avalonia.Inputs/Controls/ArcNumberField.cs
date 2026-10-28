using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A bounded number with stepper buttons, hold-to-repeat, arrow-key stepping and a limit hint beside
/// the label. Typing is parsed on Enter or blur and always clamped to the range.
/// </summary>
public class ArcNumberField : ArcFieldBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcNumberField, double>(nameof(Value));

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<ArcNumberField, double>(nameof(Min));

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcNumberField, double>(nameof(Max), double.MaxValue);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcNumberField, double>(nameof(Step), 1d);

    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<ArcNumberField, double>(nameof(LargeStep), double.NaN);

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<ArcNumberField, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<ArcNumberField, string?>(nameof(Suffix));

    public static readonly StyledProperty<bool> ShowLimitHintProperty =
        AvaloniaProperty.Register<ArcNumberField, bool>(nameof(ShowLimitHint), true);

    private TextBox? _input;
    private TextBlock? _message;
    private TextBlock? _hint;
    private TextBlock? _prefix;
    private TextBlock? _suffix;
    private Button? _increase;
    private Button? _decrease;
    private DispatcherTimer? _repeatTimer;
    private DispatcherTimer? _hintTimer;
    private double _heldDirection;
    private bool _ignoreNextClick;
    private bool _syncing;

    static ArcNumberField()
    {
        ValueProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.OnValueUpdated());
        MinProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.RefreshAffixes());
        MaxProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.RefreshAffixes());
        PrefixProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.RefreshAffixes());
        SuffixProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.RefreshAffixes());
        StepProperty.Changed.AddClassHandler<ArcNumberField>((field, _) => field.RefreshAffixes());
    }

    /// <summary>Raised whenever the committed value changes.</summary>
    public event EventHandler? ValueChanged;

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

    /// <summary>Distance for PageUp, PageDown and Shift with an arrow; defaults to ten steps.</summary>
    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    public string? Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string? Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    /// <summary>Shows "Max 500 seats" beside the label when a press meets a limit.</summary>
    public bool ShowLimitHint
    {
        get => GetValue(ShowLimitHintProperty);
        set => SetValue(ShowLimitHintProperty, value);
    }

    private double EffectiveLargeStep => double.IsNaN(LargeStep) || LargeStep <= 0 ? Step * 10 : LargeStep;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_input is not null)
        {
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
            _input.GotFocus -= OnInputGotFocus;
        }

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _message = e.NameScope.Find<TextBlock>("Message");
        _hint = e.NameScope.Find<TextBlock>("LimitHint");
        _prefix = e.NameScope.Find<TextBlock>("PrefixText");
        _suffix = e.NameScope.Find<TextBlock>("SuffixText");
        _increase = e.NameScope.Find<Button>("IncreaseAction");
        _decrease = e.NameScope.Find<Button>("DecreaseAction");

        if (_input is not null)
        {
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            _input.GotFocus += OnInputGotFocus;
        }

        if (_increase is not null)
        {
            WireStepper(_increase, 1);
        }

        if (_decrease is not null)
        {
            WireStepper(_decrease, -1);
        }

        ApplyMessage(_message);
        SyncInput();
        RefreshAffixes();
        UpdateButtonClasses();
    }

    protected override void OnMessageChanged()
    {
        ApplyMessage(_message);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty)
        {
            RefreshAffixes();
        }
    }

    private void OnValueUpdated()
    {
        if (!_syncing)
        {
            SyncInput();
        }

        UpdateButtonClasses();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncInput()
    {
        if (_input is null)
        {
            return;
        }

        _syncing = true;
        _input.Text = FormatValue(Value);
        _syncing = false;
        UpdateAutomation();
    }

    private void RefreshAffixes()
    {
        if (_prefix is not null)
        {
            _prefix.Text = Prefix;
            _prefix.IsVisible = !string.IsNullOrEmpty(Prefix);
        }

        if (_suffix is not null)
        {
            _suffix.Text = Suffix;
            _suffix.IsVisible = !string.IsNullOrEmpty(Suffix);
        }

        if (_input is not null && !_input.IsFocused)
        {
            SyncInput();
        }

        UpdateAutomation();
    }

    private void UpdateAutomation()
    {
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, Label ?? "Number");
            AutomationProperties.SetHelpText(_input, FormatValue(Value));
        }

        if (_increase is not null)
        {
            AutomationProperties.SetName(_increase, $"Increase {Label}".Trim());
        }

        if (_decrease is not null)
        {
            AutomationProperties.SetName(_decrease, $"Decrease {Label}".Trim());
        }
    }

    private void UpdateButtonClasses()
    {
        if (_increase is not null)
        {
            _increase.Classes.Set("at-limit", Value >= Max);
        }

        if (_decrease is not null)
        {
            _decrease.Classes.Set("at-limit", Value <= Min);
        }
    }

    private void WireStepper(Button button, double direction)
    {
        AutomationProperties.SetName(button, direction > 0 ? "Increase" : "Decrease");
        button.Click += (_, _) =>
        {
            if (_ignoreNextClick)
            {
                _ignoreNextClick = false;
                return;
            }

            StepBy(direction);
        };

        button.PointerPressed += (_, _) =>
        {
            _ignoreNextClick = true;
            StepBy(direction);
            StartRepeat(button, direction);
        };
        button.PointerReleased += (_, _) => StopRepeat();
        button.PointerCaptureLost += (_, _) => StopRepeat();
    }

    private void StartRepeat(Button button, double direction)
    {
        StopRepeat();
        _heldDirection = direction;
        _repeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _repeatTimer.Tick += (_, _) =>
        {
            if (_repeatTimer is not null)
            {
                _repeatTimer.Interval = TimeSpan.FromMilliseconds(80);
            }

            StepBy(_heldDirection);
        };
        _repeatTimer.Start();
        AutomationProperties.SetHelpText(button, "Hold to repeat");
    }

    private void StopRepeat()
    {
        _repeatTimer?.Stop();
        _repeatTimer = null;
    }

    private void StepBy(double direction)
    {
        var next = Clamp(Value + direction * Step);
        if (Math.Abs(next - Value) < double.Epsilon)
        {
            ShowLimit(direction > 0);
            return;
        }

        SetCurrentValue(ValueProperty, next);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Up:
                StepBy(large ? EffectiveLargeStep / Step : 1);
                e.Handled = true;
                break;
            case Key.Down:
                StepBy(large ? -EffectiveLargeStep / Step : -1);
                e.Handled = true;
                break;
            case Key.PageUp:
                StepBy(EffectiveLargeStep / Step);
                e.Handled = true;
                break;
            case Key.PageDown:
                StepBy(-EffectiveLargeStep / Step);
                e.Handled = true;
                break;
            case Key.Home:
                SetCurrentValue(ValueProperty, Clamp(Min));
                e.Handled = true;
                break;
            case Key.End:
                SetCurrentValue(ValueProperty, Clamp(Max));
                e.Handled = true;
                break;
            case Key.Enter:
                CommitDraft();
                e.Handled = true;
                break;
            case Key.Escape:
                SyncInput();
                _input?.SelectAll();
                e.Handled = true;
                break;
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => CommitDraft();

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void CommitDraft()
    {
        Classes.Set("focus", false);
        if (_input is null)
        {
            return;
        }

        var text = (_input.Text ?? string.Empty)
            .Replace(Prefix ?? string.Empty, string.Empty, StringComparison.Ordinal)
            .Replace(Suffix ?? string.Empty, string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!TryParse(text, out var parsed))
        {
            SyncInput();
            return;
        }

        var clamped = Clamp(parsed);
        if (Math.Abs(parsed - clamped) > double.Epsilon)
        {
            ShowLimit(parsed > Max);
        }

        if (Math.Abs(clamped - Value) > double.Epsilon)
        {
            SetCurrentValue(ValueProperty, clamped);
        }
        else
        {
            SyncInput();
        }
    }

    private static bool TryParse(string text, out double value)
    {
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return double.TryParse(text, styles, CultureInfo.CurrentCulture, out value)
               || double.TryParse(text, styles, CultureInfo.InvariantCulture, out value);
    }

    private double Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            return Value;
        }

        return Math.Min(Max, Math.Max(Min, value));
    }

    private string FormatValue(double value)
    {
        var format = Math.Abs(Step % 1) < 0.0000001 ? "0" : "0.###";
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    private void ShowLimit(bool upper)
    {
        if (!ShowLimitHint || _hint is null)
        {
            return;
        }

        _hint.Text = upper ? $"Max {FormatWithAffix(Max)}" : $"Min {FormatWithAffix(Min)}";
        _hint.IsVisible = true;

        _hintTimer?.Stop();
        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        _hintTimer.Tick += (_, _) =>
        {
            _hint.IsVisible = false;
            _hintTimer?.Stop();
            _hintTimer = null;
        };
        _hintTimer.Start();
    }

    private string FormatWithAffix(double value) => $"{Prefix}{FormatValue(value)}{Suffix}";
}
