using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>One filter chip.</summary>
public sealed class ArcChipOption
{
    public ArcChipOption()
    {
    }

    public ArcChipOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Filter chips that morph when selected. Several can be active by default; single mode still lets the
/// active chip be cleared. Long sets fold behind a +N more chip, and selected chips always stay visible.
/// </summary>
[TemplatePart("PART_ChipPanel", typeof(WrapPanel))]
public class ArcChipGroup : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ArcChipOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcChipGroup, IReadOnlyList<ArcChipOption>?>(nameof(Options));

    public static readonly StyledProperty<IReadOnlyList<string>?> ValueProperty =
        AvaloniaProperty.Register<ArcChipGroup, IReadOnlyList<string>?>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcChipGroup, string?>(nameof(Label));

    public static readonly StyledProperty<bool> MultipleProperty =
        AvaloniaProperty.Register<ArcChipGroup, bool>(nameof(Multiple), true);

    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<ArcChipGroup, int>(nameof(MaxVisible), int.MaxValue);

    private WrapPanel? _chips;
    private Button? _more;
    private readonly List<Button> _buttons = new();
    private bool _expanded;

    static ArcChipGroup()
    {
        OptionsProperty.Changed.AddClassHandler<ArcChipGroup>((group, _) => group.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcChipGroup>((group, _) => group.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcChipGroup>((group, _) => group.UpdateAutomation());
        MultipleProperty.Changed.AddClassHandler<ArcChipGroup>((group, _) => group.Rebuild());
        MaxVisibleProperty.Changed.AddClassHandler<ArcChipGroup>((group, _) => group.Rebuild());
    }

    public event EventHandler? ValueChanged;

    public IReadOnlyList<ArcChipOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public IReadOnlyList<string>? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Accessible name of the group, for example "Topics".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool Multiple
    {
        get => GetValue(MultipleProperty);
        set => SetValue(MultipleProperty, value);
    }

    public int MaxVisible
    {
        get => GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _chips = e.NameScope.Find<WrapPanel>("PART_ChipPanel");
        Rebuild();
    }

    private void Rebuild()
    {
        if (_chips is null)
        {
            return;
        }

        var focusedValue = _buttons.Find(button => button.IsFocused)?.Tag as string;
        _chips.Children.Clear();
        _buttons.Clear();

        var options = Options ?? Array.Empty<ArcChipOption>();
        var selected = Value ?? Array.Empty<string>();

        var visible = _expanded
            ? options
            : options.Where((option, index) => index < MaxVisible || selected.Contains(option.Value, StringComparer.Ordinal)).ToList();

        foreach (var option in visible)
        {
            _chips.Children.Add(BuildChip(option, selected.Contains(option.Value, StringComparer.Ordinal)));
        }

        var hidden = options.Count - visible.Count;
        if (hidden > 0 || _expanded)
        {
            _more = new Button
            {
                Content = _expanded ? "Show less" : $"+{hidden} more",
                Classes = { "chip-more" },
                Margin = new Thickness(0, 0, 8, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (ArcMenuHelper.FindTheme(this, "ArcChipTheme") is { } theme)
            {
                _more.Theme = theme;
            }

            _more.Click += (_, _) =>
            {
                _expanded = !_expanded;
                Rebuild();
            };
            AutomationProperties.SetName(_more, _expanded ? "Show fewer filters" : $"Show {hidden} more filters");
            _chips.Children.Add(_more);
            _buttons.Add(_more);
        }
        else
        {
            _more = null;
        }

        UpdateTabStops();
        UpdateAutomation();

        if (focusedValue is not null)
        {
            var index = _buttons.FindIndex(button => string.Equals(button.Tag as string, focusedValue, StringComparison.Ordinal));
            if (index >= 0)
            {
                Dispatcher.UIThread.Post(() => FocusButton(index), DispatcherPriority.Loaded);
            }
        }
    }

    private Button BuildChip(ArcChipOption option, bool selected)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (selected)
        {
            row.Children.Add(new ArcIcon
            {
                Icon = "check",
                Size = 13,
                StrokeWidth = 2.2,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ArcResources.Brush(ArcTokens.AccentForeground)
            });
        }

        row.Children.Add(new TextBlock
        {
            Text = option.Label,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcResources.Brush(selected ? ArcTokens.AccentForeground : ArcTokens.Foreground)
        });

        var button = new Button
        {
            Content = row,
            Tag = option.Value,
            Classes = { "chip" },
            Margin = new Thickness(0, 0, 8, 8)
        };
        if (ArcMenuHelper.FindTheme(this, "ArcChipTheme") is { } theme)
        {
            button.Theme = theme;
        }

        button.Classes.Set("selected", selected);
        AutomationProperties.SetName(button, option.Label);
        AutomationProperties.SetHelpText(button, selected ? "Selected" : "Not selected");

        button.Click += (_, _) => Toggle(option.Value);

        _buttons.Add(button);
        return button;
    }

    private void Toggle(string value)
    {
        var options = Options ?? Array.Empty<ArcChipOption>();
        var selected = (Value ?? Array.Empty<string>()).ToList();

        if (!Multiple)
        {
            var next = selected.Contains(value, StringComparer.Ordinal) ? new List<string>() : new List<string> { value };
            SetCurrentValue(ValueProperty, next);
            ValueChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!selected.Remove(value))
        {
            selected.Add(value);
        }

        var ordered = options.Select(option => option.Value).Where(selected.Contains).ToList();
        SetCurrentValue(ValueProperty, ordered);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateTabStops()
    {
        if (_buttons.Count == 0)
        {
            return;
        }

        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsTabStop = i == 0;
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "Filters");
        AutomationProperties.SetHelpText(this, (Value?.Count ?? 0) == 0 ? "No filters selected" : $"{Value!.Count} filters selected");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_buttons.Count == 0)
        {
            return;
        }

        var focused = _buttons.FindIndex(button => button.IsFocused);
        if (focused < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                FocusButton((focused - 1 + _buttons.Count) % _buttons.Count);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                FocusButton((focused + 1) % _buttons.Count);
                e.Handled = true;
                break;
            case Key.Home:
                FocusButton(0);
                e.Handled = true;
                break;
            case Key.End:
                FocusButton(_buttons.Count - 1);
                e.Handled = true;
                break;
        }
    }

    private void FocusButton(int index)
    {
        foreach (var button in _buttons)
        {
            button.IsTabStop = false;
        }

        var target = _buttons[index];
        target.IsTabStop = true;
        target.Focus();
    }
}
