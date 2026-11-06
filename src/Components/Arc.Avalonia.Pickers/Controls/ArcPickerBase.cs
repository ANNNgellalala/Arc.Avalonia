using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// Shared plumbing for the popup pickers: a field trigger, a floating panel that light-dismisses,
/// Escape to close and focus returned to the field. The panel itself is declared in each template.
/// </summary>
[TemplatePart("PART_Trigger", typeof(Button))]
[TemplatePart("PART_Popup", typeof(Popup))]
public abstract class ArcPickerBase : TemplatedControl
{
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<ArcPickerBase, bool>(nameof(IsDropDownOpen));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcPickerBase, string?>(nameof(Label));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcPickerBase, string?>(nameof(Placeholder));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcPickerBase, string?>(nameof(Description));

    private Button? _trigger;
    private Popup? _popup;
    private DateTime _lastPopupClosed = DateTime.MinValue;

    static ArcPickerBase()
    {
        IsDropDownOpenProperty.Changed.AddClassHandler<ArcPickerBase>((picker, _) => picker.UpdateOpenState());
    }

    /// <summary>Whether the floating panel is showing. Escape or a pick closes it.</summary>
    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>Visible label above the field.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Text shown while no value is set.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Helper copy under the field.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    protected Button? TriggerPart => _trigger;

    protected Popup? PopupPart => _popup;

    /// <summary>Noun used in the trigger's accessible name when no label is set.</summary>
    protected virtual string AccessibleRole => "Picker";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.RemoveHandler(KeyDownEvent, OnTriggerKeyDown);
        }

        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            if (_popup.Child is Control previous)
            {
                previous.RemoveHandler(KeyDownEvent, OnPopupKeyDown);
            }
        }

        _trigger = e.NameScope.Find<Button>("PART_Trigger");
        _popup = e.NameScope.Find<Popup>("PART_Popup");

        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.AddHandler(KeyDownEvent, OnTriggerKeyDown, RoutingStrategies.Tunnel);
        }

        if (_popup is not null)
        {
            _popup.PlacementTarget = this;
            _popup.Closed += OnPopupClosed;
            if (_popup.Child is Control child)
            {
                child.AddHandler(KeyDownEvent, OnPopupKeyDown, RoutingStrategies.Tunnel);
            }
        }

        UpdateOpenState();
    }

    /// <summary>Re-reads the content parts after a template change. Subclasses update their own text.</summary>
    protected virtual void OnContentChanged()
    {
    }

    /// <summary>Called after the panel opens, so the subclass can sync and focus its content.</summary>
    protected virtual void OnOpened()
    {
    }

    /// <summary>Called after the panel closes.</summary>
    protected virtual void OnClosed()
    {
    }

    /// <summary>Closes the panel and puts keyboard focus back on the field, as Arc does on Escape or a pick.</summary>
    protected void CloseAndFocus()
    {
        IsDropDownOpen = false;
        _trigger?.Focus();
    }

    protected void SetTriggerName(string? value)
    {
        if (_trigger is null)
        {
            return;
        }

        var label = string.IsNullOrWhiteSpace(Label) ? AccessibleRole : Label;
        AutomationProperties.SetName(_trigger, string.IsNullOrWhiteSpace(value) ? $"{label}, empty" : $"{label}, {value}");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsDropDownOpen)
        {
            CloseAndFocus();
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == PlaceholderProperty || change.Property == DescriptionProperty)
        {
            OnContentChanged();
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        // A click that dismissed the light-dismiss overlay must not immediately reopen the panel.
        if ((DateTime.UtcNow - _lastPopupClosed).TotalMilliseconds < 250)
        {
            e.Handled = true;
            return;
        }

        IsDropDownOpen = !IsDropDownOpen;
        e.Handled = true;
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    private void OnPopupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !IsDropDownOpen)
        {
            return;
        }

        CloseAndFocus();
        e.Handled = true;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _lastPopupClosed = DateTime.UtcNow;
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private void UpdateOpenState()
    {
        var open = IsDropDownOpen;
        if (_popup is not null && _popup.IsOpen != open)
        {
            _popup.IsOpen = open;
        }

        if (_trigger is not null)
        {
            _trigger.Classes.Set("open", open);
        }

        if (open)
        {
            OnOpened();
        }
        else
        {
            OnClosed();
        }
    }
}
