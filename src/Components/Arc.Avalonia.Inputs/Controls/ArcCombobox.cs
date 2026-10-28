using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One option in a combobox. Keywords and aliases also match the query.</summary>
public sealed class ArcComboboxOption
{
    public ArcComboboxOption()
    {
    }

    public ArcComboboxOption(string value, string label, bool disabled = false, IReadOnlyList<string>? keywords = null)
    {
        Value = value;
        Label = label;
        Disabled = disabled;
        Keywords = keywords;
    }

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool Disabled { get; set; }

    public IReadOnlyList<string>? Keywords { get; set; }

    public override string ToString() => Label;
}

/// <summary>
/// A searchable single choice field. Typing filters the list in place; focus never leaves the input, so
/// ArrowDown, ArrowUp, Enter and Escape stay available while the query is being edited.
/// </summary>
public class ArcCombobox : ArcFieldBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcComboboxOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcCombobox, IReadOnlyList<ArcComboboxOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcCombobox, string?>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcCombobox, string?>(nameof(Placeholder), "Search or select…");

    public static readonly StyledProperty<string?> EmptyMessageProperty =
        AvaloniaProperty.Register<ArcCombobox, string?>(nameof(EmptyMessage), "No matches found");

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcCombobox, bool>(nameof(IsOpen));

    private TextBox? _input;
    private TextBlock? _message;
    private TextBlock? _emptyText;
    private Border? _trigger;
    private Button? _clear;
    private Popup? _popup;
    private Border? _surface;
    private ListBox? _menu;
    private bool _syncing;
    private bool _restoring;

    static ArcCombobox()
    {
        ValueProperty.Changed.AddClassHandler<ArcCombobox>((control, _) =>
        {
            control.SyncText();
            control.UpdateClasses();
        });
        PlaceholderProperty.Changed.AddClassHandler<ArcCombobox>((control, _) => control.SyncText());
        OptionsProperty.Changed.AddClassHandler<ArcCombobox>((control, _) => control.SyncText());
        IsOpenProperty.Changed.AddClassHandler<ArcCombobox>((control, _) => control.OnOpenChanged());
    }

    public ArcCombobox()
    {
        UpdateClasses();
    }

    public event EventHandler? ValueChanged;

    public IReadOnlyList<ArcComboboxOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
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

    public string? EmptyMessage
    {
        get => GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

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

        if (_clear is not null)
        {
            _clear.Click -= OnClearClicked;
        }

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _message = e.NameScope.Find<TextBlock>("Message");
        _emptyText = e.NameScope.Find<TextBlock>("EmptyText");
        _trigger = e.NameScope.Find<Border>("Trigger");
        _clear = e.NameScope.Find<Button>("ClearAction");
        _popup = e.NameScope.Find<Popup>("MenuPopup");
        _surface = e.NameScope.Find<Border>("MenuSurface");
        _menu = e.NameScope.Find<ListBox>("Menu");

        if (_input is not null)
        {
            _input.PlaceholderText = Placeholder;
            _input.TextChanged += OnTextChanged;
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            _input.GotFocus += OnInputGotFocus;
        }

        if (_clear is not null)
        {
            _clear.Click += OnClearClicked;
            AutomationProperties.SetName(_clear, "Clear selection");
        }

        if (_popup is not null && _trigger is not null)
        {
            ArcMenuHelper.ConfigurePopup(_popup, _trigger);
            _popup.Closed += (_, _) =>
            {
                if (IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, false);
                }
                else
                {
                    RestoreText();
                }
            };
        }

        if (_menu is not null)
        {
            _menu.SelectionChanged += (_, _) => { };
            _menu.AddHandler(PointerReleasedEvent, OnMenuPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        ApplyMessage(_message);
        SyncText();
        UpdateClasses();
    }

    protected override void OnMessageChanged() => ApplyMessage(_message);

    private string? SelectedLabel => (Options ?? Array.Empty<ArcComboboxOption>())
        .FirstOrDefault(option => string.Equals(option.Value, Value, StringComparison.Ordinal))?.Label;

    private void SyncText()
    {
        if (_input is null || _restoring)
        {
            return;
        }

        _syncing = true;
        _input.PlaceholderText = Placeholder;
        _input.Text = SelectedLabel ?? string.Empty;
        _syncing = false;

        AutomationProperties.SetName(_input, Label ?? "Search");
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _restoring || _input is null || !_input.IsFocused)
        {
            return;
        }

        var text = _input.Text ?? string.Empty;
        if (string.Equals(text, SelectedLabel ?? string.Empty, StringComparison.Ordinal))
        {
            // Programmatic sync of the selected label, not a query.
            return;
        }

        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }

        ApplyFilter(text);
    }

    private void ApplyFilter(string query)
    {
        if (_menu is null)
        {
            return;
        }

        var options = Options ?? Array.Empty<ArcComboboxOption>();
        var matches = options
            .Where(option => Matches(option, query))
            .ToList();

        var theme = ArcMenuHelper.FindTheme(this, "ArcMenuItemTheme");
        _syncing = true;
        _menu.Items.Clear();

        foreach (var option in matches)
        {
            var item = ArcMenuHelper.CreateItem(option.Label, option.Value, option.Value == Value, option.Disabled);
            if (theme is not null)
            {
                item.Theme = theme;
            }

            _menu.Items.Add(item);
        }

        if (_emptyText is not null)
        {
            _emptyText.Text = EmptyMessage;
            _emptyText.IsVisible = matches.Count == 0;
        }

        if (_menu is not null)
        {
            _menu.IsVisible = matches.Count > 0;
            _menu.SelectedIndex = matches.FindIndex(option => !option.Disabled);
        }

        _syncing = false;
    }

    private static bool Matches(ArcComboboxOption option, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        if (option.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            option.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return option.Keywords?.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private void OnOpenChanged()
    {
        UpdateClasses();

        if (_popup is null)
        {
            return;
        }

        if (IsOpen)
        {
            ApplyFilter(string.Empty);
            if (_surface is not null && _trigger is not null)
            {
                _surface.MinWidth = Math.Max(160, _trigger.Bounds.Width);
                _surface.MaxWidth = 384;
            }

            _popup.IsOpen = true;
            _input?.Focus();
        }
        else
        {
            _popup.IsOpen = false;
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, true);
                }
                else
                {
                    MoveActive(1);
                }

                e.Handled = true;
                break;
            case Key.Up:
                if (!IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, true);
                }
                else
                {
                    MoveActive(-1);
                }

                e.Handled = true;
                break;
            case Key.Enter:
                if (IsOpen && _menu?.SelectedItem is ListBoxItem item && item.IsEnabled)
                {
                    Commit(item.Tag as string);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                if (IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, false);
                    RestoreText();
                    e.Handled = true;
                }

                break;
        }
    }

    private void MoveActive(int delta)
    {
        if (_menu is null || _menu.ItemCount == 0)
        {
            return;
        }

        var index = _menu.SelectedIndex;
        for (var i = 0; i < _menu.ItemCount; i++)
        {
            index = (index + delta + _menu.ItemCount) % _menu.ItemCount;
            if (_menu.Items[index] is ListBoxItem { IsEnabled: true })
            {
                _menu.SelectedIndex = index;
                _menu.ScrollIntoView(index);
                return;
            }
        }
    }

    private void OnMenuPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _menu?.SelectedItem is not ListBoxItem item)
        {
            return;
        }

        if (item.IsEnabled)
        {
            Commit(item.Tag as string);
        }

        e.Handled = true;
    }

    private void Commit(string? value)
    {
        SetCurrentValue(ValueProperty, value);
        SetCurrentValue(IsOpenProperty, false);
        RestoreText();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        _input?.Focus();
    }

    private void RestoreText()
    {
        if (_input is null)
        {
            return;
        }

        _restoring = true;
        _syncing = true;
        _input.Text = SelectedLabel ?? string.Empty;
        _syncing = false;
        _restoring = false;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        Commit(string.Empty);
    }

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e)
    {
        Classes.Set("focus", true);

        if (!string.IsNullOrEmpty(Value))
        {
            // A chosen label is selected on focus so typing starts a fresh query instead of appending.
            _input?.SelectAll();
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        Classes.Set("focus", false);
    }

    private void UpdateClasses()
    {
        Classes.Set("empty", string.IsNullOrEmpty(Value));
        if (_clear is not null)
        {
            _clear.IsVisible = !string.IsNullOrEmpty(Value);
        }
    }
}
