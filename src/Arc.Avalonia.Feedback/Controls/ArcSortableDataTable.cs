using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcSortDirection
{
    Asc,
    Desc
}

/// <summary>Current sort of an <see cref="ArcSortableDataTable"/>.</summary>
public sealed class ArcTableSort
{
    public string Key { get; set; } = string.Empty;

    public ArcSortDirection Direction { get; set; }
}

/// <summary>One column of an <see cref="ArcSortableDataTable"/>.</summary>
public sealed class ArcTableColumn
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Columns sort unless this is false.</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>Right-aligns the cell and uses tabular figures.</summary>
    public bool Numeric { get; set; }

    /// <summary>Fixed width in px; zero shares the remaining width.</summary>
    public double Width { get; set; }

    /// <summary>Optional cell formatter.</summary>
    public Func<object?, string>? Format { get; set; }
}

/// <summary>Nouns for the selection count line.</summary>
public sealed class ArcTableItemName
{
    public string One { get; set; } = "row";

    public string Other { get; set; } = "rows";
}

/// <summary>
/// A structured-records table with sortable headers, optional selection, zebra rows, right-aligned
/// numeric columns with tabular figures, and an empty state. Rows are plain objects or dictionaries.
/// </summary>
public class ArcSortableDataTable : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<object>?> RowsProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, IEnumerable<object>?>(nameof(Rows));

    public static readonly StyledProperty<IEnumerable<ArcTableColumn>?> ColumnsProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, IEnumerable<ArcTableColumn>?>(nameof(Columns));

    public static readonly StyledProperty<Func<object, string>?> RowKeyProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, Func<object, string>?>(nameof(RowKey));

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, string>(nameof(Caption), "Data table");

    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, string>(nameof(EmptyMessage), "No rows to show");

    public static readonly StyledProperty<ArcTableSort?> DefaultSortProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, ArcTableSort?>(nameof(DefaultSort));

    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, bool>(nameof(Selectable));

    public static readonly StyledProperty<IEnumerable<string>?> SelectedKeysProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, IEnumerable<string>?>(nameof(SelectedKeys));

    public static readonly StyledProperty<ArcTableItemName?> ItemNameProperty =
        AvaloniaProperty.Register<ArcSortableDataTable, ArcTableItemName?>(nameof(ItemName));

    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly List<Border> _rowFrames = new();
    private readonly List<string> _rowKeys = new();
    private Panel? _host;
    private StackPanel? _body;
    private Panel? _footer;
    private ArcTableSort? _sort;
    private string? _anchorKey;
    private int _focusIndex;
    private bool _keyboardFocus;

    static ArcSortableDataTable()
    {
        RowsProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.Rebuild());
        ColumnsProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.Rebuild());
        RowKeyProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.Rebuild());
        CaptionProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.UpdateAutomation());
        EmptyMessageProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.Rebuild());
        DefaultSortProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.ApplyDefaultSort());
        SelectableProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.Rebuild());
        SelectedKeysProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.SyncSelection());
        ItemNameProperty.Changed.AddClassHandler<ArcSortableDataTable>((table, _) => table.UpdateFooter());
    }

    public ArcSortableDataTable()
    {
        Focusable = true;
    }

    /// <summary>Row objects; each column reads a property or dictionary key.</summary>
    public IEnumerable<object>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public IEnumerable<ArcTableColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Stable key per row; defaults to an "id" member or the row index.</summary>
    public Func<object, string>? RowKey
    {
        get => GetValue(RowKeyProperty);
        set => SetValue(RowKeyProperty, value);
    }

    /// <summary>Table caption and accessible name.</summary>
    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string EmptyMessage
    {
        get => GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public ArcTableSort? DefaultSort
    {
        get => GetValue(DefaultSortProperty);
        set => SetValue(DefaultSortProperty, value);
    }

    /// <summary>Adds row selection, the count line, and Clear.</summary>
    public bool Selectable
    {
        get => GetValue(SelectableProperty);
        set => SetValue(SelectableProperty, value);
    }

    public IEnumerable<string>? SelectedKeys
    {
        get => GetValue(SelectedKeysProperty);
        set => SetValue(SelectedKeysProperty, value);
    }

    public ArcTableItemName? ItemName
    {
        get => GetValue(ItemNameProperty);
        set => SetValue(ItemNameProperty, value);
    }

    public event EventHandler<ArcTableSort>? SortChanged;

    public event EventHandler<IReadOnlyList<string>>? SelectionChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        ApplyDefaultSort();
    }

    private void ApplyDefaultSort()
    {
        _sort = DefaultSort is null
            ? null
            : new ArcTableSort { Key = DefaultSort.Key, Direction = DefaultSort.Direction };
        Rebuild();
    }

    private void SyncSelection()
    {
        _selected.Clear();
        foreach (var key in SelectedKeys ?? Enumerable.Empty<string>())
        {
            _selected.Add(key);
        }

        Rebuild();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Caption);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _rowFrames.Clear();
        _rowKeys.Clear();
        _body = null;
        _footer = null;

        var columns = (Columns ?? Array.Empty<ArcTableColumn>()).ToList();
        if (columns.Count == 0)
        {
            return;
        }

        var rows = (Rows ?? Array.Empty<object>()).ToList();
        var keys = rows.Select((row, index) => RowKey?.Invoke(row) ?? DefaultKey(row, index)).ToList();
        var order = Enumerable.Range(0, rows.Count).ToList();
        if (_sort is not null)
        {
            var column = columns.FirstOrDefault(item => item.Key == _sort.Key);
            if (column is not null)
            {
                order.Sort((left, right) =>
                {
                    var result = Compare(Value(rows[left], column.Key), Value(rows[right], column.Key));
                    return _sort.Direction == ArcSortDirection.Asc ? result : -result;
                });
            }
        }

        var shell = new Border
        {
            CornerRadius = ArcUi.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        shell.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        shell.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        var root = new StackPanel();

        var header = BuildHeader(columns, keys);
        root.Children.Add(header);
        root.Children.Add(ArcUi.Divider());

        _body = new StackPanel();
        if (rows.Count == 0)
        {
            _body.Children.Add(BuildEmpty());
        }
        else
        {
            var zebra = 0;
            foreach (var index in order)
            {
                _body.Children.Add(BuildRow(rows[index], columns, keys[index], zebra));
                zebra++;
            }
        }

        root.Children.Add(_body);
        shell.Child = root;
        _host.Children.Add(shell);

        UpdateFooter();
        UpdateAutomation();
        UpdateFocusVisuals();
    }

    private Control BuildHeader(IReadOnlyList<ArcTableColumn> columns, IReadOnlyList<string> keys)
    {
        var grid = new Grid();
        var firstColumn = 0;
        if (Selectable)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(44, GridUnitType.Pixel));
            var cell = new Border { Padding = new Thickness(0, 4) };
            var all = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            var selectedCount = keys.Count(key => _selected.Contains(key));
            var mixed = selectedCount > 0 && selectedCount < keys.Count;
            if (selectedCount > 0)
            {
                all.Paint(Border.BackgroundProperty, ArcTokens.Accent);
                all.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
                all.Child = ArcUi.Icon(mixed ? "minus" : "check", 12, ArcTokens.AccentForeground);
            }
            else
            {
                all.Paint(Border.BackgroundProperty, ArcTokens.Surface);
                all.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
            }

            AutomationProperties.SetName(all, mixed ? "Clear selection" : selectedCount > 0 ? "Clear selection" : "Select all rows");
            all.PointerPressed += (_, args) =>
            {
                if (_selected.Count > 0)
                {
                    _selected.Clear();
                }
                else
                {
                    foreach (var key in keys)
                    {
                        _selected.Add(key);
                    }
                }

                Rebuild();
                SelectionChanged?.Invoke(this, _selected.ToList());
                args.Handled = true;
            };
            cell.Child = all;
            grid.Children.Add(cell);
            firstColumn = 1;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            grid.ColumnDefinitions.Add(column.Width > 0
                ? new ColumnDefinition(column.Width, GridUnitType.Pixel)
                : new ColumnDefinition(1, GridUnitType.Star));

            var sorted = _sort?.Key == column.Key;
            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = column.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            var label = ArcUi.Text(column.Label, "arc-label");
            label.FontSize = ArcResources.Double(ArcTokens.TextXs);
            label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            content.Children.Add(label);

            if (column.Sortable)
            {
                content.Children.Add(ArcUi.Icon(
                    sorted ? (_sort!.Direction == ArcSortDirection.Asc ? "arrow-up" : "arrow-down") : "arrow-up-down",
                    12,
                    sorted ? ArcTokens.Foreground : ArcTokens.TextMuted));
            }

            var button = new ArcFlatButton
            {
                Content = content,
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = column.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(0)
            };
            var columnIndex = i;
            AutomationProperties.SetName(button, column.Sortable
                ? $"Sort by {column.Label}{(sorted ? $", currently {(_sort!.Direction == ArcSortDirection.Asc ? "ascending" : "descending")}" : string.Empty)}"
                : column.Label);
            if (column.Sortable)
            {
                button.Click += (_, _) => ToggleSort(column);
            }
            else
            {
                button.Cursor = new Cursor(StandardCursorType.Arrow);
            }

            Grid.SetColumn(button, firstColumn + columnIndex);
            grid.Children.Add(button);
        }

        return new Border { Child = grid, Background = Brushes.Transparent };
    }

    private Control BuildRow(object row, IReadOnlyList<ArcTableColumn> columns, string key, int zebra)
    {
        var grid = new Grid();
        var firstColumn = 0;
        var selected = _selected.Contains(key);

        if (Selectable)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(44, GridUnitType.Pixel));
            var cell = new Border { Padding = new Thickness(0, 2) };
            var box = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            if (selected)
            {
                box.Paint(Border.BackgroundProperty, ArcTokens.Accent);
                box.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
                box.Child = ArcUi.Icon("check", 12, ArcTokens.AccentForeground);
            }
            else
            {
                box.Paint(Border.BackgroundProperty, ArcTokens.Surface);
                box.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
            }

            AutomationProperties.SetName(box, selected ? $"Deselect {key}" : $"Select {key}");
            box.PointerPressed += (_, args) =>
            {
                ToggleSelection(key);
                args.Handled = true;
            };
            cell.Child = box;
            grid.Children.Add(cell);
            firstColumn = 1;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            grid.ColumnDefinitions.Add(column.Width > 0
                ? new ColumnDefinition(column.Width, GridUnitType.Pixel)
                : new ColumnDefinition(1, GridUnitType.Star));

            var text = CellText(row, column);
            var cell = new TextBlock
            {
                Text = text,
                FontSize = ArcResources.Double(ArcTokens.TextSm),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = column.Numeric ? TextAlignment.Right : TextAlignment.Left,
                Padding = new Thickness(12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            if (column.Numeric)
            {
                cell.FontFeatures = ArcUi.Tabular;
            }

            cell.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
            Grid.SetColumn(cell, firstColumn + i);
            grid.Children.Add(cell);
        }

        var frame = new Border
        {
            MinHeight = 44,
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Background = selected
                ? ArcResources.Brush(ArcTokens.AccentSubtle)
                : zebra % 2 == 1
                    ? ArcResources.Brush(ArcTokens.SurfaceMuted)
                    : Brushes.Transparent,
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        AutomationProperties.SetName(frame, $"{CellText(row, columns[0])}, {key}");
        frame.PointerPressed += (_, args) =>
        {
            var shift = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _keyboardFocus = false;
            Focus();
            if (Selectable)
            {
                if (shift && _anchorKey is not null)
                {
                    SelectRange(_anchorKey, key);
                }
                else
                {
                    ToggleSelection(key);
                    _anchorKey = key;
                }
            }

            args.Handled = true;
        };
        frame.PointerEntered += (_, _) =>
        {
            if (!selected && Selectable)
            {
                frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            }
        };
        frame.PointerExited += (_, _) =>
        {
            if (!_selected.Contains(key))
            {
                frame.Background = zebra % 2 == 1
                    ? ArcResources.Brush(ArcTokens.SurfaceMuted)
                    : Brushes.Transparent;
            }
        };

        _rowFrames.Add(frame);
        _rowKeys.Add(key);
        var index = _rowFrames.Count - 1;
        frame.Tag = index;
        return frame;
    }

    private Control BuildEmpty()
    {
        return new ArcEmptyState
        {
            Title = EmptyMessage,
            Description = "Adjust the filters, or add the first record.",
            Icon = "inbox",
            Action = null
        };
    }

    private void ToggleSort(ArcTableColumn column)
    {
        if (!column.Sortable)
        {
            return;
        }

        var ascending = _sort is null || _sort.Key != column.Key || _sort.Direction == ArcSortDirection.Desc;
        _sort = new ArcTableSort { Key = column.Key, Direction = ascending ? ArcSortDirection.Asc : ArcSortDirection.Desc };
        Rebuild();
        SortChanged?.Invoke(this, _sort);
    }

    private void ToggleSelection(string key)
    {
        if (!Selectable)
        {
            return;
        }

        if (!_selected.Add(key))
        {
            _selected.Remove(key);
        }

        Rebuild();
        SelectionChanged?.Invoke(this, _selected.ToList());
    }

    private void SelectRange(string from, string to)
    {
        var start = _rowKeys.IndexOf(from);
        var end = _rowKeys.IndexOf(to);
        if (start < 0 || end < 0)
        {
            return;
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        for (var i = start; i <= end; i++)
        {
            _selected.Add(_rowKeys[i]);
        }

        Rebuild();
        SelectionChanged?.Invoke(this, _selected.ToList());
    }

    private void UpdateFooter()
    {
        if (_host is null)
        {
            return;
        }

        if (_footer is not null)
        {
            _host.Children.Remove(_footer);
            _footer = null;
        }

        var rows = (Rows ?? Array.Empty<object>()).ToList();
        var names = ItemName ?? new ArcTableItemName();
        var text = Selectable && _selected.Count > 0
            ? $"{_selected.Count} {(_selected.Count == 1 ? names.One : names.Other)} selected"
            : $"{rows.Count} {(rows.Count == 1 ? names.One : names.Other)}";

        var line = new Grid { Margin = new Thickness(2, 8, 2, 0) };
        line.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        line.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var caption = ArcUi.Caption(text);
        line.Children.Add(caption);

        if (Selectable && _selected.Count > 0)
        {
            var clear = new ArcFlatButton { Text = "Clear", Quiet = true, HorizontalAlignment = HorizontalAlignment.Right };
            clear.Click += (_, _) =>
            {
                _selected.Clear();
                Rebuild();
                SelectionChanged?.Invoke(this, _selected.ToList());
            };
            Grid.SetColumn(clear, 1);
            line.Children.Add(clear);
        }

        _footer = line;
        _host.Children.Add(line);
    }

    private void UpdateFocusVisuals()
    {
        for (var i = 0; i < _rowFrames.Count; i++)
        {
            _rowFrames[i].Paint(Border.BorderBrushProperty, IsFocused && _keyboardFocus && i == _focusIndex ? ArcTokens.FocusOutline : ArcTokens.BorderSubtle);
            if (!IsFocused || !_keyboardFocus || i != _focusIndex)
            {
                _rowFrames[i].BorderBrush = Brushes.Transparent;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rowFrames.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                _focusIndex = Math.Min(_focusIndex + 1, _rowFrames.Count - 1);
                _keyboardFocus = true;
                UpdateFocusVisuals();
                _rowFrames[_focusIndex].BringIntoView();
                e.Handled = true;
                break;
            case Key.Up:
                _focusIndex = Math.Max(_focusIndex - 1, 0);
                _keyboardFocus = true;
                UpdateFocusVisuals();
                _rowFrames[_focusIndex].BringIntoView();
                e.Handled = true;
                break;
            case Key.Home:
                _focusIndex = 0;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.End:
                _focusIndex = _rowFrames.Count - 1;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.Space:
            case Key.Enter:
                if (Selectable && _focusIndex < _rowKeys.Count)
                {
                    ToggleSelection(_rowKeys[_focusIndex]);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                if (_selected.Count > 0)
                {
                    _selected.Clear();
                    Rebuild();
                    SelectionChanged?.Invoke(this, _selected.ToList());
                    e.Handled = true;
                }

                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateFocusVisuals();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusVisuals();
    }

    private static string DefaultKey(object row, int index)
    {
        var id = Value(row, "id");
        return id?.ToString() ?? index.ToString(CultureInfo.InvariantCulture);
    }

    private static string CellText(object row, ArcTableColumn column)
    {
        var value = Value(row, column.Key);
        if (column.Format is not null)
        {
            return column.Format(value);
        }

        if (value is null)
        {
            return "—";
        }

        if (column.Numeric && value is IConvertible)
        {
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("#,##0.##", CultureInfo.InvariantCulture);
            }
            catch
            {
                // Fall through to the plain string form.
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private static object? Value(object row, string key)
    {
        if (row is IReadOnlyDictionary<string, object?> readOnly && readOnly.TryGetValue(key, out var found))
        {
            return found;
        }

        if (row is IDictionary<string, object?> dictionary && dictionary.TryGetValue(key, out var other))
        {
            return other;
        }

        if (row is IDictionary plain && plain.Contains(key))
        {
            return plain[key];
        }

        var property = row.GetType().GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        return property?.GetValue(row);
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        if (left is IConvertible && right is IConvertible)
        {
            try
            {
                return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                    .CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
            }
            catch
            {
                // Strings next.
            }
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

