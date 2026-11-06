using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One password requirement shown in the checklist, e.g. "A number".</summary>
public sealed class ArcPasswordRule
{
    public ArcPasswordRule(string id, string label, Func<string, bool> test)
    {
        Id = id;
        Label = label;
        Test = test;
    }

    public string Id { get; }

    public string Label { get; }

    public Func<string, bool> Test { get; }
}

/// <summary>Carries the value and its computed strength when a password changes.</summary>
public sealed class ArcPasswordStrengthChangedEventArgs : RoutedEventArgs
{
    public ArcPasswordStrengthChangedEventArgs(string value, int level, string label, int met, int total)
    {
        Value = value;
        Level = level;
        Label = label;
        Met = met;
        Total = total;
    }

    /// <summary>Routed payload factory used by <c>ValueChangedRouted</c>.</summary>
    public ArcPasswordStrengthChangedEventArgs(RoutedEvent routedEvent, string value, int level, string label, int met, int total)
        : base(routedEvent)
    {
        Value = value;
        Level = level;
        Label = label;
        Met = met;
        Total = total;
    }

    public string Value { get; }

    /// <summary>0 too short, 1 weak, 2 fair, 3 good, 4 strong.</summary>
    public int Level { get; }

    public string Label { get; }

    public int Met { get; }

    public int Total { get; }
}

