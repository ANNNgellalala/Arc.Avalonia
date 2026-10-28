using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A setting that applies immediately. The thumb travels on a spring, the track takes the accent role
/// when on, and the whole row is the hit target. Space and Enter toggle.
/// </summary>
public class ArcSwitch : TemplatedControl
{
    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<ArcSwitch, bool>(nameof(Checked));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcSwitch, string?>(nameof(Label));

    private Border? _thumb;
    private Border? _track;
    private TranslateTransform? _thumbTransform;
    private bool _pointerDown;

    static ArcSwitch()
    {
        CheckedProperty.Changed.AddClassHandler<ArcSwitch>((control, _) => control.UpdateState());
        LabelProperty.Changed.AddClassHandler<ArcSwitch>((control, _) => control.UpdateAutomation());
    }

    public ArcSwitch()
    {
        Focusable = true;
        UpdateState();
        UpdateAutomation();
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    /// <summary>Visible label beside the track; also the accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _track = e.NameScope.Find<Border>("Track");
        _thumb = e.NameScope.Find<Border>("Thumb");
        _thumbTransform = _thumb?.RenderTransform as TranslateTransform;
        UpdateState();
        UpdateAutomation();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEnabled)
        {
            _pointerDown = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_pointerDown && IsEnabled)
        {
            Toggle();
        }

        _pointerDown = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            Toggle();
            e.Handled = true;
        }
    }

    private void Toggle()
    {
        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateState()
    {
        Classes.Set("checked", Checked);
        Classes.Set("disabled", !IsEnabled);

        if (_thumbTransform is not null)
        {
            _thumbTransform.X = Checked ? OnOffset : 0;
        }


        AutomationProperties.SetHelpText(this, Checked ? "On" : "Off");
    }

    private double OnOffset => 18;

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Switch" : Label);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEnabledProperty)
        {
            UpdateState();
        }
    }
}
