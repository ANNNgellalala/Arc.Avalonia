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
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcMorphSelectAlign
{
    Start,
    End
}

/// <summary>An option or a group of options in a morph select list.</summary>
public abstract class ArcMorphSelectItem
{
}

public sealed class ArcMorphSelectOption : ArcMorphSelectItem
{
    public ArcMorphSelectOption(string value, string label, string? meta = null, string? keywords = null, bool disabled = false)
    {
        Value = value;
        Label = label;
        Meta = meta;
        Keywords = keywords;
        Disabled = disabled;
    }

    public string Value { get; }

    public string Label { get; }

    public string? Meta { get; }

    public string? Keywords { get; }

    public bool Disabled { get; }
}

public sealed class ArcMorphSelectGroup : ArcMorphSelectItem
{
    public ArcMorphSelectGroup(string label, params ArcMorphSelectOption[] options)
    {
        Label = label;
        Options = options;
    }

    public string Label { get; }

    public IList<ArcMorphSelectOption> Options { get; }
}

public sealed class ArcMorphSelectValueEventArgs : RoutedEventArgs
{
    public ArcMorphSelectValueEventArgs(string value, ArcMorphSelectOption option)
    {
        Value = value;
        Option = option;
    }

    /// <summary>Routed payload factory used by <c>ValueChangedRouted</c>.</summary>
    public ArcMorphSelectValueEventArgs(RoutedEvent routedEvent, string value, ArcMorphSelectOption option)
        : base(routedEvent)
    {
        Value = value;
        Option = option;
    }

    public string Value { get; }

    public ArcMorphSelectOption Option { get; }
}

