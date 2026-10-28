using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedInputs.Controls;

public enum ArcExpandingSearchAnchor
{
    Start,
    End
}

public sealed class ArcExpandingSearchItem
{
    public ArcExpandingSearchItem(string id, string title, string? group = null, string? meta = null, IList<string>? keywords = null)
    {
        Id = id;
        Title = title;
        Group = group;
        Meta = meta;
        Keywords = keywords;
    }

    public string Id { get; }

    public string Title { get; }

    public string? Group { get; }

    public string? Meta { get; }

    public IList<string>? Keywords { get; }
}

public sealed class ArcExpandingSearchSelectedEventArgs : EventArgs
{
    public ArcExpandingSearchSelectedEventArgs(ArcExpandingSearchItem item) => Item = item;

    public ArcExpandingSearchItem Item { get; }
}

/// <summary>
/// Arc's expanding search: an icon button that morphs into a field on a width spring, with grouped
/// results beneath and Escape folding it back to the icon.
/// </summary>
public class ArcExpandingSearch : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, string?>(nameof(Label));

    public static readonly StyledProperty<IList<ArcExpandingSearchItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, IList<ArcExpandingSearchItem>?>(nameof(Items));

    public static readonly StyledProperty<IList<ArcExpandingSearchItem>?> SuggestionsProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, IList<ArcExpandingSearchItem>?>(nameof(Suggestions));

    public static readonly StyledProperty<string?> SuggestionsLabelProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, string?>(nameof(SuggestionsLabel), "Recent");

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, string?>(nameof(Placeholder));

    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, double>(nameof(ExpandedWidth), 360d);

    public static readonly StyledProperty<int> MaxResultsProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, int>(nameof(MaxResults), 6);

    public static readonly StyledProperty<ArcExpandingSearchAnchor> AnchorProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, ArcExpandingSearchAnchor>(nameof(Anchor), ArcExpandingSearchAnchor.End);

    public static readonly StyledProperty<string?> EmptyHintProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, string?>(nameof(EmptyHint), "Try a shorter word or check the spelling.");

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcExpandingSearch, bool>(nameof(IsExpanded));

    private Border? _field;
    private ArcIcon? _icon;
    private TextBox? _query;
    private Button? _clear;
    private Border? _panel;
    private StackPanel? _rows;
    private Panel? _host;

    private readonly List<ResultEntry> _flat = new();
    private int _active;
    private bool _expanded;
    private double _width = 40;

    static ArcExpandingSearch()
    {
        IsExpandedProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) =>
        {
            if (search.IsExpanded != search._expanded)
            {
                search.SetExpanded(search.IsExpanded, animate: true);
            }
        });
        AnchorProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) => search.ApplyAnchor());
        PlaceholderProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) => search.UpdatePlaceholder());
        LabelProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) => search.UpdatePlaceholder());
        ItemsProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) =>
        {
            if (search._expanded)
            {
                search.BuildRows();
            }
        });
        SuggestionsProperty.Changed.AddClassHandler<ArcExpandingSearch>((search, _) =>
        {
            if (search._expanded)
            {
                search.BuildRows();
            }
        });
    }

    public ArcExpandingSearch()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    /// <summary>Raised when a result is chosen; the field then folds back.</summary>
    public event EventHandler<ArcExpandingSearchSelectedEventArgs>? Selected;

    /// <summary>Raised when the field opens or folds.</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Names the button, the field and the results, such as "Search projects and docs".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Everything the field can find; filtered locally by title and keywords.</summary>
    public IList<ArcExpandingSearchItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Shown before anything is typed, such as recent searches.</summary>
    public IList<ArcExpandingSearchItem>? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public string? SuggestionsLabel
    {
        get => GetValue(SuggestionsLabelProperty);
        set => SetValue(SuggestionsLabelProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Widest the field grows, never past its container.</summary>
    public double ExpandedWidth
    {
        get => GetValue(ExpandedWidthProperty);
        set => SetValue(ExpandedWidthProperty, value);
    }

    public int MaxResults
    {
        get => GetValue(MaxResultsProperty);
        set => SetValue(MaxResultsProperty, value);
    }

    /// <summary>Edge the button sits on; the field grows away from it.</summary>
    public ArcExpandingSearchAnchor Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    public string? EmptyHint
    {
        get => GetValue(EmptyHintProperty);
        set => SetValue(EmptyHintProperty, value);
    }

    /// <summary>Open state, exposed so page code can show the field and its results.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Opens the field and focuses it.</summary>
    public void Expand()
    {
        SetExpanded(true, animate: true);
        Dispatcher.UIThread.Post(() => _query?.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>Folds the field back into the icon and clears the query.</summary>
    public void Collapse()
    {
        if (_query is not null)
        {
            _query.Text = string.Empty;
        }

        SetExpanded(false, animate: true);
        Focus();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_query is not null)
        {
            _query.RemoveHandler(KeyDownEvent, OnQueryKeyDown);
            _query.TextChanged -= OnQueryTextChanged;
            _query.GotFocus -= OnQueryGotFocus;
            _query.LostFocus -= OnQueryLostFocus;
        }

        if (_clear is not null)
        {
            _clear.Click -= OnClearClicked;
        }

        _field = e.NameScope.Find<Border>("Field");
        _icon = e.NameScope.Find<ArcIcon>("Icon");
        _query = e.NameScope.Find<TextBox>("PART_Query");
        _clear = e.NameScope.Find<Button>("ClearButton");
        _panel = e.NameScope.Find<Border>("Panel");
        _rows = e.NameScope.Find<StackPanel>("Rows");
        _host = e.NameScope.Find<Panel>("Host");

        if (_query is not null)
        {
            _query.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
            _query.TextChanged += OnQueryTextChanged;
            _query.GotFocus += OnQueryGotFocus;
            _query.LostFocus += OnQueryLostFocus;
            AutomationProperties.SetName(_query, Label ?? "Search");
        }

        if (_clear is not null)
        {
            _clear.Click += OnClearClicked;
        }

        ApplyAnchor();
        UpdatePlaceholder();
        if (_field is not null)
        {
            _field.Width = 40;
        }

        if (_expanded)
        {
            SetExpanded(true, animate: false);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == BoundsProperty && _expanded)
        {
            ApplySize(ExpandedTargetWidth());
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled)
        {
            return;
        }

        var source = e.Source as Visual;
        if (_panel is not null && _panel.IsVisible && source is not null && IsWithin(source, _panel))
        {
            return;
        }

        if (_field is not null && source is not null && IsWithin(source, _field))
        {
            if (_expanded && source is not null && _query is not null && IsWithin(source, _query))
            {
                return;
            }

            e.Handled = true;
            if (_expanded)
            {
                _query?.Focus();
            }
            else
            {
                Expand();
            }

            return;
        }

        if (_expanded)
        {
            Collapse();
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

        if (!_expanded && e.Key is Key.Enter or Key.Space or Key.Down)
        {
            e.Handled = true;
            Expand();
            return;
        }

        if (!_expanded)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Collapse();
                break;
            case Key.Down:
                e.Handled = true;
                Move(1);
                break;
            case Key.Up:
                e.Handled = true;
                Move(-1);
                break;
            case Key.Enter:
                e.Handled = true;
                ChooseActive();
                break;
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_expanded)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Collapse();
                return;
            case Key.Down:
                e.Handled = true;
                Move(1);
                return;
            case Key.Up:
                e.Handled = true;
                Move(-1);
                return;
            case Key.Enter:
                e.Handled = true;
                ChooseActive();
                return;
        }
    }

    private void OnQueryGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void OnQueryLostFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", false);

    private void OnQueryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_expanded)
        {
            return;
        }


        BuildRows();
        UpdateClear();
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        if (_query is not null)
        {
            _query.Text = string.Empty;
            _query.Focus();
        }
    }

    private void UpdateClear()
    {
        if (_clear is not null)
        {
            _clear.IsVisible = _expanded && !string.IsNullOrEmpty(_query?.Text);
        }
    }

    private void UpdatePlaceholder()
    {
        if (_query is not null)
        {
            _query.PlaceholderText = Placeholder ?? Label ?? "Search";
            AutomationProperties.SetName(_query, Label ?? "Search");
        }

        AutomationProperties.SetName(this, Label ?? "Search");
        AutomationProperties.SetHelpText(this, _expanded ? "Type to search. Escape folds the field." : "Press Enter to search.");
    }

    private void ApplyAnchor()
    {
        var horizontal = Anchor == ArcExpandingSearchAnchor.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        if (_field is not null)
        {
            _field.HorizontalAlignment = horizontal;
        }

        if (_panel is not null)
        {
            _panel.HorizontalAlignment = horizontal;
        }
    }

    private double ExpandedTargetWidth()
    {
        var available = this.GetVisualParent() is Visual parent && parent.Bounds.Width > 0
            ? parent.Bounds.Width
            : ExpandedWidth;
        return Math.Max(120, Math.Min(ExpandedWidth, available - 2));
    }

    private void SetExpanded(bool expanded, bool animate)
    {
        if (_field is null)
        {
            _expanded = expanded;
            return;
        }

        _expanded = expanded;
        SetCurrentValue(IsExpandedProperty, expanded);
        Classes.Set("expanded", expanded);
        Classes.Set("collapsed", !expanded);
        if (_query is not null)
        {
            _query.IsVisible = true;
        }

        var target = expanded ? ExpandedTargetWidth() : 40d;
        var from = _width;

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            ApplySize(target);
        }
        else
        {
            ArcAnim.To(from, target, ArcAnim.SmoothDuration, ArcAnim.Smooth, ApplySize);
        }

        if (_icon is not null)
        {
            _icon.Icon = expanded ? "search" : "search";
        }

        if (expanded)
        {

            BuildRows();
            UpdateClear();
        }
        else
        {
            if (_panel is not null)
            {
                _panel.IsVisible = false;
            }

            if (_query is not null)
            {
                _query.Text = string.Empty;
            }
        }

        ExpandedChanged?.Invoke(this, EventArgs.Empty);
        UpdatePlaceholder();
    }

    private void ApplySize(double width)
    {
        _width = width;
        if (_field is not null)
        {
            _field.Width = width;
            _field.CornerRadius = new CornerRadius(Math.Min(20, width / 2));
        }

        if (_panel is not null)
        {
            _panel.Width = Math.Max(160, width);
        }

        if (_query is not null)
        {
            _query.IsVisible = width > 76;
            _query.Opacity = Math.Clamp((width - 60) / 60, 0, 1);
        }

        if (_clear is not null)
        {
            UpdateClear();
        }
    }

    private void BuildRows()
    {
        if (_rows is null || _panel is null)
        {
            return;
        }

        _rows.Children.Clear();
        _flat.Clear();

        var query = _query?.Text?.Trim() ?? string.Empty;
        var source = query.Length == 0
            ? (Suggestions ?? Array.Empty<ArcExpandingSearchItem>())
            : (Items ?? Array.Empty<ArcExpandingSearchItem>());

        var heading = query.Length == 0 ? SuggestionsLabel ?? "Recent" : null;
        var matches = query.Length == 0
            ? source.Take(Math.Max(1, MaxResults)).ToList()
            : source.Where(item => Matches(item, query)).Take(Math.Max(1, MaxResults)).ToList();

        if (matches.Count == 0)
        {
            if (query.Length > 0)
            {
                var empty = new StackPanel { Spacing = 2, Margin = new Thickness(12, 10, 12, 12) };
                empty.Children.Add(new TextBlock { Text = "No matches", Classes = { "arc-body-sm" }, FontWeight = FontWeight.Medium });
                if (!string.IsNullOrWhiteSpace(EmptyHint))
                {
                    empty.Children.Add(new TextBlock
                    {
                        Text = EmptyHint,
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                _rows.Children.Add(empty);
            }

            _panel.IsVisible = matches.Count > 0 || query.Length > 0;
            _active = -1;
            return;
        }

        if (!string.IsNullOrEmpty(heading))
        {
            _rows.Children.Add(new TextBlock
            {
                Text = heading,
                Classes = { "arc-caption" },
                Margin = new Thickness(12, 8, 12, 4)
            });
        }

        string? currentGroup = null;
        foreach (var item in matches)
        {
            if (query.Length > 0 && !string.IsNullOrEmpty(item.Group) &&
                !string.Equals(currentGroup, item.Group, StringComparison.Ordinal))
            {
                currentGroup = item.Group;
                _rows.Children.Add(new TextBlock
                {
                    Text = item.Group,
                    Classes = { "arc-caption" },
                    Margin = new Thickness(12, 8, 12, 4)
                });
            }

            var index = _flat.Count;
            var row = CreateRow(item, index);
            _flat.Add(new ResultEntry(item, row));
            _rows.Children.Add(row);
        }

        _active = 0;

        _panel.IsVisible = true;
        UpdateHighlight();
    }

    private static bool Matches(ArcExpandingSearchItem item, string query)
    {
        if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (item.Meta?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return item.Keywords?.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private Border CreateRow(ArcExpandingSearchItem item, int index)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock
        {
            Text = item.Title,
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrEmpty(item.Meta))
        {
            var meta = new TextBlock
            {
                Text = item.Meta,
                Classes = { "arc-caption" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(meta, 1);
            grid.Children.Add(meta);
        }

        var row = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 0),
            Background = Brushes.Transparent,
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        row.PointerEntered += (_, _) =>
        {
            var current = _flat.FindIndex(entry => ReferenceEquals(entry.Row, row));
            if (current >= 0)
            {
                _active = current;
                UpdateHighlight();
            }
        };

        row.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            Select(item);
        };

        return row;
    }

    private void UpdateHighlight()
    {
        for (var i = 0; i < _flat.Count; i++)
        {
            _flat[i].Row.Background = i == _active
                ? ArcResources.Brush(ArcTokens.SurfaceMuted)
                : Brushes.Transparent;
        }

        AutomationProperties.SetHelpText(this, _active >= 0 && _active < _flat.Count
            ? _flat[_active].Item.Title
            : "No matches");
    }

    private void Move(int delta)
    {
        if (_flat.Count == 0)
        {
            return;
        }

        _active = (_active + delta + _flat.Count) % _flat.Count;
        UpdateHighlight();
    }

    private void ChooseActive()
    {
        if (_active >= 0 && _active < _flat.Count)
        {
            Select(_flat[_active].Item);
        }
    }

    private void Select(ArcExpandingSearchItem item)
    {
        Selected?.Invoke(this, new ArcExpandingSearchSelectedEventArgs(item));
        SetExpanded(false, animate: true);
        Focus();
    }

    private sealed record ResultEntry(ArcExpandingSearchItem Item, Border Row);
}
