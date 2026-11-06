using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
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
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Controls;

/// <summary>Horizontal alignment of an <see cref="ArcTreeTableColumn"/>'s cells.</summary>
public enum ArcTreeTableAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// One column of an <see cref="ArcTreeTable"/>. <see cref="Key"/> reads the matching entry in each
/// row's <see cref="ArcTreeTableRow.Values"/> dictionary. The first visible column hosts the tree:
/// its cells get indentation, indent guides, the expand chevron and the row label.
/// </summary>
public sealed class ArcTreeTableColumn
{
    /// <summary>Key into <see cref="ArcTreeTableRow.Values"/>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Header { get; set; } = string.Empty;

    /// <summary>Fixed width in px; zero makes the column absorb the remaining width (star).</summary>
    public double Width { get; set; }

    /// <summary>Narrowest the column can shrink to, in px.</summary>
    public double MinWidth { get; set; } = 72;

    public ArcTreeTableAlign Align { get; set; } = ArcTreeTableAlign.Left;

    /// <summary>Header click cycles ascending, descending, off. Siblings sort inside their parent.</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>Optional display formatter. Receives the raw cell value; sorting still uses the raw value.</summary>
    public Func<object?, string>? Format { get; set; }
}

/// <summary>
/// One node of an <see cref="ArcTreeTable"/>. Ids must be unique and stable across rebuilds so
/// expansion, selection and focus keep their place. Children may be supplied eagerly; for server
/// data instead set <see cref="HasChildren"/> and leave <see cref="Children"/> empty, then give the
/// table a <see cref="ArcTreeTable.LoadChildren"/> callback.
/// </summary>
public sealed class ArcTreeTableRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The row's cells, keyed by <see cref="ArcTreeTableColumn.Key"/>.</summary>
    public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

    public object? this[string key]
    {
        get => Values.TryGetValue(key, out var value) ? value : null;
        set => Values[key] = value;
    }

    /// <summary>Preloaded children; sorted within this row, never across levels.</summary>
    public List<ArcTreeTableRow> Children { get; set; } = new();

    /// <summary>
    /// True when children exist on the server but are not loaded yet. The table shows an expand
    /// chevron and calls <see cref="ArcTreeTable.LoadChildren"/> the first time the row opens.
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>Accessible row name; falls back to the first column's cell text, then the id.</summary>
    public string? Label { get; set; }
}

/// <summary>A sort for an <see cref="ArcTreeTable"/>; a null sort in an event means unsorted.</summary>
public sealed class ArcTreeTableSort
{
    public string Key { get; set; } = string.Empty;

    public bool Descending { get; set; }
}

/// <summary>
/// A table whose rows nest: indented rows with expand chevrons, tri-state selection that cascades
/// both ways, hierarchy-preserving sorting within each level, lazy-loaded children, a pinned header,
/// an empty state and a footer summary slot.
/// </summary>
/// <remarks>
/// Independent Avalonia implementation of Arc's public tree-table documentation
/// (uiarc.dev/components/tree-table); no Pro source was used. The rows model is
/// <see cref="ArcTreeTableRow"/> with a keyed <see cref="ArcTreeTableRow.Values"/> dictionary.
/// Deliberately simplified from the React component: there is no DOM grid-track/ResizeObserver
/// responsive column hiding (<c>hideBelow</c>), no height-spring layout animation or sort position
/// animation (expand, collapse and sort rebuild instantly), and no external
/// <c>collectExpandableIds</c> helper. The visible tree is flattened into a row list and only the
/// rows intersecting the scroll viewport (plus an overscan margin) are instantiated, so a 10,000 node
/// tree keeps a viewport-sized control tree; the scrollbar still reflects every row. The pinned header
/// works whenever <see cref="MaxBodyHeight"/> constrains the body. Sorting reorders siblings only, with
/// blank values kept at the bottom in either direction, and selection is normalised so a parent is
/// checked exactly when every known child is checked and mixed when only some descendants are. Keyboard
/// support follows the public treegrid table (arrows, Home/End, PageUp/PageDown, <c>*</c>, Space,
/// Enter, Ctrl/Cmd+A) plus label typeahead.
/// </remarks>
[TemplatePart("PART_Toolbar", typeof(Panel))]
[TemplatePart("PART_HeaderHost", typeof(Border))]
[TemplatePart("PART_Scroll", typeof(ScrollViewer))]
[TemplatePart("PART_Body", typeof(Panel))]
[TemplatePart("PART_FooterHost", typeof(Border))]
[TemplatePart("PART_Live", typeof(TextBlock))]
public class ArcTreeTable : TemplatedControl
{
    private const double RowHeight = 44;
    private const double RowContentHeight = 40;
    private const double Indent = 20;
    private const double CheckColumnWidth = 44;
    private const double ChevronSlot = 22;

    public static readonly StyledProperty<IList<ArcTreeTableColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IList<ArcTreeTableColumn>?>(nameof(Columns));

    public static readonly StyledProperty<IList<ArcTreeTableRow>?> RowsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IList<ArcTreeTableRow>?>(nameof(Rows));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcTreeTable, string>(nameof(Label), "Tree table");

    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<ArcTreeTable, bool>(nameof(Selectable));

    public static readonly StyledProperty<IEnumerable<string>?> DefaultExpandedIdsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IEnumerable<string>?>(nameof(DefaultExpandedIds));

    public static readonly StyledProperty<IEnumerable<string>?> ExpandedIdsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IEnumerable<string>?>(nameof(ExpandedIds));

    public static readonly StyledProperty<IEnumerable<string>?> DefaultSelectedIdsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IEnumerable<string>?>(nameof(DefaultSelectedIds));

    public static readonly StyledProperty<IEnumerable<string>?> SelectedIdsProperty =
        AvaloniaProperty.Register<ArcTreeTable, IEnumerable<string>?>(nameof(SelectedIds));

    public static readonly StyledProperty<ArcTreeTableSort?> DefaultSortProperty =
        AvaloniaProperty.Register<ArcTreeTable, ArcTreeTableSort?>(nameof(DefaultSort));

    public static readonly StyledProperty<Func<ArcTreeTableRow, Task<IReadOnlyList<ArcTreeTableRow>>>?> LoadChildrenProperty =
        AvaloniaProperty.Register<ArcTreeTable, Func<ArcTreeTableRow, Task<IReadOnlyList<ArcTreeTableRow>>>?>(
            nameof(LoadChildren));

    public static readonly StyledProperty<Func<ArcTreeTableRow, string>?> RowLabelProperty =
        AvaloniaProperty.Register<ArcTreeTable, Func<ArcTreeTableRow, string>?>(nameof(RowLabel));

    public static readonly StyledProperty<double> MaxBodyHeightProperty =
        AvaloniaProperty.Register<ArcTreeTable, double>(nameof(MaxBodyHeight), 420);

    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<ArcTreeTable, string>(nameof(EmptyMessage), "Nothing to show");

    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<ArcTreeTable, string>(
            nameof(EmptyDescription), "Add a row, or clear the filters to see the hierarchy again.");

    public static readonly StyledProperty<bool> ShowSummaryProperty =
        AvaloniaProperty.Register<ArcTreeTable, bool>(nameof(ShowSummary), true);

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ArcTreeTable, object?>(nameof(Footer));

    private enum CheckState
    {
        Unchecked,
        Checked,
        Mixed
    }

    private enum DisplayKind
    {
        Row,
        Loading,
        Error
    }

    private sealed record DisplayRow(ArcTreeTableRow Row, int Depth, DisplayKind Kind, int Position = 1, int SetSize = 1);

    private sealed record VisibleRow(ArcTreeTableRow Row, int Depth);

    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ArcTreeTableRow>> _loadedChildren = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loading = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadFailed = new(StringComparer.Ordinal);
    private readonly List<DisplayRow> _display = new();
    private readonly List<VisibleRow> _visible = new();

    // Windowed realisation: _display is the flattened row list, and only the rows intersecting the
    // viewport (plus overscan) exist as controls. The maps keep focus/frame bookkeeping aligned when
    // loading/error placeholder rows are interleaved with data rows.
    private readonly Dictionary<int, Border> _framesByIndex = new();
    private readonly Dictionary<int, Control> _realizedRows = new();
    private readonly List<int> _displayVisibleIndex = new();
    private readonly List<int> _visibleDisplayIndex = new();
    private readonly ArcRowWindowCache _rowCache = new(80);
    private readonly List<ArcTreeTableRow> _loadQueue = new();
    private readonly Dictionary<string, ArcTreeTableRow> _known = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcTreeTableRow?> _parentOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RotateTransform> _chevrons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcFlatButton> _headerButtons = new(StringComparer.Ordinal);
    private readonly ArcTween _chevronTween = new();

