using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// Shared behaviour for the text-entry family: a single native TextBox inside the Arc shell, clear
/// affordance, optional leading and trailing icons and two-way <see cref="Value"/>.
/// </summary>
public abstract class ArcTextFieldBase : ArcFieldBase
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcTextFieldBase, string?>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcTextFieldBase, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> LeadingIconProperty =
        AvaloniaProperty.Register<ArcTextFieldBase, string?>(nameof(LeadingIcon));

    public static readonly StyledProperty<string?> TrailingIconProperty =
        AvaloniaProperty.Register<ArcTextFieldBase, string?>(nameof(TrailingIcon));

    public static readonly StyledProperty<bool> ShowClearButtonProperty =
        AvaloniaProperty.Register<ArcTextFieldBase, bool>(nameof(ShowClearButton), true);

    private bool _syncing;
    private TextBlock? _disabledText;

    protected ArcTextFieldBase()
    {
        AddHandler(PointerPressedEvent, OnShellPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised on every edit, after <see cref="Value"/> has been updated.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Lets a derived field raise <see cref="ValueChanged"/> after a programmatic edit.</summary>
    protected void RaiseValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);

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

    /// <summary>Lucide icon name drawn before the input, such as "mail".</summary>
    public string? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }

    /// <summary>Lucide icon name drawn after the input, such as "credit-card".</summary>
    public string? TrailingIcon
    {
        get => GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
    }

    /// <summary>Shows the clear button while the field has text.</summary>
    public bool ShowClearButton
    {
        get => GetValue(ShowClearButtonProperty);
        set => SetValue(ShowClearButtonProperty, value);
    }

    /// <summary>Search reserves the clear slot so the field width never shifts.</summary>
    protected virtual bool ReserveClearSlot => false;

    protected TextBox? InputBox { get; private set; }

    protected TextBlock? MessageText { get; private set; }

    private ArcIcon? _leadingIcon;
    private ArcIcon? _trailingIcon;
    private Button? _clearButton;

    /// <summary>True while the inner text box owns keyboard focus.</summary>
    public bool IsInputFocused => InputBox?.IsFocused == true;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (InputBox is not null)
        {
            InputBox.TextChanged -= OnInputTextChanged;
            InputBox.GotFocus -= OnInputGotFocus;
            InputBox.LostFocus -= OnInputLostFocus;
            InputBox.KeyDown -= OnInputKeyDown;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClicked;
        }

        InputBox = e.NameScope.Find<TextBox>("PART_Input");
        MessageText = e.NameScope.Find<TextBlock>("Message");
        _leadingIcon = e.NameScope.Find<ArcIcon>("LeadingIcon");
        _trailingIcon = e.NameScope.Find<ArcIcon>("TrailingIcon");
        _clearButton = e.NameScope.Find<Button>("ClearAction");

        if (InputBox is not null)
        {
            InputBox.TextChanged += OnInputTextChanged;
            InputBox.GotFocus += OnInputGotFocus;
            InputBox.LostFocus += OnInputLostFocus;
            InputBox.KeyDown += OnInputKeyDown;
            InputBox.PlaceholderText = Placeholder;
            AutomationProperties.SetName(InputBox, string.IsNullOrWhiteSpace(Label) ? "Text field" : Label);
            AutomationProperties.SetHelpText(InputBox, Description ?? string.Empty);
            InputBox.RevealPassword = false;
            SyncText();
            Classes.Set("focus", InputBox.IsFocused);
        }

        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClicked;
        }

        RefreshAffordances();
        ApplyMessage(MessageText);
        OnFieldTemplateApplied(e);
        UpdateDisabledOverlay();
    }

    /// <summary>Text drawn in place of the native input while the field is disabled.</summary>
    protected virtual string DisabledDisplay
    {
        get
        {
            var text = Value ?? string.Empty;
            return string.IsNullOrEmpty(text) ? Placeholder ?? string.Empty : text;
        }
    }

    /// <summary>
    /// Disables the native text box and draws its value with Arc text colours. Avalonia's disabled
    /// TextBox theme paints a grey fill inside the shell, which breaks the quiet Arc surface.
    /// </summary>
    protected void UpdateDisabledOverlay()
    {
        if (InputBox is null)
        {
            return;
        }

        if (IsEnabled)
        {
            InputBox.IsVisible = true;
            if (_disabledText is not null)
            {
                _disabledText.IsVisible = false;
            }

            return;
        }

        if (InputBox.Parent is not Panel panel)
        {
            return;
        }

        if (_disabledText is null)
        {
            _disabledText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = InputBox.FontSize
            };

            if (panel is Grid grid)
            {
                Grid.SetColumn(_disabledText, Grid.GetColumn(InputBox));
            }

            panel.Children.Add(_disabledText);
        }

        InputBox.IsVisible = false;
        _disabledText.Text = DisabledDisplay;
        _disabledText.Foreground = ArcResources.Brush(string.IsNullOrEmpty(Value) ? ArcTokens.TextMuted : ArcTokens.TextSecondary);
        _disabledText.FontSize = InputBox.FontSize;
        _disabledText.IsVisible = true;
    }

    /// <summary>Template hook for derived fields.</summary>
    protected virtual void OnFieldTemplateApplied(TemplateAppliedEventArgs e)
    {
    }

    /// <summary>Called after a user edit, before <see cref="ValueChanged"/>.</summary>
    protected virtual void OnInputChanged()
    {
    }

    protected virtual void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
    }

    protected override void OnMessageChanged()
    {
        ApplyMessage(MessageText);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            if (!_syncing)
            {
                SyncText();
            }

            RefreshAffordances();
            UpdateDisabledOverlay();
        }
        else if (change.Property == PlaceholderProperty)
        {
            if (InputBox is not null)
            {
                InputBox.PlaceholderText = Placeholder;
            AutomationProperties.SetName(InputBox, string.IsNullOrWhiteSpace(Label) ? "Text field" : Label);
            AutomationProperties.SetHelpText(InputBox, Description ?? string.Empty);
            }

            UpdateDisabledOverlay();
        }
        else if (change.Property == IsEnabledProperty)
        {
            UpdateDisabledOverlay();
        }
        else if (change.Property == LeadingIconProperty || change.Property == TrailingIconProperty ||
                 change.Property == ShowClearButtonProperty)
        {
            RefreshAffordances();
        }
    }

    /// <summary>Pushes <see cref="Value"/> into the native text box without echoing a change event.</summary>
    protected void SyncText()
    {
        if (InputBox is null)
        {
            return;
        }

        var text = Value ?? string.Empty;
        if (string.Equals(InputBox.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _syncing = true;
        InputBox.Text = text;
        _syncing = false;
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = InputBox?.Text ?? string.Empty;
        if (_syncing || string.Equals(Value ?? string.Empty, text, StringComparison.Ordinal))
        {
            return;
        }

        _syncing = true;
        Value = text;
        _syncing = false;

        RefreshAffordances();
        OnInputChanged();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        Value = string.Empty;
        SyncText();
        RefreshAffordances();
        OnInputChanged();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        InputBox?.Focus();
    }

    private void OnShellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (InputBox is null || InputBox.IsFocused || !IsEnabled)
        {
            return;
        }

        if (e.Source is Visual source && source.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        InputBox.Focus();
    }

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        Classes.Set("focus", true);
        OnInputFocused();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        Classes.Set("focus", false);
    }

    /// <summary>Called when the inner text box gains focus.</summary>
    protected virtual void OnInputFocused()
    {
    }

    /// <summary>Keeps icon and clear affordances in sync with the current text.</summary>
    protected void RefreshAffordances()
    {
        if (_leadingIcon is not null)
        {
            _leadingIcon.Icon = LeadingIcon;
            _leadingIcon.IsVisible = !string.IsNullOrEmpty(LeadingIcon);
        }

        if (_trailingIcon is not null)
        {
            _trailingIcon.Icon = TrailingIcon;
            _trailingIcon.IsVisible = !string.IsNullOrEmpty(TrailingIcon);
        }

        if (_clearButton is not null)
        {
            var hasText = IsEnabled && !string.IsNullOrEmpty(Value);
            if (ReserveClearSlot)
            {
                _clearButton.IsVisible = ShowClearButton;
                _clearButton.Opacity = hasText ? 1 : 0;
                _clearButton.IsHitTestVisible = hasText;
                _clearButton.IsTabStop = hasText;
            }
            else
            {
                _clearButton.IsVisible = ShowClearButton && hasText;
            }
        }
    }
}