/// <summary>
/// A new-password field with a four-segment strength meter, a morphing strength word and a live rule
/// checklist. Scoring is local and simple, matching the documented on-device estimate.
/// </summary>
[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_StrengthWord", typeof(TextBlock))]
[TemplatePart("PART_RulesList", typeof(StackPanel))]
[TemplatePart("PART_RevealAction", typeof(Button))]
[TemplatePart("PART_RevealGlyph", typeof(ArcIcon))]
[TemplatePart("PART_Seg1", typeof(Border))]
[TemplatePart("PART_Seg2", typeof(Border))]
[TemplatePart("PART_Seg3", typeof(Border))]
[TemplatePart("PART_Seg4", typeof(Border))]
[TemplatePart("PART_Message", typeof(TextBlock))]
public class ArcPasswordStrength : ArcFieldBase
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcPasswordStrength, string?>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcPasswordStrength, string?>(nameof(Placeholder), "Enter a new password");

    public static readonly StyledProperty<bool> RevealedProperty =
        AvaloniaProperty.Register<ArcPasswordStrength, bool>(nameof(Revealed));

    public static readonly StyledProperty<int> ShortLengthProperty =
        AvaloniaProperty.Register<ArcPasswordStrength, int>(nameof(ShortLength), 8);

    public static readonly StyledProperty<IReadOnlyList<ArcPasswordRule>?> RulesProperty =
        AvaloniaProperty.Register<ArcPasswordStrength, IReadOnlyList<ArcPasswordRule>?>(nameof(Rules));

    private static readonly string[] LevelLabels = { "Too short", "Weak", "Fair", "Good", "Strong" };

    private TextBox? _input;
    private TextBlock? _message;
    private readonly Border?[] _segments = new Border?[4];
    private TextBlock? _word;
    private StackPanel? _rulesList;
    private Button? _revealButton;
    private ArcIcon? _revealGlyph;
    private int _level;
    private int _met;
    private int _total;

    static ArcPasswordStrength()
    {
        ValueProperty.Changed.AddClassHandler<ArcPasswordStrength>((field, _) => field.OnValueChanged());
        ShortLengthProperty.Changed.AddClassHandler<ArcPasswordStrength>((field, _) => field.UpdateStrength());
        RulesProperty.Changed.AddClassHandler<ArcPasswordStrength>((field, _) => field.UpdateStrength());
        RevealedProperty.Changed.AddClassHandler<ArcPasswordStrength>((field, _) => field.UpdateReveal());
    }

    /// <summary>Raised on every change with the value and computed strength.</summary>
    public event EventHandler<ArcPasswordStrengthChangedEventArgs>? ValueChanged;

    public static readonly RoutedEvent<ArcPasswordStrengthChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ArcPasswordStrength, ArcPasswordStrengthChangedEventArgs>("ValueChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling form of <see cref="ValueChanged"/>; the CLR event above keeps firing as before.</summary>
    public event EventHandler<ArcPasswordStrengthChangedEventArgs>? ValueChangedRouted
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool Revealed
    {
        get => GetValue(RevealedProperty);
        set => SetValue(RevealedProperty, value);
    }

    /// <summary>Below this many characters the meter holds at its first step and reads Too short.</summary>
    public int ShortLength
    {
        get => GetValue(ShortLengthProperty);
        set => SetValue(ShortLengthProperty, value);
    }

    /// <summary>Custom requirements; null uses the four default rules.</summary>
    public IReadOnlyList<ArcPasswordRule>? Rules
    {
        get => GetValue(RulesProperty);
        set => SetValue(RulesProperty, value);
    }

    public int StrengthLevel => _level;

    public string StrengthLabel => LevelLabels[Math.Clamp(_level, 0, 4)];

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_input is not null)
        {
            _input.TextChanged -= OnTextChanged;
            _input.GotFocus -= OnGotFocus;
            _input.LostFocus -= OnLostFocus;
        }

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _word = e.NameScope.Find<TextBlock>("PART_StrengthWord");
        _rulesList = e.NameScope.Find<StackPanel>("PART_RulesList");
        _revealButton = e.NameScope.Find<Button>("PART_RevealAction");
        _revealGlyph = e.NameScope.Find<ArcIcon>("PART_RevealGlyph");
        _segments[0] = e.NameScope.Find<Border>("PART_Seg1");
        _segments[1] = e.NameScope.Find<Border>("PART_Seg2");
        _segments[2] = e.NameScope.Find<Border>("PART_Seg3");
        _segments[3] = e.NameScope.Find<Border>("PART_Seg4");

        if (_input is not null)
        {
            _input.Text = Value ?? string.Empty;
            _input.PlaceholderText = Placeholder;
            AutomationProperties.SetName(_input, Label ?? "New password");
            _input.PasswordChar = '\u25CF';
            _input.TextChanged += OnTextChanged;
            _input.GotFocus += OnGotFocus;
            _input.LostFocus += OnLostFocus;
        }

        if (_revealButton is not null)
        {
            _revealButton.Click += (_, _) =>
            {
                Revealed = !Revealed;
                _input?.Focus();
            };
        }

        ApplyMessage(e.NameScope.Find<TextBlock>("PART_Message"));
        _message = e.NameScope.Find<TextBlock>("PART_Message");
        UpdateReveal();
        UpdateStrength();
    }

    protected override void OnMessageChanged()
    {
        ApplyMessage(_message);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && _input is not null &&
            !string.Equals(_input.Text ?? string.Empty, Value ?? string.Empty, StringComparison.Ordinal))
        {
            _input.Text = Value ?? string.Empty;
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = _input?.Text ?? string.Empty;
        if (!string.Equals(Value ?? string.Empty, text, StringComparison.Ordinal))
        {
            SetCurrentValue(ValueProperty, text);
        }
        else
        {
            UpdateStrength();
        }
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void OnLostFocus(object? sender, RoutedEventArgs e) => Classes.Set("focus", false);

    private void OnValueChanged()
    {
        UpdateStrength();
    }

    private void UpdateStrength()
    {
        var password = Value ?? string.Empty;
        var rules = Rules ?? BuildDefaultRules();
        var met = rules.Count(rule => rule.Test(password));
        var level = password.Length < ShortLength
            ? 0
            : met == 0 ? 1 : 1 + (int)Math.Floor(3.0 * met / Math.Max(1, rules.Count));

        _level = Math.Clamp(level, 0, 4);
        _met = met;
        _total = rules.Count;

        for (var i = 0; i < _segments.Length; i++)
        {
            var segment = _segments[i];
            if (segment is null)
            {
                continue;
            }

            segment.Classes.Set("filled", i < _level);
        }

        Classes.Set("level-0", _level == 0);
        Classes.Set("level-1", _level == 1);
        Classes.Set("level-2", _level == 2);
        Classes.Set("level-3", _level == 3);
        Classes.Set("level-4", _level == 4);

        if (_word is not null)
        {
            _word.Text = StrengthLabel;
            _word.IsVisible = !string.IsNullOrEmpty(password);
            _word.Foreground = ArcResources.Brush(_level switch
            {
                0 or 1 => ArcTokens.Danger,
                2 => ArcTokens.Warning,
                _ => ArcTokens.Success
            });
        }

        UpdateRuleRows(rules);
        ValueChanged?.Invoke(this, new ArcPasswordStrengthChangedEventArgs(password, _level, StrengthLabel, _met, _total));
        RaiseEvent(new ArcPasswordStrengthChangedEventArgs(ValueChangedEvent, password, _level, StrengthLabel, _met, _total));
    }

    private void UpdateRuleRows(IReadOnlyList<ArcPasswordRule> rules)
    {
        if (_rulesList is null)
        {
            return;
        }

        _rulesList.Children.Clear();
        var password = Value ?? string.Empty;

        foreach (var rule in rules)
        {
            var passed = rule.Test(password);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 0, 0, 1)
            };
            row.Children.Add(new ArcIcon
            {
                Icon = passed ? "check" : "dot",
                Size = 12,
                StrokeWidth = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ArcResources.Brush(passed ? ArcTokens.Success : ArcTokens.TextMuted)
            });
            row.Children.Add(new TextBlock
            {
                Text = rule.Label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ArcResources.Brush(passed ? ArcTokens.TextSecondary : ArcTokens.TextMuted)
            });

            AutomationProperties.SetName(row, rule.Label + (passed ? ", met" : ", not met"));
            _rulesList.Children.Add(row);
        }
    }

    private IReadOnlyList<ArcPasswordRule> BuildDefaultRules() => new[]
    {
        new ArcPasswordRule("length", $"At least {ShortLength} characters", value => value.Length >= ShortLength),
        new ArcPasswordRule("case", "Upper and lower case", value => value.Any(char.IsUpper) && value.Any(char.IsLower)),
        new ArcPasswordRule("number", "A number", value => value.Any(char.IsDigit)),
        new ArcPasswordRule("symbol", "A symbol", value => value.Any(character => !char.IsLetterOrDigit(character)))
    };

    private void UpdateReveal()
    {
        if (_input is not null)
        {
            _input.RevealPassword = Revealed;
        }

        if (_revealGlyph is not null)
        {
            _revealGlyph.Icon = Revealed ? "eye-off" : "eye";
        }

        if (_revealButton is not null)
        {
            var label = Revealed ? "Hide password" : "Show password";
            AutomationProperties.SetName(_revealButton, label);
            ToolTip.SetTip(_revealButton, label);
        }
    }
}