    private Panel? _toolbar;
    private Border? _headerHost;
    private ScrollViewer? _scroll;
    private Panel? _bodyHost;
    private ArcVirtualRowsPanel? _rowsPanel;
    private int _windowFirst;
    private int _windowLast = -1;
    private bool _updatingWindow;
    private Border? _footerHost;
    private TextBlock? _live;
    private TextBlock? _countText;
    private ArcFlatButton? _selectAllButton;
    private ArcFlatButton? _expandAll;
    private ArcFlatButton? _collapseAll;
    private string? _sortKey;
    private bool _sortDescending;
    private int _focusIndex;
    private bool _keyboardFocus;
    private bool _syncingSelection;
    private bool _syncingExpansion;
    private int _loadEpoch;
    private double _headerGutter;
    private string _typeBuffer = string.Empty;
    private DateTime _typeBufferAt;

    static ArcTreeTable()
    {
        ColumnsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildAll());
        RowsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.OnRowsChanged());
        LabelProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) =>
        {
            table.BuildToolbar();
            table.UpdateAutomation();
        });
        SelectableProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildAll());
        DefaultExpandedIdsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.ResetExpansion());
        ExpandedIdsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.SyncExpansion());
        DefaultSelectedIdsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.ResetSelection());
        SelectedIdsProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.SyncSelection());
        DefaultSortProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.ApplyDefaultSort());
        LoadChildrenProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildAll());
        RowLabelProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildContent());
        MaxBodyHeightProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.ApplyBodyHeight());
        EmptyMessageProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildAll());
        EmptyDescriptionProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildAll());
        ShowSummaryProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildFooter());
        FooterProperty.Changed.AddClassHandler<ArcTreeTable>((table, _) => table.RebuildFooter());
    }

    public ArcTreeTable()
    {
        Focusable = true;
    }

    /// <summary>Column definitions in display order. The first column holds the tree.</summary>
    public IList<ArcTreeTableColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Top-level rows. Child rows come from <see cref="ArcTreeTableRow.Children"/> or the loader.</summary>
    public IList<ArcTreeTableRow>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Accessible name for the table and its toolbar.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Adds the tri-state checkbox column and Space/Ctrl+A selection.</summary>
    public bool Selectable
    {
        get => GetValue(SelectableProperty);
        set => SetValue(SelectableProperty, value);
    }

    /// <summary>Rows open at first when expansion is uncontrolled.</summary>
    public IEnumerable<string>? DefaultExpandedIds
    {
        get => GetValue(DefaultExpandedIdsProperty);
        set => SetValue(DefaultExpandedIdsProperty, value);
    }

    /// <summary>Controlled open rows; when set, overrides the internal expansion set.</summary>
    public IEnumerable<string>? ExpandedIds
    {
        get => GetValue(ExpandedIdsProperty);
        set => SetValue(ExpandedIdsProperty, value);
    }

    /// <summary>Rows checked at first; a checked parent checks its whole known subtree.</summary>
    public IEnumerable<string>? DefaultSelectedIds
    {
        get => GetValue(DefaultSelectedIdsProperty);
        set => SetValue(DefaultSelectedIdsProperty, value);
    }

    /// <summary>Checked row ids, kept current as people select. Parents are normalised from their children.</summary>
    public IEnumerable<string>? SelectedIds
    {
        get => GetValue(SelectedIdsProperty);
        set => SetValue(SelectedIdsProperty, value);
    }

    /// <summary>Starting sort, or null for unsorted. Siblings sort inside their parent.</summary>
    public ArcTreeTableSort? DefaultSort
    {
        get => GetValue(DefaultSortProperty);
        set => SetValue(DefaultSortProperty, value);
    }

    /// <summary>
    /// Loads the children of a row that has <see cref="ArcTreeTableRow.HasChildren"/> but no loaded
    /// children. Called once per row on first expand; a rejection shows a retry row.
    /// </summary>
    public Func<ArcTreeTableRow, Task<IReadOnlyList<ArcTreeTableRow>>>? LoadChildren
    {
        get => GetValue(LoadChildrenProperty);
        set => SetValue(LoadChildrenProperty, value);
    }

    /// <summary>Names a row for its checkbox and automation; falls back to the row label and first cell.</summary>
    public Func<ArcTreeTableRow, string>? RowLabel
    {
        get => GetValue(RowLabelProperty);
        set => SetValue(RowLabelProperty, value);
    }

    /// <summary>Tallest the scrolling body grows before it scrolls, in px. Zero leaves it uncapped.</summary>
    public double MaxBodyHeight
    {
        get => GetValue(MaxBodyHeightProperty);
        set => SetValue(MaxBodyHeightProperty, value);
    }

    public string EmptyMessage
    {
        get => GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public string EmptyDescription
    {
        get => GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    /// <summary>Shows the row and selection count plus the current sort in the footer.</summary>
    public bool ShowSummary
    {
        get => GetValue(ShowSummaryProperty);
        set => SetValue(ShowSummaryProperty, value);
    }

    /// <summary>Optional footer content; takes the place of the default summary line when set.</summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public event EventHandler<ArcTreeTableSort?>? SortChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SortChanged"/>.</summary>
    public static readonly RoutedEvent<ArcTreeTableSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<ArcTreeTable, ArcTreeTableSortChangedEventArgs>("SortChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcTreeTableSortChangedEventArgs"/> version of <see cref="SortChanged"/>.</summary>
    public event EventHandler<ArcTreeTableSortChangedEventArgs>? SortChangedRouted
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    private void RaiseSortChanged(ArcTreeTableSort? sort)
    {
        SortChanged?.Invoke(this, sort);
        RaiseEvent(new ArcTreeTableSortChangedEventArgs(SortChangedEvent) { Sort = sort });
    }

    public event EventHandler<IReadOnlyList<string>>? SelectionChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SelectionChanged"/>.</summary>
    public static readonly RoutedEvent<ArcTreeTableSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ArcTreeTable, ArcTreeTableSelectionChangedEventArgs>("SelectionChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcTreeTableSelectionChangedEventArgs"/> version of <see cref="SelectionChanged"/>.</summary>
    public event EventHandler<ArcTreeTableSelectionChangedEventArgs>? SelectionChangedRouted
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    private void RaiseSelectionChanged(IReadOnlyList<string> selectedKeys)
    {
        SelectionChanged?.Invoke(this, selectedKeys);
        RaiseEvent(new ArcTreeTableSelectionChangedEventArgs(SelectionChangedEvent) { SelectedKeys = selectedKeys });
    }

    public event EventHandler<IReadOnlyList<string>>? ExpandedChanged;

    /// <summary>Bubbling routed counterpart of <see cref="ExpandedChanged"/>.</summary>
    public static readonly RoutedEvent<ArcTreeTableExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ArcTreeTable, ArcTreeTableExpandedChangedEventArgs>("ExpandedChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcTreeTableExpandedChangedEventArgs"/> version of <see cref="ExpandedChanged"/>.</summary>
    public event EventHandler<ArcTreeTableExpandedChangedEventArgs>? ExpandedChangedRouted
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private void RaiseExpandedChanged(IReadOnlyList<string> expandedKeys)
    {
        ExpandedChanged?.Invoke(this, expandedKeys);
        RaiseEvent(new ArcTreeTableExpandedChangedEventArgs(ExpandedChangedEvent) { ExpandedKeys = expandedKeys });
    }

    /// <summary>Raised when a row without children is clicked or gets Enter.</summary>
    public event EventHandler<ArcTreeTableRow>? RowActivated;

    /// <summary>Bubbling routed counterpart of <see cref="RowActivated"/>.</summary>
    public static readonly RoutedEvent<ArcTreeTableRowActivatedEventArgs> RowActivatedEvent =
        RoutedEvent.Register<ArcTreeTable, ArcTreeTableRowActivatedEventArgs>("RowActivated", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcTreeTableRowActivatedEventArgs"/> version of <see cref="RowActivated"/>.</summary>
    public event EventHandler<ArcTreeTableRowActivatedEventArgs>? RowActivatedRouted
    {
        add => AddHandler(RowActivatedEvent, value);
        remove => RemoveHandler(RowActivatedEvent, value);
    }

    private void RaiseRowActivated(ArcTreeTableRow row)
    {
        RowActivated?.Invoke(this, row);
        RaiseEvent(new ArcTreeTableRowActivatedEventArgs(RowActivatedEvent) { Row = row });
    }

    /// <summary>Re-reads rows, columns and child lists after they were changed in place.</summary>
    public void Refresh()
    {
        RebuildIndex();
        NormalizeSelection();
        RebuildAll();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _toolbar = e.NameScope.Find<Panel>("PART_Toolbar");
        _headerHost = e.NameScope.Find<Border>("PART_HeaderHost");
        _scroll = e.NameScope.Find<ScrollViewer>("PART_Scroll");
        _bodyHost = e.NameScope.Find<Panel>("PART_Body");
        _footerHost = e.NameScope.Find<Border>("PART_FooterHost");
        _live = e.NameScope.Find<TextBlock>("PART_Live");
        if (_live is not null)
        {
            AutomationProperties.SetLiveSetting(_live, AutomationLiveSetting.Polite);
        }

        if (_scroll is not null)
        {
            _scroll.LayoutUpdated += (_, _) =>
            {
                SyncHeaderGutter();
                UpdateBodyWindow();
            };
            _scroll.ScrollChanged += (_, _) =>
            {
                SyncHeaderGutter();
                UpdateBodyWindow();
            };
            _scroll.SizeChanged += (_, _) => UpdateBodyWindow(force: true);
        }

        ApplyBodyHeight();
        ResetState();
        RebuildAll();
    }

    private IReadOnlyList<ArcTreeTableColumn> VisibleColumns() =>
        (Columns ?? Array.Empty<ArcTreeTableColumn>()).ToList();

    private List<ArcTreeTableRow> Roots() => Rows?.ToList() ?? new List<ArcTreeTableRow>();

    private void ResetState()
    {
        RebuildIndex();
        _expanded.Clear();
        foreach (var id in DefaultExpandedIds ?? Enumerable.Empty<string>())
        {
            _expanded.Add(id);
        }

        if (ExpandedIds is not null)
        {
            _expanded.Clear();
            foreach (var id in ExpandedIds)
            {
                _expanded.Add(id);
            }
        }

        _selected.Clear();
        foreach (var id in DefaultSelectedIds ?? Enumerable.Empty<string>())
        {
            if (_known.TryGetValue(id, out var row))
            {
                AddSubtree(row, true);
            }
        }

        NormalizeSelection();
        _sortKey = DefaultSort?.Key;
        _sortDescending = DefaultSort?.Descending ?? false;
        _focusIndex = 0;
        _keyboardFocus = false;
        _typeBuffer = string.Empty;
    }

    private void OnRowsChanged()
    {
        _loadEpoch++;
        _loadedChildren.Clear();
        _loading.Clear();
        _loadFailed.Clear();
        RebuildIndex();
        var live = new HashSet<string>(_known.Keys, StringComparer.Ordinal);
        _expanded.RemoveWhere(id => !live.Contains(id));
        _selected.RemoveWhere(id => !live.Contains(id));
        _focusIndex = 0;
        NormalizeSelection();
        RebuildAll();
    }

    private void ResetExpansion()
    {
        _expanded.Clear();
        foreach (var id in DefaultExpandedIds ?? Enumerable.Empty<string>())
        {
            _expanded.Add(id);
        }

        if (ExpandedIds is not null)
        {
            _expanded.Clear();
            foreach (var id in ExpandedIds)
            {
                _expanded.Add(id);
            }
        }

        RebuildContent();
    }

    private void SyncExpansion()
    {
        if (_syncingExpansion || ExpandedIds is null)
        {
            return;
        }

        _expanded.Clear();
        foreach (var id in ExpandedIds)
        {
            _expanded.Add(id);
        }

        RebuildContent();
    }

    private void ResetSelection()
    {
        RebuildIndex();
        _selected.Clear();
        foreach (var id in DefaultSelectedIds ?? Enumerable.Empty<string>())
        {
            if (_known.TryGetValue(id, out var row))
            {
                AddSubtree(row, true);
            }
        }

        NormalizeSelection();
        RebuildContent();
    }

    private void SyncSelection()
    {
        if (_syncingSelection)
        {
            return;
        }

        RebuildIndex();
        _selected.Clear();
        foreach (var id in SelectedIds ?? Enumerable.Empty<string>())
        {
            if (_known.TryGetValue(id, out var row))
            {
                AddSubtree(row, true);
            }
        }

        NormalizeSelection();
        RebuildContent();
    }

    private void ApplyDefaultSort()
    {
        _sortKey = DefaultSort?.Key;
        _sortDescending = DefaultSort?.Descending ?? false;
        RebuildContent();
    }

    private void ApplyBodyHeight()
    {
        if (_scroll is null)
        {
            return;
        }

        _scroll.MaxHeight = MaxBodyHeight > 0 ? MaxBodyHeight : double.PositiveInfinity;
        Dispatcher.UIThread.Post(SyncHeaderGutter, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Keeps the pinned header's columns aligned with the scrolling body by reserving exactly the
    /// width the vertical scrollbar takes from the viewport.
    /// </summary>
    private void SyncHeaderGutter()
    {
        if (_headerHost is null || _scroll is null)
        {
            return;
        }

        var gutter = _scroll.Bounds.Width > 0 && _scroll.Viewport.Width > 0
            ? Math.Max(0, _scroll.Bounds.Width - _scroll.Viewport.Width)
            : 0;
        if (Math.Abs(gutter - _headerGutter) < 0.5)
        {
            return;
        }

        _headerGutter = gutter;
        _headerHost.Padding = new Thickness(0, 0, gutter, 0);
    }

    private void RebuildAll()
    {
        if (_toolbar is null)
        {
            return;
        }

        BuildToolbar();
        RebuildContent();
    }

    private void RebuildContent()
    {
        if (_headerHost is null || _bodyHost is null || _footerHost is null)
        {
            return;
        }

        var focusedId = _focusIndex >= 0 && _focusIndex < _visible.Count ? _visible[_focusIndex].Row.Id : null;
        Flatten();
        RestoreFocus(focusedId);
        BuildHeader();
        BuildBody();
        RebuildFooter();
        UpdateCountText();
        UpdateAutomation();
        StartQueuedLoads();
        Dispatcher.UIThread.Post(() =>
        {
            SyncHeaderGutter();
            UpdateBodyWindow(force: true);
        }, DispatcherPriority.Loaded);
    }

    private void RebuildIndex()
    {
        _known.Clear();
        _parentOf.Clear();
        foreach (var root in Roots())
        {
            IndexRow(root, null);
        }
    }

    private void IndexRow(ArcTreeTableRow row, ArcTreeTableRow? parent)
    {
        if (_known.ContainsKey(row.Id))
        {
            return;
        }

        _known[row.Id] = row;
        _parentOf[row.Id] = parent;
        var children = ChildrenOf(row);
        foreach (var child in children)
        {
            IndexRow(child, row);
        }
    }

    private IReadOnlyList<ArcTreeTableRow> ChildrenOf(ArcTreeTableRow row) =>
        _loadedChildren.TryGetValue(row.Id, out var loaded) ? loaded : row.Children;

    private ArcTreeTableRow? ParentOf(ArcTreeTableRow row) =>
        _parentOf.TryGetValue(row.Id, out var parent) ? parent : null;

    private bool NeedsLoad(ArcTreeTableRow row) =>
        row.HasChildren && ChildrenOf(row).Count == 0 && !_loadedChildren.ContainsKey(row.Id);

    private bool CanExpand(ArcTreeTableRow row) =>
        ChildrenOf(row).Count > 0 || (NeedsLoad(row) && LoadChildren is not null);

    private void Flatten()
    {
        if (_sortKey is not null && VisibleColumns().All(column => column.Key != _sortKey))
        {
            _sortKey = null;
            _sortDescending = false;
        }

        _display.Clear();
        _visible.Clear();
        _displayVisibleIndex.Clear();
        _visibleDisplayIndex.Clear();
        _framesByIndex.Clear();
        _realizedRows.Clear();
        _rowCache.Clear();
        _chevrons.Clear();
        _loadQueue.Clear();
        RebuildIndex();
        var roots = SortSiblings(Roots());
        for (var index = 0; index < roots.Count; index++)
        {
            AddDisplayRow(roots[index], 0, index + 1, roots.Count);
        }

        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, _visible.Count - 1));
    }

    private void AddDisplayRow(ArcTreeTableRow row, int depth, int position, int setSize)
    {
        var visibleIndex = _visible.Count;
        _visible.Add(new VisibleRow(row, depth));
        _visibleDisplayIndex.Add(_display.Count);
        _display.Add(new DisplayRow(row, depth, DisplayKind.Row, position, setSize));
        _displayVisibleIndex.Add(visibleIndex);

        if (!_expanded.Contains(row.Id))
        {
            return;
        }

        var children = ChildrenOf(row);
        if (children.Count > 0)
        {
            var sorted = SortSiblings(children);
            for (var index = 0; index < sorted.Count; index++)
            {
                AddDisplayRow(sorted[index], depth + 1, index + 1, sorted.Count);
            }

            return;
        }

        if (!NeedsLoad(row) || LoadChildren is null)
        {
            return;
        }

        if (_loadFailed.Contains(row.Id))
        {
            _display.Add(new DisplayRow(row, depth + 1, DisplayKind.Error));
            _displayVisibleIndex.Add(Math.Max(0, _visible.Count - 1));
            return;
        }

        if (!_loading.Contains(row.Id))
        {
            _loading.Add(row.Id);
            _loadQueue.Add(row);
        }

        _display.Add(new DisplayRow(row, depth + 1, DisplayKind.Loading));
        _displayVisibleIndex.Add(Math.Max(0, _visible.Count - 1));
    }

    private List<ArcTreeTableRow> SortSiblings(IReadOnlyList<ArcTreeTableRow> rows)
    {
        var list = rows.ToList();
        if (_sortKey is null || list.Count < 2)
        {
            return list;
        }

        var column = VisibleColumns().FirstOrDefault(item => item.Key == _sortKey);
        if (column is null)
        {
            return list;
        }

        var indexed = list.Select((row, index) => (Row: row, Index: index)).ToList();
        indexed.Sort((left, right) =>
        {
            var leftValue = Value(left.Row, column);
            var rightValue = Value(right.Row, column);
            var leftBlank = IsBlank(leftValue);
            var rightBlank = IsBlank(rightValue);
            int result;
            if (leftBlank || rightBlank)
            {
                // Blank values stay at the bottom in both directions, like a spreadsheet.
                result = leftBlank == rightBlank ? 0 : leftBlank ? 1 : -1;
            }
            else
            {
                result = CompareValues(leftValue, rightValue);
                if (_sortDescending)
                {
                    result = -result;
                }
            }

            return result != 0 ? result : left.Index.CompareTo(right.Index);
        });
        return indexed.Select(item => item.Row).ToList();
    }

    private static int CompareValues(object? left, object? right)
    {
        var leftBlank = IsBlank(left);
        var rightBlank = IsBlank(right);
        if (leftBlank || rightBlank)
        {
            return leftBlank == rightBlank ? 0 : leftBlank ? 1 : -1;
        }

        if (left is IComparable comparable && left.GetType() == right!.GetType())
        {
            return comparable.CompareTo(right);
        }

        if (TryNumber(left, out var leftNumber) && TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNumber(object? value, out double number)
    {
        number = 0;
        if (value is null || value is string)
        {
            return false;
        }

        try
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBlank(object? value) =>
        value is null || (value is string text && string.IsNullOrWhiteSpace(text));

    private static object? Value(ArcTreeTableRow row, ArcTreeTableColumn column) =>
        row.Values.TryGetValue(column.Key, out var value) ? value : null;

    private static string CellText(ArcTreeTableRow row, ArcTreeTableColumn column)
    {
        var value = Value(row, column);
        if (column.Format is not null)
        {
            return column.Format(value);
        }

        if (value is null)
        {
            return "—";
        }

        if (value is double number)
        {
            return number.ToString("#,##0.##", CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private string LabelOf(ArcTreeTableRow row)
    {
        var custom = RowLabel?.Invoke(row);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return custom;
        }

        if (!string.IsNullOrWhiteSpace(row.Label))
        {
            return row.Label!;
        }

        var first = VisibleColumns().FirstOrDefault();
        var text = first is null ? null : CellText(row, first);
        return string.IsNullOrWhiteSpace(text) ? row.Id : text;
    }

    private void RestoreFocus(string? id)
    {
        if (id is not null && _known.TryGetValue(id, out var row))
        {
            var current = row;
            while (current is not null)
            {
                var index = _visible.FindIndex(item => ReferenceEquals(item.Row, current));
                if (index >= 0)
                {
                    _focusIndex = index;
                    return;
                }

                current = ParentOf(current);
            }
        }

        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, _visible.Count - 1));
    }

    // ---- Toolbar -----------------------------------------------------------

    private void BuildToolbar()
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Children.Clear();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = FeedbackUi.Text(Label, "arc-label");
        label.FontSize = ArcResources.Double(ArcTokens.TextSm);
        label.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
        left.Children.Add(label);
        _countText = FeedbackUi.Caption(string.Empty);
        _countText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        left.Children.Add(_countText);
        grid.Children.Add(left);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        _expandAll = ToolbarAction("Expand all", "Expand every row whose children are already loaded");
        _expandAll.Click += (_, _) => ExpandAllKnown();
        _collapseAll = ToolbarAction("Collapse all", "Collapse every open row");
        _collapseAll.Click += (_, _) => CollapseAll();
        actions.Children.Add(_expandAll);
        actions.Children.Add(_collapseAll);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        _toolbar.Children.Add(grid);
        UpdateCountText();
    }

    private static ArcFlatButton ToolbarAction(string text, string help)
    {
        var button = new ArcFlatButton
        {
            Text = text,
            Quiet = true,
            Padding = new Thickness(8, 4),
            FontSize = ArcResources.Double(ArcTokens.TextXs),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Paint(TemplatedControl.ForegroundProperty, ArcTokens.TextSecondary);
        AutomationProperties.SetName(button, text);
        AutomationProperties.SetHelpText(button, help);
        return button;
    }

    private void UpdateCountText()
    {
        if (_countText is null)
        {
            return;
        }

        var rows = $"{_visible.Count} {(_visible.Count == 1 ? "row" : "rows")}";
        var selected = Selectable ? _selected.Count : 0;
        _countText.Text = selected > 0 ? $"{rows} · {selected} selected" : rows;
        if (_expandAll is not null)
        {
            _expandAll.IsEnabled = _known.Values.Any(row =>
                ChildrenOf(row).Count > 0 && !_expanded.Contains(row.Id));
        }

        if (_collapseAll is not null)
        {
            _collapseAll.IsEnabled = _expanded.Count > 0;
        }
    }

    private void ExpandAllKnown()
    {
        var changed = false;
        foreach (var row in _known.Values.Where(row =>
                     ChildrenOf(row).Count > 0 && !_expanded.Contains(row.Id)).ToList())
        {
            _expanded.Add(row.Id);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RebuildContent();
        PublishExpansion();
        Announce("Expanded every row with loaded children.");
    }

    private void CollapseAll()
    {
        if (_expanded.Count == 0)
        {
            return;
        }

        _expanded.Clear();
        RebuildContent();
        PublishExpansion();
        Announce("Collapsed every row.");
    }

    // ---- Header ------------------------------------------------------------

    private void BuildHeader()
    {
        if (_headerHost is null)
        {
            return;
        }

        _headerHost.Child = null;
        _headerButtons.Clear();
        _selectAllButton = null;
        var columns = VisibleColumns();
        _headerHost.IsVisible = columns.Count > 0;
        if (columns.Count == 0)
        {
            return;
        }

        var grid = CreateGrid(columns);
        var offset = Selectable ? 1 : 0;

        if (Selectable)
        {
            var host = new Border
            {
                Child = BuildSelectAllCheck(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(host, 0);
            grid.Children.Add(host);
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var sorted = _sortKey == column.Key;
            var cell = new Grid { MinHeight = 42 };
            var alignment = AlignToAlignment(column.Align);
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = alignment
            };
            var label = FeedbackUi.Text(column.Header, "arc-label");
            label.FontSize = ArcResources.Double(ArcTokens.TextXs);
            label.FontWeight = FontWeight.Medium;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.Paint(
                TextBlock.ForegroundProperty,
                sorted ? ArcTokens.Foreground : ArcTokens.TextSecondary);
            content.Children.Add(label);

            if (column.Sortable)
            {
                var icon = sorted
                    ? _sortDescending ? "arrow-down" : "arrow-up"
                    : "arrow-up-down";
                var indicator = FeedbackUi.Icon(
                    icon,
                    12,
                    sorted ? ArcTokens.AccentStrong : ArcTokens.TextMuted);
                indicator.Opacity = sorted ? 1 : 0.4;
                content.Children.Add(indicator);

                var button = new ArcFlatButton
                {
                    Content = content,
                    Padding = new Thickness(12, 0),
                    MinHeight = 34,
                    Margin = new Thickness(2, 3, 2, 3),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = alignment,
                    Focusable = true,
                    IsTabStop = true,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                var captured = column;
                var direction = sorted
                    ? _sortDescending ? ", currently descending" : ", currently ascending"
                    : string.Empty;
                AutomationProperties.SetName(button, $"Sort by {column.Header}{direction}");
                button.Click += (_, _) => ToggleSort(captured);
                _headerButtons[column.Key] = button;
                Grid.SetColumn(button, 0);
                cell.Children.Add(button);
            }
            else
            {
                var header = new Border
                {
                    Child = content,
                    Padding = new Thickness(12, 0),
                    MinHeight = 34,
                    Margin = new Thickness(2, 3, 2, 3),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                AutomationProperties.SetName(header, column.Header);
                Grid.SetColumn(header, 0);
                cell.Children.Add(header);
            }

            if (index < columns.Count - 1)
            {
                var divider = new Border
                {
                    Width = 1,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    IsHitTestVisible = false
                };
                divider.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
                cell.Children.Add(divider);
            }

            Grid.SetColumn(cell, offset + index);
            grid.Children.Add(cell);
        }

        _headerHost.Child = grid;
    }

    private ArcFlatButton BuildSelectAllCheck()
    {
        var roots = Roots();
        var all = roots.Count > 0 && roots.All(row => _selected.Contains(row.Id));
        var any = !all && roots.Any(row => _selected.Contains(row.Id) || AnySelectedBelow(row));
        var state = all ? CheckState.Checked : any ? CheckState.Mixed : CheckState.Unchecked;
        var button = new ArcFlatButton
        {
            Content = CheckVisual(state),
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            Focusable = true,
            IsTabStop = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            IsEnabled = roots.Count > 0
        };
        AutomationProperties.SetName(button, all ? "Clear selection" : "Select all rows");
        AutomationProperties.SetHelpText(button, "Checks every row, including collapsed children");
        button.Click += (_, _) => ToggleSelectAll();
        _selectAllButton = button;
        return button;
    }

    private static Border CheckVisual(CheckState state)
    {
        var box = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (state == CheckState.Unchecked)
        {
            box.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            box.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
            return box;
        }

        box.Paint(Border.BackgroundProperty, ArcTokens.Accent);
        box.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
        box.Child = FeedbackUi.Icon(
            state == CheckState.Mixed ? "minus" : "check",
            12,
            ArcTokens.AccentForeground);
        return box;
    }

    private void ToggleSort(ArcTreeTableColumn column)
    {
        if (!column.Sortable)
        {
            return;
        }

        if (_sortKey != column.Key)
        {
            _sortKey = column.Key;
            _sortDescending = false;
        }
        else if (!_sortDescending)
        {
            _sortDescending = true;
        }
        else
        {
            _sortKey = null;
            _sortDescending = false;
        }

        RebuildContent();
        RaiseSortChanged(_sortKey is null
            ? null
            : new ArcTreeTableSort { Key = _sortKey, Descending = _sortDescending });
        Announce(_sortKey is null
            ? "Sort cleared."
            : $"Sorted by {column.Header}, {(_sortDescending ? "descending" : "ascending")}. Siblings sort within their parent.");
        Dispatcher.UIThread.Post(() =>
        {
            if (_headerButtons.TryGetValue(column.Key, out var button))
            {
                button.Focus();
            }
        }, DispatcherPriority.Input);
    }

    private Grid CreateGrid(IReadOnlyList<ArcTreeTableColumn> columns)
    {
        var grid = new Grid();
        if (Selectable)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(CheckColumnWidth, GridUnitType.Pixel));
        }

        foreach (var column in columns)
        {
            var definition = column.Width > 0
                ? new ColumnDefinition(column.Width, GridUnitType.Pixel)
                : new ColumnDefinition(1, GridUnitType.Star);
            definition.MinWidth = column.MinWidth;
            grid.ColumnDefinitions.Add(definition);
        }

        return grid;
    }

    private static HorizontalAlignment AlignToAlignment(ArcTreeTableAlign align) => align switch
    {
        ArcTreeTableAlign.Right => HorizontalAlignment.Right,
        ArcTreeTableAlign.Center => HorizontalAlignment.Center,
        _ => HorizontalAlignment.Left
    };

    private static TextAlignment AlignToTextAlignment(ArcTreeTableAlign align) => align switch
    {
        ArcTreeTableAlign.Right => TextAlignment.Right,
        ArcTreeTableAlign.Center => TextAlignment.Center,
        _ => TextAlignment.Left
    };

    // ---- Body --------------------------------------------------------------

    private void BuildBody()
    {
        if (_bodyHost is null)
        {
            return;
        }

        _bodyHost.Children.Clear();
        _rowsPanel = null;
        _framesByIndex.Clear();
        _realizedRows.Clear();
        _rowCache.Clear();
        _windowFirst = 0;
        _windowLast = -1;

        var columns = VisibleColumns();
        if (columns.Count == 0)
        {
            _bodyHost.Children.Add(BuildEmptyState(
                "table",
                "No columns to show",
                "Add at least one column to the tree table.",
                $"{Label} has no visible columns"));
            return;
        }

        if (Roots().Count == 0)
        {
            _bodyHost.Children.Add(BuildEmptyState(
                "rows-3",
                EmptyMessage,
                EmptyDescription,
                $"{Label} empty. {EmptyMessage}. {EmptyDescription}"));
            return;
        }

        _rowsPanel = new ArcVirtualRowsPanel
        {
            RowHeight = RowHeight,
            TotalRows = _display.Count
        };
        _bodyHost.Children.Add(_rowsPanel);
        UpdateBodyWindow(force: true);
        UpdateFocusVisuals();
    }

    private Control BuildRowControl(int displayIndex, IReadOnlyList<ArcTreeTableColumn> columns)
    {
        var item = _display[displayIndex];
        return item.Kind == DisplayKind.Row
            ? BuildDataRow(item, columns, displayIndex)
            : BuildPlaceholderRow(item, columns, displayIndex);
    }

    /// <summary>Flattened row count (diagnostics for the 10k scenario pages and the Verify harness).</summary>
    internal int FlattenedRowCount => _display.Count;

    /// <summary>Row controls that currently exist. Must stay viewport-sized, independent of the row count.</summary>
    internal int RealisedRowCount => _realizedRows.Count;

    /// <summary>First flattened index in the current window, or 0 when empty.</summary>
    internal int WindowFirstRow => _windowFirst;

    /// <summary>Last flattened index in the current window, or -1 when empty.</summary>
    internal int WindowLastRow => _windowLast;

    /// <summary>Detached row controls kept for reuse.</summary>
    internal int CachedRowCount => _rowCache.Count;

    private double ViewportHeight
    {
        get
        {
            var height = _scroll?.Viewport.Height ?? 0;
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
            {
                height = MaxBodyHeight > 0 ? MaxBodyHeight : 360;
            }

            return height;
        }
    }

    /// <summary>
    /// Realises exactly the rows the viewport can show (plus overscan) and detaches the rest. Rows
    /// that leave the window are recycled through <see cref="_rowCache"/> so scrolling back reuses the
    /// visual instead of rebuilding it. Budget: at most <c>ceil(viewport/RowHeight) + 2*overscan + 1</c>
    /// controls exist at any time, regardless of the flattened row count.
    /// </summary>
    private void UpdateBodyWindow(bool force = false)
    {
        if (_rowsPanel is null || _display.Count == 0 || _updatingWindow)
        {
            return;
        }

        _updatingWindow = true;
        try
        {
            _rowsPanel.TotalRows = _display.Count;
            var offset = _scroll?.Offset.Y ?? 0;
            var (first, last) = ArcTreeTableWindow.Compute(
                _display.Count,
                RowHeight,
                ViewportHeight,
                offset,
                ArcTreeTableWindow.DefaultOverscan);

            if (!force && first == _windowFirst && last == _windowLast)
            {
                return;
            }

            _windowFirst = first;
            _windowLast = last;
            var columns = VisibleColumns();

            foreach (var pair in _realizedRows.ToList())
            {
                if (pair.Key >= first && pair.Key <= last)
                {
                    continue;
                }

                _rowsPanel.RemoveRow(pair.Value);
                _realizedRows.Remove(pair.Key);
                _framesByIndex.Remove(pair.Key);
                _rowCache.Return(pair.Key, pair.Value);
            }

            for (var index = first; index <= last; index++)
            {
                if (_realizedRows.ContainsKey(index))
                {
                    continue;
                }

                if (!_rowCache.TryTake(index, out var control))
                {
                    control = BuildRowControl(index, columns);
                }

                _realizedRows[index] = control;
                _rowsPanel.SetRow(control, index);
            }

            UpdateFocusVisuals();
        }
        finally
        {
            _updatingWindow = false;
        }
    }

    private int DisplayToVisibleIndex(int displayIndex) =>
        displayIndex >= 0 && displayIndex < _displayVisibleIndex.Count
            ? _displayVisibleIndex[displayIndex]
            : 0;

    private int VisibleToDisplayIndex(int visibleIndex) =>
        visibleIndex >= 0 && visibleIndex < _visibleDisplayIndex.Count
            ? _visibleDisplayIndex[visibleIndex]
            : -1;

    /// <summary>
    /// Scrolls the viewport just enough to show a flattened row and realises it before focus paints.
    /// </summary>
    private void EnsureVisible(int visibleIndex)
    {
        if (_rowsPanel is null || _visible.Count == 0)
        {
            return;
        }

        var displayIndex = VisibleToDisplayIndex(Math.Clamp(visibleIndex, 0, _visible.Count - 1));
        if (displayIndex < 0)
        {
            return;
        }

        var viewport = ViewportHeight;
        var offset = _scroll?.Offset.Y ?? 0;
        var top = displayIndex * RowHeight;
        if (top < offset)
        {
            offset = top;
        }
        else if (top + RowHeight > offset + viewport)
        {
            offset = top + RowHeight - viewport;
        }

        var maxOffset = Math.Max(0, _display.Count * RowHeight - viewport);
        offset = Math.Clamp(offset, 0, maxOffset);
        if (_scroll is not null && Math.Abs(_scroll.Offset.Y - offset) > 0.5)
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, offset);
        }

        UpdateBodyWindow(force: true);
        if (_framesByIndex.TryGetValue(displayIndex, out var frame))
        {
            frame.BringIntoView();
        }

        UpdateFocusVisuals();
    }

    private Control BuildEmptyState(string icon, string title, string description, string label)
    {
        return new Border
        {
            MinHeight = 180,
            Padding = new Thickness(24),
            Child = new ArcEmptyState
            {
                Icon = icon,
                Title = title,
                Description = description,
                Label = label
            }
        };
    }

    private Control BuildDataRow(DisplayRow item, IReadOnlyList<ArcTreeTableColumn> columns, int displayIndex)
    {
        var row = item.Row;
        var depth = item.Depth;
        var expanded = _expanded.Contains(row.Id);
        var state = Selectable ? CheckStateOf(row) : CheckState.Unchecked;
        var isSelected = _selected.Contains(row.Id);
        var grid = CreateGrid(columns);
        var offset = Selectable ? 1 : 0;

        if (Selectable)
        {
            var check = new ArcFlatButton
            {
                Content = CheckVisual(state),
                Padding = new Thickness(2),
                Background = Brushes.Transparent,
                Focusable = false,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            AutomationProperties.SetName(check, state == CheckState.Checked
                ? $"Deselect {LabelOf(row)} and its children"
                : $"Select {LabelOf(row)} and its children");
            AutomationProperties.SetHelpText(check, state == CheckState.Mixed
                ? "Partly selected; selects every child"
                : "Space toggles selection while the row is focused");
            var captured = row;
            check.Click += (_, _) => ToggleRowSelection(captured);
            var host = new Border
            {
                Child = check,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(host, 0);
            grid.Children.Add(host);
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            Control cell;
            if (index == 0)
            {
                cell = BuildTreeCell(row, depth, expanded);
            }
            else
            {
                var text = CellText(row, column);
                var label = new TextBlock
                {
                    Text = text,
                    FontSize = ArcResources.Double(ArcTokens.TextSm),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = AlignToTextAlignment(column.Align),
                    Padding = new Thickness(12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                if (column.Align == ArcTreeTableAlign.Right)
                {
                    label.FontFeatures = FeedbackUi.Tabular;
                }

                label.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                AutomationProperties.SetName(label, $"{column.Header}: {text}");
                cell = label;
            }

            Grid.SetColumn(cell, offset + index);
            grid.Children.Add(cell);
        }

        var frame = new Border
        {
            MinHeight = RowHeight,
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            CornerRadius = new CornerRadius(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid
        };
        frame.Paint(
            Border.BackgroundProperty,
            isSelected ? ArcTokens.AccentSubtle : ArcTokens.Surface);

        if (!ArcThemeManager.ReducedMotion)
        {
            frame.Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = ArcMotion.Fast
                }
            };
        }

        var indexInView = displayIndex;
        _framesByIndex[displayIndex] = frame;

        frame.PointerEntered += (_, _) =>
        {
            if (!_selected.Contains(row.Id))
            {
                frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            }
        };
        frame.PointerExited += (_, _) =>
        {
            if (!_selected.Contains(row.Id))
            {
                frame.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            }
        };
        frame.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(frame).Properties.IsLeftButtonPressed || FromButton(args.Source))
            {
                return;
            }

            _keyboardFocus = false;
            _focusIndex = DisplayToVisibleIndex(indexInView);
            Focus();
            UpdateFocusVisuals();
            if (CanExpand(row))
            {
                SetExpanded(row, !_expanded.Contains(row.Id));
            }
            else
            {
                Activate(row);
            }

            args.Handled = true;
        };

        var level = depth + 1;
        var stateText = !Selectable
            ? string.Empty
            : state switch
            {
                CheckState.Checked => ", checked",
                CheckState.Mixed => ", mixed",
                _ => ", unchecked"
            };
        var expandedText = CanExpand(row) ? (expanded ? ", expanded" : ", collapsed") : ", leaf";
        AutomationProperties.SetName(frame, $"{LabelOf(row)}, level {level}{stateText}{expandedText}");
        AutomationProperties.SetPositionInSet(frame, item.Position);
        AutomationProperties.SetSizeOfSet(frame, item.SetSize);
        AutomationProperties.SetHelpText(frame, CanExpand(row)
            ? "Enter expands or collapses; Space toggles selection; Arrow keys move"
            : "Enter activates this row; Space toggles selection; Arrow keys move");

        var root = new Panel { MinHeight = RowHeight };
        root.Children.Add(frame);
        var line = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };
        line.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
        root.Children.Add(line);
        if (isSelected)
        {
            var accent = new Border
            {
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            accent.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            root.Children.Add(accent);
        }

        return root;
    }

    private Control BuildTreeCell(ArcTreeTableRow row, int depth, bool expanded)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        for (var level = 0; level < depth; level++)
        {
            var guide = new Border
            {
                Width = Indent,
                Height = RowContentHeight,
                BorderThickness = new Thickness(1, 0, 0, 0),
                IsHitTestVisible = false
            };
            guide.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            stack.Children.Add(guide);
        }

        var rotate = new RotateTransform(expanded ? 90 : 0);
        var chevron = FeedbackUi.Icon(
            "chevron-right",
            12,
            expanded ? ArcTokens.TextSecondary : ArcTokens.TextMuted);
        chevron.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        chevron.RenderTransform = rotate;
        if (CanExpand(row))
        {
            _chevrons[row.Id] = rotate;
        }

        stack.Children.Add(new Border
        {
            Width = ChevronSlot,
            Height = RowContentHeight,
            Child = chevron,
            IsHitTestVisible = false
        });

        var label = new TextBlock
        {
            Text = LabelOf(row),
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            FontWeight = CanExpand(row) ? FontWeight.Medium : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        label.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
        AutomationProperties.SetIsRowHeader(label, true);
        stack.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(12, 0, 0, 0),
            Child = stack,
            VerticalAlignment = VerticalAlignment.Stretch
        };
    }

    private Control BuildPlaceholderRow(DisplayRow item, IReadOnlyList<ArcTreeTableColumn> columns, int displayIndex)
    {
        _ = displayIndex;
        var row = item.Row;
        var depth = item.Depth;
        var grid = CreateGrid(columns);
        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12 + depth * Indent, 0, 12, 0)
        };

        if (item.Kind == DisplayKind.Loading)
        {
            host.Children.Add(new ArcSpinner { Size = 14, StrokeWidth = 2 });
            var text = FeedbackUi.Caption("Loading children…");
            text.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            host.Children.Add(text);
            AutomationProperties.SetName(host, $"Loading children of {LabelOf(row)}");
        }
        else
        {
            var icon = FeedbackUi.Icon("triangle-alert", 14, ArcTokens.Warning);
            host.Children.Add(icon);
            var text = FeedbackUi.Caption($"Couldn't load children of {LabelOf(row)}.");
            text.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            host.Children.Add(text);
            var retry = new ArcFlatButton
            {
                Text = "Retry",
                Quiet = true,
                Padding = new Thickness(8, 2),
                FontSize = ArcResources.Double(ArcTokens.TextXs)
            };
            retry.Paint(TemplatedControl.ForegroundProperty, ArcTokens.AccentStrong);
            AutomationProperties.SetName(retry, $"Retry loading children of {LabelOf(row)}");
            var captured = row;
            retry.Click += (_, _) => Retry(captured);
            host.Children.Add(retry);
            AutomationProperties.SetName(host, $"Couldn't load children of {LabelOf(row)}. Retry available.");
        }

        var cell = new Border
        {
            MinHeight = 36,
            Child = host,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(cell, 0);
        Grid.SetColumnSpan(cell, Math.Max(1, grid.ColumnDefinitions.Count));
        grid.Children.Add(cell);

        var root = new Panel { MinHeight = 36 };
        root.Children.Add(grid);
        var line = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false
        };
        line.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
        root.Children.Add(line);
        return root;
    }

    private static bool FromButton(object? source)
    {
        var current = source as Visual;
        while (current is not null)
        {
            if (current is Button)
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    // ---- Selection ---------------------------------------------------------

    private CheckState CheckStateOf(ArcTreeTableRow row)
    {
        if (_selected.Contains(row.Id))
        {
            return CheckState.Checked;
        }

        return AnySelectedBelow(row) ? CheckState.Mixed : CheckState.Unchecked;
    }

    private bool AnySelectedBelow(ArcTreeTableRow row)
    {
        foreach (var child in ChildrenOf(row))
        {
            if (_selected.Contains(child.Id) || AnySelectedBelow(child))
            {
                return true;
            }
        }

        return false;
    }

    private void AddSubtree(ArcTreeTableRow row, bool selected)
    {
        if (selected)
        {
            _selected.Add(row.Id);
        }
        else
        {
            _selected.Remove(row.Id);
        }

        foreach (var child in ChildrenOf(row))
        {
            AddSubtree(child, selected);
        }
    }

    /// <summary>
    /// Recomputes every parent from its children, bottom-up: a row is checked exactly when all of
    /// its children are checked; otherwise it is removed from the selection set and reads as mixed
    /// when any descendant remains checked.
    /// </summary>
    private void NormalizeSelection()
    {
        foreach (var root in Roots())
        {
            NormalizeNode(root);
        }
    }

    private bool NormalizeNode(ArcTreeTableRow row)
    {
        var children = ChildrenOf(row);
        var any = false;
        foreach (var child in children)
        {
            any |= NormalizeNode(child);
        }

        if (children.Count == 0)
        {
            return _selected.Contains(row.Id);
        }

        var allChecked = true;
        any = false;
        foreach (var child in children)
        {
            if (_selected.Contains(child.Id))
            {
                any = true;
            }
            else
            {
                allChecked = false;
            }
        }

        if (allChecked)
        {
            _selected.Add(row.Id);
            any = true;
        }
        else
        {
            _selected.Remove(row.Id);
        }

        return any;
    }

    private void NormalizeAncestors(ArcTreeTableRow row)
    {
        var parent = ParentOf(row);
        while (parent is not null)
        {
            var children = ChildrenOf(parent);
            var allChecked = children.Count > 0 && children.All(child => _selected.Contains(child.Id));
            if (allChecked)
            {
                _selected.Add(parent.Id);
            }
            else
            {
                _selected.Remove(parent.Id);
            }

            parent = ParentOf(parent);
        }
    }

    private void ToggleRowSelection(ArcTreeTableRow row)
    {
        if (!Selectable)
        {
            return;
        }

        var target = CheckStateOf(row) != CheckState.Checked;
        AddSubtree(row, target);
        NormalizeAncestors(row);
        PublishSelection();
        RebuildContent();
        Announce(target
            ? $"Selected {LabelOf(row)} and its children."
            : $"Cleared {LabelOf(row)} and its children.");
    }

    private void ToggleSelectAll()
    {
        if (!Selectable)
        {
            return;
        }

        var roots = Roots();
        if (roots.Count == 0)
        {
            return;
        }

        var restoreFocus = _selectAllButton?.IsFocused == true;
        var all = roots.All(row => _selected.Contains(row.Id));
        if (all)
        {
            _selected.Clear();
        }
        else
        {
            foreach (var root in roots)
            {
                AddSubtree(root, true);
            }
        }

        NormalizeSelection();
        PublishSelection();
        RebuildContent();
        if (restoreFocus)
        {
            Dispatcher.UIThread.Post(() => _selectAllButton?.Focus(), DispatcherPriority.Input);
        }

        Announce(all ? "Selection cleared." : "Selected every row.");
    }

    private void PublishSelection()
    {
        var ordered = PreOrderIds().Where(id => _selected.Contains(id)).ToList();
        _syncingSelection = true;
        SetCurrentValue(SelectedIdsProperty, ordered);
        _syncingSelection = false;
        RaiseSelectionChanged(ordered);
    }

    private List<string> PreOrderIds()
    {
        var result = new List<string>();
        foreach (var root in SortSiblings(Roots()))
        {
            AddIds(root, result);
        }

        return result;
    }

    private void AddIds(ArcTreeTableRow row, List<string> result)
    {
        result.Add(row.Id);
        foreach (var child in SortSiblings(ChildrenOf(row)))
        {
            AddIds(child, result);
        }
    }

    // ---- Expansion and loading --------------------------------------------

    private void SetExpanded(ArcTreeTableRow row, bool expanded)
    {
        if (expanded == _expanded.Contains(row.Id))
        {
            return;
        }

        if (expanded)
        {
            _expanded.Add(row.Id);
        }
        else
        {
            _expanded.Remove(row.Id);
        }

        RebuildContent();
        AnimateChevron(row.Id, expanded);
        PublishExpansion();
        Announce(expanded ? $"{LabelOf(row)} expanded." : $"{LabelOf(row)} collapsed.");
    }

    private void AnimateChevron(string rowId, bool expanding)
    {
        if (!_chevrons.TryGetValue(rowId, out var rotate))
        {
            return;
        }

        var from = expanding ? 0d : 90d;
        var to = expanding ? 90d : 0d;
        rotate.Angle = from;
        _chevronTween.Run(
            from,
            to,
            expanding ? ArcMotion.SpringSnappyDuration : ArcMotion.Exit,
            angle => rotate.Angle = angle,
            ease: expanding ? ArcMotion.SpringSnappy.Ease : ArcMotion.EaseExit.Ease);
    }

    private void PublishExpansion()
    {
        var ordered = PreOrderIds().Where(id => _expanded.Contains(id)).ToList();
        if (ExpandedIds is not null)
        {
            _syncingExpansion = true;
            SetCurrentValue(ExpandedIdsProperty, ordered);
            _syncingExpansion = false;
        }

        RaiseExpandedChanged(ordered);
    }

    private void StartQueuedLoads()
    {
        if (_loadQueue.Count == 0)
        {
            return;
        }

        var queue = _loadQueue.Distinct().ToList();
        _loadQueue.Clear();
        foreach (var row in queue)
        {
            Announce($"Loading children of {LabelOf(row)}.");
            _ = LoadChildrenAsync(row);
        }
    }

    private async Task LoadChildrenAsync(ArcTreeTableRow row)
    {
        var epoch = _loadEpoch;
        try
        {
            var children = await LoadChildren!(row) ?? Array.Empty<ArcTreeTableRow>();
            if (epoch != _loadEpoch)
            {
                return;
            }

            var list = children.ToList();
            _loadedChildren[row.Id] = list;
            _loadFailed.Remove(row.Id);
            if (list.Count == 0)
            {
                _expanded.Remove(row.Id);
            }
            else if (_selected.Contains(row.Id))
            {
                // A parent checked before its lazy children arrived checks them as they load.
                foreach (var child in list)
                {
                    AddSubtree(child, true);
                }
            }

            NormalizeSelection();
        }
        catch
        {
            if (epoch != _loadEpoch)
            {
                return;
            }

            _loadFailed.Add(row.Id);
        }
        finally
        {
            if (epoch == _loadEpoch)
            {
                _loading.Remove(row.Id);
                RebuildContent();
                if (_loadFailed.Contains(row.Id))
                {
                    Announce($"Couldn't load children of {LabelOf(row)}. Press Enter to retry.");
                }
                else
                {
                    Announce($"Loaded {ChildrenOf(row).Count} children of {LabelOf(row)}.");
                }
            }
        }
    }

    private void Retry(ArcTreeTableRow row)
    {
        _loadFailed.Remove(row.Id);
        _loadedChildren.Remove(row.Id);
        _loading.Remove(row.Id);
        var index = _visible.FindIndex(item => ReferenceEquals(item.Row, row));
        if (index >= 0)
        {
            _focusIndex = index;
        }

        RebuildContent();
        Focus();
        _keyboardFocus = true;
        UpdateFocusVisuals();
        Announce($"Retrying children of {LabelOf(row)}.");
    }

    // ---- Keyboard ----------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (FromButton(e.Source))
        {
            return;
        }

        if (_visible.Count == 0)
        {
            return;
        }

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                      || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (command && e.Key == Key.A && Selectable)
        {
            ToggleSelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Multiply || (e.Key == Key.D8 && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            ExpandSiblings();
            e.Handled = true;
            return;
        }

        var handled = true;
        switch (e.Key)
        {
            case Key.Down:
                MoveFocus(1);
                break;
            case Key.Up:
                MoveFocus(-1);
                break;
            case Key.PageDown:
                MoveFocus(10);
                break;
            case Key.PageUp:
                MoveFocus(-10);
                break;
            case Key.Home:
                SetFocusIndex(0);
                break;
            case Key.End:
                SetFocusIndex(_visible.Count - 1);
                break;
            case Key.Right:
                ExpandOrEnterChild();
                break;
            case Key.Left:
                CollapseOrGoToParent();
                break;
            case Key.Space:
                if (Selectable)
                {
                    ToggleRowSelection(_visible[_focusIndex].Row);
                }
                else
                {
                    handled = false;
                }

                break;
            case Key.Enter:
                ActivateFocused();
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
            return;
        }

        if (TryTypeahead(e))
        {
            e.Handled = true;
        }
    }

    private void ActivateFocused()
    {
        var row = _visible[_focusIndex].Row;
        if (_loadFailed.Contains(row.Id))
        {
            Retry(row);
            return;
        }

        if (CanExpand(row))
        {
            SetExpanded(row, !_expanded.Contains(row.Id));
            return;
        }

        Activate(row);
    }

    private void Activate(ArcTreeTableRow row)
    {
        RaiseRowActivated(row);
        Announce($"{LabelOf(row)} activated.");
    }

    private void ExpandOrEnterChild()
    {
        var row = _visible[_focusIndex].Row;
        if (!CanExpand(row))
        {
            return;
        }

        if (!_expanded.Contains(row.Id))
        {
            SetExpanded(row, true);
            return;
        }

        if (_focusIndex + 1 < _visible.Count
            && _visible[_focusIndex + 1].Depth == _visible[_focusIndex].Depth + 1)
        {
            SetFocusIndex(_focusIndex + 1);
        }
    }

    private void CollapseOrGoToParent()
    {
        var row = _visible[_focusIndex].Row;
        if (CanExpand(row) && _expanded.Contains(row.Id))
        {
            SetExpanded(row, false);
            return;
        }

        var parent = ParentOf(row);
        if (parent is null)
        {
            return;
        }

        var index = _visible.FindIndex(item => ReferenceEquals(item.Row, parent));
        if (index >= 0)
        {
            SetFocusIndex(index);
        }
    }

    private void ExpandSiblings()
    {
        var row = _visible[_focusIndex].Row;
        var parent = ParentOf(row);
        var siblings = parent is null ? Roots() : ChildrenOf(parent).ToList();
        var changed = false;
        foreach (var sibling in siblings)
        {
            if (CanExpand(sibling) && !_expanded.Contains(sibling.Id))
            {
                _expanded.Add(sibling.Id);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        RebuildContent();
        PublishExpansion();
        Announce($"Expanded {siblings.Count} sibling rows.");
    }

    private void MoveFocus(int delta)
    {
        SetFocusIndex(_focusIndex + delta);
    }

    private void SetFocusIndex(int index)
    {
        _focusIndex = Math.Clamp(index, 0, Math.Max(0, _visible.Count - 1));
        _keyboardFocus = true;
        EnsureVisible(_focusIndex);
        UpdateFocusVisuals();
    }

    private bool TryTypeahead(KeyEventArgs e)
    {
        var character = TypeaheadCharacter(e.Key, e.KeyModifiers);
        if (character is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if ((now - _typeBufferAt).TotalMilliseconds > 800)
        {
            _typeBuffer = string.Empty;
        }

        _typeBufferAt = now;
        _typeBuffer += char.ToLowerInvariant(character.Value);
        if (_typeBuffer.Length > 1 && _typeBuffer.All(ch => ch == character.Value))
        {
            // Pressing the same letter again cycles through matches instead of searching for "aa".
            _typeBuffer = character.Value.ToString();
        }
        for (var offset = 1; offset <= _visible.Count; offset++)
        {
            var index = (_focusIndex + offset) % _visible.Count;
            if (LabelOf(_visible[index].Row).StartsWith(_typeBuffer, StringComparison.OrdinalIgnoreCase))
            {
                _focusIndex = index;
                _keyboardFocus = true;
                EnsureVisible(index);
                UpdateFocusVisuals();

                return true;
            }
        }

        _typeBuffer = string.Empty;
        return false;
    }

    private static char? TypeaheadCharacter(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control)
            || modifiers.HasFlag(KeyModifiers.Alt)
            || modifiers.HasFlag(KeyModifiers.Meta))
        {
            return null;
        }

        var code = (int)key;
        if (code >= (int)Key.A && code <= (int)Key.Z)
        {
            return (char)('a' + code - (int)Key.A);
        }

        if (code >= (int)Key.D0 && code <= (int)Key.D9)
        {
            return (char)('0' + code - (int)Key.D0);
        }

        if (code >= (int)Key.NumPad0 && code <= (int)Key.NumPad9)
        {
            return (char)('0' + code - (int)Key.NumPad0);
        }

        return null;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _keyboardFocus = true;
        }

        if (_keyboardFocus && _visible.Count > 0)
        {
            EnsureVisible(_focusIndex);
        }

        UpdateFocusVisuals();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        UpdateFocusVisuals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _chevronTween.Stop();
    }

    private void UpdateFocusVisuals()
    {
        foreach (var pair in _framesByIndex)
        {
            var focused = IsFocused && _keyboardFocus && DisplayToVisibleIndex(pair.Key) == _focusIndex;
            pair.Value.Paint(
                Border.BorderBrushProperty,
                focused ? ArcTokens.FocusOutline : ArcTokens.BorderSubtle);
            if (!focused)
            {
                pair.Value.BorderBrush = Brushes.Transparent;
            }
        }
    }

    // ---- Footer and automation --------------------------------------------

    private void RebuildFooter()
    {
        if (_footerHost is null)
        {
            return;
        }

        _footerHost.Child = null;
        if (Footer is not null)
        {
            _footerHost.IsVisible = true;
            _footerHost.Padding = new Thickness(14, 8, 12, 8);
            _footerHost.Child = Footer as Control ?? new ContentControl { Content = Footer };
            return;
        }

        if (!ShowSummary)
        {
            _footerHost.IsVisible = false;
            return;
        }

        _footerHost.IsVisible = true;
        _footerHost.Padding = new Thickness(0);
        var grid = new Grid
        {
            MinHeight = 40,
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        var rows = $"{_visible.Count} {(_visible.Count == 1 ? "row" : "rows")}";
        var selectedCount = Selectable ? _selected.Count : 0;
        var summaryText = selectedCount > 0 ? $"{rows} · {selectedCount} selected" : rows;
        var summary = new TextBlock
        {
            Text = summaryText,
            FontSize = ArcResources.Double(ArcTokens.TextXs),
            Padding = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        summary.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        AutomationProperties.SetName(summary, summaryText);
        grid.Children.Add(summary);

        var column = _sortKey is null
            ? null
            : VisibleColumns().FirstOrDefault(item => item.Key == _sortKey);
        var sortText = column is null
            ? "Sort within each level"
            : $"{column.Header}: {(_sortDescending ? "high to low" : "low to high")}";
        var sort = new TextBlock
        {
            Text = sortText,
            FontSize = ArcResources.Double(ArcTokens.TextXs),
            Padding = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        sort.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        AutomationProperties.SetName(sort, sortText);
        Grid.SetColumn(sort, 1);
        grid.Children.Add(sort);

        _footerHost.Child = grid;
    }

    private void UpdateAutomation()
    {
        var label = string.IsNullOrWhiteSpace(Label) ? "Tree table" : Label;
        AutomationProperties.SetName(this, $"{label}, tree table");
        AutomationProperties.SetHelpText(
            this,
            $"{_visible.Count} rows visible. Arrow keys move, Right and Left expand or collapse, Space selects, Enter activates.");
    }

    private void Announce(string message)
    {
        if (_live is null)
        {
            return;
        }

        _live.Text = $"{message} {_visible.Count} rows visible.";
    }
}

/// <summary>Event data for the bubbling <see cref="SortChangedRouted"/> event.</summary>
public sealed class ArcTreeTableSortChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcTreeTableSortChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public ArcTreeTableSort? Sort { get; init; }
}

/// <summary>Event data for the bubbling <see cref="SelectionChangedRouted"/> event.</summary>
public sealed class ArcTreeTableSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcTreeTableSelectionChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public IReadOnlyList<string> SelectedKeys { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="ExpandedChangedRouted"/> event.</summary>
public sealed class ArcTreeTableExpandedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcTreeTableExpandedChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public IReadOnlyList<string> ExpandedKeys { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="RowActivatedRouted"/> event.</summary>
public sealed class ArcTreeTableRowActivatedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcTreeTableRowActivatedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public ArcTreeTableRow Row { get; init; } = null!;
}
