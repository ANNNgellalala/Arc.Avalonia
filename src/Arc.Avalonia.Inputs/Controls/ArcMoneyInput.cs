using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>Value and display details reported when a money amount changes.</summary>
public sealed class ArcMoneyChangedEventArgs : EventArgs
{
    public ArcMoneyChangedEventArgs(long? value, string currency, decimal? major, string formatted)
    {
        Value = value;
        Currency = currency;
        Major = major;
        Formatted = formatted;
    }

    /// <summary>Amount in minor units, or null when the field is empty.</summary>
    public long? Value { get; }

    public string Currency { get; }

    public decimal? Major { get; }

    public string Formatted { get; }
}

/// <summary>
/// A currency amount field. Typed digits are grouped live, the value is exposed in minor units for
/// exact arithmetic, and quick-add chips and arrow keys nudge the amount by whole major units.
/// </summary>
public class ArcMoneyInput : ArcFieldBase
{
    public static readonly StyledProperty<long?> ValueProperty =
        AvaloniaProperty.Register<ArcMoneyInput, long?>(nameof(Value));

    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<ArcMoneyInput, string>(nameof(Currency), "USD");

    public static readonly StyledProperty<long?> MinProperty =
        AvaloniaProperty.Register<ArcMoneyInput, long?>(nameof(Min));

    public static readonly StyledProperty<long?> MaxProperty =
        AvaloniaProperty.Register<ArcMoneyInput, long?>(nameof(Max));

    public static readonly StyledProperty<long?> StepProperty =
        AvaloniaProperty.Register<ArcMoneyInput, long?>(nameof(Step));

    public static readonly StyledProperty<IReadOnlyList<int>?> QuickAddProperty =
        AvaloniaProperty.Register<ArcMoneyInput, IReadOnlyList<int>?>(nameof(QuickAdd));

    public static readonly StyledProperty<bool> HideLabelProperty =
        AvaloniaProperty.Register<ArcMoneyInput, bool>(nameof(HideLabel));

