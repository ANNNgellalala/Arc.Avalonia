using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Controls;

/// <summary>Horizontal alignment of an <see cref="ArcGridColumn"/>'s cells.</summary>
public enum ArcGridAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// One column of an <see cref="ArcDataGrid"/>. <see cref="Key"/> reads (and, unless
/// <see cref="ValueGetter"/> is set, writes) the matching entry in each row's
/// <see cref="ArcGridRow.Values"/> dictionary. A right-aligned column is treated as numeric: it uses
/// tabular figures and contributes a sum to the footer.
/// </summary>
public sealed class ArcGridColumn
{
    public string Key { get; set; } = string.Empty;

    public string Header { get; set; } = string.Empty;

    /// <summary>Starting width in px. Zero makes the column absorb the remaining width (star).</summary>
    public double Width { get; set; } = 140;

    /// <summary>Narrowest a resize can reach, matching the Pro grid's 72 px default.</summary>
    public double MinWidth { get; set; } = 72;

    public ArcGridAlign Align { get; set; } = ArcGridAlign.Left;

    public bool Sortable { get; set; } = true;

    public bool Resizable { get; set; } = true;

    /// <summary>False opts the column out of inline editing.</summary>
    public bool Editable { get; set; } = true;

    /// <summary>Starts hidden; people can show it again from the Columns menu.</summary>
    public bool Hidden { get; set; }

    /// <summary>Adds this column's numeric values to the footer when it is right aligned.</summary>
    public bool ShowTotal { get; set; } = true;

    /// <summary>Optional display formatter. Receives the raw cell value.</summary>
    public Func<object?, string>? Format { get; set; }

    /// <summary>
    /// Optional value reader for computed columns. Such columns are read-only because there is no
    /// field to write the edit back to.
    /// </summary>
    public Func<ArcGridRow, object?>? ValueGetter { get; set; }
}

/// <summary>
/// One record in an <see cref="ArcDataGrid"/>. Ids must be unique and stable so selection and
/// sorting can keep their place while rows change.
/// </summary>
public sealed class ArcGridRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The row's cells, keyed by <see cref="ArcGridColumn.Key"/>.</summary>
    public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

    public object? this[string key]
    {
        get => Values.TryGetValue(key, out var value) ? value : null;
        set => Values[key] = value;
    }
}

/// <summary>A sort key for an <see cref="ArcDataGrid"/>; a null sort in an event means unsorted.</summary>
public sealed class ArcGridSort
{
    public string Key { get; set; } = string.Empty;

    public bool Descending { get; set; }
}

/// <summary>Raised after an inline cell edit is committed.</summary>
public sealed class ArcGridCellEdit
{
    public ArcGridRow Row { get; init; } = null!;

    public string RowId => Row.Id;

    public ArcGridColumn Column { get; init; } = null!;

    public object? OldValue { get; init; }

    public object? NewValue { get; init; }
}

/// <summary>
/// A Pro-style data grid: a sticky header with click-to-cycle sorting, drag-to-resize columns, a
/// checkbox gutter with Shift-range selection, arrow-cell keyboard navigation, inline text editing,
/// a column visibility menu, a footer summary with sums, zebra rows and an empty state.
/// </summary>
/// <remarks>
/// Independent Avalonia implementation built from the public data-grid documentation
/// (uiarc.dev/components/data-grid); no Pro source was used. The rows model is
/// <see cref="ArcGridRow"/> with a keyed <see cref="ArcGridRow.Values"/> dictionary. Deliberately
/// omitted from the spreadsheet-sized public component: the fill handle, clipboard matrices, undo,
/// grouping, CSV/XLSX export, server data sources, pinned columns and row virtualisation. There is no
/// row-number gutter and no horizontal scroll: columns share the available width, and a zero-width
/// last column absorbs what is left. Range selection stays a checked-row range rather than a
/// rectangular cell range, and a sort reorders once with no row slide (a stable sort, like a
/// spreadsheet). These are noted on the demo page too.
/// </remarks>
[TemplatePart("PART_Toolbar", typeof(Panel))]
[TemplatePart("PART_HeaderHost", typeof(Border))]
[TemplatePart("PART_Scroll", typeof(ScrollViewer))]
[TemplatePart("PART_Body", typeof(Panel))]
[TemplatePart("PART_FooterHost", typeof(Border))]
[TemplatePart("PART_ColumnsPopup", typeof(Popup))]
public class ArcDataGrid : TemplatedControl
{
    private const double RowHeight = 40;

    public static readonly StyledProperty<IList<ArcGridColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<ArcDataGrid, IList<ArcGridColumn>?>(nameof(Columns));

    public static readonly StyledProperty<IList<ArcGridRow>?> RowsProperty =
        AvaloniaProperty.Register<ArcDataGrid, IList<ArcGridRow>?>(nameof(Rows));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcDataGrid, string>(nameof(Label), "Data grid");

    public static readonly StyledProperty<ArcGridSort?> DefaultSortProperty =
        AvaloniaProperty.Register<ArcDataGrid, ArcGridSort?>(nameof(DefaultSort));

    public static readonly StyledProperty<IEnumerable<string>?> SelectedIdsProperty =
        AvaloniaProperty.Register<ArcDataGrid, IEnumerable<string>?>(nameof(SelectedIds));

    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<ArcDataGrid, bool>(nameof(Selectable), true);

    public static readonly StyledProperty<bool> ZebraProperty =
        AvaloniaProperty.Register<ArcDataGrid, bool>(nameof(Zebra));

    public static readonly StyledProperty<double> MaxBodyHeightProperty =
        AvaloniaProperty.Register<ArcDataGrid, double>(nameof(MaxBodyHeight), 400);

    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<ArcDataGrid, string>(nameof(EmptyMessage), "No rows yet");

    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<ArcDataGrid, string>(
            nameof(EmptyDescription), "Add the first row, or clear the filters to see everything again.");

    public static readonly StyledProperty<bool> ShowTotalsProperty =
        AvaloniaProperty.Register<ArcDataGrid, bool>(nameof(ShowTotals), true);

