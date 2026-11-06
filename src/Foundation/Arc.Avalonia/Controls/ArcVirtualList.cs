using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arc.Avalonia.Controls;

/// <summary>Scroll position bucket of an <see cref="ArcVirtualList"/>, used for status display.</summary>
public enum ArcVirtualListScrollState
{
    /// <summary>The viewport sits at the start of the list.</summary>
    AtTop,

    /// <summary>The viewport sits between the start and the end.</summary>
    InMiddle,

    /// <summary>The viewport reaches the last item.</summary>
    AtBottom
}

/// <summary>
/// Arc's virtualising list. It is a <see cref="ListBox"/> whose item panel is always a
/// <see cref="VirtualizingStackPanel"/>, so only the rows inside the viewport are measured and drawn. The
/// control is a pure UI container: it shows whatever <see cref="ItemsControl.ItemsSource"/> the host gives it,
/// reports the scroll position and raises no data or loading work of its own.
/// <para>
/// Chat transcripts, logs, ledgers and inboxes reuse it by supplying an item template, an
/// <see cref="System.Collections.IEnumerable"/> and, for streaming surfaces, <see cref="StickToBottom"/>. When
/// the source raises <see cref="INotifyCollectionChanged"/> and <see cref="StickToBottom"/> is true, a new item
/// scrolls the viewport to the end once layout has run. The host owns the data, the append and any jump-to-latest
/// affordance; the list only mirrors it.
/// </para>
/// <para>
/// The control ships no ControlTheme of its own: it reuses the host theme's <see cref="ListBox"/> template and
/// reads that template's <c>PART_ScrollViewer</c> to report <see cref="ScrollState"/>. A theme that names the
/// scroll viewer differently still works through the visual-tree fallback, but the contract expects
/// <c>PART_ScrollViewer</c>.
/// </para>
/// </summary>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
public class ArcVirtualList : ListBox
{
    /// <summary>
    /// Follow new items at the end of the list, the chat / log "stay at the latest" behaviour. Defaults to false
    /// so an ordinary list opens at its start; a live transcript sets it to true.
    /// </summary>
    public static readonly StyledProperty<bool> StickToBottomProperty =
        AvaloniaProperty.Register<ArcVirtualList, bool>(nameof(StickToBottom));

    private static readonly DirectProperty<ArcVirtualList, ArcVirtualListScrollState> ScrollStateProperty =
        AvaloniaProperty.RegisterDirect<ArcVirtualList, ArcVirtualListScrollState>(
            nameof(ScrollState), list => list.ScrollState);

    private INotifyCollectionChanged? _collection;
    private ScrollViewer? _scrollViewer;
    private ArcVirtualListScrollState _scrollState;

    static ArcVirtualList()
    {
        ItemsSourceProperty.Changed.AddClassHandler<ArcVirtualList>((list, _) => list.OnItemsSourceChanged());
        StickToBottomProperty.Changed.AddClassHandler<ArcVirtualList>((list, _) => list.OnStickToBottomChanged());
    }

    public ArcVirtualList()
    {
        // Pin the item panel so virtualisation never depends on the active theme's ListBox template.
        ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        SelectionMode = SelectionMode.Single;
        ArcUi.Name(this, "Virtualized list", "Use the scroll bar or the page buttons to move through the rows.");
    }

    /// <summary>Uses the Fluent ListBox control theme, which supplies the ScrollViewer and item containers.</summary>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>Raised after <see cref="ScrollState"/> changes because the visitor scrolled or a jump completed.</summary>
    public event EventHandler? ScrollStateChanged;

    /// <summary>Raised when <see cref="StickToBottom"/> is switched on or off.</summary>
    public event EventHandler? StickToBottomChanged;

    /// <summary>
    /// Whether new items keep the viewport at the end. Set it from the host when the list is a live feed; the
    /// control never changes it on its own, so reading history is never interrupted by an accidental append.
    /// </summary>
    public bool StickToBottom
    {
        get => GetValue(StickToBottomProperty);
        set => SetValue(StickToBottomProperty, value);
    }

    /// <summary>Current scroll bucket, ready for a status label or an accessibility announcement.</summary>
    public ArcVirtualListScrollState ScrollState => _scrollState;

    /// <summary>True when the last item is visible; a jump-to-latest control usually shows only then.</summary>
    public bool AtBottom => _scrollState == ArcVirtualListScrollState.AtBottom;

