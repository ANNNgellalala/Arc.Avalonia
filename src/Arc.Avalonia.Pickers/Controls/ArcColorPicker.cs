using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// Arc's colour picker: a swatch field that opens the full colour surface. Values are always hex with
/// alpha digits when the colour is not opaque; the editor raises every change while the panel stays open.
/// </summary>
public class ArcColorPicker : ArcPickerBase
{
    public static readonly StyledProperty<Color> ValueProperty =
        AvaloniaProperty.Register<ArcColorPicker, Color>(nameof(Value));

    public static readonly StyledProperty<IReadOnlyList<ArcColorSwatch>?> SwatchesProperty =
        AvaloniaProperty.Register<ArcColorPicker, IReadOnlyList<ArcColorSwatch>?>(nameof(Swatches));

    public static readonly StyledProperty<IReadOnlyList<ArcColorSwatch>?> DefaultSwatchesProperty =
        AvaloniaProperty.Register<ArcColorPicker, IReadOnlyList<ArcColorSwatch>?>(nameof(DefaultSwatches));

    public static readonly StyledProperty<int> MaxSwatchesProperty =
        AvaloniaProperty.Register<ArcColorPicker, int>(nameof(MaxSwatches), 7);

    public static readonly StyledProperty<ArcColorFormat> DefaultFormatProperty =
        AvaloniaProperty.Register<ArcColorPicker, ArcColorFormat>(nameof(DefaultFormat));

    private Border? _swatch;
    private TextBlock? _name;
    private TextBlock? _hex;
    private ArcColorEditor? _editor;
    private bool _syncing;

    static ArcColorPicker()
    {
        ValueProperty.Changed.AddClassHandler<ArcColorPicker>((picker, _) => picker.SyncAll());
        SwatchesProperty.Changed.AddClassHandler<ArcColorPicker>((picker, _) => picker.SyncEditor());
        DefaultSwatchesProperty.Changed.AddClassHandler<ArcColorPicker>((picker, _) => picker.SyncEditor());
        MaxSwatchesProperty.Changed.AddClassHandler<ArcColorPicker>((picker, _) => picker.SyncEditor());
        DefaultFormatProperty.Changed.AddClassHandler<ArcColorPicker>((picker, _) => picker.SyncEditor());
    }

    public ArcColorPicker()
    {
        SetCurrentValue(ValueProperty, ArcResources.Color(ArcTokens.Accent));
        SetCurrentValue(LabelProperty, "Color");
    }

    /// <summary>Current colour, alpha included.</summary>
    public Color Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Controlled saved swatches shown in the panel.</summary>
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

    /// <summary>Format the text field shows first: hex, RGB or HSL.</summary>
    public ArcColorFormat DefaultFormat
    {
        get => GetValue(DefaultFormatProperty);
        set => SetValue(DefaultFormatProperty, value);
    }

    /// <summary>Raised as the colour changes, including while dragging.</summary>
    public event EventHandler<Color>? ValueChanged;

    /// <summary>Raised when the saved swatch row changes.</summary>
    public event EventHandler<IReadOnlyList<ArcColorSwatch>>? SwatchesChanged;

    protected override string AccessibleRole => "Color";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_editor is not null)
        {
            _editor.ValueChanged -= OnEditorValueChanged;
            _editor.SwatchesChanged -= OnEditorSwatchesChanged;
        }

        _swatch = e.NameScope.Find<Border>("PART_Swatch");
        _name = e.NameScope.Find<TextBlock>("PART_Name");
        _hex = e.NameScope.Find<TextBlock>("PART_Hex");
        _editor = e.NameScope.Find<ArcColorEditor>("PART_Editor");

        if (_editor is not null)
        {
            _editor.ValueChanged += OnEditorValueChanged;
            _editor.SwatchesChanged += OnEditorSwatchesChanged;
        }

        SyncAll();
    }

    protected override void OnContentChanged()
    {
        if (_name is not null)
        {
            _name.Text = string.IsNullOrWhiteSpace(Label) ? "Color" : Label;
        }

        UpdateTrigger();
    }

    protected override void OnOpened()
    {
        SyncEditor();
        Dispatcher.UIThread.Post(() => _editor?.FocusArea(), DispatcherPriority.Loaded);
    }

    private void SyncAll()
    {
        UpdateTrigger();
        SyncEditor();
        OnContentChanged();
    }

    private void SyncEditor()
    {
        if (_editor is null || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            _editor.Value = Value;
            _editor.MaxSwatches = MaxSwatches;
            _editor.Format = DefaultFormat;
            if (Swatches is not null)
            {
                _editor.Swatches = Swatches;
            }

            if (DefaultSwatches is not null)
            {
                _editor.DefaultSwatches = DefaultSwatches;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateTrigger()
    {
        var color = Value;
        if (_swatch is not null)
        {
            _swatch.Background = new SolidColorBrush(color);
        }

        if (_hex is not null)
        {
            _hex.Text = ArcColorMath.ToHex(color);
        }

        if (_name is not null)
        {
            _name.Text = string.IsNullOrWhiteSpace(Label) ? "Color" : Label;
        }

        var label = string.IsNullOrWhiteSpace(Label) ? "Color" : Label;
        SetTriggerName(ArcColorMath.ToHex(color));
        AutomationProperties.SetName(this, $"Color picker, {label}, {ArcColorMath.ToHex(color)}");
    }

    private void OnEditorValueChanged(object? sender, Color color)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            SetCurrentValue(ValueProperty, color);
        }
        finally
        {
            _syncing = false;
        }

        UpdateTrigger();
        ValueChanged?.Invoke(this, color);
    }

    private void OnEditorSwatchesChanged(object? sender, IReadOnlyList<ArcColorSwatch> swatches)
    {
        SetCurrentValue(SwatchesProperty, (IReadOnlyList<ArcColorSwatch>?)swatches);
        SwatchesChanged?.Invoke(this, swatches);
    }
}
