using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// The open Color surface: saturation and brightness square, hue and opacity ramps, a typed value, a
/// WCAG contrast readout and the saved swatch row. Reused inside <see cref="ArcColorPicker"/> and shown
/// inline in the gallery so the open state is visible in screenshots.
/// </summary>
[TemplatePart("PART_Area", typeof(ArcColorArea))]
[TemplatePart("PART_Hue", typeof(ArcColorRamp))]
[TemplatePart("PART_Alpha", typeof(ArcColorRamp))]
[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_Format", typeof(ArcButton))]
[TemplatePart("PART_Swatches", typeof(StackPanel))]
[TemplatePart("PART_Contrast", typeof(StackPanel))]
[TemplatePart("PART_Error", typeof(TextBlock))]
public class ArcColorEditor : TemplatedControl
{
    public static readonly StyledProperty<Color> ValueProperty =
        AvaloniaProperty.Register<ArcColorEditor, Color>(nameof(Value));

    public static readonly StyledProperty<IReadOnlyList<ArcColorSwatch>?> SwatchesProperty =
        AvaloniaProperty.Register<ArcColorEditor, IReadOnlyList<ArcColorSwatch>?>(nameof(Swatches));

    public static readonly StyledProperty<IReadOnlyList<ArcColorSwatch>?> DefaultSwatchesProperty =
        AvaloniaProperty.Register<ArcColorEditor, IReadOnlyList<ArcColorSwatch>?>(nameof(DefaultSwatches));

    public static readonly StyledProperty<int> MaxSwatchesProperty =
        AvaloniaProperty.Register<ArcColorEditor, int>(nameof(MaxSwatches), 7);

    public static readonly StyledProperty<ArcColorFormat> FormatProperty =
        AvaloniaProperty.Register<ArcColorEditor, ArcColorFormat>(nameof(Format));

    private readonly List<ArcColorSwatch> _swatches = new();
    private readonly List<(Button Button, ArcColorSwatch Swatch)> _swatchButtons = new();
    private ArcColorArea? _area;
    private ArcColorRamp? _hue;
    private ArcColorRamp? _alpha;
    private TextBox? _input;
    private ArcButton? _formatButton;
    private StackPanel? _swatchRow;
    private StackPanel? _contrastRow;
    private TextBlock? _error;
    private ArcHsv _hsv;
    private bool _syncing;

    static ArcColorEditor()
    {
        ValueProperty.Changed.AddClassHandler<ArcColorEditor>((editor, _) => editor.SyncFromValue());
        SwatchesProperty.Changed.AddClassHandler<ArcColorEditor>((editor, _) => editor.RebuildSwatches());
        DefaultSwatchesProperty.Changed.AddClassHandler<ArcColorEditor>((editor, _) => editor.RebuildSwatches());
        FormatProperty.Changed.AddClassHandler<ArcColorEditor>((editor, _) => editor.UpdateInput());
    }

    public ArcColorEditor()
    {
        SetCurrentValue(ValueProperty, ArcResources.Color(ArcTokens.Accent));
        _hsv = ArcHsv.FromColor(Value);
    }

    /// <summary>Current color, alpha included.</summary>
    public Color Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Controlled saved swatches. When null the editor keeps its own list.</summary>
    public IReadOnlyList<ArcColorSwatch>? Swatches
    {
        get => GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    /// <summary>Starting swatches when uncontrolled.</summary>
    public IReadOnlyList<ArcColorSwatch>? DefaultSwatches
    {
        get => GetValue(DefaultSwatchesProperty);
        set => SetValue(DefaultSwatchesProperty, value);
    }

    /// <summary>Most saved swatches kept; saving past it drops the oldest.</summary>
    public int MaxSwatches
    {
        get => GetValue(MaxSwatchesProperty);
        set => SetValue(MaxSwatchesProperty, value);
    }

    /// <summary>Format shown in the text field first.</summary>
    public ArcColorFormat Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Raised when the visitor changes the color.</summary>
    public event EventHandler<Color>? ValueChanged;

    /// <summary>Bubbled copy of <see cref="ValueChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcColorValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ArcColorEditor, ArcColorValueChangedEventArgs>("ValueChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="ValueChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcColorValueChangedEventArgs>? ValueChangedRouted
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    private void RaiseValueChanged(Color color)
    {
        ValueChanged?.Invoke(this, color);
        RaiseEvent(new ArcColorValueChangedEventArgs(ValueChangedEvent) { Color = color });
    }

    /// <summary>Raised when the saved swatch row changes.</summary>
    public event EventHandler<IReadOnlyList<ArcColorSwatch>>? SwatchesChanged;

    /// <summary>Moves focus onto the saturation and brightness square, used when the panel opens.</summary>
    internal void FocusArea() => _area?.Focus();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_area is not null)
        {
            _area.ValueChanged -= OnAreaChanged;
        }

        if (_hue is not null)
        {
            _hue.ValueChanged -= OnHueChanged;
        }

        if (_alpha is not null)
        {
            _alpha.ValueChanged -= OnAlphaChanged;
        }

        if (_formatButton is not null)
        {
            _formatButton.Click -= OnFormatClick;
        }

        if (_input is not null)
        {
            _input.KeyDown -= OnInputKeyDown;
            _input.LostFocus -= OnInputLostFocus;
        }

        _area = e.NameScope.Find<ArcColorArea>("PART_Area");
        _hue = e.NameScope.Find<ArcColorRamp>("PART_Hue");
        _alpha = e.NameScope.Find<ArcColorRamp>("PART_Alpha");
        _input = e.NameScope.Find<TextBox>("PART_Input");
        _formatButton = e.NameScope.Find<ArcButton>("PART_Format");
        _swatchRow = e.NameScope.Find<StackPanel>("PART_Swatches");
        _contrastRow = e.NameScope.Find<StackPanel>("PART_Contrast");
        _error = e.NameScope.Find<TextBlock>("PART_Error");

        if (_area is not null)
        {
            _area.ValueChanged += OnAreaChanged;
        }

        if (_hue is not null)
        {
            _hue.ValueChanged += OnHueChanged;
        }

        if (_alpha is not null)
        {
            _alpha.ValueChanged += OnAlphaChanged;
        }

        if (_formatButton is not null)
        {
            _formatButton.Click += OnFormatClick;
        }

        if (_input is not null)
        {
            _input.KeyDown += OnInputKeyDown;
            _input.LostFocus += OnInputLostFocus;
        }

        if (Swatches is null && _swatches.Count == 0)
        {
            _swatches.Clear();
            if (DefaultSwatches is { } starting)
            {
                _swatches.AddRange(starting);
            }
        }

        _hsv = ArcHsv.FromColor(Value);
        SyncFromValue();
        RebuildSwatches();
        UpdateInput();
        UpdateFormatLabel();
    }

    private void SyncFromValue()
    {
        if (_syncing)
        {
            return;
        }

        var fromColor = ArcHsv.FromColor(Value);
        // A gray has no hue of its own; keep the last one so the square does not jump.
        _hsv = fromColor.Saturation < 0.001
            ? _hsv.WithSaturation(fromColor.Saturation).WithValue(fromColor.Value).WithAlpha(fromColor.Alpha)
            : fromColor;

        PushToSurfaces();
        UpdateReadouts();
        UpdateInput();
    }

    private void PushToSurfaces()
    {
        _syncing = true;
        try
        {
            if (_area is not null)
            {
                _area.Hue = _hsv.Hue;
                _area.Saturation = _hsv.Saturation;
                _area.Brightness = _hsv.Value;
            }

            if (_hue is not null)
            {
                _hue.Hue = _hsv.Hue;
            }

            if (_alpha is not null)
            {
                _alpha.Alpha = _hsv.Alpha;
                _alpha.RampColor = Color.FromArgb(255, Value.R, Value.G, Value.B);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Commit()
    {
        var color = _hsv.ToColor();
        _syncing = true;
        try
        {
            SetCurrentValue(ValueProperty, color);
        }
        finally
        {
            _syncing = false;
        }

        PushToSurfaces();
        UpdateReadouts();
        UpdateInput();
        RaiseValueChanged(color);
    }

    private void OnAreaChanged(object? sender, EventArgs e)
    {
        if (_syncing || _area is null)
        {
            return;
        }

        _hsv = _hsv.WithSaturation(_area.Saturation).WithValue(_area.Brightness);
        Commit();
    }

    private void OnHueChanged(object? sender, EventArgs e)
    {
        if (_syncing || _hue is null)
        {
            return;
        }

        _hsv = _hsv.WithHue(_hue.Hue);
        Commit();
    }

    private void OnAlphaChanged(object? sender, EventArgs e)
    {
        if (_syncing || _alpha is null)
        {
            return;
        }

        _hsv = _hsv.WithAlpha(_alpha.Alpha);
        Commit();
    }

    private void UpdateReadouts()
    {
        if (_contrastRow is null)
        {
            return;
        }

        _contrastRow.Children.Clear();
        _contrastRow.Children.Add(ContrastChip("White", Colors.White));
        _contrastRow.Children.Add(ContrastChip("Black", Colors.Black));
    }

    private Control ContrastChip(string label, Color backdrop)
    {
        var composed = ArcColorMath.Mix(Value, backdrop);
        var ratio = ArcColorMath.Contrast(composed, backdrop);
        var text = $"{label} {ratio:0.0}:1 \u00b7 {ArcColorMath.Grade(ratio)}";
        var chip = new Border { Classes = { "picker-chip" } };
        chip.Child = new TextBlock { Text = text, Classes = { "arc-caption" }, Foreground = ArcResources.Brush(ArcTokens.TextSecondary) };
        AutomationProperties.SetName(chip, $"{label} background contrast {ratio:0.0} to 1, {ArcColorMath.Grade(ratio)}");
        return chip;
    }

    private void UpdateInput()
    {
        if (_input is null || _input.IsFocused)
        {
            return;
        }

        _input.Text = ArcColorMath.Format(Value, Format);
        _input.Classes.Set("invalid", false);
        if (_error is not null)
        {
            _error.IsVisible = false;
        }
    }

    private void UpdateFormatLabel()
    {
        if (_formatButton is null)
        {
            return;
        }

        _formatButton.Content = Format switch
        {
            ArcColorFormat.Rgb => "RGB",
            ArcColorFormat.Hsl => "HSL",
            _ => "HEX"
        };

        AutomationProperties.SetName(_formatButton, "Change color format");
    }    private void OnFormatClick(object? sender, RoutedEventArgs e)
    {
        Format = Format switch
        {
            ArcColorFormat.Hex => ArcColorFormat.Rgb,
            ArcColorFormat.Rgb => ArcColorFormat.Hsl,
            _ => ArcColorFormat.Hex
        };
        UpdateFormatLabel();
        UpdateInput();
        e.Handled = true;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyInput();
        e.Handled = true;
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => ApplyInput();

    private void ApplyInput()
    {
        if (_input is null)
        {
            return;
        }

        var parsed = ArcColorMath.Parse(_input.Text);
        if (parsed is null)
        {
            _input.Classes.Set("invalid", true);
            if (_error is not null)
            {
                _error.Text = "Use #RRGGBB, rgb() or hsl().";
                _error.IsVisible = true;
            }

            return;
        }

        _input.Classes.Set("invalid", false);
        if (_error is not null)
        {
            _error.IsVisible = false;
        }

        _hsv = ArcHsv.FromColor(parsed.Value);
        Commit();
    }

    private void RebuildSwatches()
    {
        if (_swatchRow is null)
        {
            return;
        }

        if (Swatches is { } controlled)
        {
            _swatches.Clear();
            _swatches.AddRange(controlled);
        }
        else if (_swatches.Count == 0 && DefaultSwatches is { } starting)
        {
            _swatches.AddRange(starting);
        }

        _swatchRow.Children.Clear();
        _swatchButtons.Clear();

        var swatchTheme = this.TryFindResource("ArcPickerSwatchTheme", out var theme) ? theme as ControlTheme : null;
        var iconTheme = this.TryFindResource("ArcPickerIconTheme", out var icons) ? icons as ControlTheme : null;

        foreach (var swatch in _swatches)
        {
            var button = new Button
            {
                Background = new SolidColorBrush(swatch.Color),
                Theme = swatchTheme,
                Tag = swatch
            };
            AutomationProperties.SetName(button, $"Use {ArcColorMath.ToHex(swatch.Color)}");
            button.Click += OnSwatchClick;
            button.KeyDown += OnSwatchKeyDown;
            if (swatch.Color == Value)
            {
                button.Classes.Add("selected");
            }

            _swatchButtons.Add((button, swatch));
            _swatchRow.Children.Add(button);
        }

        var add = new Button
        {
            Theme = iconTheme,
            Width = 26,
            Height = 26,
            Content = new ArcIcon { Icon = "plus", Size = 14 }
        };
        AutomationProperties.SetName(add, "Save current color");
        ToolTip.SetTip(add, "Save current color");
        add.Click += OnAddSwatch;
        _swatchRow.Children.Add(add);
    }

    private void OnSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ArcColorSwatch swatch })
        {
            return;
        }

        _hsv = ArcHsv.FromColor(swatch.Color);
        Commit();
        e.Handled = true;
    }

    private void OnSwatchKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Button { Tag: ArcColorSwatch swatch } button)
        {
            return;
        }

        var index = _swatches.IndexOf(swatch);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                _swatches.RemoveAt(index);
                PublishSwatches();
                RebuildSwatches();
                e.Handled = true;
                break;
            case Key.Left when e.KeyModifiers.HasFlag(KeyModifiers.Alt) && index > 0:
                MoveSwatch(index, index - 1, button);
                e.Handled = true;
                break;
            case Key.Right when e.KeyModifiers.HasFlag(KeyModifiers.Alt) && index < _swatches.Count - 1:
                MoveSwatch(index, index + 1, button);
                e.Handled = true;
                break;
        }
    }

    private void MoveSwatch(int from, int to, Button button)
    {
        var swatch = _swatches[from];
        _swatches.RemoveAt(from);
        _swatches.Insert(to, swatch);
        PublishSwatches();
        RebuildSwatches();
        if (_swatchRow is not null && to < _swatchRow.Children.Count)
        {
            _swatchRow.Children[to].Focus();
        }
    }

    private void OnAddSwatch(object? sender, RoutedEventArgs e)
    {
        var color = Value;
        if (_swatches.Exists(item => item.Color == color))
        {
            return;
        }

        var max = Math.Max(1, MaxSwatches);
        while (_swatches.Count >= max)
        {
            _swatches.RemoveAt(0);
        }

        _swatches.Add(new ArcColorSwatch(Guid.NewGuid().ToString("N")[..6], color));
        PublishSwatches();
        RebuildSwatches();
        e.Handled = true;
    }

    private void PublishSwatches()
    {
        var snapshot = _swatches.ToArray();
        if (Swatches is not null)
        {
            SetCurrentValue(SwatchesProperty, (IReadOnlyList<ArcColorSwatch>?)snapshot);
        }

        SwatchesChanged?.Invoke(this, snapshot);
    }
}
