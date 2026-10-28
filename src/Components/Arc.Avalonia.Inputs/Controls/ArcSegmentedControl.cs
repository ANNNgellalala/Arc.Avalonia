using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One segment in a segmented control.</summary>
public sealed class ArcSegmentOption
{
    public ArcSegmentOption()
    {
    }

    public ArcSegmentOption(string value, string label, string? accessory = null)
    {
        Value = value;
        Label = label;
        Accessory = accessory;
    }

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Small trailing note, such as a count or badge.</summary>
    public string? Accessory { get; set; }
}

/// <summary>Reports the value a pointer or keyboard focus is about to choose.</summary>
public sealed class ArcSegmentIntentEventArgs : EventArgs
{
    public ArcSegmentIntentEventArgs(string? value) => Value = value;

    public string? Value { get; }
}

/// <summary>
/// Two to five short, mutually exclusive view options. One shared pill glides between segments on the
/// morph spring; arrows, Home and End select, and only the selected segment is a tab stop.
/// </summary>
public class ArcSegmentedControl : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ArcSegmentOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcSegmentedControl, IReadOnlyList<ArcSegmentOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcSegmentedControl, string?>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcSegmentedControl, string?>(nameof(Label));

    private StackPanel? _segments;
    private Border? _pill;
    private TranslateTransform? _pillTransform;
    private readonly List<Button> _buttons = new();

    static ArcSegmentedControl()
    {
        OptionsProperty.Changed.AddClassHandler<ArcSegmentedControl>((control, _) => control.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcSegmentedControl>((control, _) => control.OnValueChanged());
        LabelProperty.Changed.AddClassHandler<ArcSegmentedControl>((control, _) => control.UpdateAutomation());
    }

    public event EventHandler? ValueChanged;

    public event EventHandler<ArcSegmentIntentEventArgs>? OptionIntent;

    public IReadOnlyList<ArcSegmentOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Accessible name for the group.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _segments = e.NameScope.Find<StackPanel>("Segments");
        _pill = e.NameScope.Find<Border>("Pill");
        _pillTransform = _pill?.RenderTransform as TranslateTransform;
        SizeChanged += (_, _) => UpdatePill(false);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_segments is null)
        {
            return;
        }

        _segments.Children.Clear();
        _buttons.Clear();

        foreach (var option in Options ?? Array.Empty<ArcSegmentOption>())
        {
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new TextBlock
            {
                Text = option.Label,
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (!string.IsNullOrWhiteSpace(option.Accessory))
            {
                content.Children.Add(new TextBlock
                {
                    Text = option.Accessory,
                    FontSize = 11,
                    Foreground = ArcResources.Brush(ArcTokens.TextMuted),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var button = new Button
            {
                Content = content,
                Tag = option.Value,
                MinHeight = 34,
                Padding = new Thickness(18, 0)
            };

            if (ArcMenuHelper.FindTheme(this, "ArcSegmentButtonTheme") is { } theme)
            {
                button.Theme = theme;
            }

            AutomationProperties.SetName(button, option.Label);
            button.Click += (_, _) => Commit(option.Value);
            button.PointerEntered += (_, _) => OptionIntent?.Invoke(this, new ArcSegmentIntentEventArgs(option.Value));
            button.GotFocus += (_, _) => OptionIntent?.Invoke(this, new ArcSegmentIntentEventArgs(option.Value));
            button.KeyDown += OnSegmentKeyDown;

            _buttons.Add(button);
            _segments.Children.Add(button);
        }

        UpdateSelection();
        UpdateAutomation();
        Dispatcher.UIThread.Post(() => UpdatePill(false), DispatcherPriority.Loaded);
    }

    private void Commit(string? value)
    {
        if (string.Equals(Value, value, StringComparison.Ordinal))
        {
            return;
        }

        SetCurrentValue(ValueProperty, value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnValueChanged()
    {
        UpdateSelection();
        Dispatcher.UIThread.Post(() => UpdatePill(true), DispatcherPriority.Loaded);
    }

    private void UpdateSelection()
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            var selected = string.Equals(_buttons[i].Tag as string, Value, StringComparison.Ordinal);
            _buttons[i].Classes.Set("selected", selected);
            _buttons[i].IsTabStop = selected;
            AutomationProperties.SetHelpText(_buttons[i], selected ? "Selected" : "Not selected");

            if (_buttons[i].Content is StackPanel content && content.Children.Count > 0 &&
                content.Children[0] is TextBlock label)
            {
                label.Foreground = ArcResources.Brush(selected ? ArcTokens.Foreground : ArcTokens.TextSecondary);
            }
        }
    }

    private void UpdatePill(bool animate)
    {
        if (_pill is null || _pillTransform is null)
        {
            return;
        }

        var selected = _buttons.FirstOrDefault(button => string.Equals(button.Tag as string, Value, StringComparison.Ordinal));
        if (selected is null || selected.Bounds.Width <= 0)
        {
            _pill.IsVisible = false;
            return;
        }

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            var transitions = _pill.Transitions;
            var transformTransitions = _pillTransform.Transitions;
            _pill.Transitions = null;
            _pillTransform.Transitions = null;
            _pill.Width = selected.Bounds.Width;
            _pillTransform.X = selected.Bounds.X;
            _pill.Transitions = transitions;
            _pillTransform.Transitions = transformTransitions;
        }
        else
        {
            _pill.Width = selected.Bounds.Width;
            _pillTransform.X = selected.Bounds.X;
        }

        _pill.IsVisible = true;
    }

    private void OnSegmentKeyDown(object? sender, KeyEventArgs e)
    {
        if (_buttons.Count == 0)
        {
            return;
        }

        var current = _buttons.FindIndex(button => button.IsFocused);
        if (current < 0)
        {
            current = _buttons.FindIndex(button => string.Equals(button.Tag as string, Value, StringComparison.Ordinal));
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                Select((current - 1 + _buttons.Count) % _buttons.Count);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                Select((current + 1) % _buttons.Count);
                e.Handled = true;
                break;
            case Key.Home:
                Select(0);
                e.Handled = true;
                break;
            case Key.End:
                Select(_buttons.Count - 1);
                e.Handled = true;
                break;
        }
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _buttons.Count)
        {
            return;
        }

        var button = _buttons[index];
        Commit(button.Tag as string);
        button.Focus();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "View");
    }
}
