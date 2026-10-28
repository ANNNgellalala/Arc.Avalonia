using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Controls;

public enum ArcJsonValueType
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}

/// <summary>
/// A collapsible JSON tree with type-coloured values, search that opens matching branches,
/// paging for long arrays, and per-row copy of the value or path.
/// </summary>
public class ArcJsonViewer : TemplatedControl
{
    public static readonly StyledProperty<object?> DataProperty =
        AvaloniaProperty.Register<ArcJsonViewer, object?>(nameof(Data));

    public static readonly StyledProperty<string> RootNameProperty =
        AvaloniaProperty.Register<ArcJsonViewer, string>(nameof(RootName), "root");

    public static readonly StyledProperty<int> DefaultExpandDepthProperty =
        AvaloniaProperty.Register<ArcJsonViewer, int>(nameof(DefaultExpandDepth), 1);

    public static readonly StyledProperty<bool> SearchableProperty =
        AvaloniaProperty.Register<ArcJsonViewer, bool>(nameof(Searchable), true);

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<ArcJsonViewer, string?>(nameof(Query));

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<ArcJsonViewer, int>(nameof(PageSize), 50);

    public static readonly StyledProperty<bool> CopyableProperty =
        AvaloniaProperty.Register<ArcJsonViewer, bool>(nameof(Copyable), true);

