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
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// Picks several values from a fixed list. Chosen values stay visible as small chips in the trigger,
/// with a +N overflow, while the menu rows carry checkboxes.
/// </summary>
[TemplatePart("PART_Trigger", typeof(Border))]
[TemplatePart("PART_Ring", typeof(Border))]
[TemplatePart("PART_ChipPanel", typeof(StackPanel))]
[TemplatePart("PART_PlaceholderText", typeof(TextBlock))]
[TemplatePart("PART_Overflow", typeof(TextBlock))]
[TemplatePart("PART_Chevron", typeof(ArcIcon))]
[TemplatePart("PART_Message", typeof(TextBlock))]
[TemplatePart("PART_MenuPopup", typeof(Popup))]
[TemplatePart("PART_MenuSurface", typeof(Border))]
[TemplatePart("PART_Menu", typeof(ListBox))]
public class ArcMultiSelect : ArcFieldBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcSelectOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcMultiSelect, IReadOnlyList<ArcSelectOption>?>(nameof(Options));

    public static readonly StyledProperty<IReadOnlyList<string>?> ValueProperty =
        AvaloniaProperty.Register<ArcMultiSelect, IReadOnlyList<string>?>(nameof(Value));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcMultiSelect, string?>(nameof(Placeholder), "Select options");

    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<ArcMultiSelect, int>(nameof(MaxVisible), 2);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcMultiSelect, bool>(nameof(IsOpen));

    private Border? _trigger;
    private Border? _ring;
    private StackPanel? _chipPanel;
    private TextBlock? _placeholder;
    private TextBlock? _overflow;
    private ArcIcon? _chevron;
    private RotateTransform? _chevronTransform;
    private TextBlock? _message;
    private Popup? _popup;
    private Border? _surface;
    private ListBox? _menu;

    static ArcMultiSelect()
    {
        ValueProperty.Changed.AddClassHandler<ArcMultiSelect>((control, _) =>
        {
            control.RebuildChips();
            control.UpdateClasses();
        });
        OptionsProperty.Changed.AddClassHandler<ArcMultiSelect>((control, _) =>
        {
            control.RebuildChips();
            control.RebuildMenu();
        });
        MaxVisibleProperty.Changed.AddClassHandler<ArcMultiSelect>((control, _) => control.RebuildChips());
        IsOpenProperty.Changed.AddClassHandler<ArcMultiSelect>((control, _) => control.OnOpenChanged());
    }

    public ArcMultiSelect()
    {
        Focusable = true;
        UpdateClasses();
    }

    public event EventHandler? ValueChanged;

    public IReadOnlyList<ArcSelectOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public IReadOnlyList<string>? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Chips shown before the rest collapse into +N.</summary>
    public int MaxVisible
    {
        get => GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
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

        _trigger = e.NameScope.Find<Border>("PART_Trigger");
        _ring = e.NameScope.Find<Border>("PART_Ring");
        _chipPanel = e.NameScope.Find<StackPanel>("PART_ChipPanel");
        _placeholder = e.NameScope.Find<TextBlock>("PART_PlaceholderText");
        _overflow = e.NameScope.Find<TextBlock>("PART_Overflow");
        _chevron = e.NameScope.Find<ArcIcon>("PART_Chevron");
        _chevronTransform = _chevron?.RenderTransform as RotateTransform;
        _message = e.NameScope.Find<TextBlock>("PART_Message");
        _popup = e.NameScope.Find<Popup>("PART_MenuPopup");
        _surface = e.NameScope.Find<Border>("PART_MenuSurface");
        _menu = e.NameScope.Find<ListBox>("PART_Menu");

        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
        }

        if (_popup is not null && _trigger is not null)
        {
            ArcMenuHelper.ConfigurePopup(_popup, _trigger);
            _popup.Closed += (_, _) => SetCurrentValue(IsOpenProperty, false);
        }

        if (_menu is not null)
        {
            _menu.AddHandler(PointerReleasedEvent, OnMenuPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        ApplyMessage(_message);
        RebuildChips();
        UpdateClasses();
    }

    protected override void OnMessageChanged() => ApplyMessage(_message);

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        SetCurrentValue(IsOpenProperty, !IsOpen);
        e.Handled = true;
    }

    private void OnOpenChanged()
    {
        UpdateClasses();

        if (_popup is not null)
        {
            if (IsOpen)
            {
                RebuildMenu();
                if (_surface is not null && _trigger is not null)
                {
                    _surface.MinWidth = Math.Max(160, _trigger.Bounds.Width);
                    _surface.MaxWidth = 384;
                }

                _popup.IsOpen = true;
                Dispatcher.UIThread.Post(() =>
                {
                    Focus();
                    UpdateMenuSelection();
                }, DispatcherPriority.Loaded);
            }
            else
            {
                _popup.IsOpen = false;
            }
        }

        if (!IsOpen)
        {
            Focus();
        }
    }

    private void RebuildChips()
    {
        if (_chipPanel is null)
        {
            return;
        }

        _chipPanel.Children.Clear();
        var selected = Value ?? Array.Empty<string>();
        var options = Options ?? Array.Empty<ArcSelectOption>();
        var labels = options
            .Where(option => selected.Contains(option.Value, StringComparer.Ordinal))
            .Select(option => option.Label)
            .ToList();

        if (labels.Count == 0)
        {
            if (_placeholder is not null)
            {
                _placeholder.Text = Placeholder;
                _placeholder.IsVisible = true;
            }

            if (_overflow is not null)
            {
                _overflow.IsVisible = false;
            }
        }
        else
        {
            if (_placeholder is not null)
            {
                _placeholder.IsVisible = false;
            }

            var visible = Math.Max(1, MaxVisible);
            foreach (var label in labels.Take(visible))
            {
                _chipPanel.Children.Add(BuildChip(label));
            }

            if (_overflow is not null)
            {
                _overflow.Text = $"+{labels.Count - visible}";
                _overflow.IsVisible = labels.Count > visible;
            }
        }

        AutomationProperties.SetName(this, Label ?? "Select options");
        AutomationProperties.SetHelpText(this, labels.Count == 0 ? Placeholder : string.Join(", ", labels));
    }

    private Border BuildChip(string label)
    {
        return new Border
        {
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(9, 3, 9, 3),
            Margin = new Thickness(0, 0, 5, 0),
            MaxWidth = 120,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private void RebuildMenu()
    {
        if (_menu is null)
        {
            return;
        }

        var theme = ArcMenuHelper.FindTheme(this, "ArcMenuItemTheme");
        var selected = Value ?? Array.Empty<string>();

        _menu.Focusable = false;
        _menu.Items.Clear();

        foreach (var option in Options ?? Array.Empty<ArcSelectOption>())
        {
            var isSelected = selected.Contains(option.Value, StringComparer.Ordinal);
            var item = CreateCheckRow(option, isSelected);
            if (theme is not null)
            {
                item.Theme = theme;
            }

            _menu.Items.Add(item);
        }


        UpdateMenuSelection();
    }

    private ListBoxItem CreateCheckRow(ArcSelectOption option, bool selected)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };

        var box = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1.5),
            BorderBrush = ArcResources.Brush(selected ? ArcTokens.ControlOn : ArcTokens.BorderStrong),
            Background = selected ? ArcResources.Brush(ArcTokens.ControlOn) : Brushes.Transparent,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new ArcIcon
            {
                Icon = "check",
                Size = 12,
                StrokeWidth = 2.2,
                IsVisible = selected,
                Foreground = ArcResources.Brush(ArcTokens.ControlGlyph),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(box);

        var text = new TextBlock
        {
            Text = option.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = option.Disabled ? 0.45 : 1
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var item = new ListBoxItem
        {
            Content = grid,
            Tag = option.Value,
            IsEnabled = true,
            Focusable = false,
            IsTabStop = false,
            MinHeight = 38
        };
        TextSearch.SetText(item, option.Label);
        AutomationProperties.SetName(item, option.Label);
        AutomationProperties.SetHelpText(item, selected ? "Selected" : "Not selected");

        return item;
    }

    private void UpdateMenuSelection()
    {
        if (_menu is null)
        {
            return;
        }


        var selected = Value ?? Array.Empty<string>();
        for (var i = 0; i < _menu.ItemCount && i < (Options?.Count ?? 0); i++)
        {
            var option = Options![i];
            if (_menu.Items[i] is ListBoxItem item)
            {
                item.Classes.Set("checked", selected.Contains(option.Value, StringComparer.Ordinal));
            }
        }

        if (_menu.SelectedIndex < 0 && _menu.ItemCount > 0)
        {
            _menu.SelectedIndex = 0;
        }


    }

    private void Toggle(string value)
    {
        var selected = (Value ?? Array.Empty<string>()).ToList();
        if (!selected.Remove(value))
        {
            selected.Add(value);
        }

        // Keep the field order stable: follow option order.
        var ordered = (Options ?? Array.Empty<ArcSelectOption>())
            .Select(option => option.Value)
            .Where(selected.Contains)
            .ToList();

        SetCurrentValue(ValueProperty, ordered.Count == 0 ? Array.Empty<string>() : ordered);
        RebuildChips();
        RebuildMenu();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        Focus();
    }

    private void OnMenuPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _menu?.SelectedItem is not ListBoxItem item ||
            item.Tag is not string value)
        {
            return;
        }

        Toggle(value);
        e.Handled = true;
    }

    private void MoveActive(int delta)
    {
        if (_menu is null || _menu.ItemCount == 0)
        {
            return;
        }

        var index = _menu.SelectedIndex;
        index = (index + delta + _menu.ItemCount) % _menu.ItemCount;
        _menu.SelectedIndex = index;
        _menu.ScrollIntoView(index);
    }

    private void OnChevron()
    {
        if (_chevronTransform is not null)
        {
            _chevronTransform.Angle = IsOpen ? 180 : 0;
        }
    }

    private void UpdateClasses()
    {
        Classes.Set("open", IsOpen);
        Classes.Set("filled", (Value?.Count ?? 0) > 0);
        OnChevron();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                if (!IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, true);
                }
                else if (_menu?.SelectedItem is ListBoxItem item && item.Tag is string value)
                {
                    Toggle(value);
                }

                e.Handled = true;
                break;
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
            case Key.Escape when IsOpen:
                SetCurrentValue(IsOpenProperty, false);
                e.Handled = true;
                break;
        }
    }
}
