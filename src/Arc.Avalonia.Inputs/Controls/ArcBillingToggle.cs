using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcBillingToggleSize
{
    Md,
    Lg
}

/// <summary>One period of the billing switch, with the savings note it shows once chosen.</summary>
public sealed class ArcBillingToggleOption
{
    public ArcBillingToggleOption(string value, string label, string? badge = null, string? activeBadge = null)
    {
        Value = value;
        Label = label;
        Badge = badge;
        ActiveBadge = activeBadge;
    }

    public string Value { get; }

    public string Label { get; }

    /// <summary>Short savings note, such as "Save 20%".</summary>
    public string? Badge { get; }

    /// <summary>Badge text once this option is selected, such as "You save $48"; defaults to the badge.</summary>
    public string? ActiveBadge { get; }
}

/// <summary>
/// Arc's billing toggle: a monthly and yearly switch with a savings badge. One thumb glides between
/// the options and the badge tints and rewrites itself in place once its period is chosen.
/// </summary>
public class ArcBillingToggle : TemplatedControl
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcBillingToggle, string?>(nameof(Value));

    public static readonly StyledProperty<IList<ArcBillingToggleOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcBillingToggle, IList<ArcBillingToggleOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcBillingToggle, string?>(nameof(Label), "Billing period");

    public static readonly StyledProperty<ArcBillingToggleSize> SizeProperty =
        AvaloniaProperty.Register<ArcBillingToggle, ArcBillingToggleSize>(nameof(Size));

    private Border? _track;
    private Panel? _host;
    private Border? _thumb;
    private StackPanel? _optionsHost;
    private readonly List<OptionVisual> _visuals = new();
    private bool _syncing;

    static ArcBillingToggle()
    {
        ValueProperty.Changed.AddClassHandler<ArcBillingToggle>((toggle, _) => toggle.ApplyValue());
        OptionsProperty.Changed.AddClassHandler<ArcBillingToggle>((toggle, _) => toggle.Build());
        SizeProperty.Changed.AddClassHandler<ArcBillingToggle>((toggle, _) => toggle.Build());
    }

    public ArcBillingToggle()
    {
        ClipToBounds = false;
    }

    /// <summary>Raised with the chosen option's value on click or arrow key.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Value of the selected option.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Options in order; defaults to monthly and yearly with a Save 20% badge.</summary>
    public IList<ArcBillingToggleOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Accessible name of the radio group.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Md is 36 px tall per option, Lg is 44 px with larger text.</summary>
    public ArcBillingToggleSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>The selected option, if any.</summary>
    public ArcBillingToggleOption? SelectedOption =>
        EffectiveOptions().FirstOrDefault(option => string.Equals(option.Value, Value, StringComparison.Ordinal));

    private IList<ArcBillingToggleOption> EffectiveOptions() => Options ?? Defaults;

    private static readonly IList<ArcBillingToggleOption> Defaults = new List<ArcBillingToggleOption>
    {
        new ArcBillingToggleOption("monthly", "Monthly"),
        new ArcBillingToggleOption("yearly", "Yearly", "Save 20%", "You save $48")
    };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_optionsHost is not null)
        {
            _optionsHost.RemoveHandler(KeyDownEvent, OnOptionsKeyDown);
        }

        _track = e.NameScope.Find<Border>("Track");
        _host = e.NameScope.Find<Panel>("Host");
        _thumb = e.NameScope.Find<Border>("Thumb");
        _optionsHost = e.NameScope.Find<StackPanel>("OptionsHost");

        if (_optionsHost is not null)
        {
            _optionsHost.AddHandler(KeyDownEvent, OnOptionsKeyDown, RoutingStrategies.Tunnel);
        }

        Build();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateThumb(animate: false);
        }
    }

    private ControlTheme? OptionTheme =>
        Application.Current is { } app && app.TryFindResource("ArcSegmentOptionTheme", out var value)
            ? value as ControlTheme
            : null;

    private void Build()
    {
        if (_optionsHost is null)
        {
            return;
        }

        _optionsHost.Children.Clear();
        _visuals.Clear();
        _syncing = true;

        var options = EffectiveOptions();
        var height = Size == ArcBillingToggleSize.Lg ? 44d : 36d;
        var fontSize = Size == ArcBillingToggleSize.Lg ? 15d : 14d;
        var group = "arc-billing-" + Guid.NewGuid().ToString("N");

        foreach (var option in options)
        {
            var label = new TextBlock
            {
                Text = option.Label,
                FontSize = fontSize,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(label);

            Border? badge = null;
            TextBlock? badgeText = null;
            if (!string.IsNullOrEmpty(option.Badge) || !string.IsNullOrEmpty(option.ActiveBadge))
            {
                badgeText = new TextBlock
                {
                    Text = option.Badge ?? option.ActiveBadge,
                    FontSize = 11,
                    FontWeight = FontWeight.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge = new Border
                {
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(7, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = badgeText,
                    MinWidth = Reserve(option)
                };
                row.Children.Add(badge);
            }

            var button = new RadioButton
            {
                Theme = OptionTheme,
                Content = row,
                GroupName = group,
                Height = height,
                FontSize = fontSize,
                Padding = new Thickness(Size == ArcBillingToggleSize.Lg ? 18 : 14, 0),
                Tag = option,
                IsChecked = string.Equals(option.Value, Value, StringComparison.Ordinal) ||
                            (string.IsNullOrEmpty(Value) && ReferenceEquals(option, options.FirstOrDefault()))
            };

            button.IsTabStop = button.IsChecked == true;
            button.IsCheckedChanged += (_, _) => { if (button.IsChecked == true) { OnOptionChecked(option); } };
            button.Click += (_, _) => OnOptionChecked(option);
            button.AddHandler(KeyDownEvent, OnOptionKeyDown, RoutingStrategies.Tunnel);

            _optionsHost.Children.Add(button);
            _visuals.Add(new OptionVisual(option, button, badge, badgeText));
        }

        _syncing = false;

        if (string.IsNullOrEmpty(Value) && options.Count > 0)
        {
            SetCurrentValue(ValueProperty, options[0].Value);
        }

        ApplyValue();
    }

    private static double Reserve(ArcBillingToggleOption option)
    {
        var candidates = new[] { option.Badge, option.ActiveBadge }
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(text => ArcText.Create(text!, 11, ArcResources.Brush(ArcTokens.Foreground)))
            .ToList();

        return candidates.Count == 0 ? 0 : candidates.Max(text => text.Width) + 14;
    }

    private void OnOptionChecked(ArcBillingToggleOption option)
    {
        if (_syncing)
        {
            return;
        }

        if (string.Equals(Value, option.Value, StringComparison.Ordinal))
        {
            return;
        }

        SetCurrentValue(ValueProperty, option.Value);
        ApplyValue();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnOptionsKeyDown(object? sender, KeyEventArgs e) => OnOptionKeyDown(sender, e);

    private void OnOptionKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        var options = EffectiveOptions();
        if (options.Count == 0)
        {
            return;
        }

        var index = Math.Max(0, options.ToList().FindIndex(option =>
            string.Equals(option.Value, Value, StringComparison.Ordinal)));

        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                e.Handled = true;
                Select(options[(index + 1) % options.Count]);
                break;
            case Key.Left:
            case Key.Up:
                e.Handled = true;
                Select(options[(index - 1 + options.Count) % options.Count]);
                break;
            case Key.Home:
                e.Handled = true;
                Select(options[0]);
                break;
            case Key.End:
                e.Handled = true;
                Select(options[^1]);
                break;
        }
    }

    private void Select(ArcBillingToggleOption option)
    {
        if (string.Equals(Value, option.Value, StringComparison.Ordinal))
        {
            return;
        }

        SetCurrentValue(ValueProperty, option.Value);
        ApplyValue();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        _visuals.FirstOrDefault(visual => ReferenceEquals(visual.Option, option))?.Button.Focus();
    }

    private void ApplyValue()
    {
        if (_visuals.Count == 0)
        {
            return;
        }

        var selected = SelectedOption ?? EffectiveOptions().FirstOrDefault();

        foreach (var visual in _visuals)
        {
            var active = ReferenceEquals(visual.Option, selected) ||
                         string.Equals(visual.Option.Value, selected?.Value, StringComparison.Ordinal);
            _syncing = true;
            visual.Button.IsChecked = active;
            visual.Button.IsTabStop = active;
            _syncing = false;

            if (visual.Badge is not null && visual.BadgeText is not null)
            {
                visual.BadgeText.Text = active
                    ? visual.Option.ActiveBadge ?? visual.Option.Badge
                    : visual.Option.Badge ?? visual.Option.ActiveBadge;
                visual.BadgeText.Foreground = ArcResources.Brush(active ? ArcTokens.Success : ArcTokens.TextSecondary);
                visual.Badge.Background = active
                    ? ArcResources.Brush(ArcTokens.SuccessSubtle)
                    : ArcResources.Brush(ArcTokens.SurfaceMuted);
                visual.Badge.IsVisible = !string.IsNullOrEmpty(visual.BadgeText.Text);
            }
        }

        UpdateThumb(animate: true);
        AutomationProperties.SetName(this, Label ?? "Billing period");
        AutomationProperties.SetHelpText(this, selected?.Label ?? string.Empty);
    }

    private void UpdateThumb(bool animate)
    {
        if (_thumb is null)
        {
            return;
        }

        var visual = _visuals.FirstOrDefault(entry => entry.Button.IsChecked == true) ?? _visuals.FirstOrDefault();
        if (visual is null)
        {
            _thumb.IsVisible = false;
            return;
        }

        var bounds = visual.Button.Bounds;
        if (bounds.Width <= 0)
        {
            return;
        }

        _thumb.IsVisible = true;
        var fromX = (_thumb.RenderTransform as TranslateTransform)?.X ?? bounds.X;
        var fromWidth = _thumb.Width;
        var height = Math.Max(24, bounds.Height);

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _thumb.Width = bounds.Width;
            _thumb.Height = height;
            _thumb.RenderTransform = new TranslateTransform(bounds.X, 0);
            _thumb.Margin = new Thickness(0, 0, 0, 0);
            return;
        }

        var startWidth = double.IsNaN(fromWidth) || fromWidth <= 0 ? bounds.Width : fromWidth;
        ArcAnim.To(0, 1, TimeSpan.FromMilliseconds(340), ArcAnim.Smooth, value =>
        {
            if (_thumb is null)
            {
                return;
            }

            _thumb.Width = startWidth + (bounds.Width - startWidth) * value;
            var x = fromX + (bounds.X - fromX) * value;
            _thumb.RenderTransform = new TranslateTransform(x, 0);
        });
    }

    private sealed record OptionVisual(
        ArcBillingToggleOption Option,
        RadioButton Button,
        Border? Badge,
        TextBlock? BadgeText);
}
