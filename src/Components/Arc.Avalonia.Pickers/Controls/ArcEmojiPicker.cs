using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>One category tab: the glyph shown on the tab, the full name and the group it jumps to.</summary>
public sealed class ArcEmojiCategoryOption
{
    public required ArcEmojiCategory Category { get; init; }

    /// <summary>Full category name, used by the tooltip and the accessible name.</summary>
    public required string Label { get; init; }

    /// <summary>A representative emoji used as the tab glyph.</summary>
    public required string Glyph { get; init; }

    public override string ToString() => Label;
}

/// <summary>One skin-tone chip: the toned hand that swatches it and the tone it applies.</summary>
public sealed class ArcEmojiToneOption
{
    public required ArcSkinTone Tone { get; init; }

    public required string Glyph { get; init; }

    public required string Name { get; init; }

    public int Index => (int)Tone;

    public override string ToString() => Name;
}

/// <summary>One rendered grid row: an optional section header plus the cells in that line.</summary>
public sealed class ArcEmojiRow
{
    public string? Header { get; init; }

    public bool HasHeader => Header is not null;

    public required ArcEmojiCategory Category { get; init; }

    public required IReadOnlyList<ArcEmojiCell> Cells { get; init; }
}

/// <summary>A single grid cell. Created per rebuild so a tone change re-renders without a second dataset.</summary>
public sealed class ArcEmojiCell : INotifyPropertyChanged
{
    private bool _isActive;

    public ArcEmojiCell(ArcEmoji emoji, string glyph, double size, double fontSize, ArcEmojiCategory category, ArcSkinTone tone)
    {
        Emoji = emoji;
        Glyph = glyph;
        Size = size;
        FontSize = fontSize;
        Category = category;
        Tone = tone;
    }

    public ArcEmoji Emoji { get; }

    /// <summary>Glyph with the active skin tone applied.</summary>
    public string Glyph { get; }

    public string Name => Emoji.Name;

    public double Size { get; }

    public double FontSize { get; }

    public ArcEmojiCategory Category { get; }

    public ArcSkinTone Tone { get; }

    /// <summary>True for the one cell the keyboard is on; bound to the accent ring in the template.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Arc's emoji picker, reimplemented from the public documentation: instant search over names and keywords,
/// a category bar whose active tab follows the scroll, skin-tone chips, a recently used row, a preview line
/// with the emoji name and a virtualised grid that is fully keyboard navigable.
///
/// It ships a hand-curated set (<see cref="ArcEmojiData"/>) rather than Arc's full Emoji 15.0 table, and it
/// keeps recent emoji in memory only (this port has no per-page storage), so it approximates Arc's
/// localStorage-backed recents. Picking raises <see cref="Selected"/> but leaves <see cref="IsOpen"/> alone,
/// matching Arc's callback model; Escape clears the search first and then closes the panel. The reflowing
/// tone menu is replaced by six always-visible tone chips, so the state reads at a glance, and the active
/// category is marked by an accent-tinted tab pill rather than a gliding indicator.
/// </summary>
[TemplatePart("PART_Search", typeof(TextBox))]
[TemplatePart("PART_Clear", typeof(Button))]
[TemplatePart("PART_Categories", typeof(ListBox))]
[TemplatePart("PART_Tones", typeof(ListBox))]
[TemplatePart("PART_Grid", typeof(ListBox))]
[TemplatePart("PART_RecentHost", typeof(Border))]
[TemplatePart("PART_RecentRow", typeof(StackPanel))]
[TemplatePart("PART_PreviewGlyph", typeof(TextBlock))]
[TemplatePart("PART_PreviewName", typeof(TextBlock))]
[TemplatePart("PART_Count", typeof(TextBlock))]
[TemplatePart("PART_Empty", typeof(Border))]
[TemplatePart("PART_SearchPlaceholder", typeof(TextBlock))]
public class ArcEmojiPicker : TemplatedControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> SelectedEmojiProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, string?>(nameof(SelectedEmoji));

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, string?>(nameof(SearchText));

    public static readonly StyledProperty<ArcSkinTone> SkinToneProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, ArcSkinTone>(nameof(SkinTone));

    public static readonly StyledProperty<bool> ShowSkinTonesProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, bool>(nameof(ShowSkinTones), true);

