using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Arc.Avalonia.Controls;

public enum ArcButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

public enum ArcButtonSize
{
    Sm,
    Md,
    Lg
}

/// <summary>
/// Arc's button: one clear action with quiet secondary states, press feedback and an in-place spinner.
/// </summary>
public class ArcButton : Button
{
    public static readonly StyledProperty<ArcButtonVariant> VariantProperty =
        AvaloniaProperty.Register<ArcButton, ArcButtonVariant>(nameof(Variant), ArcButtonVariant.Primary);

    public static readonly StyledProperty<ArcButtonSize> SizeProperty =
        AvaloniaProperty.Register<ArcButton, ArcButtonSize>(nameof(Size), ArcButtonSize.Md);

    public static readonly StyledProperty<bool> LoadingProperty =
        AvaloniaProperty.Register<ArcButton, bool>(nameof(Loading));

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<ArcButton, bool>(nameof(Compact));

    static ArcButton()
    {
        VariantProperty.Changed.AddClassHandler<ArcButton>((button, _) => button.UpdateClasses());
        SizeProperty.Changed.AddClassHandler<ArcButton>((button, _) => button.UpdateClasses());
        LoadingProperty.Changed.AddClassHandler<ArcButton>((button, _) => { button.UpdateClasses(); if (button._spinner is not null) button._spinner.IsVisible = button.Loading; });
        CompactProperty.Changed.AddClassHandler<ArcButton>((button, _) => button.UpdateClasses());
    }

    public ArcButton()
    {
        UpdateClasses();
    }

    public ArcButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcButtonSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Shows a spinner, keeps keyboard focus and swallows clicks while true.</summary>
    public bool Loading
    {
        get => GetValue(LoadingProperty);
        set => SetValue(LoadingProperty, value);
    }

    /// <summary>Square padding for icon-sized buttons.</summary>
    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private Controls.ArcSpinner? _spinner;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _spinner = e.NameScope.Find<Controls.ArcSpinner>("Spinner");
        if (_spinner is not null)
        {
            _spinner.IsVisible = Loading;
            _spinner.Stroke = Foreground;
        }
    }

    protected override void OnClick()
    {
        if (Loading)
        {
            return;
        }

        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }

        if (change.Property == ForegroundProperty && _spinner is not null)
        {
            _spinner.Stroke = Foreground;
        }
    }

    private void UpdateClasses()
    {
        Classes.Set("primary", Variant == ArcButtonVariant.Primary);
        Classes.Set("secondary", Variant == ArcButtonVariant.Secondary);
        Classes.Set("ghost", Variant == ArcButtonVariant.Ghost);
        Classes.Set("danger", Variant == ArcButtonVariant.Danger);
        Classes.Set("sm", Size == ArcButtonSize.Sm);
        Classes.Set("md", Size == ArcButtonSize.Md);
        Classes.Set("lg", Size == ArcButtonSize.Lg);
        Classes.Set("loading", Loading);
        Classes.Set("compact", Compact);
    }
}

