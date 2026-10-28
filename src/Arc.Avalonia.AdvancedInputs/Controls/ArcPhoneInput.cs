using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedInputs.Controls;

/// <summary>One entry of the built-in phone country table.</summary>
public sealed class ArcPhoneCountry
{
    public ArcPhoneCountry(
        string iso,
        string name,
        string dialCode,
        string trunk,
        int[] groups,
        int minDigits,
        int maxDigits,
        string example)
    {
        Iso = iso;
        Name = name;
        DialCode = dialCode;
        Trunk = trunk;
        Groups = groups;
        MinDigits = minDigits;
        MaxDigits = maxDigits;
        Example = example;
    }

    /// <summary>ISO 3166-1 alpha-2 code, such as "GB".</summary>
    public string Iso { get; }

    public string Name { get; }

    /// <summary>Calling code without the plus, such as "44".</summary>
    public string DialCode { get; }

    /// <summary>National trunk prefix, such as "0"; empty when there is none.</summary>
    public string Trunk { get; }

    /// <summary>How the national digits group for display, for example 3,3,4.</summary>
    public int[] Groups { get; }

    public int MinDigits { get; }

    public int MaxDigits { get; }

    public string Example { get; }

    /// <summary>The flag emoji for the country, computed from the ISO code.</summary>
    public string Flag => ArcPhoneCountries.FlagOf(Iso);
}

/// <summary>The inline country table, the phone input's formatting helpers and the flag fallback.</summary>
public static class ArcPhoneCountries
{
    private static readonly int[] Gb = { 4, 6 };
    private static readonly int[] Us = { 3, 3, 4 };

    /// <summary>Fourteen countries with their calling codes, grouping and length rules.</summary>
    public static readonly IReadOnlyList<ArcPhoneCountry> All = new[]
    {
        new ArcPhoneCountry("US", "United States", "1", string.Empty, Us, 10, 10, "(415) 555-0132"),
        new ArcPhoneCountry("CA", "Canada", "1", string.Empty, Us, 10, 10, "(416) 555-0198"),
        new ArcPhoneCountry("GB", "United Kingdom", "44", "0", Gb, 10, 10, "07911 123456"),
        new ArcPhoneCountry("IE", "Ireland", "353", "0", new[] { 2, 3, 4 }, 9, 9, "085 123 4567"),
        new ArcPhoneCountry("DE", "Germany", "49", "0", new[] { 3, 4, 4 }, 7, 11, "0151 23456789"),
        new ArcPhoneCountry("FR", "France", "33", "0", new[] { 1, 2, 2, 2, 2 }, 9, 9, "06 12 34 56 78"),
        new ArcPhoneCountry("ES", "Spain", "34", string.Empty, new[] { 3, 3, 3 }, 9, 9, "612 345 678"),
        new ArcPhoneCountry("IT", "Italy", "39", string.Empty, new[] { 3, 3, 4 }, 9, 10, "312 345 6789"),
        new ArcPhoneCountry("NL", "Netherlands", "31", "0", new[] { 2, 3, 4 }, 9, 9, "06 1234 5678"),
        new ArcPhoneCountry("AU", "Australia", "61", "0", new[] { 3, 3, 3 }, 9, 9, "0412 345 678"),
        new ArcPhoneCountry("IN", "India", "91", string.Empty, new[] { 5, 5 }, 10, 10, "91234 56789"),
        new ArcPhoneCountry("JP", "Japan", "81", "0", new[] { 2, 4, 4 }, 9, 10, "090 1234 5678"),
        new ArcPhoneCountry("BR", "Brazil", "55", "0", new[] { 2, 5, 4 }, 10, 11, "11 91234 5678"),
        new ArcPhoneCountry("SG", "Singapore", "65", string.Empty, new[] { 4, 4 }, 8, 8, "8123 4567")
    };

    private static readonly Dictionary<string, ArcPhoneCountry> ByIsoCode =
        All.ToDictionary(country => country.Iso, StringComparer.OrdinalIgnoreCase);

    /// <summary>Looks a country up by ISO code; falls back to the United States.</summary>
    public static ArcPhoneCountry ByIso(string? iso) =>
        iso is not null && ByIsoCode.TryGetValue(iso, out var country) ? country : ByIsoCode["US"];