    public static readonly StyledProperty<IReadOnlyList<ArcEmojiCategory>?> CategoriesProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, IReadOnlyList<ArcEmojiCategory>?>(nameof(Categories));

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, double>(nameof(CellSize), 36d);

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, int>(nameof(Columns));

    public static readonly StyledProperty<int> RecentLimitProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, int>(nameof(RecentLimit), 18);

    public static readonly StyledProperty<IReadOnlyList<string>?> DefaultRecentProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, IReadOnlyList<string>?>(nameof(DefaultRecent));

    public static readonly StyledProperty<string?> SearchPlaceholderProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, string?>(nameof(SearchPlaceholder), "Search emoji");

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcEmojiPicker, string?>(nameof(Label), "Emoji picker");

    private const double RowHeaderHeight = 30;
    private const int PageRows = 5;

    private readonly List<ArcEmoji> _recent = new();
    private readonly List<ArcEmojiRow> _rows = new();
    private readonly List<ArcEmojiCell> _flat = new();
    private readonly List<double> _rowTops = new();

    private TextBox? _search;
    private Button? _clear;
    private ListBox? _categories;
    private ListBox? _tones;
    private ListBox? _grid;
    private Border? _recentHost;
    private StackPanel? _recentRow;
    private TextBlock? _previewGlyph;
    private TextBlock? _previewName;
    private TextBlock? _count;
    private TextBlock? _placeholder;
    private Border? _empty;
    private ScrollViewer? _scroll;

    private ArcEmojiCell? _active;
    private ArcEmojiCell? _hover;
    private bool _syncing;
    private bool _picking;
    private bool _jumping;
    private int _autoColumns;
    private DispatcherTimer? _scrollTimer;

