using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// A labelled light and dark control that swaps a sun and moon in place and reports the change, so the
/// page can run its own transition. Wired to <see cref="ArcThemeManager"/> when used in the gallery.
/// </summary>
public sealed class ArcThemeSwitcher : ArcButton
{
    public static readonly StyledProperty<ArcColorScheme> SchemeProperty =
        AvaloniaProperty.Register<ArcThemeSwitcher, ArcColorScheme>(nameof(Scheme));

    public static readonly StyledProperty<bool> IconOnlyProperty =
        AvaloniaProperty.Register<ArcThemeSwitcher, bool>(nameof(IconOnly));

    protected override Type StyleKeyOverride => typeof(ArcButton);

    static ArcThemeSwitcher()
    {
        SchemeProperty.Changed.AddClassHandler<ArcThemeSwitcher>((switcher, _) => switcher.UpdateFace());
        IconOnlyProperty.Changed.AddClassHandler<ArcThemeSwitcher>((switcher, _) => switcher.UpdateFace());
    }

    public ArcThemeSwitcher()
    {
        Variant = ArcButtonVariant.Secondary;
        Size = ArcButtonSize.Sm;
        Classes.Add("theme-switcher");
        Scheme = ArcThemeManager.Scheme;
        UpdateFace();
    }

    /// <summary>Raised with the scheme that should be applied next.</summary>
    public event EventHandler<ArcColorScheme>? ThemeChanged;

    /// <summary>The theme the control reports; drives the icon and the pressed state.</summary>
    public ArcColorScheme Scheme
    {
        get => GetValue(SchemeProperty);
        set => SetValue(SchemeProperty, value);
    }

    /// <summary>Hides the mode word and renders a compact square.</summary>
    public bool IconOnly
    {
        get => GetValue(IconOnlyProperty);
        set => SetValue(IconOnlyProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        var next = Scheme == ArcColorScheme.Dark ? ArcColorScheme.Light : ArcColorScheme.Dark;
        SetCurrentValue(SchemeProperty, next);
        ThemeChanged?.Invoke(this, next);

        if (Application.Current is { } app)
        {
            ArcThemeManager.SetScheme(app, next);
        }
    }

    private void UpdateFace()
    {
        var dark = Scheme == ArcColorScheme.Dark;
        var mode = dark ? "Dark" : "Light";
        var icon = dark ? "moon" : "sun";
        Compact = IconOnly;

        if (IconOnly)
        {
            Content = ArcActionVisuals.InheritIcon(icon, this, 16);
        }
        else
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(ArcActionVisuals.InheritIcon(icon, this, 15));
            panel.Children.Add(ArcActionVisuals.InheritText(mode, this, medium: true));
            Content = panel;
        }

        Classes.Set("dark", dark);
        AutomationProperties.SetName(this, dark ? "Switch to light mode" : "Switch to dark mode");
        AutomationProperties.SetHelpText(this, $"Current appearance: {mode}");
    }
}
