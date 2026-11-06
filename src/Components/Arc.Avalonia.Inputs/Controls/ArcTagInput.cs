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
using Avalonia.Styling;
using Path = global::Avalonia.Controls.Shapes.Path;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A field that turns typed text into removable tags. Enter or comma adds, Backspace removes the last
/// tag, and the arrow keys pick tags so Delete can remove a specific one.
/// </summary>
[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_Message", typeof(TextBlock))]
[TemplatePart("PART_TagPanel", typeof(WrapPanel))]
[TemplatePart("PART_Shell", typeof(Border))]
public class ArcTagInput : ArcFieldBase
{
    public static readonly StyledProperty<IReadOnlyList<string>?> ValueProperty =
        AvaloniaProperty.Register<ArcTagInput, IReadOnlyList<string>?>(nameof(Value));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcTagInput, string?>(nameof(Placeholder), "Add a tag");

    private TextBox? _input;
    private TextBlock? _message;
    private WrapPanel? _panel;
    private Border? _shell;
    private int _picked = -1;
    private bool _syncing;

    static ArcTagInput()
    {
        ValueProperty.Changed.AddClassHandler<ArcTagInput>((field, _) => field.RebuildTags());
        PlaceholderProperty.Changed.AddClassHandler<ArcTagInput>((field, _) =>
        {
            if (field._input is not null)
            {
                field._input.PlaceholderText = field.Placeholder;
            }
        });
    }

    /// <summary>Raised with the full tag list after each add or remove.</summary>
    public event EventHandler? ValueChanged;

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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_input is not null)
        {
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
            _input.TextChanged -= OnInputTextChanged;
            _input.GotFocus -= OnInputGotFocus;
        }

        _input = e.NameScope.Find<TextBox>("PART_Input");
        _message = e.NameScope.Find<TextBlock>("PART_Message");
        _panel = e.NameScope.Find<WrapPanel>("PART_TagPanel");
        _shell = e.NameScope.Find<Border>("PART_Shell");

        if (_input is not null)
        {
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
            _input.TextChanged += OnInputTextChanged;
            _input.GotFocus += OnInputGotFocus;
            _input.PlaceholderText = Placeholder;
            AutomationProperties.SetName(_input, Label ?? "Tags");
        }

        ApplyMessage(_message);
        RebuildTags();
    }

    protected override void OnMessageChanged()
    {
        ApplyMessage(_message);
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        // Typing after picking a tag returns focus to the draft input.
        if (!string.IsNullOrEmpty(_input?.Text))
        {
            Pick(-1);
        }

        UpdatePlaceholder();
    }

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        Classes.Set("focus", false);
        CommitDraft();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.OemComma:
                CommitDraft();
                e.Handled = true;
                break;
            case Key.Back:
                if (string.IsNullOrEmpty(_input?.Text))
                {
                    if (_picked >= 0)
                    {
                        RemoveAt(_picked);
                    }
                    else
                    {
                        RemoveAt((Value?.Count ?? 0) - 1);
                    }

                    e.Handled = true;
                }

                break;
            case Key.Delete:
                if (_picked >= 0)
                {
                    RemoveAt(_picked);
                    e.Handled = true;
                }

                break;
            case Key.Left:
                if (string.IsNullOrEmpty(_input?.Text) && (Value?.Count ?? 0) > 0)
                {
                    Pick(_picked <= 0 ? (Value?.Count ?? 1) - 1 : _picked - 1);
                    e.Handled = true;
                }

                break;
            case Key.Right:
                if (_picked >= 0)
                {
                    Pick(_picked >= (Value?.Count ?? 0) - 1 ? -1 : _picked + 1);
                    e.Handled = true;
                }

                break;
            case Key.Escape:
                Pick(-1);
                break;
        }
    }

    private void CommitDraft()
    {
        var text = _input?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var tags = (Value ?? Array.Empty<string>()).ToList();
        if (tags.Any(tag => string.Equals(tag, text, StringComparison.OrdinalIgnoreCase)))
        {
            _syncing = true;
            _input!.Text = string.Empty;
            _syncing = false;
            Pick(tags.FindIndex(tag => string.Equals(tag, text, StringComparison.OrdinalIgnoreCase)));
            return;
        }

        tags.Add(text);
        _syncing = true;
        _input!.Text = string.Empty;
        _syncing = false;
        SetCurrentValue(ValueProperty, tags);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        _input.Focus();
    }

    private void RemoveAt(int index)
    {
        var tags = (Value ?? Array.Empty<string>()).ToList();
        if (index < 0 || index >= tags.Count)
        {
            return;
        }

        tags.RemoveAt(index);
        Pick(-1);
        SetCurrentValue(ValueProperty, tags);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        _input?.Focus();
    }

    private void Pick(int index)
    {
        _picked = index;
        if (_panel is null)
        {
            return;
        }

        for (var i = 0; i < _panel.Children.Count; i++)
        {
            if (_panel.Children[i] is Border chip)
            {
                var chipIndex = (int)(chip.Tag ?? -1);
                chip.Classes.Set("picked", chipIndex == _picked);
            }
        }
    }

    private void RebuildTags()
    {
        if (_panel is null || _input is null)
        {
            return;
        }

        _panel.Children.Clear();
        var tags = Value ?? Array.Empty<string>();

        for (var i = 0; i < tags.Count; i++)
        {
            _panel.Children.Add(BuildChip(tags[i], i));
        }

        _panel.Children.Add(_input);
        UpdatePlaceholder();
        Pick(Math.Min(_picked, tags.Count - 1));
    }

    private Border BuildChip(string tag, int index)
    {
        var text = new TextBlock
        {
            Text = tag,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcResources.Brush(ArcTokens.Foreground)
        };

        var remove = new Button
        {
            Width = 18,
            Height = 18,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Path
            {
                Data = Geometry.Parse("M6 6 L18 18 M18 6 L6 18"),
                Stroke = ArcResources.Brush(ArcTokens.TextMuted),
                StrokeThickness = 1.75,
                StrokeLineCap = PenLineCap.Round,
                Stretch = Stretch.Uniform,
                Width = 11,
                Height = 11
            }
        };

        if (this.TryFindResource("ArcIconButtonTheme", out var theme) && theme is ControlTheme controlTheme)
        {
            remove.Theme = controlTheme;
        }

        AutomationProperties.SetName(remove, $"Remove {tag}");
        remove.Click += (_, _) => RemoveAt(index);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(text);
        row.Children.Add(remove);

        var chip = new Border
        {
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(10, 3, 4, 3),
            Margin = new Thickness(0, 0, 6, 6),
            Child = row,
            Tag = index
        };

        chip.Classes.Add("tag-chip");
        return chip;
    }

    private void UpdatePlaceholder()
    {
        if (_input is not null)
        {
            _input.PlaceholderText = (Value?.Count ?? 0) == 0 ? Placeholder : null;
        }
    }
}
