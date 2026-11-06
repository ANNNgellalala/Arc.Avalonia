using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Display format for the time field. Values stay 24-hour internally.</summary>
public enum ArcTimeFormat
{
    TwelveHour,
    TwentyFourHour
}

/// <summary>
/// Arc's time picker: a field that opens a list of times at a fixed minute step, centred on the current
/// value. Arrow keys move through times and wrap; Enter picks and closes.
/// </summary>
[TemplatePart("PART_Value", typeof(TextBlock))]
[TemplatePart("PART_Label", typeof(TextBlock))]
[TemplatePart("PART_Description", typeof(TextBlock))]
[TemplatePart("PART_List", typeof(ListBox))]
public class ArcTimePicker : ArcPickerBase
{
    public static readonly StyledProperty<TimeSpan?> ValueProperty =
        AvaloniaProperty.Register<ArcTimePicker, TimeSpan?>(nameof(Value), TimeSpan.FromHours(9));

    public static readonly StyledProperty<int> MinuteStepProperty =
        AvaloniaProperty.Register<ArcTimePicker, int>(nameof(MinuteStep), 15);

    public static readonly StyledProperty<ArcTimeFormat> FormatProperty =
        AvaloniaProperty.Register<ArcTimePicker, ArcTimeFormat>(nameof(Format), ArcTimeFormat.TwelveHour);

    private TextBlock? _value;
    private TextBlock? _label;
    private TextBlock? _description;
    private ListBox? _list;
    private bool _syncing;
    private int _optionCount;

    static ArcTimePicker()
    {
        ValueProperty.Changed.AddClassHandler<ArcTimePicker>((picker, _) => picker.OnValueChanged());
        MinuteStepProperty.Changed.AddClassHandler<ArcTimePicker>((picker, _) => picker.BuildOptions());
        FormatProperty.Changed.AddClassHandler<ArcTimePicker>((picker, _) =>
        {
            picker.BuildOptions();
            picker.OnContentChanged();
        });
    }

    /// <summary>Time of day, or null for no selection. Arc stores "HH:mm" strings; the port uses TimeSpan.</summary>
    public TimeSpan? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Spacing between options in minutes: 1, 5, 10, 15 or 30 in Arc.</summary>
    public int MinuteStep
    {
        get => GetValue(MinuteStepProperty);
        set => SetValue(MinuteStepProperty, value);
    }

    /// <summary>12-hour or 24-hour display; the value itself is always 24-hour.</summary>
    public ArcTimeFormat Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Raised with the new value whenever the visitor moves through the list.</summary>
    public event EventHandler<TimeSpan>? TimeChanged;

    /// <summary>Bubbled copy of <see cref="TimeChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcTimePickerChangedEventArgs> TimeChangedEvent =
        RoutedEvent.Register<ArcTimePicker, ArcTimePickerChangedEventArgs>("TimeChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="TimeChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcTimePickerChangedEventArgs>? TimeChangedRouted
    {
        add => AddHandler(TimeChangedEvent, value);
        remove => RemoveHandler(TimeChangedEvent, value);
    }

    private void RaiseTimeChanged(TimeSpan time)
    {
        TimeChanged?.Invoke(this, time);
        RaiseEvent(new ArcTimePickerChangedEventArgs(TimeChangedEvent) { Time = time });
    }

    protected override string AccessibleRole => "Time";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_list is not null)
        {
            _list.SelectionChanged -= OnListSelectionChanged;
            _list.RemoveHandler(KeyDownEvent, OnListKeyDown);
            _list.RemoveHandler(PointerReleasedEvent, OnListPointerReleased);
        }

        _value = e.NameScope.Find<TextBlock>("PART_Value");
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _list = e.NameScope.Find<ListBox>("PART_List");

        if (_list is not null)
        {
            _list.SelectionChanged += OnListSelectionChanged;
            _list.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
            _list.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel);
            AutomationProperties.SetName(_list, "Times");
        }

        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        UpdatePopupWidth();
        BuildOptions();
        OnContentChanged();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePopupWidth();

    private void UpdatePopupWidth()
    {
        if (_list is null)
        {
            return;
        }

        // Arc spans the list across the field width, capped by the 250 px height.
        if (Bounds.Width > 48)
        {
            _list.Width = Bounds.Width - 16;
        }
    }

    protected override void OnContentChanged()
    {
        if (_label is not null)
        {
            _label.Text = Label;
            _label.IsVisible = !string.IsNullOrWhiteSpace(Label);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_value is not null)
        {
            var hasValue = Value.HasValue;
            _value.Text = hasValue ? Display(Value!.Value) : Placeholder ?? "Select a time";
            _value.Classes.Set("picker-placeholder", !hasValue);
            _value.Classes.Set("picker-value", hasValue);
        }

        SetTriggerName(Value.HasValue ? Display(Value!.Value) : null);
    }

    protected override void OnOpened()
    {
        if (_list is null)
        {
            return;
        }

        SyncSelection(scroll: true);
        UpdatePopupWidth();
        Dispatcher.UIThread.Post(() =>
        {
            _list?.Focus();
            SyncSelection(scroll: true);
        }, DispatcherPriority.Loaded);
    }

    protected override void OnClosed()
    {
    }

    /// <summary>Formats a time for the field and the list.</summary>
    public string Display(TimeSpan time) => Format == ArcTimeFormat.TwentyFourHour
        ? $"{(int)time.TotalHours:00}:{time.Minutes:00}"
        : new DateTime(2000, 1, 1).Add(time).ToString("h:mm tt", ArcDateMath.Culture);

    private int Step
    {
        get
        {
            var step = MinuteStep;
            return step < 1 ? 15 : step > 60 ? 60 : step;
        }
    }

    private void BuildOptions()
    {
        if (_list is null)
        {
            return;
        }

        var step = Step;
        var options = new List<string>();
        for (var minutes = 0; minutes < 24 * 60; minutes += step)
        {
            options.Add(Display(TimeSpan.FromMinutes(minutes)));
        }

        _optionCount = options.Count;

        _syncing = true;
        try
        {
            _list.ItemsSource = options;
        }
        finally
        {
            _syncing = false;
        }

        SyncSelection(scroll: false);
    }

    private void OnValueChanged()
    {
        OnContentChanged();
        if (!_syncing)
        {
            SyncSelection(scroll: false);
        }
    }

    private void SyncSelection(bool scroll)
    {
        if (_list is null || _optionCount == 0)
        {
            return;
        }

        var index = Value.HasValue
            ? Math.Clamp((int)Math.Round(Value.Value.TotalMinutes / Step) % _optionCount, 0, _optionCount - 1)
            : -1;

        _syncing = true;
        try
        {
            _list.SelectedIndex = index;
        }
        finally
        {
            _syncing = false;
        }

        if (scroll && index >= 0)
        {
            _list.ScrollIntoView(index);
        }
    }

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _list is null || _list.SelectedIndex < 0)
        {
            return;
        }

        var time = TimeSpan.FromMinutes(_list.SelectedIndex * Step);
        if (Value == time)
        {
            return;
        }

        _syncing = true;
        try
        {
            SetCurrentValue(ValueProperty, (TimeSpan?)time);
        }
        finally
        {
            _syncing = false;
        }

        OnContentChanged();
        RaiseTimeChanged(time);
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                CloseAndFocus();
                e.Handled = true;
                break;
            case Key.Escape:
                CloseAndFocus();
                e.Handled = true;
                break;
        }
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_list?.SelectedIndex >= 0)
        {
            CloseAndFocus();
        }
    }
}