    /// <summary>
    /// The flag for an ISO code. Windows ships no flag emoji, so the control shows the two letter code as
    /// a badge; this helper still returns the emoji for platforms that render it.
    /// </summary>
    public static string FlagOf(string iso)
    {
        if (iso.Length != 2)
        {
            return iso;
        }

        return string.Concat(char.ConvertFromUtf32(0x1F1E6 + iso.ToUpperInvariant()[0] - 'A'),
            char.ConvertFromUtf32(0x1F1E6 + iso.ToUpperInvariant()[1] - 'A'));
    }

    /// <summary>Groups national digits for display, keeping a typed trunk prefix in front.</summary>
    public static string FormatNational(ArcPhoneCountry country, string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return string.Empty;
        }

        var prefix = string.Empty;
        var body = digits;
        if (country.Trunk.Length > 0 && digits.StartsWith(country.Trunk, StringComparison.Ordinal))
        {
            prefix = country.Trunk;
            body = digits.Substring(country.Trunk.Length);
        }

        var builder = new StringBuilder(prefix);
        var index = 0;
        foreach (var group in country.Groups)
        {
            if (index >= body.Length)
            {
                break;
            }

            if (builder.Length > prefix.Length)
            {
                builder.Append(' ');
            }

            var take = Math.Min(group, body.Length - index);
            builder.Append(body, index, take);
            index += take;
        }

        if (index < body.Length)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(body, index, body.Length - index);
        }

        return builder.ToString();
    }

    /// <summary>Formats an E.164 number for display, or returns it unchanged when it can not be read.</summary>
    public static string Format(string? e164)
    {
        if (string.IsNullOrWhiteSpace(e164) || e164[0] != '+')
        {
            return e164 ?? string.Empty;
        }

        var parsed = Parse(e164);
        if (parsed is null)
        {
            return e164;
        }

        return $"+{parsed.Value.Country.DialCode} {FormatNational(parsed.Value.Country, parsed.Value.Digits)}";
    }

    /// <summary>Reads an international number into a country and national digits.</summary>
    public static (ArcPhoneCountry Country, string Digits)? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 0)
        {
            return null;
        }

        var international = trimmed.StartsWith("+", StringComparison.Ordinal) ||
                            (trimmed.StartsWith("00", StringComparison.Ordinal) && digitsOnly.Length > 2);

        if (!international)
        {
            return (ByIso("US"), digitsOnly);
        }

        if (trimmed.StartsWith("00", StringComparison.Ordinal))
        {
            digitsOnly = digitsOnly.Substring(2);
        }

        foreach (var country in All.OrderByDescending(entry => entry.DialCode.Length))
        {
            if (digitsOnly.StartsWith(country.DialCode, StringComparison.Ordinal) &&
                digitsOnly.Length > country.DialCode.Length)
            {
                return (country, digitsOnly.Substring(country.DialCode.Length));
            }
        }

        return null;
    }

    /// <summary>The number of significant digits, ignoring the trunk prefix.</summary>
    public static int SignificantLength(ArcPhoneCountry country, string digits) =>
        country.Trunk.Length > 0 && digits.StartsWith(country.Trunk, StringComparison.Ordinal)
            ? digits.Length - country.Trunk.Length
            : digits.Length;
}

/// <summary>Details for a phone value change, mirroring Arc's change details.</summary>
public sealed class ArcPhoneValueEventArgs : EventArgs
{
    public ArcPhoneValueEventArgs(string value, string country, string formatted, bool valid)
    {
        Value = value;
        Country = country;
        Formatted = formatted;
        Valid = valid;
    }

    /// <summary>E.164 number, empty when there are no digits.</summary>
    public string Value { get; }

    public string Country { get; }

    /// <summary>The number as the field displays it.</summary>
    public string Formatted { get; }

    public bool Valid { get; }
}

