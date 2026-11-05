using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using DataGridControl = Avalonia.Controls.DataGrid;

namespace Arc.Avalonia.Feature.DataGrid;

/// <summary>Page navigation request raised by <see cref="ArcDataGridHost"/>.</summary>
public sealed class ArcDataGridPageRequestedEventArgs : EventArgs
{
    public ArcDataGridPageRequestedEventArgs(int pageIndex)
    {
        PageIndex = pageIndex;
    }

    /// <summary>Zero-based page the host wants to show.</summary>
    public int PageIndex { get; }
}

/// <summary>
/// Optional skin and paging host for Avalonia's official <see cref="DataGrid"/>, for data sets that are
/// too large for the design-system <c>ArcDataGrid</c>. The official grid supplies row and column
/// virtualisation, editing and clipboard support; this host supplies the Arc shell, a page bar and the
/// page slicing so the consumer does not need to wire an <see cref="ItemsControl"/> by hand.
/// <para>
/// Small, design-critical tables should keep using <c>ArcDataGrid</c> (Arc.Avalonia.DataDisplay): it is
/// fully Arc themed, but sizes its rows eagerly. This host is the large-data option and lives in its own
/// package because it is the only project allowed to reference <c>Avalonia.Controls.DataGrid</c> (R7).
/// </para>
/// <para>
/// The official DataGrid theme is not embedded in this package: a consumer adds
/// <c>&lt;StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/&gt;</c> and then
/// <c>&lt;StyleInclude Source="avares://Arc.Avalonia.Feature.DataGrid/Themes/ArcFeatureDataGridTheme.axaml"/&gt;</c>
/// (the second one is the Arc skin). The Demo page in Arc.Avalonia.Docs shows the same two lines.
/// </para>
/// </summary>
[TemplatePart("PART_Grid", typeof(DataGridControl))]
[TemplatePart("PART_PagerBar", typeof(Border))]
[TemplatePart("PART_Pager", typeof(StackPanel))]
[TemplatePart("PART_Summary", typeof(TextBlock))]
[TemplatePart("PART_Empty", typeof(TextBlock))]
public class ArcDataGridHost : TemplatedControl
{
    /// <summary>The full data set (auto paging) or the current server page (manual paging).</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ArcDataGridHost, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Rows per page when <see cref="AutoPage"/> is true. At least one.</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<ArcDataGridHost, int>(nameof(PageSize), 100);

    /// <summary>Zero-based current page.</summary>
    public static readonly StyledProperty<int> PageIndexProperty =
        AvaloniaProperty.Register<ArcDataGridHost, int>(nameof(PageIndex));

    /// <summary>Total row count; -1 derives it from <see cref="ItemsSource"/> in auto mode.</summary>
    public static readonly StyledProperty<int> TotalItemsProperty =
        AvaloniaProperty.Register<ArcDataGridHost, int>(nameof(TotalItems), -1);

    /// <summary>
    /// When true (default) the host slices <see cref="ItemsSource"/> into pages. When false the consumer
    /// owns the page and answers <see cref="PageRequested"/> by replacing <see cref="ItemsSource"/>.
    /// </summary>
    public static readonly StyledProperty<bool> AutoPageProperty =
        AvaloniaProperty.Register<ArcDataGridHost, bool>(nameof(AutoPage), true);

    /// <summary>Shows the page bar under the grid. Defaults to true.</summary>
    public static readonly StyledProperty<bool> ShowPagerProperty =
        AvaloniaProperty.Register<ArcDataGridHost, bool>(nameof(ShowPager), true);

    /// <summary>Lets the official DataGrid create a column per public property. Defaults to true.</summary>
    public static readonly StyledProperty<bool> AutoGenerateColumnsProperty =
        AvaloniaProperty.Register<ArcDataGridHost, bool>(nameof(AutoGenerateColumns), true);

    /// <summary>Read-only by default: the large-data path is a viewer; edit small tables in ArcDataGrid.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<ArcDataGridHost, bool>(nameof(IsReadOnly), true);

    /// <summary>Message shown instead of the grid when the current page is empty.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ArcDataGridHost, string>(nameof(EmptyText), "No rows to show");