/// <summary>
/// Arc's morph select: the trigger surface grows into the option list in place, with a gliding
/// highlight, optional search and type-ahead, and no separate popup window.
/// </summary>
[TemplatePart("PART_LabelText", typeof(TextBlock))]
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_TriggerRow", typeof(Grid))]
[TemplatePart("PART_ValueText", typeof(TextBlock))]
[TemplatePart("PART_Search", typeof(TextBox))]
[TemplatePart("PART_Chevron", typeof(ArcIcon))]
[TemplatePart("PART_ListHost", typeof(Panel))]
[TemplatePart("PART_Scroller", typeof(ScrollViewer))]
[TemplatePart("PART_OptionsHost", typeof(Panel))]
[TemplatePart("PART_Highlight", typeof(Border))]
[TemplatePart("PART_Options", typeof(StackPanel))]
public class ArcMorphSelect : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcMorphSelect, string?>(nameof(Label));

    public static readonly StyledProperty<bool> HideLabelProperty =
        AvaloniaProperty.Register<ArcMorphSelect, bool>(nameof(HideLabel));

    public static readonly StyledProperty<IList<ArcMorphSelectItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcMorphSelect, IList<ArcMorphSelectItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcMorphSelect, string?>(nameof(Value));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcMorphSelect, string?>(nameof(Placeholder), "Select");

    public static readonly StyledProperty<bool> SearchableProperty =
        AvaloniaProperty.Register<ArcMorphSelect, bool>(nameof(Searchable));

    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<ArcMorphSelect, string?>(nameof(SearchPlaceholder));

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<ArcMorphSelect, double>(nameof(PanelWidth), 272d);

    public static readonly StyledProperty<double> MaxListHeightProperty =
        AvaloniaProperty.Register<ArcMorphSelect, double>(nameof(MaxListHeight), 296d);

    public static readonly StyledProperty<ArcMorphSelectAlign> AlignProperty =
        AvaloniaProperty.Register<ArcMorphSelect, ArcMorphSelectAlign>(nameof(Align));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcMorphSelect, bool>(nameof(IsOpen));

    private TextBlock? _labelText;
    private Border? _surface;
    private Grid? _triggerRow;
    private TextBlock? _valueText;
    private TextBox? _search;
    private ArcIcon? _chevron;
    private Panel? _listHost;
    private ScrollViewer? _scroller;
    private Panel? _optionsHost;
    private Border? _highlight;
    private StackPanel? _options;

    private readonly List<RowEntry> _visible = new();
    private int _active;
    private bool _open;
    private string _typeAhead = string.Empty;
    private DateTime _typeAheadAt = DateTime.MinValue;

    static ArcMorphSelect()
    {
        ValueProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) => select.UpdateValueText());
        ItemsProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) => select.UpdateValueText());
        LabelProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) => select.UpdateLabel());
        HideLabelProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) => select.UpdateLabel());
        SearchableProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) => select.UpdateSearchVisibility());
        IsOpenProperty.Changed.AddClassHandler<ArcMorphSelect>((select, _) =>
        {
            if (select.IsOpen != select._open)
            {
                select.SetOpen(select.IsOpen, animate: true);
            }
        });
    }

    public ArcMorphSelect()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    /// <summary>Raised with the new value and its option.</summary>
    public event EventHandler<ArcMorphSelectValueEventArgs>? ValueChanged;

    public static readonly RoutedEvent<ArcMorphSelectValueEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ArcMorphSelect, ArcMorphSelectValueEventArgs>("ValueChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling form of <see cref="ValueChanged"/>; the CLR event above keeps firing as before.</summary>
    public event EventHandler<ArcMorphSelectValueEventArgs>? ValueChangedRouted
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>Raised when the surface opens or folds back.</summary>
    public event EventHandler? OpenChanged;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool HideLabel
    {
        get => GetValue(HideLabelProperty);
        set => SetValue(HideLabelProperty, value);
    }

    /// <summary>Options or groups, in display order.</summary>
    public IList<ArcMorphSelectItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Shows a search field in place of the trigger when open.</summary>
    public bool Searchable
    {
        get => GetValue(SearchableProperty);
        set => SetValue(SearchableProperty, value);
    }

    public string? SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    /// <summary>Width of the open surface; never narrower than the trigger.</summary>
    public double PanelWidth
    {
        get => GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    public double MaxListHeight
    {
        get => GetValue(MaxListHeightProperty);
        set => SetValue(MaxListHeightProperty, value);
    }

    public ArcMorphSelectAlign Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    /// <summary>Open state; the control owns it, exposed so page code can show the list.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The selected option, if any.</summary>
    public ArcMorphSelectOption? SelectedOption => Options().FirstOrDefault(option =>
        string.Equals(option.Value, Value, StringComparison.Ordinal));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_search is not null)
        {
            _search.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
            _search.TextChanged -= OnSearchTextChanged;
        }

        _labelText = e.NameScope.Find<TextBlock>("PART_LabelText");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _triggerRow = e.NameScope.Find<Grid>("PART_TriggerRow");
        _valueText = e.NameScope.Find<TextBlock>("PART_ValueText");
        _search = e.NameScope.Find<TextBox>("PART_Search");
        _chevron = e.NameScope.Find<ArcIcon>("PART_Chevron");
        _listHost = e.NameScope.Find<Panel>("PART_ListHost");
        _scroller = e.NameScope.Find<ScrollViewer>("PART_Scroller");
        _optionsHost = e.NameScope.Find<Panel>("PART_OptionsHost");
        _highlight = e.NameScope.Find<Border>("PART_Highlight");
        _options = e.NameScope.Find<StackPanel>("PART_Options");

        if (_search is not null)
        {
            _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
            _search.TextChanged += OnSearchTextChanged;
            AutomationProperties.SetName(_search, SearchPlaceholder ?? Label ?? "Search");
        }

        if (_scroller is not null)
        {
            _scroller.MaxHeight = MaxListHeight;
        }

        UpdateLabel();
        UpdateValueText();
        UpdateSearchVisibility();
        ApplyAlign();
        if (_open)
        {
            SetOpen(true, animate: false);
        }
        else
        {
            ApplyClosedSize();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == BoundsProperty && !_open)
        {
            ApplyClosedSize();
        }
        else if (change.Property == IsFocusedProperty)
        {
            Classes.Set("focus", IsFocused);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsEnabled)
        {
            return;
        }

        if (_surface is not null && e.Source is Visual source && IsWithin(source, _surface))
        {
            if (_open && _search is not null && _search.IsVisible && IsWithin(source, _search))
            {
                return;
            }

            e.Handled = true;
            Focus();
            SetOpen(!_open, animate: true);
            return;
        }

        if (_open)
        {
            SetOpen(false, animate: true);
        }
    }

    private static bool IsWithin(Visual source, Visual container)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled)
        {
            return;
        }

        if (!_open)
        {
            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    Open();
                    return;
                case Key.Home:
                case Key.End:
                    e.Handled = true;
                    Open(e.Key == Key.End ? int.MaxValue : 0);
                    return;
            }

            if (e.KeyModifiers == KeyModifiers.None && e.Key is >= Key.A and <= Key.Z)
            {
                TypeAhead(e.Key.ToString());
                e.Handled = true;
            }

            return;
        }

        HandleOpenKey(e);
    }

    private void HandleOpenKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                SetOpen(false, animate: true);
                Focus();
                return;
            case Key.Tab:
                SetOpen(false, animate: true);
                return;
            case Key.Enter:
                e.Handled = true;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    PickActive();
                }
                else
                {
                    PickActive();
                }

                return;
            case Key.Space when !Searchable:
                e.Handled = true;
                PickActive();
                return;
            case Key.Down:
                e.Handled = true;
                Move(1);
                return;
            case Key.Up:
                e.Handled = true;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    PickActive();
                }
                else
                {
                    Move(-1);
                }

                return;
            case Key.PageDown:
                e.Handled = true;
                Move(8);
                return;
            case Key.PageUp:
                e.Handled = true;
                Move(-8);
                return;
            case Key.Home:
                e.Handled = true;
                MoveTo(0);
                return;
            case Key.End:
                e.Handled = true;
                MoveTo(int.MaxValue);
                return;
        }

        if (_search is not null && !_search.IsVisible && e.KeyModifiers == KeyModifiers.None &&
            e.Key is >= Key.A and <= Key.Z)
        {
            TypeAhead(e.Key.ToString());
            e.Handled = true;
        }
    }

    private void TypeAhead(string letter)
    {
        if ((DateTime.UtcNow - _typeAheadAt).TotalMilliseconds > 700)
        {
            _typeAhead = string.Empty;
        }

        _typeAhead += letter;
        _typeAheadAt = DateTime.UtcNow;

        var match = _visible.FirstOrDefault(entry =>
            entry.Option is not null &&
            entry.Option.Label.StartsWith(_typeAhead, StringComparison.OrdinalIgnoreCase));

        if (match.Option is not null)
        {
            _active = _visible.IndexOf(match);
            RenderList();
        }
    }

    private void Open(int activeIndex = -1)
    {
        SetOpen(true, animate: true);
        if (activeIndex >= 0)
        {
            MoveTo(activeIndex);
        }
    }

    private void SetOpen(bool open, bool animate)
    {
        if (_surface is null || _listHost is null)
        {
            _open = open;
            return;
        }

        _open = open;
        SetCurrentValue(IsOpenProperty, open);
        Classes.Set("open", open);
        _listHost.IsVisible = open;
        UpdateSearchVisibility();

        if (open)
        {
            BuildRows(string.Empty);
            _active = Math.Max(0, _visible.FindIndex(entry =>
                entry.Option is not null && string.Equals(entry.Option.Value, Value, StringComparison.Ordinal)));
            RenderList();
            if (_search is not null)
            {
                _search.Text = string.Empty;
            }
        }

        var closedWidth = Math.Max(120, Bounds.Width);
        var closedHeight = 44d;
        var targetWidth = open ? Math.Max(closedWidth, Math.Min(PanelWidth, AvailableWidth())) : closedWidth;
        var targetHeight = open ? 44 + ListHeight() : closedHeight;

        var fromWidth = _surface.Width is double width && !double.IsNaN(width) ? width : closedWidth;
        var fromHeight = _surface.Height is double height && !double.IsNaN(height) ? height : closedHeight;

        AnimateSize(fromWidth, targetWidth, fromHeight, targetHeight, open, animate);

        if (_chevron is not null)
        {
            _chevron.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            var target = open ? 180d : 0d;
            var current = (_chevron.RenderTransform as RotateTransform)?.Angle ?? 0;
            if (animate && !ArcThemeManager.ReducedMotion)
            {
                ArcAnim.To(current, target, ArcAnim.SnappyDuration, ArcAnim.Snappy, value =>
                {
                    if (_chevron is not null)
                    {
                        _chevron.RenderTransform = new RotateTransform(value);
                    }
                });
            }
            else
            {
                _chevron.RenderTransform = new RotateTransform(target);
            }
        }

        if (open && _search is not null && _search.IsVisible)
        {
            Dispatcher.UIThread.Post(() => _search?.Focus(), DispatcherPriority.Loaded);
        }

        OpenChanged?.Invoke(this, EventArgs.Empty);
    }

    private double AvailableWidth()
    {
        if (this.GetVisualParent() is Visual parent && parent.Bounds.Width > 0)
        {
            return Math.Max(120, parent.Bounds.Width - 4);
        }

        return Math.Max(120, PanelWidth);
    }

    private double ListHeight()
    {
        var height = 14d;
        foreach (var entry in _visible)
        {
            height += entry.Option is null ? 26 : 36;
        }

        return Math.Min(Math.Max(64, MaxListHeight), Math.Max(64, height));
    }

    private void AnimateSize(double fromWidth, double toWidth, double fromHeight, double toHeight, bool open, bool animate)
    {
        if (_surface is null)
        {
            return;
        }

        var openRadius = 26d;
        var closedRadius = 18d;

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _surface.Width = toWidth;
            _surface.Height = toHeight;
            _surface.CornerRadius = new CornerRadius(open ? openRadius : closedRadius);
            if (_highlight is not null)
            {
                UpdateHighlight(animate: false);
            }

            return;
        }

        var fromRadius = _surface.CornerRadius.TopLeft;
        var toRadius = open ? openRadius : closedRadius;
        ArcAnim.To(0, 1, ArcAnim.SmoothDuration, ArcAnim.Smooth, value =>
        {
            if (_surface is null)
            {
                return;
            }

            _surface.Width = fromWidth + (toWidth - fromWidth) * value;
            _surface.Height = fromHeight + (toHeight - fromHeight) * value;
            var radius = fromRadius + (toRadius - fromRadius) * value;
            _surface.CornerRadius = new CornerRadius(radius);
        }, () => UpdateHighlight(animate: true));
    }

    private void ApplyClosedSize()
    {
        if (_surface is null || _open)
        {
            return;
        }

        _surface.Width = Math.Max(120, Bounds.Width);
        _surface.Height = 44;
        _surface.CornerRadius = new CornerRadius(18);
    }

    private void ApplyAlign()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.HorizontalAlignment = Align == ArcMorphSelectAlign.End
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    }

    private void UpdateLabel()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !HideLabel && !string.IsNullOrEmpty(Label);
        }

        UpdateValueText();
    }

    private void UpdateValueText()
    {
        if (_valueText is not null)
        {
            var option = SelectedOption;
            _valueText.Text = option?.Label ?? Placeholder ?? "Select";
            _valueText.Foreground = ArcResources.Brush(option is null ? ArcTokens.TextMuted : ArcTokens.Foreground);
        }

        if (_search is not null)
        {
            _search.PlaceholderText = SearchPlaceholder ?? SelectedOption?.Label ?? "Search";
            AutomationProperties.SetName(_search, SearchPlaceholder ?? Label ?? "Search");
        }

        AutomationProperties.SetName(this, Label ?? "Select");
        AutomationProperties.SetHelpText(this, SelectedOption?.Label ?? Placeholder ?? "Select");
    }

    private void UpdateSearchVisibility()
    {
        if (_search is not null)
        {
            _search.IsVisible = _open && Searchable;
        }

        if (_valueText is not null)
        {
            _valueText.IsVisible = !(_open && Searchable);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_open)
        {
            return;
        }

        BuildRows(_search?.Text ?? string.Empty);
        _active = 0;
        RenderList();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_open)
        {
            return;
        }

        HandleOpenKey(e);
    }

    private IEnumerable<ArcMorphSelectOption> Options() => Flatten(Items);

    private static IEnumerable<ArcMorphSelectOption> Flatten(IEnumerable<ArcMorphSelectItem>? items)
    {
        if (items is null)
        {
            yield break;
        }

        foreach (var item in items)
        {
            if (item is ArcMorphSelectOption option)
            {
                yield return option;
            }
            else if (item is ArcMorphSelectGroup group)
            {
                foreach (var child in group.Options)
                {
                    yield return child;
                }
            }
        }
    }

    private void BuildRows(string query)
    {
        if (_options is null)
        {
            return;
        }

        _visible.Clear();
        _options.Children.Clear();

        var needle = query.Trim();
        foreach (var item in Items ?? Array.Empty<ArcMorphSelectItem>())
        {
            if (item is ArcMorphSelectOption option)
            {
                if (Matches(option, needle))
                {
                    _visible.Add(new RowEntry(null, option));
                }
            }
            else if (item is ArcMorphSelectGroup group)
            {
                var matches = group.Options.Where(entry => Matches(entry, needle)).ToList();
                if (matches.Count == 0)
                {
                    continue;
                }

                _visible.Add(new RowEntry(group.Label, null));
                foreach (var matched in matches)
                {
                    _visible.Add(new RowEntry(null, matched));
                }
            }
        }

        foreach (var entry in _visible)
        {
            _options.Children.Add(entry.Option is null ? CreateHeading(entry.Heading!) : CreateOptionRow(entry.Option));
        }

        if (_visible.Count == 0)
        {
            _options.Children.Add(new TextBlock
            {
                Text = "No matches",
                Classes = { "arc-caption" },
                Margin = new Thickness(12, 10)
            });
        }

        UpdateHighlight(animate: false);
    }

    private static bool Matches(ArcMorphSelectOption option, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return true;
        }

        return option.Label.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               option.Meta?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true ||
               option.Keywords?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
    }

    private Control CreateHeading(string text) => new TextBlock
    {
        Text = text,
        Classes = { "arc-caption" },
        Height = 26,
        Margin = new Thickness(12, 8, 12, 0),
        VerticalAlignment = VerticalAlignment.Center
    };

    private Control CreateOptionRow(ArcMorphSelectOption option)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Height = 36 };

        var label = new TextBlock
        {
            Text = option.Label,
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0),
            Foreground = ArcResources.Brush(option.Disabled ? ArcTokens.TextMuted : ArcTokens.Foreground),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(label);

        if (!string.IsNullOrEmpty(option.Meta))
        {
            var meta = new TextBlock
            {
                Text = option.Meta,
                Classes = { "arc-caption" },
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(meta, 1);
            grid.Children.Add(meta);
        }

        var check = new ArcIcon
        {
            Icon = "check",
            Size = 15,
            Margin = new Thickness(8, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcResources.Brush(ArcTokens.AccentStrong),
            IsVisible = string.Equals(option.Value, Value, StringComparison.Ordinal)
        };
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        var row = new Border
        {
            Height = 36,
            Background = Brushes.Transparent,
            Child = grid,
            Cursor = option.Disabled ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand),
            Opacity = option.Disabled ? 0.5 : 1,
            Tag = option
        };

        row.PointerEntered += (_, _) =>
        {
            var index = _visible.FindIndex(entry => ReferenceEquals(entry.Option, option));
            if (index >= 0)
            {
                _active = index;
                UpdateHighlight(animate: true);
            }
        };

        row.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            if (!option.Disabled)
            {
                Pick(option);
            }
        };

        return row;
    }

    private void RenderList()
    {
        UpdateHighlight(animate: true);
        UpdateAutomation();
    }

    private void UpdateHighlight(bool animate)
    {
        if (_highlight is null || _options is null || _visible.Count == 0 ||
            _options.Children.Count != _visible.Count)
        {
            if (_highlight is not null)
            {
                _highlight.IsVisible = false;
            }

            return;
        }

        var index = Math.Clamp(_active, 0, _visible.Count - 1);
        while (index < _visible.Count && _visible[index].Option is null)
        {
            index++;
        }

        if (index >= _options.Children.Count)
        {
            index = _options.Children.Count - 1;
        }

        var target = _options.Children[index];
        var y = target.Bounds.Y;
        var height = Math.Max(28, target.Bounds.Height);
        _highlight.IsVisible = true;
        _highlight.Height = height;

        var from = _highlight.Margin.Top;
        var to = y;
        if (!animate || ArcThemeManager.ReducedMotion || Math.Abs(to - from) < 0.5)
        {
            _highlight.Margin = new Thickness(4, to, 4, 0);
            return;
        }

        ArcAnim.To(from, to, ArcAnim.MorphDuration, ArcAnim.Morph, value =>
        {
            if (_highlight is not null)
            {
                _highlight.Margin = new Thickness(4, value, 4, 0);
            }
        });
    }

    private void Move(int delta)
    {
        if (_visible.Count == 0)
        {
            return;
        }

        var index = _active;
        var guard = 0;
        do
        {
            index = (index + delta) % _visible.Count;
            if (index < 0)
            {
                index += _visible.Count;
            }

            guard++;
        }
        while (_visible[index].Option is null && guard < _visible.Count * 2);

        _active = index;
        RenderList();
    }

    private void MoveTo(int index)
    {
        if (_visible.Count == 0)
        {
            return;
        }

        index = index == int.MaxValue
            ? _visible.Count - 1
            : Math.Clamp(index, 0, _visible.Count - 1);

        while (index > 0 && _visible[index].Option is null)
        {
            index--;
        }

        _active = index;
        RenderList();
    }

    private void PickActive()
    {
        if (_active >= 0 && _active < _visible.Count && _visible[_active].Option is { } option)
        {
            Pick(option);
        }
    }

    private void Pick(ArcMorphSelectOption option)
    {
        SetCurrentValue(ValueProperty, option.Value);
        UpdateValueText();
        SetOpen(false, animate: true);
        Focus();
        ValueChanged?.Invoke(this, new ArcMorphSelectValueEventArgs(option.Value, option));
        RaiseEvent(new ArcMorphSelectValueEventArgs(ValueChangedEvent, option.Value, option));
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label ?? "Select");
        var active = _active >= 0 && _active < _visible.Count ? _visible[_active].Option : null;
        AutomationProperties.SetHelpText(this, active?.Label ?? SelectedOption?.Label ?? "Select");
    }

    private sealed record RowEntry(string? Heading, ArcMorphSelectOption? Option);
}