    public static readonly StyledProperty<bool> EditableProperty =
        AvaloniaProperty.Register<ArcDataGrid, bool>(nameof(Editable), true);

    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);
    private readonly List<Grid> _layoutGrids = new();
    private readonly Dictionary<(int Row, int Column), Border> _cellFrames = new();
    private readonly Dictionary<string, ArcFlatButton> _headerButtons = new(StringComparer.Ordinal);
    private List<ArcGridRow> _view = new();
    private List<string> _viewIds = new();
    private Panel? _toolbar;
    private Border? _headerHost;
    private ScrollViewer? _scroll;
    private Panel? _bodyHost;
    private Border? _footerHost;
    private Popup? _columnsPopup;
    private StackPanel? _columnsMenu;
    private ArcButton? _columnsButton;
    private TextBlock? _countText;
    private TextBox? _editor;
    private string? _sortKey;
    private bool _sortDescending;
    private int _activeRow;
    private int _activeColumn;
    private bool _syncingSelection;
    private bool _editing;
    private int _editRow = -1;
    private int _editColumn = -1;
    private int _anchorRow = -1;
    private bool _resizing;
    private ArcGridColumn? _resizeColumn;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _committing;
    private bool _viewFrozen;

    static ArcDataGrid()
    {
        ColumnsProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildAll());
        RowsProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.SyncRows());
        LabelProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildAll());
        DefaultSortProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.ApplyDefaultSort());
        SelectedIdsProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.SyncSelectedIds());
        SelectableProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildAll());
        ZebraProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildContent());
        MaxBodyHeightProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.ApplyBodyHeight());
        EmptyMessageProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildContent());
        EmptyDescriptionProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildContent());
        ShowTotalsProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildContent());
        EditableProperty.Changed.AddClassHandler<ArcDataGrid>((grid, _) => grid.RebuildContent());
    }

    public ArcDataGrid()
    {
        Focusable = true;
    }

    /// <summary>Column definitions in display order.</summary>
    public IList<ArcGridColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Rows as keyed dictionaries; ids must be unique and stable.</summary>
    public IList<ArcGridRow>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Accessible name for the grid.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Starting sort, or null for unsorted. A header click cycles ascending, descending, off.</summary>
    public ArcGridSort? DefaultSort
    {
        get => GetValue(DefaultSortProperty);
        set => SetValue(DefaultSortProperty, value);
    }

    /// <summary>Checked row ids. Set it to pre-select a range; the grid keeps it current as people select.</summary>
    public IEnumerable<string>? SelectedIds
    {
        get => GetValue(SelectedIdsProperty);
        set => SetValue(SelectedIdsProperty, value);
    }

    /// <summary>Shows the checkbox gutter, select all, and the selected count.</summary>
    public bool Selectable
    {
        get => GetValue(SelectableProperty);
        set => SetValue(SelectableProperty, value);
    }

    /// <summary>Tints every other row with the muted surface.</summary>
    public bool Zebra
    {
        get => GetValue(ZebraProperty);
        set => SetValue(ZebraProperty, value);
    }

    /// <summary>Tallest the scrolling body grows before it scrolls, in px.</summary>
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

    /// <summary>Shows the footer count and the numeric sums.</summary>
    public bool ShowTotals
    {
        get => GetValue(ShowTotalsProperty);
        set => SetValue(ShowTotalsProperty, value);
    }

    /// <summary>Allows inline text edits (Enter, F2 or double-click) on editable columns.</summary>
    public bool Editable
    {
        get => GetValue(EditableProperty);
        set => SetValue(EditableProperty, value);
    }

    public event EventHandler<ArcGridSort?>? SortChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SortChanged"/>.</summary>
    public static readonly RoutedEvent<ArcGridSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<ArcDataGrid, ArcGridSortChangedEventArgs>("SortChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcGridSortChangedEventArgs"/> version of <see cref="SortChanged"/>.</summary>
    public event EventHandler<ArcGridSortChangedEventArgs>? SortChangedRouted
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    private void RaiseSortChanged(ArcGridSort? sort)
    {
        SortChanged?.Invoke(this, sort);
        RaiseEvent(new ArcGridSortChangedEventArgs(SortChangedEvent) { Sort = sort });
    }

    public event EventHandler<IReadOnlyList<string>>? SelectionChanged;

    /// <summary>Bubbling routed counterpart of <see cref="SelectionChanged"/>.</summary>
    public static readonly RoutedEvent<ArcGridSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ArcDataGrid, ArcGridSelectionChangedEventArgs>("SelectionChanged", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcGridSelectionChangedEventArgs"/> version of <see cref="SelectionChanged"/>.</summary>
    public event EventHandler<ArcGridSelectionChangedEventArgs>? SelectionChangedRouted
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    private void RaiseSelectionChanged(IReadOnlyList<string> selectedKeys)
    {
        SelectionChanged?.Invoke(this, selectedKeys);
        RaiseEvent(new ArcGridSelectionChangedEventArgs(SelectionChangedEvent) { SelectedKeys = selectedKeys });
    }

    public event EventHandler<ArcGridCellEdit>? CellEdited;

    /// <summary>Bubbling routed counterpart of <see cref="CellEdited"/>.</summary>
    public static readonly RoutedEvent<ArcGridCellEditedEventArgs> CellEditedEvent =
        RoutedEvent.Register<ArcDataGrid, ArcGridCellEditedEventArgs>("CellEdited", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcGridCellEditedEventArgs"/> version of <see cref="CellEdited"/>.</summary>
    public event EventHandler<ArcGridCellEditedEventArgs>? CellEditedRouted
    {
        add => AddHandler(CellEditedEvent, value);
        remove => RemoveHandler(CellEditedEvent, value);
    }

    private void RaiseCellEdited(ArcGridCellEdit edit)
    {
        CellEdited?.Invoke(this, edit);
        RaiseEvent(new ArcGridCellEditedEventArgs(CellEditedEvent) { Edit = edit });
    }

    /// <summary>Re-reads rows and columns after the lists or a column's cells were changed in place.</summary>
    public void Refresh() => RebuildAll();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _toolbar = e.NameScope.Find<Panel>("PART_Toolbar");
        _headerHost = e.NameScope.Find<Border>("PART_HeaderHost");
        _scroll = e.NameScope.Find<ScrollViewer>("PART_Scroll");
        _bodyHost = e.NameScope.Find<Panel>("PART_Body");
        _footerHost = e.NameScope.Find<Border>("PART_FooterHost");
        _columnsPopup = e.NameScope.Find<Popup>("PART_ColumnsPopup");
        _columnsMenu = new StackPanel { Spacing = 2 };
        if (_columnsPopup is not null)
        {
            _columnsPopup.Child = MenuSurface(_columnsMenu);
        }

        ApplyBodyHeight();
        RebuildAll();
    }

    private void ApplyDefaultSort()
    {
        _sortKey = DefaultSort?.Key;
        _sortDescending = DefaultSort?.Descending ?? false;
        _viewFrozen = false;
        RebuildContent();
    }

    /// <summary>Prunes checked ids that no longer exist, then rebuilds.</summary>
    private void SyncRows()
    {
        var live = Rows is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : Rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        _checked.RemoveWhere(id => !live.Contains(id));
        _anchorRow = -1;
        _viewFrozen = false;
        RebuildAll();
    }

    private void SyncSelectedIds()
    {
        if (_syncingSelection)
        {
            return;
        }

        _checked.Clear();
        foreach (var id in SelectedIds ?? Enumerable.Empty<string>())
        {
            _checked.Add(id);
        }

        _anchorRow = -1;
        RebuildContent();
    }

    private void ApplyBodyHeight()
    {
        if (_scroll is null)
        {
            return;
        }

        _scroll.MaxHeight = MaxBodyHeight > 0 ? MaxBodyHeight : double.PositiveInfinity;
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

        BuildView();
        _layoutGrids.Clear();
        _cellFrames.Clear();
        _editor = null;

        BuildHeader();
        BuildBody();
        BuildFooter();
        UpdateCountText();
        UpdateAutomation();
    }

    private List<ArcGridColumn> VisibleColumns() =>
        (Columns ?? Array.Empty<ArcGridColumn>()).Where(column => !column.Hidden).ToList();

    private void BuildView()
    {
        var rows = Rows?.ToList() ?? new List<ArcGridRow>();
        var columns = VisibleColumns();
        if (_sortKey is not null && columns.All(column => column.Key != _sortKey))
        {
            _sortKey = null;
            _sortDescending = false;
        }

        if (_viewFrozen && _viewIds.Count > 0)
        {
            // An edit holds the view order, the way a spreadsheet does, until the next sort.
            var byId = new Dictionary<string, ArcGridRow>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                byId[row.Id] = row;
            }

            var next = new List<ArcGridRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _viewIds)
            {
                if (byId.TryGetValue(id, out var row) && seen.Add(id))
                {
                    next.Add(row);
                }
            }

            foreach (var row in rows)
            {
                if (seen.Add(row.Id))
                {
                    next.Add(row);
                }
            }

            _view = next;
        }
        else
        {
            var order = Enumerable.Range(0, rows.Count).ToList();
            if (_sortKey is not null)
            {
                var column = columns.First(item => item.Key == _sortKey);
                order.Sort((left, right) =>
                {
                    var result = CompareRows(rows[left], rows[right], column);
                    if (_sortDescending)
                    {
                        result = -result;
                    }

                    return result != 0 ? result : left.CompareTo(right);
                });
            }

            _view = order.Select(index => rows[index]).ToList();
        }

        _viewIds = _view.Select(row => row.Id).ToList();
        if (_view.Count == 0)
        {
            _activeRow = 0;
            _activeColumn = 0;
        }
        else
        {
            _activeRow = Math.Clamp(_activeRow, 0, _view.Count - 1);
            _activeColumn = Math.Clamp(_activeColumn, 0, Math.Max(0, columns.Count - 1));
        }
    }

    private static int CompareRows(ArcGridRow left, ArcGridRow right, ArcGridColumn column)
    {
        var leftValue = Value(left, column);
        var rightValue = Value(right, column);
        var leftBlank = IsBlank(leftValue);
        var rightBlank = IsBlank(rightValue);
        if (leftBlank || rightBlank)
        {
            return leftBlank == rightBlank ? 0 : leftBlank ? 1 : -1;
        }

        if (leftValue is IComparable comparable && leftValue.GetType() == rightValue!.GetType())
        {
            return comparable.CompareTo(rightValue);
        }

        if (TryNumber(leftValue, out var leftNumber) && TryNumber(rightValue, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(leftValue?.ToString(), rightValue?.ToString(), StringComparison.OrdinalIgnoreCase);
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

    // ---- Toolbar ----------------------------------------------------------

    private void BuildToolbar()
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Children.Clear();
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
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
        root.Children.Add(left);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(FeedbackUi.Icon("columns-3", 14, ArcTokens.TextSecondary));
        var text = FeedbackUi.Text("Columns", "arc-body-sm");
        text.FontSize = 13;
        content.Children.Add(text);
        content.Children.Add(FeedbackUi.Icon("chevron-down", 12, ArcTokens.TextMuted));

        _columnsButton = new ArcButton
        {
            Content = content,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(12, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetName(_columnsButton, "Choose columns");
        ToolTip.SetTip(_columnsButton, "Show or hide columns");
        _columnsButton.Click += (_, _) => OpenColumnsMenu();
        Grid.SetColumn(_columnsButton, 1);
        root.Children.Add(_columnsButton);
        _toolbar.Children.Add(root);
        UpdateCountText();
    }

    private void UpdateCountText()
    {
        if (_countText is null)
        {
            return;
        }

        var selected = _checked.Count;
        var rows = $"{_view.Count} {(_view.Count == 1 ? "row" : "rows")}";
        _countText.Text = selected > 0 ? $"{rows} · {selected} selected" : rows;
    }

    private static Border MenuSurface(Control content)
    {
        var surface = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            MinWidth = 220,
            MaxWidth = 280,
            Child = content
        };
        surface.Paint(Border.BackgroundProperty, ArcTokens.SurfaceRaised);
        surface.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        surface.BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating);
        return surface;
    }

    private void OpenColumnsMenu()
    {
        if (_columnsPopup is null || _columnsButton is null)
        {
            return;
        }

        FillColumnsMenu();
        _columnsPopup.PlacementTarget = _columnsButton;
        _columnsPopup.IsOpen = true;
    }

    private void FillColumnsMenu()
    {
        if (_columnsMenu is null)
        {
            return;
        }

        _columnsMenu.Children.Clear();
        var heading = FeedbackUi.Text("Columns", "arc-caption");
        heading.Margin = new Thickness(10, 4, 10, 6);
        _columnsMenu.Children.Add(heading);

        var columns = Columns ?? Array.Empty<ArcGridColumn>();
        var visibleCount = columns.Count(column => !column.Hidden);
        foreach (var column in columns)
        {
            var visible = !column.Hidden;
            var lastVisible = visible && visibleCount == 1;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*") };
            if (visible)
            {
                row.Children.Add(FeedbackUi.Icon("check", 14, ArcTokens.AccentStrong));
            }
            else
            {
                row.Children.Add(new Border { Width = 14, Height = 14 });
            }

            var label = FeedbackUi.Text(column.Header, "arc-body-sm");
            label.Paint(TextBlock.ForegroundProperty, visible ? ArcTokens.Foreground : ArcTokens.TextMuted);
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            var item = new ArcFlatButton
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8),
                IsEnabled = !lastVisible
            };
            AutomationProperties.SetName(item, visible ? $"Hide {column.Header} column" : $"Show {column.Header} column");
            item.Click += (_, _) => ToggleColumn(column);
            _columnsMenu.Children.Add(item);
        }

        if (columns.Any(column => column.Hidden))
        {
            var divider = new Border
            {
                Height = 1,
                Margin = new Thickness(6, 4, 6, 4)
            };
            divider.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
            _columnsMenu.Children.Add(divider);
            var showAll = new ArcFlatButton
            {
                Text = "Show all",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8)
            };
            showAll.Click += (_, _) =>
            {
                foreach (var column in columns)
                {
                    column.Hidden = false;
                }

                RebuildContent();
                FillColumnsMenu();
            };
            _columnsMenu.Children.Add(showAll);
        }
    }

    private void ToggleColumn(ArcGridColumn column)
    {
        var visibleCount = (Columns ?? Array.Empty<ArcGridColumn>()).Count(item => !item.Hidden);
        if (!column.Hidden && visibleCount <= 1)
        {
            return;
        }

        column.Hidden = !column.Hidden;
        _anchorRow = -1;
        if (column.Key == _sortKey && column.Hidden)
        {
            _sortKey = null;
            _sortDescending = false;
        }

        RebuildContent();
        FillColumnsMenu();
    }

    // ---- Header -----------------------------------------------------------

    private void BuildHeader()
    {
        if (_headerHost is null)
        {
            return;
        }

        _headerHost.Child = null;
        _headerButtons.Clear();
        var visible = VisibleColumns();
        var grid = CreateLayoutGrid(visible);
        var offset = Selectable ? 1 : 0;

        if (Selectable)
        {
            var all = _viewIds.Count > 0 && _viewIds.All(id => _checked.Contains(id));
            var mixed = !all && _viewIds.Any(id => _checked.Contains(id));
            var check = BuildCheck(
                all,
                mixed,
                focusable: true,
                "Select all rows",
                "Checks every visible row",
                selectAll: true);
            var host = new Border
            {
                Child = check,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(host, 0);
            grid.Children.Add(host);
        }

        for (var index = 0; index < visible.Count; index++)
        {
            var column = visible[index];
            var sorted = _sortKey == column.Key;
            var cell = new Grid { MinHeight = 42 };
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = AlignToAlignment(column.Align)
            };
            var label = FeedbackUi.Text(column.Header, "arc-label");
            label.FontSize = ArcResources.Double(ArcTokens.TextXs);
            label.FontWeight = FontWeight.Medium;
            label.Paint(TextBlock.ForegroundProperty, sorted ? ArcTokens.Foreground : ArcTokens.TextSecondary);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            content.Children.Add(label);

            if (column.Sortable)
            {
                var icon = sorted
                    ? (_sortDescending ? "arrow-down" : "arrow-up")
                    : "arrow-up-down";
                var indicator = FeedbackUi.Icon(icon, 12, sorted ? ArcTokens.AccentStrong : ArcTokens.TextMuted);
                indicator.Opacity = sorted ? 1 : 0.4;
                content.Children.Add(indicator);
            }

            var button = new ArcFlatButton
            {
                Content = content,
                Padding = new Thickness(12, 0),
                MinHeight = 36,
                Margin = new Thickness(2, 3, 2, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = AlignToAlignment(column.Align),
                Cursor = column.Sortable ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow)
            };
            var captured = column;
            var direction = sorted
                ? _sortDescending ? ", currently descending" : ", currently ascending"
                : string.Empty;
            AutomationProperties.SetName(button, column.Sortable
                ? $"Sort by {column.Header}{direction}"
                : column.Header);
            if (column.Sortable)
            {
                button.Click += (_, _) => ToggleSort(captured);
            }

            if (column.Resizable)
            {
                button.KeyDown += (_, args) => ResizeFromKeyboard(captured, grid, args);
            }

            _headerButtons[column.Key] = button;

            Grid.SetColumn(button, 0);
            cell.Children.Add(button);

            if (index < visible.Count - 1)
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

            if (column.Resizable)
            {
                var handle = BuildResizeHandle(column, grid);
                cell.Children.Add(handle);
            }

            Grid.SetColumn(cell, offset + index);
            grid.Children.Add(cell);
        }

        _headerHost.Child = grid;
    }

    private Control BuildResizeHandle(ArcGridColumn column, Grid grid)
    {
        var handle = new Border
        {
            Width = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            ZIndex = 2
        };
        AutomationProperties.SetName(handle, $"Resize {column.Header} column");
        ToolTip.SetTip(handle, "Drag to resize; Alt + Left or Right resizes 16 px");

        handle.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var visible = VisibleColumns();
            var columnIndex = visible.IndexOf(column);
            var definitionIndex = (Selectable ? 1 : 0) + columnIndex;
            if (definitionIndex < 0 || definitionIndex >= grid.ColumnDefinitions.Count)
            {
                return;
            }

            if (column.Width <= 0)
            {
                column.Width = Math.Max(column.MinWidth, grid.ColumnDefinitions[definitionIndex].ActualWidth);
            }

            _resizing = true;
            _resizeColumn = column;
            _resizeStartX = args.GetPosition(this).X;
            _resizeStartWidth = column.Width;
            args.Pointer.Capture(handle);
            args.Handled = true;
        };

        handle.PointerMoved += (_, args) =>
        {
            if (!_resizing || !ReferenceEquals(_resizeColumn, column))
            {
                return;
            }

            var width = Math.Clamp(_resizeStartWidth + args.GetPosition(this).X - _resizeStartX, column.MinWidth, 560);
            ApplyColumnWidth(column, width);
            args.Handled = true;
        };

        handle.PointerReleased += (_, args) =>
        {
            if (!ReferenceEquals(_resizeColumn, column))
            {
                return;
            }

            EndResize(args.Pointer);
            args.Handled = true;
        };

        handle.PointerCaptureLost += (_, _) => EndResize(null);
        return handle;
    }

    private void EndResize(IPointer? pointer)
    {
        _resizing = false;
        _resizeColumn = null;
        pointer?.Capture(null);
    }

    private void ResizeFromKeyboard(ArcGridColumn column, Grid grid, KeyEventArgs args)
    {
        if (!args.KeyModifiers.HasFlag(KeyModifiers.Alt) || !column.Resizable)
        {
            return;
        }

        if (args.Key is not (Key.Left or Key.Right))
        {
            return;
        }

        var visible = VisibleColumns();
        var definitionIndex = (Selectable ? 1 : 0) + visible.IndexOf(column);
        if (definitionIndex < 0 || definitionIndex >= grid.ColumnDefinitions.Count)
        {
            return;
        }

        var current = column.Width > 0 ? column.Width : grid.ColumnDefinitions[definitionIndex].ActualWidth;
        var delta = args.Key == Key.Right ? 16 : -16;
        ApplyColumnWidth(column, Math.Clamp(current + delta, column.MinWidth, 560));
        args.Handled = true;
    }

    private void ApplyColumnWidth(ArcGridColumn column, double width)
    {
        column.Width = Math.Max(column.MinWidth, width);
        var visible = VisibleColumns();
        var index = visible.IndexOf(column);
        if (index < 0)
        {
            return;
        }

        foreach (var grid in _layoutGrids)
        {
            var definitionIndex = (Selectable ? 1 : 0) + index;
            if (definitionIndex < grid.ColumnDefinitions.Count)
            {
                grid.ColumnDefinitions[definitionIndex].Width = new GridLength(column.Width, GridUnitType.Pixel);
            }
        }
    }

    private void ToggleSort(ArcGridColumn column)
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

        _viewFrozen = false;
        _anchorRow = -1;
        RebuildContent();
        RaiseSortChanged(_sortKey is null
            ? null
            : new ArcGridSort { Key = _sortKey, Descending = _sortDescending });
        Dispatcher.UIThread.Post(() =>
        {
            if (_headerButtons.TryGetValue(column.Key, out var button))
            {
                button.Focus();
            }
        }, DispatcherPriority.Input);
    }

    private Grid CreateLayoutGrid(IReadOnlyList<ArcGridColumn> visible)
    {
        var grid = new Grid();
        if (Selectable)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(44, GridUnitType.Pixel));
        }

        foreach (var column in visible)
        {
            var definition = column.Width > 0
                ? new ColumnDefinition(column.Width, GridUnitType.Pixel)
                : new ColumnDefinition(1, GridUnitType.Star);
            definition.MinWidth = column.MinWidth;
            definition.MaxWidth = 560;
            grid.ColumnDefinitions.Add(definition);
        }

        _layoutGrids.Add(grid);
        return grid;
    }

    // ---- Body -------------------------------------------------------------

    private void BuildBody()
    {
        if (_bodyHost is null)
        {
            return;
        }

        _bodyHost.Children.Clear();
        if (VisibleColumns().Count == 0)
        {
            _bodyHost.Children.Add(new Border
            {
                MinHeight = 180,
                Padding = new Thickness(24),
                Child = new ArcEmptyState
                {
                    Icon = "columns-3",
                    Title = "No columns to show",
                    Description = "Show at least one column from the Columns menu.",
                    Label = $"{Label} has no visible columns"
                }
            });
            return;
        }

        if (_view.Count == 0)
        {
            var icon = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = FeedbackUi.Icon("table", 20, ArcTokens.TextSecondary)
            };
            icon.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);

            var title = FeedbackUi.Text(EmptyMessage, "arc-label");
            title.FontSize = ArcResources.Double(ArcTokens.TextSm);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.TextAlignment = TextAlignment.Center;
            var description = FeedbackUi.Text(EmptyDescription);
            description.FontSize = ArcResources.Double(ArcTokens.TextXs);
            description.TextAlignment = TextAlignment.Center;
            description.TextWrapping = TextWrapping.Wrap;
            description.MaxWidth = 420;
            description.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
            var content = new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 20, 24, 20),
                Children = { icon, title, description }
            };
            AutomationProperties.SetName(content, $"{Label} empty. {EmptyMessage}. {EmptyDescription}");
            _bodyHost.Children.Add(new Border { MinHeight = 160, Child = content });
            return;
        }

        var rows = new StackPanel();
        for (var index = 0; index < _view.Count; index++)
        {
            rows.Children.Add(BuildRow(_view[index], index));
        }

        _bodyHost.Children.Add(rows);
    }

    private Control BuildRow(ArcGridRow row, int viewIndex)
    {
        var visible = VisibleColumns();
        var grid = CreateLayoutGrid(visible);
        var offset = Selectable ? 1 : 0;
        var isChecked = _checked.Contains(row.Id);
        var zebra = Zebra && viewIndex % 2 == 1;

        if (Selectable)
        {
            var check = BuildCheck(
                isChecked,
                false,
                focusable: false,
                isChecked ? $"Deselect row {viewIndex + 1}" : $"Select row {viewIndex + 1}",
                "Shift-click checks the whole range",
                selectAll: false,
                rowIndex: viewIndex);
            var host = new Border
            {
                Child = check,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(host, 0);
            grid.Children.Add(host);
        }

        for (var index = 0; index < visible.Count; index++)
        {
            var column = visible[index];
            var value = Value(row, column);
            var text = CellText(value, column);
            var active = _activeRow == viewIndex && _activeColumn == index;
            var editing = _editing && _editRow == viewIndex && _editColumn == index;

            var cell = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent
            };
            if (active)
            {
                cell.Paint(Border.BorderBrushProperty, ArcTokens.AccentStrong);
            }

            AutomationProperties.SetName(cell, $"{column.Header}: {text}");

            if (editing)
            {
                cell.Child = BuildEditor(row, column);
            }
            else
            {
                var label = new TextBlock
                {
                    Text = text,
                    FontSize = ArcResources.Double(ArcTokens.TextSm),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = column.Align switch
                    {
                        ArcGridAlign.Right => TextAlignment.Right,
                        ArcGridAlign.Center => TextAlignment.Center,
                        _ => TextAlignment.Left
                    },
                    Padding = new Thickness(10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                if (column.Align == ArcGridAlign.Right)
                {
                    label.FontFeatures = FeedbackUi.Tabular;
                }

                label.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                cell.Child = label;
            }

            var capturedRow = viewIndex;
            var capturedColumn = index;
            cell.PointerPressed += (_, args) =>
            {
                if (!args.GetCurrentPoint(cell).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                SetActiveCell(capturedRow, capturedColumn, focus: true);
                if (args.ClickCount >= 2 && !_editing)
                {
                    BeginEdit(capturedRow, capturedColumn);
                }

                args.Handled = true;
            };

            Grid.SetColumn(cell, offset + index);
            grid.Children.Add(cell);
            _cellFrames[(viewIndex, index)] = cell;
        }

        var accentStrip = new Border
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        if (isChecked)
        {
            accentStrip.Paint(Border.BackgroundProperty, ArcTokens.Accent);
        }

        var content = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
            MinHeight = RowHeight
        };
        content.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        content.Paint(Border.BackgroundProperty, isChecked
            ? ArcTokens.AccentSubtle
            : zebra ? ArcTokens.SurfaceMuted : ArcTokens.Surface);
        if (!ArcThemeManager.ReducedMotion)
        {
            content.Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(160)
                }
            };
        }

        content.PointerEntered += (_, _) => content.Paint(
            Border.BackgroundProperty,
            isChecked ? ArcTokens.AccentSubtle : zebra ? ArcTokens.BorderSubtle : ArcTokens.SurfaceMuted);
        content.PointerExited += (_, _) => content.Paint(
            Border.BackgroundProperty,
            isChecked ? ArcTokens.AccentSubtle : zebra ? ArcTokens.SurfaceMuted : ArcTokens.Surface);

        var root = new Panel { MinHeight = RowHeight, ClipToBounds = true };
        root.Children.Add(content);
        root.Children.Add(accentStrip);
        return root;
    }

    private Control BuildCheck(
        bool isChecked,
        bool mixed,
        bool focusable,
        string name,
        string help,
        bool selectAll,
        int rowIndex = -1)
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

        if (isChecked || mixed)
        {
            box.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            box.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
            box.Child = FeedbackUi.Icon(mixed ? "minus" : "check", 12, ArcTokens.AccentForeground);
        }
        else
        {
            box.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            box.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
        }

        var button = new ArcFlatButton
        {
            Content = box,
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            Focusable = focusable,
            IsTabStop = focusable,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        AutomationProperties.SetName(button, name);
        AutomationProperties.SetHelpText(button, help);
        button.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (selectAll)
            {
                SelectAllRows();
            }
            else
            {
                ToggleRow(rowIndex, args.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }

            args.Handled = true;
        };
        return button;
    }

    private void SelectAllRows()
    {
        if (_viewIds.Count == 0)
        {
            return;
        }

        if (_viewIds.All(id => _checked.Contains(id)))
        {
            _checked.Clear();
        }
        else
        {
            foreach (var id in _viewIds)
            {
                _checked.Add(id);
            }
        }

        _anchorRow = -1;
        PublishSelection();
        RebuildContent();
    }

    private void ToggleRow(int viewIndex, bool shift)
    {
        if (viewIndex < 0 || viewIndex >= _viewIds.Count)
        {
            return;
        }

        var id = _viewIds[viewIndex];
        var value = !_checked.Contains(id);
        if (shift && _anchorRow >= 0)
        {
            var start = Math.Min(_anchorRow, viewIndex);
            var end = Math.Max(_anchorRow, viewIndex);
            for (var i = start; i <= end; i++)
            {
                if (value)
                {
                    _checked.Add(_viewIds[i]);
                }
                else
                {
                    _checked.Remove(_viewIds[i]);
                }
            }
        }
        else if (value)
        {
            _checked.Add(id);
        }
        else
        {
            _checked.Remove(id);
        }

        _anchorRow = viewIndex;
        PublishSelection();
        RebuildContent();
    }

    private void PublishSelection()
    {
        var ordered = _viewIds.Where(id => _checked.Contains(id)).ToList();
        _syncingSelection = true;
        SetCurrentValue(SelectedIdsProperty, ordered);
        _syncingSelection = false;
        RaiseSelectionChanged(ordered);
    }

    // ---- Footer -----------------------------------------------------------

    private void BuildFooter()
    {
        if (_footerHost is null)
        {
            return;
        }

        _footerHost.Child = null;
        if (!ShowTotals)
        {
            return;
        }

        var visible = VisibleColumns();
        if (visible.Count == 0)
        {
            return;
        }

        var grid = CreateLayoutGrid(visible);
        var offset = Selectable ? 1 : 0;

        for (var index = 0; index < visible.Count; index++)
        {
            var column = visible[index];
            var cell = new Grid { MinHeight = 42 };
            var text = index == 0
                ? CountSummary()
                : TotalText(column);
            var label = new TextBlock
            {
                Text = text,
                FontSize = ArcResources.Double(ArcTokens.TextXs),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = index == 0 ? TextAlignment.Left : column.Align switch
                {
                    ArcGridAlign.Right => TextAlignment.Right,
                    ArcGridAlign.Center => TextAlignment.Center,
                    _ => TextAlignment.Left
                },
                Padding = new Thickness(12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontFeatures = FeedbackUi.Tabular
            };
            label.Paint(TextBlock.ForegroundProperty, index == 0 ? ArcTokens.TextSecondary : ArcTokens.TextMuted);
            if (index > 0)
            {
                AutomationProperties.SetName(label, $"{column.Header} total {text}");
            }

            cell.Children.Add(label);
            Grid.SetColumn(cell, offset + index);
            grid.Children.Add(cell);
        }

        _footerHost.Child = grid;
    }

    private string CountSummary()
    {
        var rows = $"{_view.Count} {(_view.Count == 1 ? "row" : "rows")}";
        if (!Selectable)
        {
            return rows;
        }

        var selected = _checked.Count(id => _viewIds.Contains(id));
        return selected > 0 ? $"{rows} · {selected} selected" : rows;
    }

    private string TotalText(ArcGridColumn column)
    {
        if (!column.ShowTotal || column.Align != ArcGridAlign.Right)
        {
            return string.Empty;
        }

        var values = _view
            .Select(row => Value(row, column))
            .Where(value => TryNumber(value, out _))
            .Select(value => Convert.ToDouble(value, CultureInfo.InvariantCulture))
            .ToList();
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var sum = values.Sum();
        var formatted = column.Format is not null
            ? column.Format(sum)
            : sum.ToString("#,##0.##", CultureInfo.InvariantCulture);
        return $"Σ {formatted}";
    }

    // ---- Editing ----------------------------------------------------------

    private Control BuildEditor(ArcGridRow row, ArcGridColumn column)
    {
        var box = new TextBox
        {
            Text = RawText(Value(row, column)),
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Padding = new Thickness(8, 2, 8, 2),
            MinHeight = 34,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = column.Align == ArcGridAlign.Right ? TextAlignment.Right : TextAlignment.Left
        };
        box.Paint(TextBox.BackgroundProperty, ArcTokens.Surface);
        box.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);

        AutomationProperties.SetName(box, $"Edit {column.Header} for {row.Id}");
        box.KeyDown += (_, args) =>
        {
            switch (args.Key)
            {
                case Key.Enter:
                    CommitEdit(moveDown: !args.KeyModifiers.HasFlag(KeyModifiers.Shift), moveRight: false, backwards: false);
                    args.Handled = true;
                    break;
                case Key.Tab:
                    CommitEdit(moveDown: false, moveRight: true, backwards: args.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    args.Handled = true;
                    break;
                case Key.Escape:
                    CancelEdit();
                    args.Handled = true;
                    break;
            }
        };
        box.LostFocus += (_, _) =>
        {
            if (_editing && ReferenceEquals(_editor, box))
            {
                CommitEdit(moveDown: false, moveRight: false, backwards: false);
            }
        };

        _editor = box;
        Dispatcher.UIThread.Post(() =>
        {
            if (_editing && ReferenceEquals(_editor, box))
            {
                box.Focus();
                box.SelectAll();
            }
        }, DispatcherPriority.Input);
        return box;
    }

    private void BeginEdit(int viewIndex, int columnIndex)
    {
        if (!Editable || _editing || viewIndex < 0 || viewIndex >= _view.Count)
        {
            return;
        }

        var visible = VisibleColumns();
        if (columnIndex < 0 || columnIndex >= visible.Count)
        {
            return;
        }

        var column = visible[columnIndex];
        if (!column.Editable || column.ValueGetter is not null)
        {
            return;
        }

        _editing = true;
        _editRow = viewIndex;
        _editColumn = columnIndex;
        RebuildContent();
    }

    private void CommitEdit(bool moveDown, bool moveRight, bool backwards)
    {
        if (!_editing || _editor is null || _committing)
        {
            return;
        }

        _committing = true;
        var viewRow = _editRow;
        var columnIndex = _editColumn;
        var text = _editor.Text ?? string.Empty;
        _editing = false;
        _editor = null;

        var visible = VisibleColumns();
        if (viewRow >= 0 && viewRow < _view.Count && columnIndex >= 0 && columnIndex < visible.Count)
        {
            var row = _view[viewRow];
            var column = visible[columnIndex];
            var oldValue = Value(row, column);
            var newValue = ParseCell(text, column);
            if (!Equals(oldValue, newValue))
            {
                if (column.ValueGetter is null)
                {
                    row[column.Key] = newValue;
                }

                RaiseCellEdited(new ArcGridCellEdit
                {
                    Row = row,
                    Column = column,
                    OldValue = oldValue,
                    NewValue = newValue
                });
            }

            if (moveDown)
            {
                _activeRow = Math.Clamp(viewRow + 1, 0, _view.Count - 1);
                _activeColumn = columnIndex;
            }
            else if (moveRight)
            {
                _activeRow = viewRow;
                _activeColumn = Math.Clamp(columnIndex + (backwards ? -1 : 1), 0, visible.Count - 1);
            }
        }

        _committing = false;
        _viewFrozen = true;
        RebuildContent();
        Focus();
    }

    private void CancelEdit()
    {
        if (!_editing)
        {
            return;
        }

        _editing = false;
        _editor = null;
        RebuildContent();
        Focus();
    }

    private static object? ParseCell(string text, ArcGridColumn column)
    {
        if (column.Align != ArcGridAlign.Right)
        {
            return text;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var percent = trimmed.EndsWith("%", StringComparison.Ordinal);
        var negative = trimmed.StartsWith("(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal);
        trimmed = trimmed.Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            if (percent)
            {
                number /= 100;
            }

            return negative ? -number : number;
        }

        return text;
    }

    // ---- Interaction ------------------------------------------------------

    private void SetActiveCell(int viewRow, int columnIndex, bool focus)
    {
        _activeRow = viewRow;
        _activeColumn = columnIndex;
        if (focus)
        {
            Focus();
        }

        RefreshActiveCell();
    }

    private void RefreshActiveCell()
    {
        foreach (var frame in _cellFrames.Values)
        {
            frame.BorderBrush = Brushes.Transparent;
        }

        if (_cellFrames.TryGetValue((_activeRow, _activeColumn), out var active))
        {
            active.Paint(Border.BorderBrushProperty, ArcTokens.AccentStrong);
            active.BringIntoView();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_editing || _view.Count == 0)
        {
            return;
        }

        var visible = VisibleColumns();
        if (visible.Count == 0)
        {
            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var page = Math.Max(1, (int)((_scroll?.Bounds.Height ?? 400) / RowHeight) - 1);

        switch (e.Key)
        {
            case Key.Left:
                _activeColumn = control ? 0 : Math.Max(0, _activeColumn - 1);
                e.Handled = true;
                break;
            case Key.Right:
                _activeColumn = control ? visible.Count - 1 : Math.Min(visible.Count - 1, _activeColumn + 1);
                e.Handled = true;
                break;
            case Key.Up:
                _activeRow = control ? 0 : Math.Max(0, _activeRow - 1);
                e.Handled = true;
                break;
            case Key.Down:
                _activeRow = control ? _view.Count - 1 : Math.Min(_view.Count - 1, _activeRow + 1);
                e.Handled = true;
                break;
            case Key.Home:
                if (control)
                {
                    _activeRow = 0;
                    _activeColumn = 0;
                }
                else
                {
                    _activeColumn = 0;
                }

                e.Handled = true;
                break;
            case Key.End:
                if (control)
                {
                    _activeRow = _view.Count - 1;
                    _activeColumn = visible.Count - 1;
                }
                else
                {
                    _activeColumn = visible.Count - 1;
                }

                e.Handled = true;
                break;
            case Key.PageUp:
                _activeRow = Math.Max(0, _activeRow - page);
                e.Handled = true;
                break;
            case Key.PageDown:
                _activeRow = Math.Min(_view.Count - 1, _activeRow + page);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.F2:
                BeginEdit(_activeRow, _activeColumn);
                e.Handled = true;
                break;
            case Key.Space:
                if (Selectable && _activeRow >= 0 && _activeRow < _viewIds.Count)
                {
                    ToggleRow(_activeRow, shift: false);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                if (_checked.Count > 0)
                {
                    _checked.Clear();
                    PublishSelection();
                    RebuildContent();
                    e.Handled = true;
                }

                break;
            default:
                return;
        }

        RefreshActiveCell();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        RefreshActiveCell();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_editing)
        {
            RefreshActiveCell();
        }
    }

    private void UpdateAutomation()
    {
        var rows = Rows?.Count ?? 0;
        var columns = VisibleColumns().Count;
        AutomationProperties.SetName(this, $"{Label}, {rows} rows, {columns} columns");
        AutomationProperties.SetHelpText(
            this,
            "Arrow keys move between cells. Enter or F2 edits. Space checks the row. Shift-click checks a range.");
    }

    // ---- Values -----------------------------------------------------------

    private static object? Value(ArcGridRow row, ArcGridColumn column) =>
        column.ValueGetter is not null ? column.ValueGetter(row) : row[column.Key];

    private static string CellText(object? value, ArcGridColumn column)
    {
        if (column.Format is not null)
        {
            return column.Format(value);
        }

        if (value is null)
        {
            return "—";
        }

        if (column.Align == ArcGridAlign.Right && TryNumber(value, out var number))
        {
            return number.ToString("#,##0.##", CultureInfo.InvariantCulture);
        }

        if (value is DateTime date)
        {
            return date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }

    private static string RawText(object? value) => value switch
    {
        null => string.Empty,
        double number => number.ToString("0.##", CultureInfo.InvariantCulture),
        float number => number.ToString("0.##", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static HorizontalAlignment AlignToAlignment(ArcGridAlign align) => align switch
    {
        ArcGridAlign.Right => HorizontalAlignment.Right,
        ArcGridAlign.Center => HorizontalAlignment.Center,
        _ => HorizontalAlignment.Left
    };
}

/// <summary>Event data for the bubbling <see cref="SortChangedRouted"/> event.</summary>
public sealed class ArcGridSortChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcGridSortChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public ArcGridSort? Sort { get; init; }
}

/// <summary>Event data for the bubbling <see cref="SelectionChangedRouted"/> event.</summary>
public sealed class ArcGridSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcGridSelectionChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public IReadOnlyList<string> SelectedKeys { get; init; } = null!;
}

/// <summary>Event data for the bubbling <see cref="CellEditedRouted"/> event.</summary>
public sealed class ArcGridCellEditedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data for a routed event.</summary>
    public ArcGridCellEditedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The value carried by the event.</summary>
    public ArcGridCellEdit Edit { get; init; } = null!;
}
