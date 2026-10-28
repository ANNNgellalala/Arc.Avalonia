using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A 1-to-N star rating with hover preview, half stars, clearing, keyboard control and a value word.
/// The stars are drawn by <see cref="ArcStar"/>; the control itself owns focus and automation.
/// </summary>
public class ArcRating : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcRating, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcRating, string?>(nameof(Description));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcRating, double>(nameof(Value));

    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<ArcRating, int>(nameof(Max), 5);

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<ArcRating, bool>(nameof(ReadOnly));

    public static readonly StyledProperty<bool> AllowHalfProperty =
        AvaloniaProperty.Register<ArcRating, bool>(nameof(AllowHalf));

    public static readonly StyledProperty<bool> AllowClearProperty =
        AvaloniaProperty.Register<ArcRating, bool>(nameof(AllowClear), true);

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<ArcRating, bool>(nameof(ShowValue), true);

    public static readonly StyledProperty<IReadOnlyList<string>?> ValueLabelsProperty =
        AvaloniaProperty.Register<ArcRating, IReadOnlyList<string>?>(nameof(ValueLabels));

    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<ArcRating, string>(nameof(EmptyLabel), "Not rated");

    public static readonly StyledProperty<ArcFieldSize> SizeProperty =
        AvaloniaProperty.Register<ArcRating, ArcFieldSize>(nameof(Size), ArcFieldSize.Md);

    private static readonly string[] DefaultLabels = { "Poor", "Fair", "Good", "Great", "Excellent" };

    private StackPanel? _starsRow;
    private StackPanel? _starHost;
    private TextBlock? _valueText;
    private TextBlock? _labelText;
    private TextBlock? _description;
    private readonly List<ArcStar> _stars = new();
    private double? _preview;
    private bool _pressed;

    static ArcRating()
    {
        ValueProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        MaxProperty.Changed.AddClassHandler<ArcRating>((rating, _) =>
        {
            rating.RebuildStars();
            rating.Refresh();
        });
        ReadOnlyProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.ApplyReadOnly());
        AllowHalfProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        AllowClearProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        ShowValueProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        ValueLabelsProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        EmptyLabelProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.Refresh());
        SizeProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.RebuildStars());
        LabelProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.RefreshLabels());
        DescriptionProperty.Changed.AddClassHandler<ArcRating>((rating, _) => rating.RefreshLabels());
    }

    public ArcRating()
    {
        Focusable = true;
    }

    public event EventHandler? ValueChanged;

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

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public bool ReadOnly
    {
        get => GetValue(ReadOnlyProperty);
        set => SetValue(ReadOnlyProperty, value);
    }

    public bool AllowHalf
    {
        get => GetValue(AllowHalfProperty);
        set => SetValue(AllowHalfProperty, value);
    }

    public bool AllowClear
    {
        get => GetValue(AllowClearProperty);
        set => SetValue(AllowClearProperty, value);
    }

    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public IReadOnlyList<string>? ValueLabels
    {
        get => GetValue(ValueLabelsProperty);
        set => SetValue(ValueLabelsProperty, value);
    }

    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    public ArcFieldSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private double CellSize => Size switch
    {
        ArcFieldSize.Sm => 28,
        ArcFieldSize.Lg => 44,
        _ => 36
    };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_starHost is not null)
        {
            _starHost.PointerMoved -= OnPointerMoved;
            _starHost.PointerPressed -= OnPointerPressed;
            _starHost.PointerReleased -= OnPointerReleased;
            _starHost.PointerExited -= OnPointerExited;
        }

        _starsRow = e.NameScope.Find<StackPanel>("Stars");
        _valueText = e.NameScope.Find<TextBlock>("ValueText");
        _labelText = e.NameScope.Find<TextBlock>("LabelText");
        _description = e.NameScope.Find<TextBlock>("Description");

        if (_starsRow is not null)
        {
            _starHost = _starsRow;
            _starHost.PointerMoved += OnPointerMoved;
            _starHost.PointerPressed += OnPointerPressed;
            _starHost.PointerReleased += OnPointerReleased;
            _starHost.PointerExited += OnPointerExited;
        }

        RebuildStars();
        RefreshLabels();
        ApplyReadOnly();
        Refresh();
    }

    private void RebuildStars()
    {
        if (_starHost is null)
        {
            return;
        }

        _starHost.Children.Clear();
        _stars.Clear();

        for (var i = 0; i < Math.Max(1, Max); i++)
        {
            var star = new ArcStar
            {
                Width = CellSize,
                Height = CellSize,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(star, $"{i + 1} star");
            _stars.Add(star);
            _starHost.Children.Add(star);
        }

        Refresh();
    }

    private void Refresh()
    {
        var display = _preview ?? Value;

        for (var i = 0; i < _stars.Count; i++)
        {
            _stars[i].Fill = Math.Clamp(display - i, 0, 1);
        }

        if (_valueText is not null)
        {
            _valueText.Text = Describe(display);
            _valueText.IsVisible = ShowValue;
            AutomationProperties.SetLiveSetting(_valueText, AutomationLiveSetting.Polite);
        }

        AutomationProperties.SetName(this, Label ?? "Rating");
        AutomationProperties.SetHelpText(this, $"{Value:0.#} of {Max} stars");
    }

    private string Describe(double value)
    {
        if (value <= 0)
        {
            return EmptyLabel;
        }

        var labels = ValueLabels;
        if (labels is null && Max == 5)
        {
            labels = DefaultLabels;
        }

        if (labels is { Count: > 0 })
        {
            var index = Math.Clamp((int)Math.Ceiling(value) - 1, 0, labels.Count - 1);
            return labels[index];
        }

        return value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
    }

    private void RefreshLabels()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !string.IsNullOrEmpty(Label);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
    }

    private void ApplyReadOnly()
    {
        Focusable = !ReadOnly;
        Cursor = ReadOnly ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand);
    }

    private double ValueFromPointer(PointerEventArgs e)
    {
        if (_starHost is null)
        {
            return Value;
        }

        var cell = CellSize;
        var step = AllowHalf ? 0.5 : 1;
        var x = e.GetPosition(_starHost).X;
        var raw = Math.Ceiling(x / cell / step) * step;
        return Math.Clamp(raw, step, Max);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ReadOnly)
        {
            return;
        }

        _preview = ValueFromPointer(e);
        Refresh();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReadOnly || _starHost is null)
        {
            return;
        }

        _pressed = true;
        e.Pointer.Capture(_starHost);
        _preview = ValueFromPointer(e);
        Refresh();
        Focus();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        e.Pointer.Capture(null);

        var target = _preview ?? Value;
        if (AllowClear && Math.Abs(target - Value) < 0.0001)
        {
            target = 0;
        }

        _preview = null;
        Commit(target);
        e.Handled = true;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_pressed)
        {
            return;
        }

        _preview = null;
        Refresh();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ReadOnly)
        {
            return;
        }

        var step = AllowHalf ? 0.5 : 1;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Up:
                Commit(Value + step);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Down:
                Commit(Value - step);
                e.Handled = true;
                break;
            case Key.Home:
                Commit(AllowClear ? 0 : step);
                e.Handled = true;
                break;
            case Key.End:
                Commit(Max);
                e.Handled = true;
                break;
            case Key.Back:
            case Key.Delete:
                if (AllowClear)
                {
                    Commit(0);
                    e.Handled = true;
                }

                break;
            default:
                if (e.Key >= Key.D1 && e.Key <= Key.D9)
                {
                    var number = e.Key - Key.D0;
                    if (number <= Max)
                    {
                        Commit(number);
                        e.Handled = true;
                    }
                }

                break;
        }
    }

    private void Commit(double value)
    {
        value = Math.Clamp(value, 0, Max);
        if (AllowHalf)
        {
            value = Math.Round(value * 2) / 2;
        }
        else
        {
            value = Math.Round(value);
        }

        if (Math.Abs(value - Value) < 0.0001)
        {
            Refresh();
            return;
        }

        SetCurrentValue(ValueProperty, value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Loaded);
    }
}