/// <summary>
/// Arc's phone input: a country button that grows into a searchable list, formatting as you type, a
/// national display value and an E.164 value.
/// </summary>
public class ArcPhoneInput : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(Label));

    public static readonly StyledProperty<bool> HideLabelProperty =
        AvaloniaProperty.Register<ArcPhoneInput, bool>(nameof(HideLabel));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string?> CountryProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(Country));

    public static readonly StyledProperty<string?> DefaultCountryProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(DefaultCountry), "US");

    public static readonly StyledProperty<IList<string>?> CountriesProperty =
        AvaloniaProperty.Register<ArcPhoneInput, IList<string>?>(nameof(Countries));

    public static readonly StyledProperty<IList<string>?> PreferredCountriesProperty =
        AvaloniaProperty.Register<ArcPhoneInput, IList<string>?>(nameof(PreferredCountries));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<ArcPhoneInput, string?>(nameof(Error));

    public static readonly StyledProperty<bool> ValidateProperty =
        AvaloniaProperty.Register<ArcPhoneInput, bool>(nameof(Validate), true);

    public static readonly StyledProperty<bool> RequiredProperty =
        AvaloniaProperty.Register<ArcPhoneInput, bool>(nameof(Required));

    private StackPanel? _stack;
    private TextBlock? _labelText;
    private Border? _shell;
    private Button? _countryButton;
    private TextBlock? _isoText;
    private TextBlock? _dialText;
    private TextBox? _box;
    private ArcIcon? _validMark;
    private TextBlock? _message;
    private Border? _picker;
    private TextBox? _search;
    private StackPanel? _rows;
    private ScrollViewer? _scroller;

    private ArcPhoneCountry _country = ArcPhoneCountries.ByIso("US");
    private string _digits = string.Empty;
    private bool _syncing;
    private bool _pickerOpen;
    private List<ArcPhoneCountry> _visible = new();
    private int _active;
    private bool _touched;

    static ArcPhoneInput()
    {
        ValueProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.OnValueChanged());
        CountryProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.OnCountryChanged());
        DefaultCountryProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.ApplyDefaultCountry());
        CountriesProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateClasses());
        ErrorProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateMessage());
        DescriptionProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateMessage());
        ValidateProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateMessage());
        LabelProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateLabels());
        HideLabelProperty.Changed.AddClassHandler<ArcPhoneInput>((input, _) => input.UpdateLabels());
    }

    public ArcPhoneInput()
    {
        ClipToBounds = false;
    }

    /// <summary>Raised on every edit with the E.164 value and the current country and validity.</summary>
    public event EventHandler<ArcPhoneValueEventArgs>? ValueChanged;

    /// <summary>Raised when the country changes, including from a pasted international number.</summary>
    public event EventHandler? CountryChanged;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Keeps the label for screen readers only.</summary>
    public bool HideLabel
    {
        get => GetValue(HideLabelProperty);
        set => SetValue(HideLabelProperty, value);
    }

    /// <summary>E.164 number, such as "+14155550132". An empty string clears the field.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>ISO code of the selected country.</summary>
    public string? Country
    {
        get => GetValue(CountryProperty);
        set => SetValue(CountryProperty, value);
    }

    public string? DefaultCountry
    {
        get => GetValue(DefaultCountryProperty);
        set => SetValue(DefaultCountryProperty, value);
    }

    /// <summary>Limits the picker to these ISO codes.</summary>
    public IList<string>? Countries
    {
        get => GetValue(CountriesProperty);
        set => SetValue(CountriesProperty, value);
    }

    /// <summary>Pinned at the top of the picker under Suggested.</summary>
    public IList<string>? PreferredCountries
    {
        get => GetValue(PreferredCountriesProperty);
        set => SetValue(PreferredCountriesProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Replaces the built-in length message.</summary>
    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    /// <summary>Shows a message after blur when the number has the wrong length.</summary>
    public bool Validate
    {
        get => GetValue(ValidateProperty);
        set => SetValue(ValidateProperty, value);
    }

    public bool Required
    {
        get => GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <summary>The number as the field displays it.</summary>
    public string Formatted => ArcPhoneCountries.FormatNational(_country, _digits);

    /// <summary>True when the number has the length the country expects.</summary>
    public bool IsValid
    {
        get
        {
            var length = ArcPhoneCountries.SignificantLength(_country, _digits);
            return length >= _country.MinDigits && length <= _country.MaxDigits;
        }
    }

    /// <summary>Opens the country picker; page code uses it to show the open state.</summary>
    public void OpenCountryPicker() => OpenPicker(0);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_box is not null)
        {
            _box.TextChanged -= OnNumberTextChanged;
            _box.KeyDown -= OnNumberKeyDown;
            _box.LostFocus -= OnNumberLostFocus;
            _box.GotFocus -= OnNumberGotFocus;
        }

        if (_countryButton is not null)
        {
            _countryButton.Click -= OnCountryClicked;
        }

        if (_search is not null)
        {
            _search.KeyDown -= OnSearchKeyDown;
            _search.TextChanged -= OnSearchTextChanged;
        }

        _stack = e.NameScope.Find<StackPanel>("Stack");
        _labelText = e.NameScope.Find<TextBlock>("LabelText");
        _shell = e.NameScope.Find<Border>("Shell");
        _countryButton = e.NameScope.Find<Button>("CountryButton");
        _isoText = e.NameScope.Find<TextBlock>("IsoText");
        _dialText = e.NameScope.Find<TextBlock>("DialText");
        _box = e.NameScope.Find<TextBox>("PART_Number");
        _validMark = e.NameScope.Find<ArcIcon>("ValidMark");
        _message = e.NameScope.Find<TextBlock>("Message");
        _picker = e.NameScope.Find<Border>("Picker");
        _search = e.NameScope.Find<TextBox>("PART_Search");
        _rows = e.NameScope.Find<StackPanel>("Rows");
        _scroller = e.NameScope.Find<ScrollViewer>("Scroller");

        if (_box is not null)
        {
            _box.TextChanged += OnNumberTextChanged;
            _box.KeyDown += OnNumberKeyDown;
            _box.LostFocus += OnNumberLostFocus;
            _box.GotFocus += OnNumberGotFocus;
            AutomationProperties.SetName(_box, Label ?? "Phone number");
        }

        if (_countryButton is not null)
        {
            _countryButton.Click += OnCountryClicked;
        }

        if (_search is not null)
        {
            _search.KeyDown += OnSearchKeyDown;
            _search.TextChanged += OnSearchTextChanged;
            AutomationProperties.SetName(_search, "Search countries");
        }

        ApplyDefaultCountry();
        UpdateLabels();
        UpdateCountryVisuals();
        SyncFromValue();
        UpdateMessage();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            UpdateClasses();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_pickerOpen && _picker is not null && e.Source is Visual source &&
            !IsWithin(source, _picker) && !IsWithin(source, _countryButton))
        {
            ClosePicker();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _pickerOpen)
        {
            e.Handled = true;
            ClosePicker();
        }
    }

    private static bool IsWithin(Visual source, Visual? container)
    {
        if (container is null)
        {
            return false;
        }

        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyDefaultCountry()
    {
        if (_touched)
        {
            return;
        }

        var country = ArcPhoneCountries.ByIso(Country ?? DefaultCountry);
        if (country.Iso != _country.Iso)
        {
            _country = country;
            UpdateCountryVisuals();
            SyncFromValue();
        }
    }

    private void OnCountryChanged()
    {
        if (string.IsNullOrEmpty(Country))
        {
            return;
        }

        var country = ArcPhoneCountries.ByIso(Country);
        if (country.Iso == _country.Iso)
        {
            return;
        }

        _touched = true;
        _country = country;
        UpdateCountryVisuals();
        SyncFromValue();
        CountryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLabels()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !HideLabel && !string.IsNullOrEmpty(Label);
        }
    }

    private void UpdateClasses()
    {
        Classes.Set("disabled", !IsEnabled);
        Classes.Set("invalid", !string.IsNullOrWhiteSpace(Error) || (_touched && Validate && !IsValid && _digits.Length > 0));
        Classes.Set("valid", IsValid && _digits.Length > 0);
    }

    private void UpdateCountryVisuals()
    {
        if (_isoText is not null)
        {
            _isoText.Text = _country.Iso;
        }

        if (_dialText is not null)
        {
            _dialText.Text = "+" + _country.DialCode;
        }

        if (_countryButton is not null)
        {
            AutomationProperties.SetName(_countryButton, $"Country, {_country.Name} +{_country.DialCode}");
            _countryButton.IsEnabled = IsEnabled;
        }
    }

    private void SyncFromValue()
    {
        var value = Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            SetDigits(string.Empty);
            return;
        }

        var parsed = ArcPhoneCountries.Parse(value);
        if (parsed is null)
        {
            SetDigits(new string(value.Where(char.IsDigit).ToArray()));
            return;
        }

        if (parsed.Value.Country.Iso != _country.Iso)
        {
            _country = parsed.Value.Country;
            UpdateCountryVisuals();
        }

        SetDigits(parsed.Value.Digits);
    }

    private void SetDigits(string digits)
    {
        _digits = digits;
        if (_box is not null)
        {
            _syncing = true;
            _box.Text = ArcPhoneCountries.FormatNational(_country, digits);
            _box.CaretIndex = _box.Text?.Length ?? 0;
            _syncing = false;
        }

        UpdateMessage();
    }

    private void OnNumberGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void OnNumberLostFocus(object? sender, FocusChangedEventArgs e)
    {
        Classes.Set("focus", false);
        _touched = true;
        UpdateMessage();
        UpdateClasses();
    }

    private void OnNumberTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _box is null)
        {
            return;
        }

        var raw = _box.Text ?? string.Empty;
        if (raw.Contains('+'))
        {
            var parsed = ArcPhoneCountries.Parse(raw);
            if (parsed is not null)
            {
                if (parsed.Value.Country.Iso != _country.Iso)
                {
                    _country = parsed.Value.Country;
                    _touched = true;
                    UpdateCountryVisuals();
                    CountryChanged?.Invoke(this, EventArgs.Empty);
                }

                SetDigits(parsed.Value.Digits);
                EmitChange();
                return;
            }
        }

        var digitsBefore = CountDigitsBefore(raw, _box.CaretIndex);
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        _digits = digits;
        var formatted = ArcPhoneCountries.FormatNational(_country, digits);
        _syncing = true;
        _box.Text = formatted;
        _box.CaretIndex = CaretForDigits(formatted, digitsBefore);
        _syncing = false;
        UpdateMessage();
        UpdateClasses();
        EmitChange();
    }

    private static int CountDigitsBefore(string text, int caret)
    {
        var count = 0;
        for (var i = 0; i < Math.Min(caret, text.Length); i++)
        {
            if (char.IsDigit(text[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static int CaretForDigits(string text, int digits)
    {
        if (digits <= 0)
        {
            return 0;
        }

        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                seen++;
                if (seen == digits)
                {
                    return i + 1;
                }
            }
        }

        return text.Length;
    }

    private void EmitChange()
    {
        var e164 = _digits.Length == 0 ? string.Empty : "+" + _country.DialCode + Significant(_digits);
        _lastEmit = e164;
        SetCurrentValue(ValueProperty, e164);
        UpdateAutomation();
        ValueChanged?.Invoke(this, new ArcPhoneValueEventArgs(e164, _country.Iso, Formatted, IsValid));
    }

    private string _lastEmit = string.Empty;

    private string Significant(string digits) =>
        _country.Trunk.Length > 0 && digits.StartsWith(_country.Trunk, StringComparison.Ordinal)
            ? digits.Substring(_country.Trunk.Length)
            : digits;

    private void OnValueChanged()
    {
        var next = Value ?? string.Empty;
        if (!string.Equals(next, _lastEmit, StringComparison.Ordinal))
        {
            SyncFromValue();
        }

        UpdateClasses();
        UpdateAutomation();
    }

    private void UpdateAutomation()
    {
        if (_box is null)
        {
            return;
        }

        var spoken = IsValid ? "Valid number" : string.Empty;
        AutomationProperties.SetHelpText(_box, string.IsNullOrEmpty(spoken) ? Description ?? string.Empty : spoken);
        AutomationProperties.SetName(_box, Required ? $"{Label ?? "Phone number"}, required" : Label ?? "Phone number");
    }

    private void UpdateMessage()
    {
        if (_message is null)
        {
            return;
        }

        string? text = null;
        if (!string.IsNullOrWhiteSpace(Error))
        {
            text = Error;
        }
        else if (Validate && _touched)
        {
            if (_digits.Length > 0 && !IsValid)
            {
                text = $"Enter a {_country.Name} number, {_country.MinDigits} to {_country.MaxDigits} digits.";
            }
            else if (Required && _digits.Length == 0)
            {
                text = "Enter a phone number.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(Description))
        {
            text = Description;
        }

        _message.Text = text;
        _message.IsVisible = !string.IsNullOrWhiteSpace(text);
        _message.Foreground = ArcResources.Brush(
            !string.IsNullOrWhiteSpace(Error) || (Validate && _touched && _digits.Length > 0 && !IsValid)
                ? ArcTokens.Danger
                : ArcTokens.TextMuted);

        if (_validMark is not null)
        {
            _validMark.IsVisible = IsValid && _digits.Length > 0;
        }

        UpdateClasses();
    }

    private void OnNumberKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Back && _box is not null)
        {
            var text = _box.Text ?? string.Empty;
            var caret = _box.CaretIndex;
            if (caret > 0 && caret <= text.Length)
            {
                var before = text[caret - 1];
                if (!char.IsDigit(before))
                {
                    // Deleting a separator takes the digit beside it, per the Arc keyboard table.
                    var start = caret - 2;
                    while (start >= 0 && !char.IsDigit(text[start]))
                    {
                        start--;
                    }

                    if (start >= 0)
                    {
                        e.Handled = true;
                        var next = text.Remove(start, 1);
                        _box.Text = next;
                        _box.CaretIndex = Math.Min(start, next.Length);
                    }
                }
            }

            return;
        }

        if (e.Key == Key.Down || e.Key == Key.Up)
        {
            e.Handled = true;
            OpenPicker(e.Key == Key.Up ? -1 : 1);
        }
        else if (e.Key == Key.Escape && _pickerOpen)
        {
            e.Handled = true;
            ClosePicker();
        }
    }

    private void OnCountryClicked(object? sender, RoutedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (_pickerOpen)
        {
            ClosePicker();
        }
        else
        {
            OpenPicker(0);
        }
    }

    private void OpenPicker(int direction)
    {
        if (_picker is null || _rows is null || _search is null || _shell is null)
        {
            return;
        }

        _pickerOpen = true;
        BuildRows(string.Empty);
        _active = Math.Max(0, _visible.FindIndex(country => country.Iso == _country.Iso));
        Highlight(_active, focusSearch: false);

        var offset = _shell.TranslatePoint(new Point(0, _shell.Bounds.Height + 6), this) ?? new Point(0, 60);
        _picker.Margin = new Thickness(0, offset.Y, 0, 0);
        _picker.Width = Math.Clamp(Math.Max(240, _shell.Bounds.Width), 240, 340);
        _picker.IsVisible = true;
        _picker.Opacity = 1;

        if (!ArcThemeManager.ReducedMotion)
        {
            _picker.RenderTransform = new TranslateTransform(0, -4);
            _picker.Opacity = 0;
            ArcAnim.To(0, 1, TimeSpan.FromMilliseconds(160), ArcAnim.Smooth, value =>
            {
                if (_picker is null)
                {
                    return;
                }

                _picker.Opacity = value;
                if (_picker.RenderTransform is TranslateTransform transform)
                {
                    transform.Y = -4 * (1 - value);
                }
            });
        }

        _search.Text = string.Empty;
        Dispatcher.UIThread.Post(() =>
        {
            _search?.Focus();
            if (direction < 0)
            {
                _active = _visible.Count - 1;
                Highlight(_active, focusSearch: true);
            }
            else if (direction > 0)
            {
                _active = Math.Min(1, _visible.Count - 1);
                Highlight(_active, focusSearch: true);
            }
        }, DispatcherPriority.Loaded);
    }

    private void ClosePicker()
    {
        if (_picker is null)
        {
            return;
        }

        _pickerOpen = false;
        _picker.IsVisible = false;
        _countryButton?.Focus();
    }

    private void BuildRows(string query)
    {
        if (_rows is null)
        {
            return;
        }

        _rows.Children.Clear();
        _visible = FilterCountries(query);

        var suggested = new List<ArcPhoneCountry>();
        var preferred = PreferredCountries ?? new[] { "US", "CA", "GB" };
        if (string.IsNullOrEmpty(query))
        {
            foreach (var iso in preferred)
            {
                var country = ArcPhoneCountries.ByIso(iso);
                if (_visible.Contains(country))
                {
                    suggested.Add(country);
                }
            }
        }

        if (suggested.Count > 0)
        {
            AddHeading("Suggested");
            foreach (var country in suggested)
            {
                _rows.Children.Add(CreateRow(country));
            }

            AddHeading("All countries");
        }

        foreach (var country in _visible)
        {
            _rows.Children.Add(CreateRow(country));
        }

        if (_visible.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = "No matches",
                Classes = { "arc-caption" },
                Margin = new Thickness(12, 10)
            });
        }
    }

    private List<ArcPhoneCountry> FilterCountries(string query)
    {
        var allowed = Countries;
        var source = ArcPhoneCountries.All.Where(country =>
            allowed is null || allowed.Count == 0 || allowed.Any(iso => string.Equals(iso, country.Iso, StringComparison.OrdinalIgnoreCase)));

        if (string.IsNullOrWhiteSpace(query))
        {
            return source.ToList();
        }

        var needle = query.Trim().TrimStart('+');
        return source
            .Where(country =>
                country.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                country.Iso.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                country.DialCode.StartsWith(needle, StringComparison.Ordinal))
            .ToList();
    }

    private void AddHeading(string text)
    {
        _rows?.Children.Add(new TextBlock
        {
            Text = text,
            Classes = { "arc-caption" },
            Margin = new Thickness(12, 10, 12, 4)
        });
    }

    private Control CreateRow(ArcPhoneCountry country)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.Children.Add(new Border
        {
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = country.Iso,
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                Foreground = ArcResources.Brush(ArcTokens.TextSecondary)
            }
        });

        var name = new TextBlock
        {
            Text = country.Name,
            Classes = { "arc-body-sm" },
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var dial = new TextBlock
        {
            Text = "+" + country.DialCode,
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dial, 2);
        grid.Children.Add(dial);

        var row = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 7),
            Background = Brushes.Transparent,
            Child = grid,
            Tag = country,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        row.PointerEntered += (_, _) =>
        {
            var index = _rows?.Children.IndexOf(row) ?? -1;
            if (index >= 0)
            {
                Highlight(_visible.IndexOf(country), focusSearch: false);
            }
        };
        row.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            Choose(country);
        };

        return row;
    }

    private void Highlight(int index, bool focusSearch)
    {
        if (_rows is null)
        {
            return;
        }

        _active = Math.Clamp(index, 0, Math.Max(0, _visible.Count - 1));
        var activeCountry = _visible.Count > 0 ? _visible[_active] : null;

        foreach (var child in _rows.Children)
        {
            if (child is Border border && border.Tag is ArcPhoneCountry country)
            {
                var selected = ReferenceEquals(country, activeCountry);
                border.Background = selected
                    ? ArcResources.Brush(ArcTokens.SurfaceMuted)
                    : Brushes.Transparent;
            }
        }

        if (focusSearch && activeCountry is not null && _scroller is not null)
        {
            AutomationProperties.SetHelpText(_scroller, activeCountry.Name);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        BuildRows(_search?.Text ?? string.Empty);
        _active = 0;
        Highlight(0, focusSearch: false);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                Highlight(Math.Min(_active + 1, _visible.Count - 1), focusSearch: false);
                break;
            case Key.Up:
                e.Handled = true;
                Highlight(Math.Max(_active - 1, 0), focusSearch: false);
                break;
            case Key.PageDown:
                e.Handled = true;
                Highlight(Math.Min(_active + 5, _visible.Count - 1), focusSearch: false);
                break;
            case Key.PageUp:
                e.Handled = true;
                Highlight(Math.Max(_active - 5, 0), focusSearch: false);
                break;
            case Key.Home:
                e.Handled = true;
                Highlight(0, focusSearch: false);
                break;
            case Key.End:
                e.Handled = true;
                Highlight(_visible.Count - 1, focusSearch: false);
                break;
            case Key.Enter:
                e.Handled = true;
                if (_visible.Count > 0)
                {
                    Choose(_visible[Math.Clamp(_active, 0, _visible.Count - 1)]);
                }

                break;
            case Key.Escape:
                e.Handled = true;
                ClosePicker();
                break;
        }
    }

    private void Choose(ArcPhoneCountry country)
    {
        _touched = true;
        _country = country;
        SetCurrentValue(CountryProperty, country.Iso);
        UpdateCountryVisuals();
        ClosePicker();
        SyncFromValue();
        UpdateMessage();
        CountryChanged?.Invoke(this, EventArgs.Empty);
        EmitChange();
        _box?.Focus();
    }
}