    static ArcEmojiPicker()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.OnOpenChanged());
        SearchTextProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.OnSearchChanged());
        SkinToneProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.OnToneChanged());
        ShowSkinTonesProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.UpdateToneVisibility());
        SearchPlaceholderProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.UpdatePlaceholder());
        CategoriesProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.RebuildAll(true));
        CellSizeProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.RebuildAll(true));
        ColumnsProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.RebuildAll(true));
        RecentLimitProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.TrimRecent());
        DefaultRecentProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.SeedRecent());
        LabelProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.UpdateAutomation());
        SelectedEmojiProperty.Changed.AddClassHandler<ArcEmojiPicker>((picker, _) => picker.OnSelectedEmojiChanged());
    }

    /// <summary>Whether the panel is showing. Set it from a trigger; Escape closes it.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The last glyph picked, with its skin tone applied.</summary>
    public string? SelectedEmoji
    {
        get => GetValue(SelectedEmojiProperty);
        set => SetValue(SelectedEmojiProperty, value);
    }

    /// <summary>The search query; filtering is instant and grouping is kept.</summary>
    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>Active skin tone, applied to the tone-capable subset as cells render.</summary>
    public ArcSkinTone SkinTone
    {
        get => GetValue(SkinToneProperty);
        set => SetValue(SkinToneProperty, value);
    }

    /// <summary>Shows the six tone chips beside the preview name.</summary>
    public bool ShowSkinTones
    {
        get => GetValue(ShowSkinTonesProperty);
        set => SetValue(ShowSkinTonesProperty, value);
    }

    /// <summary>Restricts the picker to these categories; null shows all eight.</summary>
    public IReadOnlyList<ArcEmojiCategory>? Categories
    {
        get => GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    /// <summary>Grid cell box in px, margins included. Defaults to 36.</summary>
    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    /// <summary>Fixed cells per row; 0 derives the count from the width (at least six).</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How many recently used emoji are kept. Defaults to 18.</summary>
    public int RecentLimit
    {
        get => GetValue(RecentLimitProperty);
        set => SetValue(RecentLimitProperty, value);
    }

    /// <summary>Seed for the recently used row; ignored once something has been picked.</summary>
    public IReadOnlyList<string>? DefaultRecent
    {
        get => GetValue(DefaultRecentProperty);
        set => SetValue(DefaultRecentProperty, value);
    }

    /// <summary>Placeholder of the search field.</summary>
    public string? SearchPlaceholder
    {
        get => GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    /// <summary>Accessible name of the picker group.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Recently used glyphs, most recent first, with the active tone applied.</summary>
    public IReadOnlyList<string> Recent =>
        _recent.Select(emoji => ArcEmojiData.WithTone(emoji, SkinTone)).ToArray();

    /// <summary>Raised when an emoji is chosen by click, Enter, Space or a recent chip.</summary>
    public event EventHandler<ArcEmojiSelection>? Selected;

    /// <summary>Bubbled copy of <see cref="Selected"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcEmojiSelectedEventArgs> SelectedEvent =
        RoutedEvent.Register<ArcEmojiPicker, ArcEmojiSelectedEventArgs>("Selected", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="Selected"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcEmojiSelectedEventArgs>? SelectedRouted
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }

    private void RaiseSelected(ArcEmojiSelection selection)
    {
        Selected?.Invoke(this, selection);
        RaiseEvent(new ArcEmojiSelectedEventArgs(SelectedEvent) { Selection = selection });
    }

    /// <summary>Raised when Escape or a host closes the panel.</summary>
    public event EventHandler? Closed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_search is not null)
        {
            _search.TextChanged -= OnSearchTextChanged;
        }

        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }

        if (_categories is not null)
        {
            _categories.SelectionChanged -= OnCategorySelection;
        }

        if (_tones is not null)
        {
            _tones.SelectionChanged -= OnToneSelection;
        }

        if (_grid is not null)
        {
            _grid.RemoveHandler(PointerPressedEvent, OnGridPointerPressed);
            _grid.RemoveHandler(PointerMovedEvent, OnGridPointerMoved);
            _grid.RemoveHandler(PointerExitedEvent, OnGridPointerExited);
            _grid.RemoveHandler(KeyDownEvent, OnGridKeyDown);
            _grid.RemoveHandler(TextInputEvent, OnGridTextInput);
            _grid.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnGridScrollChanged);
            _grid.SizeChanged -= OnGridSizeChanged;
        }

        _search = e.NameScope.Find<TextBox>("PART_Search");
        _clear = e.NameScope.Find<Button>("PART_Clear");
        _categories = e.NameScope.Find<ListBox>("PART_Categories");
        _tones = e.NameScope.Find<ListBox>("PART_Tones");
        _grid = e.NameScope.Find<ListBox>("PART_Grid");
        _recentHost = e.NameScope.Find<Border>("PART_RecentHost");
        _recentRow = e.NameScope.Find<StackPanel>("PART_RecentRow");
        _previewGlyph = e.NameScope.Find<TextBlock>("PART_PreviewGlyph");
        _previewName = e.NameScope.Find<TextBlock>("PART_PreviewName");
        _count = e.NameScope.Find<TextBlock>("PART_Count");
        _empty = e.NameScope.Find<Border>("PART_Empty");
        _placeholder = e.NameScope.Find<TextBlock>("PART_SearchPlaceholder");

        if (_search is not null)
        {
            _search.TextChanged += OnSearchTextChanged;
            _search.ApplyTemplate();
            var presenter = _search.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
            if (presenter is not null)
            {
                presenter.Foreground = ArcResources.Brush(ArcTokens.Foreground);
            }
        }

        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }

        if (_categories is not null)
        {
            _categories.SelectionChanged += OnCategorySelection;
        }

        if (_tones is not null)
        {
            _tones.SelectionChanged += OnToneSelection;
        }

        if (_grid is not null)
        {
            if (_search is not null)
            {
                AutomationProperties.SetName(
                    _search,
                    string.IsNullOrWhiteSpace(SearchPlaceholder) ? "Search emoji" : SearchPlaceholder);
            }

            if (_categories is not null)
            {
                AutomationProperties.SetName(_categories, "Categories");
            }

            if (_tones is not null)
            {
                AutomationProperties.SetName(_tones, "Skin tone");
            }

            _grid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
            _grid.AddHandler(PointerMovedEvent, OnGridPointerMoved, RoutingStrategies.Tunnel);
            _grid.AddHandler(PointerExitedEvent, OnGridPointerExited);
            _grid.AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
            _grid.AddHandler(TextInputEvent, OnGridTextInput, RoutingStrategies.Tunnel);
            _grid.AddHandler(ScrollViewer.ScrollChangedEvent, OnGridScrollChanged);
            _grid.SizeChanged += OnGridSizeChanged;
            AutomationProperties.SetName(_grid, "Emoji");
            AutomationProperties.SetHelpText(
                _grid,
                "Use the arrow keys to move, Home and End for the row edges, Page Up and Page Down for a screen, " +
                "Enter to pick, Escape to clear the search or close.");
        }

        SyncSearchBox();
        SeedRecent();
        RebuildAll(true);

        if (IsOpen)
        {
            FocusSearch();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Columns == 0 && _grid is not null && EffectiveColumns() != _autoColumns)
        {
            RebuildRows();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsOpen)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            HandleEscape();
            e.Handled = true;
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var inSearch = _search is not null && (ReferenceEquals(focused, _search) || _search.IsKeyboardFocusWithin);

        if (inSearch)
        {
            if (e.Key is Key.Down or Key.Up)
            {
                MoveFocusToGrid();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter)
            {
                if (PickActive())
                {
                    e.Handled = true;
                }

                return;
            }
        }

        if (_grid is not null && _grid.IsKeyboardFocusWithin)
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                PickActive();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            {
                MoveActive(e.Key, e.KeyModifiers);
                e.Handled = true;
            }
        }
    }

    private void OnOpenChanged()
    {
        UpdateAutomation();
        if (IsOpen)
        {
            if (_active is null)
            {
                ActivateFirst();
            }

            FocusSearch();
        }
        else
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSearchChanged()
    {
        SyncSearchBox();
        RebuildRows(resetActive: true, keepCategory: true);
    }

    private void OnToneChanged()
    {
        if (_tones is not null && _tones.SelectedIndex != (int)SkinTone)
        {
            _syncing = true;
            try
            {
                _tones.SelectedIndex = (int)SkinTone;
            }
            finally
            {
                _syncing = false;
            }
        }

        RebuildRows();
        RefreshRecent();
        UpdatePreview();
    }

    private void OnSelectedEmojiChanged()
    {
        if (_picking)
        {
            return;
        }

        var glyph = SelectedEmoji;
        if (string.IsNullOrEmpty(glyph))
        {
            return;
        }

        UpdatePreview(ArcEmojiData.Find(glyph));
    }

    private void UpdateToneVisibility()
    {
        if (_tones is not null)
        {
            _tones.IsVisible = ShowSkinTones;
        }
    }

    private void UpdatePlaceholder()
    {
        if (_placeholder is not null)
        {
            _placeholder.Text = SearchPlaceholder;
        }

        if (_search is not null)
        {
            AutomationProperties.SetName(
                _search,
                string.IsNullOrWhiteSpace(SearchPlaceholder) ? "Search emoji" : SearchPlaceholder);
        }
    }

    private void SyncSearchBox()
    {
        if (_search is null)
        {
            return;
        }

        var text = SearchText ?? string.Empty;
        if (_search.Text != text)
        {
            _syncing = true;
            try
            {
                _search.Text = text;
            }
            finally
            {
                _syncing = false;
            }
        }

        UpdateSearchChrome(text);
    }

    private void UpdateSearchChrome(string text)
    {
        if (_clear is not null)
        {
            _clear.IsVisible = text.Length > 0;
        }

        if (_placeholder is not null)
        {
            _placeholder.IsVisible = text.Length == 0;
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _search is null)
        {
            return;
        }

        var text = _search.Text ?? string.Empty;
        _syncing = true;
        try
        {
            SetCurrentValue(SearchTextProperty, text);
        }
        finally
        {
            _syncing = false;
        }

        UpdateSearchChrome(text);
    }


    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(SearchTextProperty, string.Empty);
        FocusSearch();
    }

    private void HandleEscape()
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            SetCurrentValue(SearchTextProperty, string.Empty);
            FocusSearch();
            return;
        }

        SetCurrentValue(IsOpenProperty, false);
    }

    private void FocusSearch()
    {
        if (_search is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                _search?.Focus();

            },
            DispatcherPriority.Loaded);
    }

    private void MoveFocusToGrid()
    {
        if (_active is null)
        {
            ActivateFirst();
        }

        _grid?.Focus();
    }

    private void RebuildAll(bool resetTabs)
    {
        BuildCategoryList();
        BuildToneList();
        RebuildRows(resetActive: true, keepCategory: !resetTabs);
        RefreshRecent();
        UpdatePreview();
        UpdateAutomation();
    }

    private void BuildCategoryList()
    {
        if (_categories is null)
        {
            return;
        }

        var source = Categories is { Count: > 0 }
            ? Categories
            : Enum.GetValues<ArcEmojiCategory>();

        var options = source.Select(category => new ArcEmojiCategoryOption
        {
            Category = category,
            Label = ArcEmojiData.Label(category),
            Glyph = ArcEmojiData.For(category).FirstOrDefault()?.Glyph ?? "🙂"
        }).ToArray();

        var selected = (_categories.SelectedItem as ArcEmojiCategoryOption)?.Category;
        _syncing = true;
        try
        {
            _categories.ItemsSource = options;
            var index = selected is { } current ? Array.FindIndex(options, option => option.Category == current) : 0;
            _categories.SelectedIndex = options.Length == 0 ? -1 : Math.Max(0, index);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BuildToneList()
    {
        if (_tones is null)
        {
            return;
        }

        var options = Enum.GetValues<ArcSkinTone>()
            .Select(tone => new ArcEmojiToneOption
            {
                Tone = tone,
                Name = ArcEmojiData.ToneName(tone),
                Glyph = ArcEmojiData.ToneSwatch(tone)
            })
            .ToArray();

        _syncing = true;
        try
        {
            _tones.ItemsSource = options;
            _tones.SelectedIndex = (int)SkinTone;
        }
        finally
        {
            _syncing = false;
        }

        _tones.IsVisible = ShowSkinTones;
    }

    private int EffectiveColumns()
    {
        if (Columns > 0)
        {
            return Columns;
        }

        var width = _grid?.Bounds.Width ?? 0;
        if (width <= 0)
        {
            width = (Bounds.Width > 0 ? Bounds.Width : 360) - 28;
        }

        var size = Math.Max(24, CellSize);
        return Math.Clamp((int)Math.Floor(width / size), 6, 14);
    }

    private void RebuildRows(bool resetActive = false, bool keepCategory = false)
    {
        if (_grid is null)
        {
            return;
        }

        var category = keepCategory ? (_categories?.SelectedItem as ArcEmojiCategoryOption)?.Category : null;
        var previous = resetActive ? null : _active?.Emoji;
        var columns = EffectiveColumns();
        _autoColumns = columns;

        var filtered = new List<(ArcEmojiCategory Category, IReadOnlyList<ArcEmoji> Items)>();
        var categories = Categories is { Count: > 0 }
            ? Categories
            : Enum.GetValues<ArcEmojiCategory>().ToArray();

        foreach (var item in categories)
        {
            var matches = ArcEmojiData.Filter(item, SearchText);
            if (matches.Count > 0)
            {
                filtered.Add((item, matches));
            }
        }

        _rows.Clear();
        _flat.Clear();
        var size = Math.Max(24, CellSize);
        var fontSize = Math.Round(size * 0.56, 1);

        foreach (var (itemCategory, matches) in filtered)
        {
            for (var start = 0; start < matches.Count; start += columns)
            {
                var cells = new List<ArcEmojiCell>(Math.Min(columns, matches.Count - start));
                for (var i = start; i < matches.Count && i < start + columns; i++)
                {
                    var emoji = matches[i];
                    var cell = new ArcEmojiCell(
                        emoji,
                        ArcEmojiData.WithTone(emoji, SkinTone),
                        size - 4,
                        fontSize,
                        itemCategory,
                        SkinTone);
                    cells.Add(cell);
                    _flat.Add(cell);
                }

                _rows.Add(new ArcEmojiRow
                {
                    Header = start == 0 ? ArcEmojiData.Label(itemCategory) : null,
                    Category = itemCategory,
                    Cells = cells
                });
            }
        }

        _rowTops.Clear();
        var top = 0d;
        foreach (var row in _rows)
        {
            _rowTops.Add(top);
            top += (row.HasHeader ? RowHeaderHeight : 0) + size;
        }

        _grid.ItemsSource = _rows.ToArray();

        if (_empty is not null)
        {
            _empty.IsVisible = _rows.Count == 0;
        }

        if (_count is not null)
        {
            _count.Text = string.IsNullOrWhiteSpace(SearchText)
                ? string.Empty
                : _flat.Count == 1 ? "1 result" : $"{_flat.Count} results";
            _count.IsVisible = _count.Text.Length > 0;
        }

        var target = previous is not null
            ? _flat.FirstOrDefault(cell => ReferenceEquals(cell.Emoji, previous))
            : null;

        SetActive(target ?? _flat.FirstOrDefault(), scroll: false);
        _hover = null;
        UpdatePreview();

        if (category is { } wanted)
        {
            SetActiveCategory(wanted);
        }
        else if (_categories is not null && _rows.Count > 0)
        {
            SetActiveCategory(_rows[0].Category);
        }

        UpdateAutomation();
    }

    private void SetActive(ArcEmojiCell? cell, bool scroll)
    {
        if (ReferenceEquals(_active, cell))
        {
            if (cell is not null && scroll)
            {
                ScrollIntoView(cell);
            }

            return;
        }

        if (_active is not null)
        {
            _active.IsActive = false;
        }

        _active = cell;
        if (_active is not null)
        {
            _active.IsActive = true;
        }

        if (scroll && cell is not null)
        {
            ScrollIntoView(cell);
        }

        UpdatePreview();
    }

    private void ActivateFirst()
    {
        if (_flat.Count > 0)
        {
            SetActive(_flat[0], scroll: false);
        }
    }

    private bool PickActive()
    {
        var cell = _active ?? _flat.FirstOrDefault();
        if (cell is null)
        {
            return false;
        }

        SetActive(cell, scroll: false);
        Pick(cell);
        return true;
    }

    private void Pick(ArcEmojiCell cell)
    {
        var emoji = cell.Emoji;
        var glyph = ArcEmojiData.WithTone(emoji, SkinTone);

        _picking = true;
        try
        {
            SetCurrentValue(SelectedEmojiProperty, glyph);
        }
        finally
        {
            _picking = false;
        }

        PushRecent(emoji);
        UpdatePreview(emoji);
        var selection = new ArcEmojiSelection(glyph, emoji.Glyph, emoji.Name, SkinTone);
        RaiseSelected(selection);
    }

    private void MoveActive(Key key, KeyModifiers modifiers)
    {
        if (_flat.Count == 0)
        {
            return;
        }

        var current = _active ?? _flat[0];
        var rowIndex = RowIndexOf(current);
        if (rowIndex < 0)
        {
            rowIndex = 0;
        }

        var cellIndex = CellIndexInRow(_rows[rowIndex], current);

        int target;
        switch (key)
        {
            case Key.Left:
                target = FlatIndexOf(current) - 1;
                break;
            case Key.Right:
                target = FlatIndexOf(current) + 1;
                break;
            case Key.Up:
            {
                var row = Math.Max(0, rowIndex - 1);
                target = FlatIndexOf(_rows[row].Cells[Math.Min(cellIndex, _rows[row].Cells.Count - 1)]);
                break;
            }

            case Key.Down:
            {
                var row = Math.Min(_rows.Count - 1, rowIndex + 1);
                target = FlatIndexOf(_rows[row].Cells[Math.Min(cellIndex, _rows[row].Cells.Count - 1)]);
                break;
            }

            case Key.Home:
                target = modifiers.HasFlag(KeyModifiers.Control) ? 0 : FlatIndexOf(_rows[rowIndex].Cells[0]);
                break;
            case Key.End:
                target = modifiers.HasFlag(KeyModifiers.Control)
                    ? _flat.Count - 1
                    : FlatIndexOf(_rows[rowIndex].Cells[^1]);
                break;
            case Key.PageUp:
            {
                var row = Math.Max(0, rowIndex - PageRows);
                target = FlatIndexOf(_rows[row].Cells[Math.Min(cellIndex, _rows[row].Cells.Count - 1)]);
                break;
            }

            case Key.PageDown:
            {
                var row = Math.Min(_rows.Count - 1, rowIndex + PageRows);
                target = FlatIndexOf(_rows[row].Cells[Math.Min(cellIndex, _rows[row].Cells.Count - 1)]);
                break;
            }

            default:
                return;
        }

        target = Math.Clamp(target, 0, _flat.Count - 1);
        SetActive(_flat[target], scroll: true);
    }

    private int FlatIndexOf(ArcEmojiCell cell) => _flat.IndexOf(cell);

    private static int CellIndexInRow(ArcEmojiRow row, ArcEmojiCell cell)
    {
        for (var i = 0; i < row.Cells.Count; i++)
        {
            if (ReferenceEquals(row.Cells[i], cell))
            {
                return i;
            }
        }

        return 0;
    }

    private int RowIndexOf(ArcEmojiCell cell)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Cells.Contains(cell))
            {
                return i;
            }
        }

        return -1;
    }

    private void ScrollIntoView(ArcEmojiCell cell)
    {
        var row = RowIndexOf(cell);
        if (row >= 0)
        {
            _grid?.ScrollIntoView(row);
        }
    }

    private void OnGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (Columns == 0 && EffectiveColumns() != _autoColumns)
        {
            RebuildRows();
        }
    }

    private void OnCategorySelection(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _jumping)
        {
            return;
        }

        if ((_categories?.SelectedItem as ArcEmojiCategoryOption)?.Category is { } category)
        {
            JumpToCategory(category);
        }
    }

    private void OnToneSelection(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        if (_tones?.SelectedItem is ArcEmojiToneOption option)
        {
            SetCurrentValue(SkinToneProperty, option.Tone);
        }
    }

    private void SetActiveCategory(ArcEmojiCategory category)
    {
        if (_categories is null)
        {
            return;
        }

        var options = _categories.ItemsSource?.Cast<ArcEmojiCategoryOption>().ToArray() ?? Array.Empty<ArcEmojiCategoryOption>();
        var index = Array.FindIndex(options, option => option.Category == category);
        if (index < 0 || _categories.SelectedIndex == index)
        {
            return;
        }

        _syncing = true;
        try
        {
            _categories.SelectedIndex = index;
        }
        finally
        {
            _syncing = false;
        }

        UpdateAutomation();
    }

    private void JumpToCategory(ArcEmojiCategory category)
    {
        _scroll ??= _grid?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var row = -1;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Category == category)
            {
                row = i;
                break;
            }
        }

        if (row < 0 || _scroll is null)
        {
            return;
        }

        var target = row == 0 ? 0 : Math.Max(0, _rowTops[row] - 4);
        AnimateScroll(target);
    }

    private void AnimateScroll(double target)
    {
        _scrollTimer?.Stop();
        _scrollTimer = null;

        if (_scroll is null)
        {
            return;
        }

        _jumping = true;
        if (ArcThemeManager.ReducedMotion)
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, target);
            _jumping = false;
            return;
        }

        var start = _scroll.Offset.Y;
        var delta = target - start;
        const double duration = 240;
        var started = DateTime.UtcNow;

        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollTimer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            var progress = Math.Clamp(elapsed / duration, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            if (_scroll is not null)
            {
                _scroll.Offset = new Vector(_scroll.Offset.X, start + delta * eased);
            }

            if (progress >= 1)
            {
                _scrollTimer?.Stop();
                _scrollTimer = null;
                _jumping = false;
            }
        };
        _scrollTimer.Start();
    }

    private ArcEmojiCategory? CategoryAtOffset(double offset)
    {
        if (_rows.Count == 0)
        {
            return null;
        }

        var low = 0;
        var high = _rows.Count - 1;
        var found = 0;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (_rowTops[mid] <= offset + 2)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return _rows[found].Category;
    }

    private void OnGridScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer viewer)
        {
            _scroll = viewer;
        }

        if (_syncing || _jumping || _scroll is null)
        {
            return;
        }

        if (CategoryAtOffset(_scroll.Offset.Y) is { } category)
        {
            SetActiveCategory(category);
        }
    }

    private static ArcEmojiCell? CellFrom(object? source)
    {
        var control = source as Control;
        while (control is not null)
        {
            if (control.DataContext is ArcEmojiCell cell)
            {
                return cell;
            }

            control = control.GetVisualParent() as Control;
        }

        return null;
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Space)
        {
            PickActive();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            MoveActive(e.Key, e.KeyModifiers);
            e.Handled = true;
        }
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        var cell = CellFrom(e.Source);
        if (cell is null)
        {
            return;
        }

        _grid?.Focus();
        SetActive(cell, scroll: false);
        Pick(cell);
        e.Handled = true;
    }

    private void OnGridTextInput(object? sender, TextInputEventArgs e)
    {
        if (_grid is null || !_grid.IsKeyboardFocusWithin || string.IsNullOrEmpty(e.Text) || e.Text.Trim().Length == 0)
        {
            return;
        }

        // Typing while the grid has focus is routed to search, as Arc does, keeping the caret at the end.
        SetCurrentValue(SearchTextProperty, (SearchText ?? string.Empty) + e.Text);
        FocusSearch();
        e.Handled = true;
    }

    private void OnGridPointerMoved(object? sender, PointerEventArgs e)
    {
        var cell = CellFrom(e.Source);
        if (ReferenceEquals(cell, _hover))
        {
            return;
        }

        _hover = cell;
        UpdatePreview();
    }

    private void OnGridPointerExited(object? sender, PointerEventArgs e)
    {
        _hover = null;
        UpdatePreview();
    }

    private void UpdatePreview(ArcEmoji? emoji = null)
    {
        if (_previewGlyph is null || _previewName is null)
        {
            return;
        }

        emoji ??= _hover?.Emoji ?? _active?.Emoji;
        if (emoji is null)
        {
            _previewGlyph.Text = "🙂";
            _previewName.Text = "Pick an emoji";
            return;
        }

        _previewGlyph.Text = ArcEmojiData.WithTone(emoji, SkinTone);
        _previewName.Text = emoji.Name;
    }

    private void SeedRecent()
    {
        if (_recent.Count > 0 || DefaultRecent is not { Count: > 0 } seed)
        {
            return;
        }

        foreach (var glyph in seed)
        {
            var emoji = ArcEmojiData.Find(glyph);
            if (emoji is not null && _recent.All(existing => !ReferenceEquals(existing, emoji)))
            {
                _recent.Add(emoji);
            }

            if (_recent.Count >= RecentLimit)
            {
                break;
            }
        }

        RefreshRecent();
    }

    private void TrimRecent()
    {
        var limit = Math.Max(1, RecentLimit);
        while (_recent.Count > limit)
        {
            _recent.RemoveAt(_recent.Count - 1);
        }

        RefreshRecent();
    }

    private void PushRecent(ArcEmoji emoji)
    {
        _recent.RemoveAll(existing => existing.Glyph == emoji.Glyph);
        _recent.Insert(0, emoji);
        while (_recent.Count > Math.Max(1, RecentLimit))
        {
            _recent.RemoveAt(_recent.Count - 1);
        }

        RefreshRecent();
    }

    private void RefreshRecent()
    {
        if (_recentRow is null || _recentHost is null)
        {
            return;
        }

        _recentRow.Children.Clear();
        _recentHost.IsVisible = _recent.Count > 0;

        foreach (var emoji in _recent)
        {
            var theme = this.TryFindResource("ArcEmojiRecentTheme", out var resource) ? resource as ControlTheme : null;
            var glyph = new TextBlock
            {
                Text = ArcEmojiData.WithTone(emoji, SkinTone),
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var button = new Button
            {
                Theme = theme,
                Content = glyph,
                Margin = new Thickness(0, 0, 4, 0)
            };
            AutomationProperties.SetName(button, $"Recently used, {emoji.Name}");
            ToolTip.SetTip(button, emoji.Name);
            button.Click += (_, args) =>
            {
                args.Handled = true;
                Pick(emoji, allowRecentReorder: true);
            };

            _recentRow.Children.Add(button);
        }
    }

    private void Pick(ArcEmoji emoji, bool allowRecentReorder)
    {
        var glyph = ArcEmojiData.WithTone(emoji, SkinTone);
        _picking = true;
        try
        {
            SetCurrentValue(SelectedEmojiProperty, glyph);
        }
        finally
        {
            _picking = false;
        }

        if (allowRecentReorder)
        {
            PushRecent(emoji);
        }

        UpdatePreview(emoji);
        var selection = new ArcEmojiSelection(glyph, emoji.Glyph, emoji.Name, SkinTone);
        RaiseSelected(selection);
    }

    private void UpdateAutomation()
    {
        var label = string.IsNullOrWhiteSpace(Label) ? "Emoji picker" : Label;
        AutomationProperties.SetName(this, label);
        UpdateStateAutomation();
    }

    private void UpdateStateAutomation()
    {
        var name = string.IsNullOrWhiteSpace(Label) ? "Emoji picker" : Label;
        var category = (_categories?.SelectedItem as ArcEmojiCategoryOption)?.Label;
        if (category is not null)
        {
            name += $", {category}";
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            name += $", search {SearchText}";
        }

        AutomationProperties.SetName(this, name);
    }
}