    public static readonly StyledProperty<bool> ShowPathProperty =
        AvaloniaProperty.Register<ArcJsonViewer, bool>(nameof(ShowPath), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcJsonViewer, string>(nameof(Label), "JSON");

    /// <summary>Height of the scrolling tree area; the port of the documented maxHeight.</summary>
    public static readonly StyledProperty<double> ScrollHeightProperty =
        AvaloniaProperty.Register<ArcJsonViewer, double>(nameof(ScrollHeight), 420d);

    private sealed class JsonRow
    {
        public string Path { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public ArcJsonValueType Kind { get; init; }
        public string? Value { get; init; }
        public string? Hint { get; init; }
        public int Depth { get; init; }
        public bool IsBranch { get; init; }
        public bool Expanded { get; init; }
        public bool IsMore { get; init; }
        public string CopyText { get; init; } = string.Empty;
        public Border Frame { get; set; } = null!;
    }

    private readonly List<JsonRow> _rows = new();
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _autoExpanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pages = new(StringComparer.Ordinal);

    private Panel? _host;
    private StackPanel? _tree;
    private ScrollViewer? _scroller;
    private TextBlock? _pathText;
    private TextBlock? _statusText;
    private TextBlock? _matchText;
    private TextBox? _search;
    private JsonElement? _root;
    private string _selectedPath = string.Empty;
    private bool _keyboardFocus;
    private DispatcherTimer? _statusTimer;

    static ArcJsonViewer()
    {
        DataProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.ResetData());
        RootNameProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.ResetData());
        DefaultExpandDepthProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.ResetData());
        SearchableProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.BuildShell());
        PageSizeProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.Refresh());
        CopyableProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.Refresh());
        ShowPathProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.BuildShell());
        QueryProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => viewer.ApplyQuery());
        LabelProperty.Changed.AddClassHandler<ArcJsonViewer>((viewer, _) => AutomationProperties.SetName(viewer, viewer.Label));
    }

    public ArcJsonViewer()
    {
        Focusable = true;
    }

    /// <summary>Any JSON-like value, or a JSON string.</summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Name of the root in paths, as in root.users[0].name.</summary>
    public string RootName
    {
        get => GetValue(RootNameProperty);
        set => SetValue(RootNameProperty, value);
    }

    /// <summary>Levels open on first render; 1 opens the root.</summary>
    public int DefaultExpandDepth
    {
        get => GetValue(DefaultExpandDepthProperty);
        set => SetValue(DefaultExpandDepthProperty, value);
    }

    /// <summary>Shows the search field.</summary>
    public bool Searchable
    {
        get => GetValue(SearchableProperty);
        set => SetValue(SearchableProperty, value);
    }

    /// <summary>Search text; matching branches open automatically.</summary>
    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <summary>Children shown per page in a long array or object.</summary>
    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>Shows copy value and copy path actions.</summary>
    public bool Copyable
    {
        get => GetValue(CopyableProperty);
        set => SetValue(CopyableProperty, value);
    }

    /// <summary>Shows the current row's path under the tree.</summary>
    public bool ShowPath
    {
        get => GetValue(ShowPathProperty);
        set => SetValue(ShowPathProperty, value);
    }

    /// <summary>Accessible name of the tree.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Height of the scrolling tree area, in px; 0 removes the cap.</summary>
    public double ScrollHeight
    {
        get => GetValue(ScrollHeightProperty);
        set => SetValue(ScrollHeightProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        BuildShell();
    }

    private void BuildShell()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _rows.Clear();
        _tree = null;
        _scroller = null;
        _search = null;
        _pathText = null;
        _statusText = null;
        _matchText = null;

        var root = new StackPanel { Spacing = 10 };

        if (Searchable)
        {
            var searchRow = new Grid();
            searchRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            searchRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var field = new Panel();
            var box = FeedbackUi.Input("Search keys and values", 36);
            box.Padding = new Thickness(34, 6, 34, 6);
            box.Text = Query ?? string.Empty;
            box.TextChanged += (_, _) =>
            {
                SetCurrentValue(QueryProperty, box.Text ?? string.Empty);
            };
            box.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    box.Text = string.Empty;
                    args.Handled = true;
                }
            };
            field.Children.Add(box);
            var icon = FeedbackUi.Icon("search", 15, ArcTokens.TextMuted);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.Margin = new Thickness(12, 0, 0, 0);
            field.Children.Add(icon);

            var clear = new ArcIconButton
            {
                Icon = "x",
                IconSize = 13,
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
            field.Children.Add(clear);
            searchRow.Children.Add(field);

            _matchText = FeedbackUi.Caption(string.Empty);
            _matchText.Margin = new Thickness(10, 0, 0, 0);
            _matchText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_matchText, 1);
            searchRow.Children.Add(_matchText);

            _search = box;
            root.Children.Add(searchRow);
        }

        _tree = new StackPanel { Spacing = 0 };
        _scroller = new ScrollViewer
        {
            Content = _tree,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        if (!double.IsInfinity(ScrollHeight) && !double.IsNaN(ScrollHeight) && ScrollHeight > 0)
        {
            _scroller.MaxHeight = ScrollHeight;
        }

        root.Children.Add(_scroller);

        if (ShowPath)
        {
            var pathBar = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 0),
                Margin = new Thickness(0, 2, 0, 0)
            };
            pathBar.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

            var pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _pathText = new TextBlock
            {
                FontFamily = FeedbackUi.MonoFamily,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pathText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
            pathGrid.Children.Add(_pathText);

            var copyPath = new ArcIconButton
            {
                Icon = "link",
                IconSize = 13,
                BoxSize = 24,
                Label = "Copy path",
                Muted = true
            };
            copyPath.Click += (_, _) =>
            {
                CopyText(_selectedPath);
                ShowStatus("Path copied");
            };
            Grid.SetColumn(copyPath, 1);
            pathGrid.Children.Add(copyPath);

            _statusText = FeedbackUi.Caption(string.Empty);
            _statusText.HorizontalAlignment = HorizontalAlignment.Right;
            _statusText.Margin = new Thickness(0, 4, 0, 0);
            _statusText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);

            pathBar.Child = pathGrid;
            root.Children.Add(pathBar);
            root.Children.Add(_statusText);
        }

        _host.Children.Add(root);
        AutomationProperties.SetName(this, Label);
        ApplyQuery();
        Refresh();
    }

    private void ResetData()
    {
        _expanded.Clear();
        _pages.Clear();
        _autoExpanded.Clear();
        _selectedPath = RootName;
        _root = ParseData(Data);

        if (_root is { } root)
        {
            ExpandDefaults(root, RootName, 0);
        }

        Refresh();
    }

    private void ExpandDefaults(JsonElement element, string path, int depth)
    {
        if (!IsBranch(element))
        {
            return;
        }

        if (depth < Math.Max(0, DefaultExpandDepth))
        {
            _expanded.Add(path);
        }

        if (depth >= Math.Max(0, DefaultExpandDepth))
        {
            return;
        }

        foreach (var (child, childPath) in Children(element, path))
        {
            ExpandDefaults(child, childPath, depth + 1);
        }
    }

    private void ApplyQuery()
    {
        _autoExpanded.Clear();
        var query = Query;
        var matches = 0;

        if (!string.IsNullOrWhiteSpace(query) && _root is { } root)
        {
            var found = new List<string>();
            CollectMatches(root, RootName, query.Trim(), found);
            matches = found.Count;
            foreach (var path in found)
            {
                var current = path;
                while (!string.IsNullOrEmpty(current))
                {
                    _autoExpanded.Add(current);
                    var cut = current.LastIndexOfAny(new[] { '.', '[' });
                    current = cut > 0 ? current[..cut].TrimEnd('.') : string.Empty;
                }
            }
        }

        if (_matchText is not null)
        {
            _matchText.Text = string.IsNullOrWhiteSpace(Query)
                ? string.Empty
                : $"{matches} {FeedbackUi.Plural(matches, "match", "matches")}";
        }

        Refresh();
    }

    private void CollectMatches(JsonElement element, string path, string query, List<string> found)
    {
        var key = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        if (key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (!IsBranch(element) && element.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            found.Add(path);
        }

        if (!IsBranch(element))
        {
            return;
        }

        foreach (var (child, childPath) in Children(element, path))
        {
            CollectMatches(child, childPath, query, found);
        }
    }

    private void Refresh()
    {
        if (_tree is null)
        {
            return;
        }

        _tree.Children.Clear();
        _rows.Clear();

        if (_root is not { } root)
        {
            _tree.Children.Add(FeedbackUi.Text("No data.", "arc-caption"));
            return;
        }

        AddRow(root, RootName, RootName, 0);
        RenderRows();

        if (_pathText is not null)
        {
            _pathText.Text = string.IsNullOrEmpty(_selectedPath) ? RootName : _selectedPath;
        }

        AutomationProperties.SetHelpText(this, $"{_rows.Count} visible rows");
    }

    private void AddRow(JsonElement element, string key, string path, int depth)
    {
        var kind = KindOf(element);
        var branch = IsBranch(element);
        var expanded = branch && IsExpanded(path);
        var hint = branch ? DescribeCount(element) : null;

        _rows.Add(new JsonRow
        {
            Path = path,
            Key = key,
            Kind = kind,
            Value = branch ? null : PrimitiveText(element),
            Hint = hint,
            Depth = depth,
            IsBranch = branch,
            Expanded = expanded,
            CopyText = element.GetRawText()
        });

        if (!branch || !expanded)
        {
            return;
        }

        var pageSize = Math.Max(1, PageSize);
        var pages = _pages.TryGetValue(path, out var count) ? Math.Max(1, count) : 1;
        // While searching, open every page so a matching row is never hidden behind "Show more".
        var take = string.IsNullOrWhiteSpace(Query) ? pageSize * pages : int.MaxValue;
        var total = Count(element);
        var index = 0;

        foreach (var (child, childPath) in Children(element, path))
        {
            if (index >= take)
            {
                _rows.Add(new JsonRow
                {
                    Path = path,
                    Key = $"Show {total - take} more",
                    Kind = kind,
                    Depth = depth + 1,
                    IsMore = true,
                    CopyText = string.Empty
                });
                break;
            }

            AddRow(child, ChildKey(element, index), childPath, depth + 1);
            index++;
        }
    }

    private void RenderRows()
    {
        if (_tree is null)
        {
            return;
        }

        _tree.Children.Clear();

        foreach (var row in _rows)
        {
            var frame = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(0, 3, 4, 3),
                Background = row.Path == _selectedPath && !row.IsMore ? ArcResources.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent
            };
            row.Frame = frame;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(2, row.Depth * 16), GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(20, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var chevron = row.IsBranch
                ? new ArcIconButton
                {
                    Icon = "chevron-right",
                    IconSize = 12,
                    BoxSize = 18,
                    Label = row.Expanded ? $"Collapse {row.Key}" : $"Expand {row.Key}",
                    Muted = true,
                    RenderTransform = row.Expanded ? new RotateTransform(90) : null
                }
                : null;
            if (chevron is not null)
            {
                chevron.Click += (_, _) => Toggle(row);
                Grid.SetColumn(chevron, 1);
                grid.Children.Add(chevron);
            }

            var key = new TextBlock
            {
                Text = row.Key,
                FontFamily = FeedbackUi.MonoFamily,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            key.Paint(TextBlock.ForegroundProperty, row.IsMore ? ArcTokens.AccentStrong : ArcTokens.Foreground);
            if (row.IsMore)
            {
                key.Cursor = new Cursor(StandardCursorType.Hand);
            }

            Grid.SetColumn(key, 2);
            grid.Children.Add(key);

            if (!row.IsMore && row.Depth > 0)
            {
                var colon = new TextBlock
                {
                    Text = ":",
                    FontFamily = FeedbackUi.MonoFamily,
                    FontSize = 12.5,
                    Margin = new Thickness(1, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                colon.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                Grid.SetColumn(colon, 3);
                grid.Children.Add(colon);
            }

            if (!string.IsNullOrEmpty(row.Value))
            {
                var value = new TextBlock
                {
                    Text = row.Value,
                    FontFamily = FeedbackUi.MonoFamily,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 620
                };
                value.Paint(TextBlock.ForegroundProperty, row.Kind switch
                {
                    ArcJsonValueType.String => ArcTokens.Series4,
                    ArcJsonValueType.Number => ArcTokens.Series1,
                    ArcJsonValueType.Boolean => ArcTokens.AccentStrong,
                    _ => ArcTokens.TextMuted
                });
                Grid.SetColumn(value, 4);
                grid.Children.Add(value);
            }
            else if (!string.IsNullOrEmpty(row.Hint) && !row.IsMore)
            {
                var hint = new TextBlock
                {
                    Text = row.Hint,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                hint.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                Grid.SetColumn(hint, 4);
                grid.Children.Add(hint);
            }

            if (Copyable && !row.IsMore)
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Opacity = row.Path == _selectedPath ? 1 : 0 };
                var copyValue = new ArcIconButton
                {
                    Icon = "copy",
                    IconSize = 12,
                    BoxSize = 22,
                    Label = "Copy value",
                    Muted = true
                };
                copyValue.Click += (_, _) =>
                {
                    CopyText(row.CopyText);
                    ShowStatus("Value copied");
                };
                actions.Children.Add(copyValue);

                var copyPath = new ArcIconButton
                {
                    Icon = "link",
                    IconSize = 12,
                    BoxSize = 22,
                    Label = "Copy path",
                    Muted = true
                };
                copyPath.Click += (_, _) =>
                {
                    CopyText(row.Path);
                    ShowStatus("Path copied");
                };
                actions.Children.Add(copyPath);

                Grid.SetColumn(actions, 5);
                grid.Children.Add(actions);
            }

            frame.Child = grid;
            frame.PointerEntered += (_, _) =>
            {
                if (row.Path != _selectedPath && !row.IsMore)
                {
                    frame.Background = ArcResources.Brush(ArcTokens.SurfaceMuted);
                }
            };
            frame.PointerExited += (_, _) =>
            {
                if (row.Path != _selectedPath && !row.IsMore)
                {
                    frame.Background = Brushes.Transparent;
                }
            };
            frame.PointerPressed += (_, args) =>
            {
                _keyboardFocus = false;
                Focus();
                Select(row);
                if (row.IsMore)
                {
                    LoadMore(row);
                }

                args.Handled = true;
            };

            AutomationProperties.SetName(frame, row.IsMore ? row.Key : $"{row.Key}, {row.Kind.ToString().ToLowerInvariant()}{(row.Hint is null ? string.Empty : ", " + row.Hint)}");
            _tree.Children.Add(frame);
        }
    }

    private void Select(JsonRow row)
    {
        _selectedPath = row.Path;
        if (_pathText is not null)
        {
            _pathText.Text = _selectedPath;
        }

        foreach (var other in _rows)
        {
            var selected = other.Path == _selectedPath && ReferenceEquals(other, row);
            other.Frame.Background = selected ? ArcResources.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent;
            if (other.Frame.Child is Grid grid && grid.Children.LastOrDefault() is StackPanel actions)
            {
                actions.Opacity = selected ? 1 : 0;
            }
        }

        AutomationProperties.SetHelpText(this, _selectedPath);
    }

    private void Toggle(JsonRow row)
    {
        if (!row.IsBranch)
        {
            return;
        }

        if (!_expanded.Remove(row.Path))
        {
            _expanded.Add(row.Path);
        }

        Refresh();
        SelectByPath(row.Path);
    }

    private void LoadMore(JsonRow row)
    {
        _pages[row.Path] = (_pages.TryGetValue(row.Path, out var count) ? count : 1) + 1;
        Refresh();
        SelectByPath(row.Path);
    }

    private void SelectByPath(string path)
    {
        var row = _rows.FirstOrDefault(item => !item.IsMore && item.Path == path);
        if (row is not null)
        {
            Select(row);
        }
    }

    private bool IsExpanded(string path) => _expanded.Contains(path) || _autoExpanded.Contains(path);

    private static JsonElement? ParseData(object? data)
    {
        try
        {
            if (data is null)
            {
                return JsonSerializer.SerializeToElement<object?>(null);
            }

            if (data is JsonElement element)
            {
                return element;
            }

            if (data is string text)
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.Clone();
            }

            return JsonSerializer.SerializeToElement(data);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsBranch(JsonElement element) =>
        element.ValueKind is JsonValueKind.Object or JsonValueKind.Array;

    private static ArcJsonValueType KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ArcJsonValueType.Object,
        JsonValueKind.Array => ArcJsonValueType.Array,
        JsonValueKind.String => ArcJsonValueType.String,
        JsonValueKind.Number => ArcJsonValueType.Number,
        JsonValueKind.True or JsonValueKind.False => ArcJsonValueType.Boolean,
        _ => ArcJsonValueType.Null
    };

    private static string PrimitiveText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString() ?? string.Empty;
            return text.Length > 80 ? $"\"{text[..77]}…\"" : $"\"{text}\"";
        }

        return element.GetRawText();
    }

    private static int Count(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.GetArrayLength(),
        JsonValueKind.Object => element.EnumerateObject().Count(),
        _ => 0
    };

    private static string DescribeCount(JsonElement element)
    {
        var count = Count(element);
        return element.ValueKind == JsonValueKind.Array
            ? $"{count} {FeedbackUi.Plural(count, "item", "items")}"
            : $"{count} {FeedbackUi.Plural(count, "field", "fields")}";
    }

    private static string ChildKey(JsonElement parent, int index) =>
        parent.ValueKind == JsonValueKind.Array
            ? $"[{index}]"
            : parent.EnumerateObject().ElementAt(index).Name;

    private static IEnumerable<(JsonElement Element, string Path)> Children(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                yield return (child, $"{path}[{index}]");
                index++;
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var name = property.Name;
                var childPath = IsSimpleName(name) ? $"{path}.{name}" : $"{path}[\"{name}\"]";
                yield return (property.Value, childPath);
            }
        }
    }

    private static bool IsSimpleName(string name) =>
        name.Length > 0 && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '$');

    private void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    private void ShowStatus(string message)
    {
        if (_statusText is null)
        {
            return;
        }

        _statusText.Text = message;
        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.6), DispatcherPriority.Background, (_, _) =>
        {
            if (_statusText is not null)
            {
                _statusText.Text = string.Empty;
            }

            _statusTimer?.Stop();
            _statusTimer = null;
        });
        _statusTimer.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Source is TextBox)
        {
            return;
        }

        if (_rows.Count == 0)
        {
            return;
        }

        var index = _rows.FindIndex(row => row.Path == _selectedPath && !row.IsMore);
        if (index < 0)
        {
            index = 0;
        }

        var current = _rows[index];

        switch (e.Key)
        {
            case Key.Down:
                SelectIndex(Math.Min(index + 1, _rows.Count - 1));
                e.Handled = true;
                break;
            case Key.Up:
                SelectIndex(Math.Max(index - 1, 0));
                e.Handled = true;
                break;
            case Key.Right:
                if (current.IsBranch && !current.Expanded)
                {
                    Toggle(current);
                }
                else
                {
                    SelectIndex(Math.Min(index + 1, _rows.Count - 1));
                }

                e.Handled = true;
                break;
            case Key.Left:
                if (current.IsBranch && current.Expanded)
                {
                    Toggle(current);
                }
                else
                {
                    SelectParent(index);
                }

                e.Handled = true;
                break;
            case Key.Home:
                SelectIndex(0);
                e.Handled = true;
                break;
            case Key.End:
                SelectIndex(_rows.Count - 1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (current.IsMore)
                {
                    LoadMore(current);
                }
                else if (current.IsBranch)
                {
                    Toggle(current);
                }

                e.Handled = true;
                break;
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
                if (current.IsBranch || current.IsMore)
                {
                    break;
                }

                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    CopyText(current.Path);
                    ShowStatus("Path copied");
                }
                else
                {
                    CopyText(current.CopyText);
                    ShowStatus("Value copied");
                }

                e.Handled = true;
                break;
            case Key.OemQuestion when string.IsNullOrEmpty(Query) && _search is not null:
                _search.Focus();
                e.Handled = true;
                break;
        }
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        _keyboardFocus = true;
        Select(_rows[index]);
        _rows[index].Frame.BringIntoView();
    }

    private void SelectParent(int index)
    {
        var current = _rows[index];
        for (var i = index - 1; i >= 0; i--)
        {
            if (_rows[i].IsBranch && _selectedPath.StartsWith(_rows[i].Path, StringComparison.Ordinal))
            {
                SelectIndex(i);
                return;
            }
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_keyboardFocus && _rows.Count > 0 && !_rows.Any(row => row.Path == _selectedPath))
        {
            SelectIndex(0);
        }
    }
}

