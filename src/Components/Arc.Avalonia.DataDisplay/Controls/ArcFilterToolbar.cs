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
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Controls;

/// <summary>One applied filter, shown as a removable chip.</summary>
public sealed class ArcFilterChip
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Value { get; set; }
}

/// <summary>A selectable value for a filter field.</summary>
public sealed class ArcFilterOption
{
    public string Value { get; set; } = string.Empty;

    public string? Label { get; set; }
}

/// <summary>A field offered by the Add filter menu.</summary>
public sealed class ArcFilterField
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<ArcFilterOption> Options { get; set; } = new();
}

/// <summary>One option in the sort menu.</summary>
public sealed class ArcSortOption
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// A wrapping toolbar with search, removable filter chips, an Add filter menu, a sort menu, and a
/// result count. Everything stays on one row until the space runs out, then wraps.
/// </summary>
[TemplatePart("PART_Host", typeof(WrapPanel))]
public class ArcFilterToolbar : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcFilterChip>?> FiltersProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, IEnumerable<ArcFilterChip>?>(nameof(Filters));

    public static readonly StyledProperty<IEnumerable<ArcFilterField>?> FieldsProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, IEnumerable<ArcFilterField>?>(nameof(Fields));

    public static readonly StyledProperty<IEnumerable<ArcSortOption>?> SortOptionsProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, IEnumerable<ArcSortOption>?>(nameof(SortOptions));

    public static readonly StyledProperty<string?> SortKeyProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, string?>(nameof(SortKey));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, string>(nameof(SearchText), string.Empty);

    public static readonly StyledProperty<bool> ShowSearchProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, bool>(nameof(ShowSearch), true);

    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, string>(nameof(SearchPlaceholder), "Search");

    public static readonly StyledProperty<int?> ResultCountProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, int?>(nameof(ResultCount));

    public static readonly StyledProperty<string> ResultLabelProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, string>(nameof(ResultLabel), "results");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcFilterToolbar, string>(nameof(Label), "Filters");

    private WrapPanel? _host;
    private Panel? _searchPanel;
    private Panel? _popupHost;
    private Popup? _filterPopup;
    private Popup? _sortPopup;
    private StackPanel? _filterMenu;
    private StackPanel? _sortMenu;
    private ArcButton? _addButton;
    private ArcButton? _sortButton;

    static ArcFilterToolbar()
    {
        FiltersProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        FieldsProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        SortOptionsProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        SortKeyProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        ShowSearchProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.BuildShell());
        SearchPlaceholderProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        ResultCountProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        ResultLabelProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RebuildRow());
        LabelProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => AutomationProperties.SetName(toolbar, toolbar.Label));
        SearchTextProperty.Changed.AddClassHandler<ArcFilterToolbar>((toolbar, _) => toolbar.RaiseSearchChanged(toolbar.SearchText));
    }

    /// <summary>Applied filters, shown as chips.</summary>
    public IEnumerable<ArcFilterChip>? Filters
    {
        get => GetValue(FiltersProperty);
        set => SetValue(FiltersProperty, value);
    }

    /// <summary>Fields offered by the Add filter menu.</summary>
    public IEnumerable<ArcFilterField>? Fields
    {
        get => GetValue(FieldsProperty);
        set => SetValue(FieldsProperty, value);
    }

    public IEnumerable<ArcSortOption>? SortOptions
    {
        get => GetValue(SortOptionsProperty);
        set => SetValue(SortOptionsProperty, value);
    }

    public string? SortKey
    {
        get => GetValue(SortKeyProperty);
        set => SetValue(SortKeyProperty, value);
    }

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool ShowSearch
    {
        get => GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    public string SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    /// <summary>Result count shown at the end of the row; omit to hide it.</summary>
    public int? ResultCount
    {
        get => GetValue(ResultCountProperty);
        set => SetValue(ResultCountProperty, value);
    }

    public string ResultLabel
    {
        get => GetValue(ResultLabelProperty);
        set => SetValue(ResultLabelProperty, value);
    }

    /// <summary>Accessible name for the toolbar group.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public event EventHandler<string>? FilterRemoved;

    /// <summary>Bubbling routed counterpart of <see cref="FilterRemoved"/>.</summary>
    public static readonly RoutedEvent<ArcFilterRemovedEventArgs> FilterRemovedEvent =
        RoutedEvent.Register<ArcFilterToolbar, ArcFilterRemovedEventArgs>("FilterRemoved", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcFilterRemovedEventArgs"/> version of <see cref="FilterRemoved"/>.</summary>
    public event EventHandler<ArcFilterRemovedEventArgs>? FilterRemovedRouted
    {
        add => AddHandler(FilterRemovedEvent, value);
        remove => RemoveHandler(FilterRemovedEvent, value);
    }

    private void RaiseFilterRemoved(string id)
    {
        FilterRemoved?.Invoke(this, id);
        RaiseEvent(new ArcFilterRemovedEventArgs(FilterRemovedEvent) { Id = id });
    }

    public event EventHandler<ArcFilterChip>? FilterAdded;

    /// <summary>Bubbling routed counterpart of <see cref="FilterAdded"/>.</summary>
    public static readonly RoutedEvent<ArcFilterAddedEventArgs> FilterAddedEvent =
        RoutedEvent.Register<ArcFilterToolbar, ArcFilterAddedEventArgs>("FilterAdded", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcFilterAddedEventArgs"/> version of <see cref="FilterAdded"/>.</summary>
    public event EventHandler<ArcFilterAddedEventArgs>? FilterAddedRouted
    {
        add => AddHandler(FilterAddedEvent, value);
        remove => RemoveHandler(FilterAddedEvent, value);
    }

    private void RaiseFilterAdded(ArcFilterChip chip)
    {
        FilterAdded?.Invoke(this, chip);
        RaiseEvent(new ArcFilterAddedEventArgs(FilterAddedEvent) { Chip = chip });
    }

    public event EventHandler? ClearAllRequested;

    /// <summary>Bubbling routed counterpart of <see cref="ClearAllRequested"/>.</summary>
    public static readonly RoutedEvent<ArcFilterClearAllRequestedEventArgs> ClearAllRequestedEvent =
        RoutedEvent.Register<ArcFilterToolbar, ArcFilterClearAllRequestedEventArgs>("ClearAllRequested", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcFilterClearAllRequestedEventArgs"/> version of <see cref="ClearAllRequested"/>.</summary>
    public event EventHandler<ArcFilterClearAllRequestedEventArgs>? ClearAllRequestedRouted
    {
        add => AddHandler(ClearAllRequestedEvent, value);
        remove => RemoveHandler(ClearAllRequestedEvent, value);
    }

    private void RaiseClearAllRequested()
    {
        ClearAllRequested?.Invoke(this, EventArgs.Empty);
        RaiseEvent(new ArcFilterClearAllRequestedEventArgs(ClearAllRequestedEvent));
    }

    public event EventHandler<string>? SearchChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SearchChanged"/>.</summary>
    public static readonly RoutedEvent<ArcFilterSearchChangedEventArgs> SearchChangedEvent =
        RoutedEvent.Register<ArcFilterToolbar, ArcFilterSearchChangedEventArgs>("SearchChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcFilterSearchChangedEventArgs"/> version of <see cref="SearchChanged"/>.</summary>
    public event EventHandler<ArcFilterSearchChangedEventArgs>? SearchChangedRouted
    {
        add => AddHandler(SearchChangedEvent, value);
        remove => RemoveHandler(SearchChangedEvent, value);
    }

    private void RaiseSearchChanged(string text)
    {
        SearchChanged?.Invoke(this, text);
        RaiseEvent(new ArcFilterSearchChangedEventArgs(SearchChangedEvent) { Text = text });
    }

    public event EventHandler<string?>? SortChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SortChanged"/>.</summary>
    public static readonly RoutedEvent<ArcFilterSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<ArcFilterToolbar, ArcFilterSortChangedEventArgs>("SortChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcFilterSortChangedEventArgs"/> version of <see cref="SortChanged"/>.</summary>
    public event EventHandler<ArcFilterSortChangedEventArgs>? SortChangedRouted
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    private void RaiseSortChanged(string? sortKey)
    {
        SortChanged?.Invoke(this, sortKey);
        RaiseEvent(new ArcFilterSortChangedEventArgs(SortChangedEvent) { SortKey = sortKey });
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<WrapPanel>("PART_Host");
        BuildShell();
    }

    private void BuildShell()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _searchPanel = null;

        if (ShowSearch)
        {
            var panel = new Panel { Width = 260, Margin = new Thickness(0, 0, 10, 8), VerticalAlignment = VerticalAlignment.Center };
            var box = FeedbackUi.Input(SearchPlaceholder, 36);
            box.Padding = new Thickness(32, 6, 30, 6);
            box.Text = SearchText;
            box.TextChanged += (_, _) => SetCurrentValue(SearchTextProperty, box.Text ?? string.Empty);
            box.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    box.Text = string.Empty;
                    args.Handled = true;
                }
            };
            panel.Children.Add(box);
            panel.Children.Add(new ArcIcon
            {
                Icon = "search",
                Size = 15,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(11, 0, 0, 0)
            }.Paint(ArcIcon.ForegroundProperty, ArcTokens.TextMuted));

            var clear = new ArcIconButton
            {
                Icon = "x",
                IconSize = 12,
                BoxSize = 22,
                Label = "Clear search",
                Muted = true,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                IsVisible = !string.IsNullOrEmpty(box.Text)
            };
            clear.Click += (_, _) => box.Text = string.Empty;
            box.TextChanged += (_, _) => clear.IsVisible = !string.IsNullOrEmpty(box.Text);
            panel.Children.Add(clear);

            _searchPanel = panel;
        }

        RebuildRow();
    }

    private void RebuildRow()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _host.Children.Add(BuildPopupHost());

        if (ShowSearch && _searchPanel is not null)
        {
            _host.Children.Add(_searchPanel);
        }

        foreach (var chip in Filters ?? Enumerable.Empty<ArcFilterChip>())
        {
            _host.Children.Add(BuildChip(chip));
        }

        if (Fields is not null && Fields.Any())
        {
            _host.Children.Add(BuildAddButton());
        }

        if (SortOptions is not null && SortOptions.Any())
        {
            _host.Children.Add(BuildSortButton());
        }

        if (ResultCount is { } count)
        {
            var caption = FeedbackUi.Caption($"{count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} {ResultLabel}");
            caption.Margin = new Thickness(2, 0, 12, 8);
            caption.VerticalAlignment = VerticalAlignment.Center;
            _host.Children.Add(caption);
        }

        if ((Filters ?? Enumerable.Empty<ArcFilterChip>()).Any())
        {
            var clearAll = new ArcFlatButton { Text = "Clear all", Quiet = true, Margin = new Thickness(0, 0, 8, 8) };
            clearAll.Click += (_, _) => ClearAll();
            _host.Children.Add(clearAll);
        }

        AutomationProperties.SetName(this, Label);
        UpdatePopupMenus();
    }

    /// <summary>The popups live in a persistent zero-size host so row rebuilds keep them usable.</summary>
    private Control BuildPopupHost()
    {
        if (_popupHost is not null)
        {
            return _popupHost;
        }

        _filterMenu = new StackPanel { Spacing = 2 };
        _sortMenu = new StackPanel { Spacing = 2 };

        _filterPopup = new Popup
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = MenuSurface(_filterMenu),
            IsOpen = false
        };

        _sortPopup = new Popup
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = MenuSurface(_sortMenu),
            IsOpen = false
        };

        _popupHost = new Panel { Width = 0, Height = 0 };
        _popupHost.Children.Add(_filterPopup);
        _popupHost.Children.Add(_sortPopup);
        return _popupHost;
    }

    private static Border MenuSurface(Control content)
    {
        var surface = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            MinWidth = 220,
            MaxWidth = 300,
            Child = content
        };
        surface.Paint(Border.BackgroundProperty, ArcTokens.SurfaceRaised);
        surface.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        surface.BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating);
        return surface;
    }

    private Control BuildChip(ArcFilterChip chip)
    {
        var text = string.IsNullOrWhiteSpace(chip.Value) ? chip.Label : $"{chip.Label}: {chip.Value}";
        var label = FeedbackUi.Text(text);
        label.FontSize = ArcResources.Double(ArcTokens.TextXs);
        label.FontWeight = FontWeight.Medium;
        label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);

        var remove = new ArcIconButton
        {
            Icon = "x",
            IconSize = 11,
            BoxSize = 18,
            Label = $"Remove {text}",
            Muted = true
        };
        remove.Click += (_, _) => RemoveFilter(chip.Id);

        var row = FeedbackUi.Row(4, label, remove);
        var border = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPill),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 2, 3, 2),
            Margin = new Thickness(0, 0, 8, 8),
            VerticalAlignment = VerticalAlignment.Center,
            Child = row
        };
        border.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        border.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        AutomationProperties.SetName(border, text);
        return border;
    }

    private Control BuildAddButton()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(FeedbackUi.Icon("plus", 14, ArcTokens.TextSecondary));
        content.Children.Add(new TextBlock { Text = "Add filter", Classes = { "arc-body-sm" }, VerticalAlignment = VerticalAlignment.Center });

        _addButton = new ArcButton
        {
            Content = content,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Margin = new Thickness(0, 0, 8, 8)
        };
        _addButton.Click += (_, _) =>
        {
            ShowFields();
            _filterPopup!.PlacementTarget = _addButton;
            _filterPopup.IsOpen = !_filterPopup.IsOpen;
        };
        return _addButton;
    }

    private Control BuildSortButton()
    {
        var label = SortOptions?.FirstOrDefault(option => option.Key == SortKey)?.Label ?? "Sort";
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(FeedbackUi.Icon("arrow-up-down", 14, ArcTokens.TextSecondary));
        content.Children.Add(new TextBlock { Text = label, Classes = { "arc-body-sm" }, VerticalAlignment = VerticalAlignment.Center });

        _sortButton = new ArcButton
        {
            Content = content,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Margin = new Thickness(0, 0, 12, 8)
        };
        _sortButton.Click += (_, _) =>
        {
            ShowSortOptions();
            _sortPopup!.PlacementTarget = _sortButton;
            _sortPopup.IsOpen = !_sortPopup.IsOpen;
        };
        return _sortButton;
    }

    private void ShowFields()
    {
        if (_filterMenu is null)
        {
            return;
        }

        _filterMenu.Children.Clear();
        _filterMenu.Children.Add(MenuHeading("Add filter"));
        foreach (var field in Fields ?? Enumerable.Empty<ArcFilterField>())
        {
            var row = FeedbackUi.Row(8, FeedbackUi.Text(field.Label, "arc-body-sm"));
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            var chevron = FeedbackUi.Icon("chevron-right", 13, ArcTokens.TextMuted);
            chevron.HorizontalAlignment = HorizontalAlignment.Right;
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            line.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            line.Children.Add(row);
            Grid.SetColumn(chevron, 1);
            line.Children.Add(chevron);

            var button = new ArcFlatButton
            {
                Content = line,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };
            var captured = field;
            button.Click += (_, _) => ShowValues(captured);
            AutomationProperties.SetName(button, field.Label);
            _filterMenu.Children.Add(button);
        }
    }

    private void ShowValues(ArcFilterField field)
    {
        if (_filterMenu is null)
        {
            return;
        }

        _filterMenu.Children.Clear();
        var back = new ArcFlatButton { Text = $"‹ {field.Label}", Quiet = true, Padding = new Thickness(8, 6) };
        back.Click += (_, _) => ShowFields();
        _filterMenu.Children.Add(back);

        foreach (var option in field.Options)
        {
            var label = option.Label ?? option.Value;
            var active = (Filters ?? Enumerable.Empty<ArcFilterChip>())
                .Any(chip => chip.Id == field.Id && chip.Value == option.Value);
            var button = new ArcFlatButton
            {
                Text = label,
                Selected = active,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };
            var captured = option;
            button.Click += (_, _) => AddFilter(field, captured);
            _filterMenu.Children.Add(button);
        }
    }

    private void ShowSortOptions()
    {
        if (_sortMenu is null)
        {
            return;
        }

        _sortMenu.Children.Clear();
        _sortMenu.Children.Add(MenuHeading("Sort by"));
        foreach (var option in SortOptions ?? Enumerable.Empty<ArcSortOption>())
        {
            var active = option.Key == SortKey;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Children.Add(new TextBlock { Text = option.Label, Classes = { "arc-body-sm" }, VerticalAlignment = VerticalAlignment.Center });
            if (active)
            {
                var check = FeedbackUi.Icon("check", 14, ArcTokens.AccentStrong);
                Grid.SetColumn(check, 1);
                row.Children.Add(check);
            }

            var button = new ArcFlatButton
            {
                Content = row,
                Selected = active,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };
            var captured = option;
            button.Click += (_, _) =>
            {
                SetCurrentValue(SortKeyProperty, captured.Key);
                RaiseSortChanged(captured.Key);
                if (_sortPopup is not null)
                {
                    _sortPopup.IsOpen = false;
                }
            };
            _sortMenu.Children.Add(button);
        }
    }

    private static Control MenuHeading(string text)
    {
        var heading = FeedbackUi.Text(text, "arc-caption");
        heading.Margin = new Thickness(10, 4, 10, 6);
        return heading;
    }

    private void AddFilter(ArcFilterField field, ArcFilterOption option)
    {
        var chip = new ArcFilterChip
        {
            Id = field.Id,
            Label = field.Label,
            Value = option.Label ?? option.Value
        };

        var list = (Filters ?? Enumerable.Empty<ArcFilterChip>())
            .Where(item => item.Id != field.Id)
            .ToList();
        list.Add(chip);
        if (_filterPopup is not null)
        {
            _filterPopup.IsOpen = false;
        }

        SetCurrentValue(FiltersProperty, list);
        RaiseFilterAdded(chip);
    }

    private void RemoveFilter(string id)
    {
        if (_filterPopup is not null)
        {
            _filterPopup.IsOpen = false;
        }

        var list = (Filters ?? Enumerable.Empty<ArcFilterChip>())
            .Where(item => item.Id != id)
            .ToList();
        SetCurrentValue(FiltersProperty, list);
        RaiseFilterRemoved(id);
    }

    private void ClearAll()
    {
        SetCurrentValue(FiltersProperty, new List<ArcFilterChip>());
        RaiseClearAllRequested();
    }

    private void UpdatePopupMenus()
    {
        if (_filterMenu is not null && _filterMenu.Children.Count == 0)
        {
            ShowFields();
        }

        if (_sortMenu is not null && _sortMenu.Children.Count == 0)
        {
            ShowSortOptions();
        }
    }
}

/// <summary>Event data for the bubbling <see cref="FilterRemovedRouted"/> event.</summary>
public sealed class ArcFilterRemovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcFilterRemovedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public string Id { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="FilterAddedRouted"/> event.</summary>
public sealed class ArcFilterAddedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcFilterAddedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public ArcFilterChip Chip { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="ClearAllRequestedRouted"/> event.</summary>
public sealed class ArcFilterClearAllRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcFilterClearAllRequestedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Event data for the bubbling <see cref="SearchChangedRouted"/> event.</summary>
public sealed class ArcFilterSearchChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcFilterSearchChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public string Text { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="SortChangedRouted"/> event.</summary>
public sealed class ArcFilterSortChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcFilterSortChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public string? SortKey { get; init; }
}
