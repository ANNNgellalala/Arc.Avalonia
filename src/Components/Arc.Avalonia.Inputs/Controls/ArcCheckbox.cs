using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A binary choice with a precise check and an indeterminate dash. Space toggles, the whole label row is
/// the hit target, and null <see cref="Checked"/> renders the mixed state.
/// </summary>
[TemplatePart("PART_CheckGlyph", typeof(ArcIcon))]
[TemplatePart("PART_DashGlyph", typeof(ArcIcon))]
public class ArcCheckbox : TemplatedControl
{
    public static readonly StyledProperty<bool?> CheckedProperty =
        AvaloniaProperty.Register<ArcCheckbox, bool?>(nameof(Checked), false);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcCheckbox, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcCheckbox, string?>(nameof(Description));

    static ArcCheckbox()
    {
        CheckedProperty.Changed.AddClassHandler<ArcCheckbox>((control, _) => control.UpdateClasses());
        LabelProperty.Changed.AddClassHandler<ArcCheckbox>((control, _) => control.UpdateAutomation());
        DescriptionProperty.Changed.AddClassHandler<ArcCheckbox>((control, _) => control.UpdateAutomation());
    }

    public ArcCheckbox()
    {
        Focusable = true;
        UpdateClasses();
        UpdateAutomation();
    }

    public event EventHandler? CheckedChanged;

    private ArcIcon? _check;
    private ArcIcon? _dash;

    /// <summary>True checked, false unchecked, null indeterminate.</summary>
    public bool? Checked
    {
        get => GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _check = e.NameScope.Find<ArcIcon>("PART_CheckGlyph");
        _dash = e.NameScope.Find<ArcIcon>("PART_DashGlyph");
        UpdateClasses();
        UpdateAutomation();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (e.InitialPressMouseButton == MouseButton.Left && IsEnabled)
        {
            Toggle();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space)
        {
            Toggle();
            e.Handled = true;
        }
    }

    private void Toggle()
    {
        Checked = Checked != true;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateClasses()
    {
        Classes.Set("checked", Checked == true);
        Classes.Set("indeterminate", Checked is null);
        Classes.Set("disabled", !IsEnabled);

        if (_check is not null)
        {
            _check.IsVisible = Checked == true;
        }

        if (_dash is not null)
        {
            _dash.IsVisible = Checked is null;
        }
        AutomationProperties.SetHelpText(this, Checked switch
        {
            true => "Checked",
            false => "Unchecked",
            _ => "Mixed"
        });
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Checkbox" : Label);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEnabledProperty)
        {
            UpdateClasses();
        }
    }
}
