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
using Avalonia.Styling;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One visible radio row.</summary>
public sealed class ArcRadioOption
{
    public ArcRadioOption()
    {
    }

    public ArcRadioOption(string value, string label, string? description = null)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// A small set of mutually exclusive options shown as rows with optional descriptions. Real radio
/// buttons keep native semantics; arrow keys move the selection with a roving tab stop.
/// </summary>
public class ArcRadioGroup : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ArcRadioOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcRadioGroup, IReadOnlyList<ArcRadioOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcRadioGroup, string?>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcRadioGroup, string?>(nameof(Label));

    private StackPanel? _rows;
    private readonly List<RadioButton> _buttons = new();
    private string _groupName = "arc-radio-" + Guid.NewGuid().ToString("N");
    private bool _syncing;

    static ArcRadioGroup()
    {
        OptionsProperty.Changed.AddClassHandler<ArcRadioGroup>((group, _) => group.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcRadioGroup>((group, _) => group.UpdateSelection());
        LabelProperty.Changed.AddClassHandler<ArcRadioGroup>((group, _) => group.UpdateAutomation());
    }

    public event EventHandler? ValueChanged;

    public IReadOnlyList<ArcRadioOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Fieldset legend shown above the rows.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rows = e.NameScope.Find<StackPanel>("Rows");
        Rebuild();
    }

    private void Rebuild()
    {
        if (_rows is null)
        {
            return;
        }

        _rows.Children.Clear();
        _buttons.Clear();
        _groupName = "arc-radio-" + Guid.NewGuid().ToString("N");

        foreach (var option in Options ?? Array.Empty<ArcRadioOption>())
        {
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(new TextBlock
            {
                Text = option.Label,
                FontSize = 14,
                FontWeight = FontWeight.Medium
            });

            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                content.Children.Add(new TextBlock
                {
                    Text = option.Description,
                    FontSize = 12,
                    Foreground = ArcResources.Brush(ArcTokens.TextSecondary),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            var radio = new RadioButton
            {
                GroupName = _groupName,
                Content = content,
                Tag = option.Value,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (ArcMenuHelper.FindTheme(this, "ArcRadioButtonTheme") is { } theme)
            {
                radio.Theme = theme;
            }

            AutomationProperties.SetName(radio, option.Label);
            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                AutomationProperties.SetHelpText(radio, option.Description);
            }

            radio.IsCheckedChanged += (_, _) =>
            {
                if (_syncing)
                {
                    return;
                }

                SetCurrentValue(ValueProperty, option.Value);
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };

            _buttons.Add(radio);
            _rows.Children.Add(radio);
        }

        UpdateSelection();
        UpdateAutomation();
    }

    private void UpdateSelection()
    {
        _syncing = true;
        var hasSelection = false;

        for (var i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var isSelected = string.Equals(button.Tag as string, Value, StringComparison.Ordinal);
            button.IsChecked = isSelected;
            hasSelection |= isSelected;
        }

        for (var i = 0; i < _buttons.Count; i++)
        {
            var isSelected = string.Equals(_buttons[i].Tag as string, Value, StringComparison.Ordinal);
            _buttons[i].IsTabStop = isSelected || (!hasSelection && i == 0);
        }

        _syncing = false;
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "Options");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

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
        var value = button.Tag as string;
        if (!string.Equals(Value, value, StringComparison.Ordinal))
        {
            SetCurrentValue(ValueProperty, value);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        button.IsChecked = true;
        button.Focus();
        UpdateSelection();
    }
}