    /// <summary>Moves the viewport to the last item. A no-op for an empty list.</summary>
    public void ScrollToEnd()
    {
        var count = Items.Count;
        if (count == 0)
        {
            Dispatcher.UIThread.Post(RefreshScrollState, DispatcherPriority.Background);
            return;
        }

        ScrollIntoView(count - 1);
        Dispatcher.UIThread.Post(ScrollToBottomCore, DispatcherPriority.Loaded);
    }

    /// <summary>Scrolls the given item index into view and updates <see cref="ScrollState"/>.</summary>
    /// <param name="index">Zero based item index; must fall inside <see cref="ItemCollection"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or past the last item.</exception>
    public void ScrollToIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the list range.");
        }

        ScrollIntoView(index);
        Dispatcher.UIThread.Post(RefreshScrollState, DispatcherPriority.Background);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Prefer the declared theme part; themes that name the scroll viewer differently fall back to the
        // first ScrollViewer in the visual tree (see HookScrollViewer). No virtualisation or row-reuse logic
        // is involved here — this only wires scroll-state reporting.
        HookScrollViewer(e.NameScope.Find<ScrollViewer>("PART_ScrollViewer"));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The template (and with it the ScrollViewer) can be applied after the first attach pass.
        Dispatcher.UIThread.Post(() => HookScrollViewer(), DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachScrollViewer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            AutomationProperties.SetHelpText(this, IsEnabled
                ? "Virtualized list. Use the scroll bar or the page buttons to move through the rows."
                : "Virtualized list is disabled.");
        }
    }

    private void OnItemsSourceChanged()
    {
        if (_collection is not null)
        {
            _collection.CollectionChanged -= OnCollectionChanged;
        }

        _collection = ItemsSource as INotifyCollectionChanged;
        if (_collection is not null)
        {
            _collection.CollectionChanged += OnCollectionChanged;
        }

        if (StickToBottom)
        {
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);
        }

        Dispatcher.UIThread.Post(RefreshScrollState, DispatcherPriority.Background);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (StickToBottom)
        {
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);
        }

        Dispatcher.UIThread.Post(RefreshScrollState, DispatcherPriority.Background);
    }

    private void OnStickToBottomChanged()
    {
        StickToBottomChanged?.Invoke(this, EventArgs.Empty);
        if (StickToBottom)
        {
            ScrollToEnd();
        }
    }

    private void ScrollToBottomCore()
    {
        if (_scrollViewer is null)
        {
            HookScrollViewer();
        }

        if (_scrollViewer is null)
        {
            return;
        }

        var maximum = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, maximum);
        UpdateScrollState(_scrollViewer);
    }

    private void HookScrollViewer(ScrollViewer? templatePart = null)
    {
        var scrollViewer = templatePart ?? this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (ReferenceEquals(scrollViewer, _scrollViewer))
        {
            if (scrollViewer is not null)
            {
                UpdateScrollState(scrollViewer);
            }

            return;
        }

        DetachScrollViewer();
        _scrollViewer = scrollViewer;
        if (_scrollViewer is null)
        {
            return;
        }

        _scrollViewer.ScrollChanged += OnScrollChanged;
        UpdateScrollState(_scrollViewer);
        if (StickToBottom)
        {
            ScrollToEnd();
        }
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer = null;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            UpdateScrollState(scrollViewer);
        }
    }

    private void RefreshScrollState()
    {
        if (_scrollViewer is null)
        {
            HookScrollViewer();
        }

        if (_scrollViewer is not null)
        {
            UpdateScrollState(_scrollViewer);
        }
    }

    private void UpdateScrollState(ScrollViewer scrollViewer)
    {
        var maximum = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var offset = Math.Max(0, scrollViewer.Offset.Y);

        var state = maximum <= 0.5
            ? ArcVirtualListScrollState.AtTop
            : offset <= 0.5
                ? ArcVirtualListScrollState.AtTop
                : offset >= maximum - 0.5
                    ? ArcVirtualListScrollState.AtBottom
                    : ArcVirtualListScrollState.InMiddle;

        if (state == _scrollState)
        {
            return;
        }

        SetAndRaise(ScrollStateProperty, ref _scrollState, state);
        AutomationProperties.SetHelpText(this, state switch
        {
            ArcVirtualListScrollState.AtTop => "At the start of the list.",
            ArcVirtualListScrollState.AtBottom => "At the end of the list.",
            _ => "In the middle of the list."
        });
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