    private static readonly Dictionary<string, string> Symbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["JPY"] = "¥",
        ["CAD"] = "CA$",
        ["AUD"] = "A$",
        ["CHF"] = "Fr.",
        ["INR"] = "₹"
    };

    private TextBox? _input;
    private TextBlock? _message;
    private TextBlock? _rangeHint;
    private TextBlock? _symbol;
    private StackPanel? _quickAddPanel;
    private DispatcherTimer? _hintTimer;
    private bool _syncing;

    static ArcMoneyInput()
    {
        ValueProperty.Changed.AddClassHandler<ArcMoneyInput>((field, _) => field.OnValueUpdated());
        CurrencyProperty.Changed.AddClassHandler<ArcMoneyInput>((field, _) => field.RefreshCurrency());
        QuickAddProperty.Changed.AddClassHandler<ArcMoneyInput>((field, _) => field.RebuildQuickAdd());
        StepProperty.Changed.AddClassHandler<ArcMoneyInput>((field, _) => { });
    }

    /// <summary>Raised with the amount in minor units plus display details.</summary>
    public event EventHandler<ArcMoneyChangedEventArgs>? ValueChanged;

    /// <summary>Amount in minor units (cents for USD); null is empty.</summary>
    public long? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>ISO 4217 code, for example "EUR".</summary>
    public string Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    public long? Min
    {
        get => GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public long? Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    /// <summary>Arrow-key distance in minor units; defaults to one major unit.</summary>
    public long? Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>Quick add chips in major units; empty hides the row.</summary>
    public IReadOnlyList<int>? QuickAdd
    {
        get => GetValue(QuickAddProperty);
        set => SetValue(QuickAddProperty, value);
    }

    /// <summary>Keeps the label available to screen readers without drawing it.</summary>
    public bool HideLabel
    {
        get => GetValue(HideLabelProperty);
        set => SetValue(HideLabelProperty, value);
    }

    /// <summary>Major-unit value for display, or null when empty.</summary>
    public decimal? Major => Value is { } minor ? minor / (decimal)MinorFactor : null;

    /// <summary>Fully formatted amount including the currency symbol.</summary>
    public string Formatted => Value is { } minor ? $"{Symbol}{FormatMinor(minor)}" : string.Empty;

    private string Symbol => Symbols.TryGetValue(Currency ?? "USD", out var symbol) ? symbol : Currency + " ";

    private int FractionDigits => string.Equals(Currency, "JPY", StringComparison.OrdinalIgnoreCase) ? 0 : 2;

    private long MinorFactor => FractionDigits == 0 ? 1 : 100;

    private string GroupSeparator =>
        string.Equals(Currency, "CHF", StringComparison.OrdinalIgnoreCase) ? "\u2009" : ",";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_input is not null)
        {
            _input.TextChanged -= OnTextChanged;
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
            _input.GotFocus -= OnInputGotFocus;
        }

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _message = e.NameScope.Find<TextBlock>("Message");
        _rangeHint = e.NameScope.Find<TextBlock>("RangeHint");
        _symbol = e.NameScope.Find<TextBlock>("CurrencySymbol");
        _quickAddPanel = e.NameScope.Find<StackPanel>("QuickAddPanel");

        if (_input is not null)
        {
            _input.TextChanged += OnTextChanged;
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            _input.GotFocus += OnInputGotFocus;
        }

        ApplyMessage(_message);
        RefreshCurrency();
        RebuildQuickAdd();
        SyncInput();
    }

    protected override void OnMessageChanged()
    {
        ApplyMessage(_message);
    }

    private void OnValueUpdated()
    {
        SyncInput();
        ValueChanged?.Invoke(this, new ArcMoneyChangedEventArgs(Value, Currency, Major, Formatted));
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _input is null)
        {
            return;
        }

        var parsed = ParseDraft(_input.Text ?? string.Empty);
        if (parsed is not { } draft)
        {
            Restore();
            return;
        }

        if (Max is { } max && draft.Amount is { } next && next > max)
        {
            ShowRangeHint($"Maximum {Symbol}{FormatMinor(max)}");
            Restore();
            return;
        }

        if (Value != draft.Amount)
        {
            SetCurrentValue(ValueProperty, draft.Amount);
        }

        if (!string.Equals(_input.Text, draft.Display, StringComparison.Ordinal))
        {
            _syncing = true;
            _input.Text = draft.Display;
            _input.CaretIndex = draft.Display.Length;
            _syncing = false;
        }
    }

    private (long? Amount, string Display)? ParseDraft(string text)
    {
        var filtered = new string(text.Where(character => char.IsDigit(character) || character == '.').ToArray());
        if (filtered.Length == 0)
        {
            return (null, string.Empty);
        }

        var dot = filtered.IndexOf('.');
        var integerDigits = (dot < 0 ? filtered : filtered[..dot]).TrimStart('0');
        var fractionDigits = dot < 0 ? string.Empty : filtered[(dot + 1)..];

        if (FractionDigits == 0)
        {
            fractionDigits = string.Empty;
            dot = -1;
        }
        else if (fractionDigits.Length > FractionDigits)
        {
            fractionDigits = fractionDigits[..FractionDigits];
        }

        if (integerDigits.Length > 12)
        {
            return null;
        }

        var integer = integerDigits.Length == 0 ? 0L : long.Parse(integerDigits, CultureInfo.InvariantCulture);
        if (integer > 9_999_999_999_999L)
        {
            return null;
        }

        var fraction = fractionDigits.Length == 0
            ? 0L
            : long.Parse(fractionDigits.PadRight(FractionDigits, '0'), CultureInfo.InvariantCulture);

        var value = integer * MinorFactor + fraction;
        var grouped = integer.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", GroupSeparator);
        var display = dot >= 0 ? $"{grouped}.{fractionDigits}" : grouped;
        return (value, display);
    }

    private void SyncInput()
    {
        if (_input is null)
        {
            return;
        }

        _syncing = true;
        _input.Text = Value is { } minor ? FormatMinor(minor) : string.Empty;
        _syncing = false;

        AutomationProperties.SetName(_input, Label ?? "Amount");
        AutomationProperties.SetHelpText(_input, Formatted);
    }

    private string FormatMinor(long minor)
    {
        var factor = MinorFactor;
        var major = minor / factor;
        var fraction = (int)(minor % factor);
        var grouped = major.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", GroupSeparator);
        return FractionDigits == 0
            ? grouped
            : $"{grouped}.{fraction.ToString(new string('0', FractionDigits), CultureInfo.InvariantCulture)}";
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var step = Step ?? MinorFactor;

        switch (e.Key)
        {
            case Key.Up:
                Add(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? step * 10 : step);
                e.Handled = true;
                break;
            case Key.Down:
                Add(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -step * 10 : -step);
                e.Handled = true;
                break;
            case Key.PageUp:
                Add(step * 10);
                e.Handled = true;
                break;
            case Key.PageDown:
                Add(-step * 10);
                e.Handled = true;
                break;
            case Key.Enter:
                Settle();
                e.Handled = true;
                break;
        }
    }

    private void Add(long delta)
    {
        var current = Value ?? 0;
        var next = Math.Max(0, current + delta);
        if (Max is { } max && next > max)
        {
            next = max;
            ShowRangeHint($"Maximum {Symbol}{FormatMinor(max)}");
        }

        next = Math.Max(next, Min ?? 0);
        SetCurrentValue(ValueProperty, next);
        Settle();
    }

    private void Settle()
    {
        if (_input is null)
        {
            return;
        }

        SyncInput();
        _input.CaretIndex = _input.Text?.Length ?? 0;
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        Classes.Set("focus", false);
        if (Value is { } value && Min is { } min && value < min)
        {
            SetCurrentValue(ValueProperty, min);
            ShowRangeHint($"Minimum {Symbol}{FormatMinor(min)}");
        }

        Settle();
    }

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void Restore()
    {
        if (_input is null)
        {
            return;
        }

        _syncing = true;
        _input.Text = Value is { } minor ? FormatMinor(minor) : string.Empty;
        _input.CaretIndex = _input.Text?.Length ?? 0;
        _syncing = false;
    }

    private void RefreshCurrency()
    {
        if (_symbol is not null)
        {
            _symbol.Text = Symbol;
            _symbol.IsVisible = true;
        }

        if (_input is not null)
        {
            _input.PlaceholderText = $"{Symbol}0{(FractionDigits == 0 ? string.Empty : ".00")}";
        }

        SyncInput();
        RebuildQuickAdd();
    }

    private void RebuildQuickAdd()
    {
        if (_quickAddPanel is null)
        {
            return;
        }

        _quickAddPanel.Children.Clear();
        var chips = QuickAdd ?? new[] { 10, 50, 100 };

        foreach (var major in chips)
        {
            var label = $"{Symbol}{major}";
            var chip = new Button
            {
                Content = "+" + label,
                Classes = { "quick-add" },
                Margin = new Thickness(0, 0, 6, 0)
            };
            if (this.TryFindResource("ArcChipTheme", out var theme) && theme is ControlTheme controlTheme)
            {
                chip.Theme = controlTheme;
            }

            var amount = (long)major * MinorFactor;
            chip.Click += (_, _) => Add(amount);
            AutomationProperties.SetName(chip, $"Add {label}");
            _quickAddPanel.Children.Add(chip);
        }

        _quickAddPanel.IsVisible = _quickAddPanel.Children.Count > 0;
    }

    private void ShowRangeHint(string text)
    {
        if (_rangeHint is null)
        {
            return;
        }

        _rangeHint.Text = text;
        _rangeHint.IsVisible = true;
        _hintTimer?.Stop();
        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _hintTimer.Tick += (_, _) =>
        {
            _rangeHint.IsVisible = false;
            _hintTimer?.Stop();
            _hintTimer = null;
        };
        _hintTimer.Start();
    }
}
