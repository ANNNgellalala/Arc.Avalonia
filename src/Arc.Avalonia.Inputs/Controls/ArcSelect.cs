using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One choice in a select or multi-select list.</summary>
public sealed class ArcSelectOption
{
    public ArcSelectOption()
    {
    }

    public ArcSelectOption(string value, string label, bool disabled = false)
    {
        Value = value;
        Label = label;
        Disabled = disabled;
    }

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool Disabled { get; set; }

    public override string ToString() => Label;
}

/// <summary>
/// A compact single choice field: trigger plus a floating list with one highlight that glides to the
/// selected row. Enter, Space and ArrowDown open; arrows move; Escape closes without changing value.
/// </summary>
public class ArcSelect : ArcFieldBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcSelectOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcSelect, IReadOnlyList<ArcSelectOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcSelect, string?>(nameof(Value));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcSelect, string?>(nameof(Placeholder), "Select an option");

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcSelect, bool>(nameof(IsOpen));

    private Border? _trigger;
    private TextBlock? _valueText;
    private ArcIcon? _chevron;
    private RotateTransform? _chevronTransform;
    private TextBlock? _message;
    private Popup? _popup;
    private Border? _surface;
    private Border? _highlight;
    private TranslateTransform? _highlightTransform;
    private ListBox? _menu;

    static ArcSelect()
    {
        OptionsProperty.Changed.AddClassHandler<ArcSelect>((control, _) => control.RefreshValueText());
        ValueProperty.Changed.AddClassHandler<ArcSelect>((control, _) =>
        {
            control.RefreshValueText();
            control.SyncSelection();
        });
        PlaceholderProperty.Changed.AddClassHandler<ArcSelect>((control, _) => control.RefreshValueText());
        IsOpenProperty.Changed.AddClassHandler<ArcSelect>((control, _) => control.OnOpenChanged());
    }

    public ArcSelect()
    {
        Focusable = true;
        UpdateClasses();
    }

    /// <summary>Raised when a value is chosen or cleared.</summary>
    public event EventHandler? ValueChanged;

    public IReadOnlyList<ArcSelectOption>? Options
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

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
        }

        _trigger = e.NameScope.Find<Border>("Trigger");
        _valueText = e.NameScope.Find<TextBlock>("ValueText");
        _chevron = e.NameScope.Find<ArcIcon>("Chevron");
        _chevronTransform = _chevron?.RenderTransform as RotateTransform;
        _message = e.NameScope.Find<TextBlock>("Message");
        _popup = e.NameScope.Find<Popup>("MenuPopup");
        _surface = e.NameScope.Find<Border>("MenuSurface");
        _highlight = e.NameScope.Find<Border>("Highlight");
        _highlightTransform = _highlight?.RenderTransform as TranslateTransform;
        _menu = e.NameScope.Find<ListBox>("Menu");

        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
        }

        if (_popup is not null && _trigger is not null)
        {
            ArcMenuHelper.ConfigurePopup(_popup, _trigger);
            _popup.Closed += (_, _) => SetCurrentValue(IsOpenProperty, false);
            _popup.Opened += (_, _) => Dispatcher.UIThread.Post(UpdateHighlight, DispatcherPriority.Loaded);
        }

        if (_menu is not null)
        {
            _menu.SelectionChanged += (_, _) => UpdateHighlight();
            _menu.KeyDown += OnMenuKeyDown;
            _menu.AddHandler(PointerReleasedEvent, OnMenuPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        ApplyMessage(_message);
        RefreshValueText();
        SyncSelection();
        UpdateClasses();
        UpdateChevron();
    }

    protected override void OnMessageChanged() => ApplyMessage(_message);

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (_popup?.IsOpen == true)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }

        e.Handled = true;
    }

    private void OnOpenChanged()
    {
        UpdateClasses();

        if (_popup is not null)
        {
            if (IsOpen)
            {
                BuildMenu();
                if (_surface is not null && _trigger is not null)
                {
                    _surface.MinWidth = Math.Max(160, _trigger.Bounds.Width);
                    _surface.MaxWidth = 384;
                }

                _popup.IsOpen = true;
                Dispatcher.UIThread.Post(() =>
                {
                    _menu?.Focus();
                    ScrollSelectionIntoView();
                    UpdateHighlight();
                }, DispatcherPriority.Loaded);
            }
            else
            {
                _popup.IsOpen = false;
            }
        }

        if (!IsOpen && _menu is not null)
        {
            Focus();
        }
    }

    private void BuildMenu()
    {
        if (_menu is null)
        {
            return;
        }

        var theme = ArcMenuHelper.FindTheme(this, "ArcSelectMenuItemTheme");

        _menu.Items.Clear();

        foreach (var option in Options ?? Array.Empty<ArcSelectOption>())
        {
            var item = ArcMenuHelper.CreateItem(option.Label, option.Value, option.Value == Value, option.Disabled);
            if (theme is not null)
            {
                item.Theme = theme;
            }

            _menu.Items.Add(item);
        }

        var index = IndexOf(Value);
        _menu.SelectedIndex = index;

    }

    private void SyncSelection()
    {
        if (_menu is null)
        {
            return;
        }


        _menu.SelectedIndex = IndexOf(Value);

        UpdateHighlight();
    }

    private int IndexOf(string? value)
    {
        var options = Options ?? Array.Empty<ArcSelectOption>();
        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void OnMenuPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _menu?.SelectedItem is not ListBoxItem item)
        {
            return;
        }

        if (!item.IsEnabled)
        {
            return;
        }

        Commit(item.Tag as string);
        e.Handled = true;
    }

    private void OnMenuKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                if (_menu?.SelectedItem is ListBoxItem item && item.IsEnabled)
                {
                    Commit(item.Tag as string);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                SetCurrentValue(IsOpenProperty, false);
                e.Handled = true;
                break;
        }
    }

    private void Commit(string? value)
    {
        SetCurrentValue(ValueProperty, value);
        SetCurrentValue(IsOpenProperty, false);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        Focus();
    }

    private void ScrollSelectionIntoView()
    {
        if (_menu is not null && _menu.SelectedIndex >= 0)
        {
            _menu.ScrollIntoView(_menu.SelectedIndex);
        }
    }

    private void UpdateHighlight()
    {
        if (_highlight is null || _menu is null || _highlightTransform is null)
        {
            return;
        }

        if (_menu.SelectedIndex < 0 || _menu.ContainerFromIndex(_menu.SelectedIndex) is not Control container)
        {
            _highlight.IsVisible = false;
            return;
        }

        var point = container.TranslatePoint(new Point(0, 0), _menu);
        if (point is null)
        {
            return;
        }

        _highlight.Height = container.Bounds.Height;
        _highlight.IsVisible = true;
        _highlightTransform.Y = point.Value.Y;
    }

    private void RefreshValueText()
    {
        if (_valueText is null)
        {
            return;
        }

        var option = (Options ?? Array.Empty<ArcSelectOption>())
            .FirstOrDefault(candidate => string.Equals(candidate.Value, Value, StringComparison.Ordinal));

        if (option is null)
        {
            _valueText.Text = Placeholder;
            Classes.Set("empty", true);
        }
        else
        {
            _valueText.Text = option.Label;
            Classes.Set("empty", false);
        }

        AutomationProperties.SetName(this, Label ?? "Select");
        AutomationProperties.SetHelpText(this, option?.Label ?? Placeholder ?? string.Empty);
    }

    private void UpdateClasses()
    {
        Classes.Set("open", IsOpen);
        Classes.Set("filled", !string.IsNullOrEmpty(Value));
    }

    private void UpdateChevron()
    {
        if (_chevronTransform is not null)
        {
            _chevronTransform.Angle = IsOpen ? 180 : 0;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOpenProperty)
        {
            UpdateChevron();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
            case Key.Down:
            case Key.Up:
                SetCurrentValue(IsOpenProperty, true);
                e.Handled = true;
                break;
            case Key.Escape when IsOpen:
                SetCurrentValue(IsOpenProperty, false);
                e.Handled = true;
                break;
        }
    }
}