    private DataGridControl? _grid;
    private Border? _pagerBar;
    private StackPanel? _pager;
    private TextBlock? _summary;
    private TextBlock? _empty;
    private TextBlock? _pageLabel;
    private ArcButton? _first;
    private ArcButton? _previous;
    private ArcButton? _next;
    private ArcButton? _last;
    private IReadOnlyList<object?> _pageItems = Array.Empty<object?>();
    private int _total;
    private bool _updating;

    static ArcDataGridHost()
    {
        ItemsSourceProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.OnItemsSourceChanged());
        PageSizeProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.Refresh());
        PageIndexProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.Refresh());
        TotalItemsProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.Refresh());
        AutoPageProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.Refresh());
        ShowPagerProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.UpdatePagerVisibility());
        AutoGenerateColumnsProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.ApplyGridOptions());
        IsReadOnlyProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.ApplyGridOptions());
        EmptyTextProperty.Changed.AddClassHandler<ArcDataGridHost>((host, _) => host.UpdateEmptyState());
    }

    public ArcDataGridHost()
    {
        Focusable = false;
    }

    /// <summary>Raised when a pager button asks for another page; the argument is zero-based.</summary>
    public event EventHandler<ArcDataGridPageRequestedEventArgs>? PageRequested;

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public int PageIndex
    {
        get => GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    public int TotalItems
    {
        get => GetValue(TotalItemsProperty);
        set => SetValue(TotalItemsProperty, value);
    }

    public bool AutoPage
    {
        get => GetValue(AutoPageProperty);
        set => SetValue(AutoPageProperty, value);
    }

    public bool ShowPager
    {
        get => GetValue(ShowPagerProperty);
        set => SetValue(ShowPagerProperty, value);
    }

    public bool AutoGenerateColumns
    {
        get => GetValue(AutoGenerateColumnsProperty);
        set => SetValue(AutoGenerateColumnsProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Number of pages for the current total and page size; always at least one.</summary>
    public int PageCount => EffectivePageSize > 0
        ? Math.Max(1, (int)Math.Ceiling(_total / (double)EffectivePageSize))
        : 1;

    /// <summary>Rows currently shown by the grid.</summary>
    public IReadOnlyList<object?> CurrentPageItems => _pageItems;

    /// <summary>Recomputes the page after the consumer changed the data in place.</summary>
    public void Refresh()
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        try
        {
            var pageSize = EffectivePageSize;
            var source = ItemsSource;
            if (AutoPage)
            {
                var all = Materialize(source);
                _total = TotalItems >= 0 ? TotalItems : all.Count;
                var maxPage = Math.Max(0, (int)Math.Ceiling(_total / (double)pageSize) - 1);
                if (PageIndex > maxPage)
                {
                    SetCurrentValue(PageIndexProperty, maxPage);
                }

                var start = Math.Clamp(PageIndex, 0, maxPage) * pageSize;
                _pageItems = start >= all.Count
                    ? Array.Empty<object?>()
                    : all.Skip(start).Take(pageSize).ToArray();
            }
            else
            {
                _pageItems = Materialize(source);
                _total = TotalItems >= 0
                    ? TotalItems
                    : PageIndex * pageSize + _pageItems.Count;
            }
        }
        finally
        {
            _updating = false;
        }

        ApplyGridOptions();
        if (_grid is not null)
        {
            _grid.ItemsSource = _pageItems;
        }

        UpdatePager();
        UpdateEmptyState();
    }

    /// <summary>Requests the first page.</summary>
    public void FirstPage() => GoToPage(0);

    /// <summary>Requests the previous page.</summary>
    public void PreviousPage() => GoToPage(PageIndex - 1);

    /// <summary>Requests the next page.</summary>
    public void NextPage() => GoToPage(PageIndex + 1);

    /// <summary>Requests the last page.</summary>
    public void LastPage() => GoToPage(PageCount - 1);

    /// <summary>Requests a zero-based page, clamped to the available range.</summary>
    public void GoToPage(int pageIndex)
    {
        var next = Math.Clamp(pageIndex, 0, PageCount - 1);
        if (next == PageIndex)
        {
            return;
        }

        SetCurrentValue(PageIndexProperty, next);
        PageRequested?.Invoke(this, new ArcDataGridPageRequestedEventArgs(next));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid = e.NameScope.Find<DataGridControl>("PART_Grid");
        _pagerBar = e.NameScope.Find<Border>("PART_PagerBar");
        _pager = e.NameScope.Find<StackPanel>("PART_Pager");
        _summary = e.NameScope.Find<TextBlock>("PART_Summary");
        _empty = e.NameScope.Find<TextBlock>("PART_Empty");

        if (_grid is not null)
        {
            _grid.ItemsSource = _pageItems;
        }

        BuildPagerButtons();
        Refresh();
        UpdatePagerVisibility();
    }

    private int EffectivePageSize => Math.Max(1, PageSize);

    private void OnItemsSourceChanged()
    {
        if (AutoPage)
        {
            SetCurrentValue(PageIndexProperty, 0);
        }

        Refresh();
    }

    private void ApplyGridOptions()
    {
        if (_grid is null)
        {
            return;
        }

        _grid.AutoGenerateColumns = AutoGenerateColumns;
        _grid.IsReadOnly = IsReadOnly;
    }

    private void BuildPagerButtons()
    {
        if (_pager is null)
        {
            return;
        }

        _pager.Children.Clear();
        _first = PagerButton("«", "First page", FirstPage);
        _previous = PagerButton("‹", "Previous page", PreviousPage);
        _pageLabel = new TextBlock
        {
            MinWidth = 72,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _pageLabel.Foreground = ArcResources.Brush(ArcTokens.TextSecondary);
        _next = PagerButton("›", "Next page", NextPage);
        _last = PagerButton("»", "Last page", LastPage);
        _pager.Children.Add(_first);
        _pager.Children.Add(_previous);
        _pager.Children.Add(_pageLabel);
        _pager.Children.Add(_next);
        _pager.Children.Add(_last);
    }

    private static ArcButton PagerButton(string glyph, string name, Action action)
    {
        var button = new ArcButton
        {
            Content = glyph,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(8, 2),
            MinWidth = 30
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => action();
        return button;
    }

    private void UpdatePager()
    {
        if (_summary is null)
        {
            return;
        }

        var count = _pageItems.Count;
        var start = _total == 0 ? 0 : PageIndex * EffectivePageSize + 1;
        var end = PageIndex * EffectivePageSize + count;
        _summary.Text = _total == 0
            ? "No rows"
            : $"{start:N0}–{end:N0} of {_total:N0} rows";
        AutomationProperties.SetName(_summary, _summary.Text);

        if (_pageLabel is not null)
        {
            _pageLabel.Text = $"Page {PageIndex + 1} / {PageCount}";
        }

        var first = PageIndex <= 0;
        var last = PageIndex >= PageCount - 1;
        if (_first is not null)
        {
            _first.IsEnabled = !first;
        }

        if (_previous is not null)
        {
            _previous.IsEnabled = !first;
        }

        if (_next is not null)
        {
            _next.IsEnabled = !last;
        }

        if (_last is not null)
        {
            _last.IsEnabled = !last;
        }
    }

    private void UpdatePagerVisibility()
    {
        if (_pagerBar is not null)
        {
            _pagerBar.IsVisible = ShowPager;
        }
    }

    private void UpdateEmptyState()
    {
        if (_empty is not null)
        {
            _empty.Text = EmptyText;
            _empty.IsVisible = _pageItems.Count == 0;
        }

        if (_grid is not null)
        {
            _grid.IsVisible = _pageItems.Count > 0;
        }
    }

    private static IReadOnlyList<object?> Materialize(IEnumerable? source)
    {
        if (source is null)
        {
            return Array.Empty<object?>();
        }

        if (source is IReadOnlyList<object?> readOnly)
        {
            return readOnly;
        }

        if (source is IList list)
        {
            var items = new object?[list.Count];
            for (var index = 0; index < list.Count; index++)
            {
                items[index] = list[index];
            }

            return items;
        }

        return source.Cast<object?>().ToArray();
    }
}
