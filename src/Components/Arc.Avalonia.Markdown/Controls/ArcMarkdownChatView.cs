using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Chat surface for AI answers: a virtualized message list (one full Markdown <see cref="ArcMarkdownView"/>
/// per realized row), a fixed streaming slot below the list, status/error rows and an auto-scroll policy
/// that pauses when the visitor scrolls up.
/// <para>
/// Message-level virtualization comes from <see cref="ArcVirtualList"/> (L1, ListBox + VirtualizingStackPanel),
/// so a 1000 message transcript only realizes the handful of rows inside the viewport. The streaming message
/// lives outside the virtualized list: it is upserted into <see cref="Messages"/> when the stream completes or
/// aborts, which guarantees the in-flight view is never recycled mid-stream.
/// </para>
/// </summary>
[TemplatePart("PART_List", typeof(ArcVirtualList))]
[TemplatePart("PART_StreamingSlot", typeof(ContentControl))]
public class ArcMarkdownChatView : TemplatedControl, IDisposable
{
    /// <summary>Distance from the end (px) that still counts as "near bottom".</summary>
    public const double NearBottomThreshold = 56;

    public static readonly StyledProperty<ArcMarkdownOptions?> OptionsProperty =
        AvaloniaProperty.Register<ArcMarkdownChatView, ArcMarkdownOptions?>(nameof(Options));

    public static readonly StyledProperty<ArcMarkdownChatAutoScrollMode> AutoScrollModeProperty =
        AvaloniaProperty.Register<ArcMarkdownChatView, ArcMarkdownChatAutoScrollMode>(
            nameof(AutoScrollMode), ArcMarkdownChatAutoScrollMode.NearBottom);

    private readonly Dictionary<string, ArcMarkdownView> _rowViews = new(StringComparer.Ordinal);
    private ArcVirtualList? _list;
    private ContentControl? _slot;
    private ScrollViewer? _scroll;
    private ArcMarkdownStreamingSession? _session;
    private ArcMarkdownChatMessage? _streamingTarget;
    private bool _mutating;
    private bool _follow = true;
    private double _lastOffset;
    private bool _themeSubscribed;
    private bool _disposed;

    /// <summary>Creates the chat surface.</summary>
    public ArcMarkdownChatView()
    {
        Messages = new ObservableCollection<ArcMarkdownChatMessage>();
        Messages.CollectionChanged += OnMessagesCollectionChanged;
        SizeChanged += (_, _) => UpdateSlotMaxHeight();
    }

    /// <summary>Messages, oldest first. The collection may be mutated directly; the surface mirrors it.</summary>
    public ObservableCollection<ArcMarkdownChatMessage> Messages { get; }

    /// <summary>Markdown rendering options shared by every message row.</summary>
    public ArcMarkdownOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Auto-scroll policy; <see cref="ArcMarkdownChatAutoScrollMode.NearBottom"/> pauses once the visitor scrolls up.</summary>
    public ArcMarkdownChatAutoScrollMode AutoScrollMode
    {
        get => GetValue(AutoScrollModeProperty);
        set => SetValue(AutoScrollModeProperty, value);
    }

    /// <summary>Active streaming session, if any.</summary>
    public ArcMarkdownStreamingSession? ActiveSession => _session;

    /// <summary>Whether a message is currently streaming.</summary>
    public bool IsStreaming => _session?.State is ArcMarkdownStreamingState.Streaming or ArcMarkdownStreamingState.Created;

    /// <summary>Whether the viewport is close enough to the end for follow mode to resume.</summary>
    public bool IsNearBottom
    {
        get
        {
            if (_scroll is null)
            {
                return _follow;
            }

            var maximum = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
            return maximum - Math.Max(0, _scroll.Offset.Y) <= NearBottomThreshold;
        }
    }

    /// <summary>Raised when a message is added (including a completed streaming slot upsert).</summary>
    public event EventHandler<ArcMarkdownChatMessage>? MessageAdded;

    /// <summary>Raised when a message body/status is updated or re-assigned.</summary>
    public event EventHandler<ArcMarkdownChatMessage>? MessageUpdated;

    /// <summary>Raised when a message is removed.</summary>
    public event EventHandler<ArcMarkdownChatMessage>? MessageRemoved;

    /// <summary>Raised for agent-component interactions inside any message.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Raised for renderer warnings inside any message.</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs>? Warning;

    /// <summary>Raised for accessibility announcements (streaming completion, status changes).</summary>
    public event EventHandler<string>? Announced;

    /// <summary>Adds a message to the end; an existing id is updated in place instead of duplicated.</summary>
    public void AddMessage(ArcMarkdownChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var follow = ShouldAutoScrollToEnd();
        var existing = IndexOf(message.Id);
        _mutating = true;
        try
        {
            if (existing >= 0)
            {
                if (!ReferenceEquals(Messages[existing], message))
                {
                    Messages[existing] = message;
                }
            }
            else
            {
                Messages.Add(message);
            }
        }
        finally
        {
            _mutating = false;
        }

        if (existing >= 0)
        {
            MessageUpdated?.Invoke(this, message);
        }
        else
        {
            MessageAdded?.Invoke(this, message);
        }

        if (follow)
        {
            ScrollToEnd();
        }
    }

    /// <summary>Inserts a message at an index; an existing id is updated instead of duplicated.</summary>
    public void InsertMessage(int index, ArcMarkdownChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var follow = ShouldAutoScrollToEnd();
        var existing = IndexOf(message.Id);
        _mutating = true;
        try
        {
            if (existing >= 0)
            {
                if (!ReferenceEquals(Messages[existing], message))
                {
                    Messages[existing] = message;
                }
            }
            else
            {
                Messages.Insert(Math.Clamp(index, 0, Messages.Count), message);
            }
        }
        finally
        {
            _mutating = false;
        }

        if (existing >= 0)
        {
            MessageUpdated?.Invoke(this, message);
        }
        else
        {
            MessageAdded?.Invoke(this, message);
        }

        if (follow)
        {
            ScrollToEnd();
        }
    }

    /// <summary>Replaces a message with the same id (adds it when absent) and notifies listeners.</summary>
    public void UpdateMessage(ArcMarkdownChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var existing = IndexOf(message.Id);
        if (existing < 0)
        {
            AddMessage(message);
            return;
        }

        _mutating = true;
        try
        {
            if (!ReferenceEquals(Messages[existing], message))
            {
                Messages[existing] = message;
            }
        }
        finally
        {
            _mutating = false;
        }

        MessageUpdated?.Invoke(this, message);
    }

    /// <summary>Removes a message by id.</summary>
    public bool RemoveMessage(string messageId)
    {
        var existing = IndexOf(messageId);
        if (existing < 0)
        {
            return false;
        }

        var message = Messages[existing];
        _mutating = true;
        try
        {
            Messages.RemoveAt(existing);
        }
        finally
        {
            _mutating = false;
        }

        MessageRemoved?.Invoke(this, message);
        return true;
    }

    /// <summary>Removes every message.</summary>
    public void ClearMessages()
    {
        if (Messages.Count == 0)
        {
            return;
        }

        var removed = Messages.ToArray();
        _mutating = true;
        try
        {
            Messages.Clear();
        }
        finally
        {
            _mutating = false;
        }

        foreach (var message in removed)
        {
            MessageRemoved?.Invoke(this, message);
        }
    }

    /// <summary>Replaces the whole list (fires removed then added for every affected message).</summary>
    public void ReplaceMessages(IEnumerable<ArcMarkdownChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var replacement = messages.ToArray();
        var removed = Messages.ToArray();
        _mutating = true;
        try
        {
            Messages.Clear();
            foreach (var message in replacement)
            {
                Messages.Add(message);
            }
        }
        finally
        {
            _mutating = false;
        }

        foreach (var message in removed)
        {
            MessageRemoved?.Invoke(this, message);
        }

        foreach (var message in replacement)
        {
            MessageAdded?.Invoke(this, message);
        }

        if (ShouldAutoScrollToEnd())
        {
            ScrollToEnd();
        }
    }

    /// <summary>Finds a message by id.</summary>
    public ArcMarkdownChatMessage? FindMessage(string messageId)
    {
        var index = IndexOf(messageId);
        return index < 0 ? null : Messages[index];
    }

    /// <summary>Index of a message id, or -1.</summary>
    public int IndexOf(string messageId)
    {
        for (var i = 0; i < Messages.Count; i++)
        {
            if (string.Equals(Messages[i].Id, messageId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Starts streaming into a message (created when null). The message leaves the virtualized list while it
    /// streams (the dedicated bottom slot shows it) and is upserted back into <see cref="Messages"/> when the
    /// stream completes or aborts. An active stream is aborted first.
    /// </summary>
    public ArcMarkdownStreamingSession BeginStreamingMessage(ArcMarkdownChatMessage? message = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is not null)
        {
            AbortStreaming();
        }

        var target = message ?? new ArcMarkdownChatMessage { Role = ArcMarkdownChatRole.Assistant };
        var insertIndex = IndexOf(target.Id);
        if (insertIndex >= 0)
        {
            _mutating = true;
            try
            {
                Messages.RemoveAt(insertIndex);
            }
            finally
            {
                _mutating = false;
            }
        }

        target.Status = ArcMarkdownChatMessageStatus.Streaming;
        target.Error = null;
        target.Markdown = string.Empty;

        var view = new ArcMarkdownView { Options = Options };
        view.InteractionOccurred += OnRowInteraction;
        view.Warning += OnRowWarning;
        view.Announced += OnRowAnnounced;

        if (_slot is not null)
        {
            _slot.Content = view;
            _slot.IsVisible = true;
        }

        var session = view.BeginStreaming(new ArcMarkdownStreamingOptions { AutoScrollToEnd = true });
        _session = session;
        _streamingTarget = target;
        session.Updated += OnStreamingUpdated;
        session.Completed += (_, _) => FinishStreaming(session, view, target, insertIndex, aborted: false);
        session.Aborted += (_, _) => FinishStreaming(session, view, target, insertIndex, aborted: true);
        UpdateSlotMaxHeight();
        if (ShouldAutoScrollToEnd())
        {
            ScrollToEnd();
        }

        return session;
    }

    /// <summary>Aborts the active stream, keeping the partial body and writing it back into the list.</summary>
    public void AbortStreaming()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _ = AbortStreamingAsync(session);
    }

    /// <summary>Aborts the active stream and waits until the partial body has been written back.</summary>
    public Task AbortStreamingAsync()
    {
        var session = _session;
        return session is null ? Task.CompletedTask : AbortStreamingAsync(session);
    }

    private Task AbortStreamingAsync(ArcMarkdownStreamingSession session)
        => session.AbortAsync();

    /// <summary>Scrolls to the newest message and re-arms follow mode.</summary>
    public void ScrollToEnd()
    {
        _follow = true;
        _list?.ScrollToEnd();
    }

    /// <summary>Scrolls to the message with the given id when it is inside the list.</summary>
    public void ScrollToMessage(string messageId)
    {
        var index = IndexOf(messageId);
        if (index >= 0)
        {
            _list?.ScrollToIndex(index);
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachScrollViewer();
        _list = e.NameScope.Find<ArcVirtualList>("PART_List");
        _slot = e.NameScope.Find<ContentControl>("PART_StreamingSlot");
        if (_list is not null)
        {
            _list.ItemsSource = null;
            _list.ItemTemplate = BuildItemTemplate();
            _list.ItemsSource = Messages;
            _list.StickToBottom = AutoScrollMode == ArcMarkdownChatAutoScrollMode.Always;
        }

        if (_slot is not null)
        {
            _slot.IsVisible = _session is not null;
            if (_session is not null && _slot.Content is null)
            {
                _slot.Content = new ArcMarkdownView { Options = Options };
            }
        }

        UpdateSlotMaxHeight();
        Dispatcher.UIThread.Post(HookScrollViewer, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_themeSubscribed || Application.Current is null)
        {
            return;
        }

        Application.Current.ActualThemeVariantChanged += OnThemeVariantChanged;
        _themeSubscribed = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_themeSubscribed && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _themeSubscribed = false;
        }

        DetachScrollViewer();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Messages.CollectionChanged -= OnMessagesCollectionChanged;
        if (_themeSubscribed && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _themeSubscribed = false;
        }

        DetachScrollViewer();
        var session = _session;
        _session = null;
        if (session is not null)
        {
            _ = session.DisposeAsync().AsTask();
        }

        if (_list is not null)
        {
            _list.ItemsSource = null;
        }

        if (_slot is not null)
        {
            _slot.Content = null;
        }

        _rowViews.Clear();
        GC.SuppressFinalize(this);
    }

    private FuncDataTemplate<ArcMarkdownChatMessage> BuildItemTemplate()
        => new((message, _) => message is null ? new Border() : BuildRow(message), supportsRecycling: false);

    private Control BuildRow(ArcMarkdownChatMessage message)
    {
        var style = ArcMarkdownStyle.Current;
        var roleLine = new TextBlock
        {
            FontSize = style.CaptionSize,
            Foreground = style.TextMuted,
            TextWrapping = TextWrapping.NoWrap,
        };
        var errorLine = new TextBlock
        {
            FontSize = style.CaptionSize,
            Foreground = style.Danger,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        var view = new ArcMarkdownView { Options = Options, Markdown = message.Markdown, Tag = message.Id };
        view.InteractionOccurred += OnRowInteraction;
        view.Warning += OnRowWarning;
        view.Announced += OnRowAnnounced;
        _rowViews[message.Id] = view;

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(roleLine);
        stack.Children.Add(view);
        stack.Children.Add(errorLine);
        var row = new Border
        {
            Margin = new Thickness(0, 0, 0, 16),
            Child = stack,
            Tag = message.Id,
        };

        void Update()
        {
            var status = message.Status switch
            {
                ArcMarkdownChatMessageStatus.Streaming => "生成中…",
                ArcMarkdownChatMessageStatus.Failed => "生成失败",
                ArcMarkdownChatMessageStatus.Aborted => "已中断",
                _ => string.Empty,
            };
            var time = message.CreatedAt.ToLocalTime().ToString("HH:mm");
            roleLine.Text = string.IsNullOrEmpty(status)
                ? $"{RoleLabel(message.Role)} · {time}"
                : $"{RoleLabel(message.Role)} · {time} · {status}";
            if (!string.Equals(view.Markdown, message.Markdown, StringComparison.Ordinal))
            {
                view.Markdown = message.Markdown;
            }

            errorLine.Text = message.Error ?? string.Empty;
            errorLine.IsVisible = !string.IsNullOrWhiteSpace(message.Error);
            AutomationProperties.SetName(row, $"{RoleLabel(message.Role)}，{time}");
        }

        void OnChanged(object? sender, EventArgs e)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Update();
            }
            else
            {
                Dispatcher.UIThread.Post(Update, DispatcherPriority.Background);
            }
        }

        message.Changed += OnChanged;
        row.DetachedFromVisualTree += (_, _) =>
        {
            message.Changed -= OnChanged;
            view.InteractionOccurred -= OnRowInteraction;
            view.Warning -= OnRowWarning;
            view.Announced -= OnRowAnnounced;
            if (_rowViews.TryGetValue(message.Id, out var current) && ReferenceEquals(current, view))
            {
                _rowViews.Remove(message.Id);
            }
        };

        Update();
        return row;
    }

    private void OnRowInteraction(object? sender, ArcMarkdownInteractionEventArgs args)
        => InteractionOccurred?.Invoke(this, args);

    private void OnRowWarning(object? sender, ArcMarkdownWarningEventArgs args)
        => Warning?.Invoke(this, args);

    private void OnRowAnnounced(object? sender, string text)
        => Announced?.Invoke(this, text);

    private void OnStreamingUpdated(object? sender, EventArgs e)
    {
        if (sender is not ArcMarkdownStreamingSession session || !ReferenceEquals(_session, session))
        {
            return;
        }

        if (_streamingTarget is { } target)
        {
            target.Markdown = session.CurrentText;
        }

        if (ShouldAutoScrollToEnd())
        {
            ScrollToEnd();
        }
    }

    private void FinishStreaming(
        ArcMarkdownStreamingSession session,
        ArcMarkdownView view,
        ArcMarkdownChatMessage target,
        int insertIndex,
        bool aborted)
    {
        if (_disposed)
        {
            return;
        }

        DetachSession(session);
        target.Markdown = session.CurrentText;
        if (aborted)
        {
            target.Status = ArcMarkdownChatMessageStatus.Aborted;
            target.Error ??= "已中断";
        }
        else if (session.LastWorkerError is { } error)
        {
            target.Status = ArcMarkdownChatMessageStatus.Failed;
            target.Error = error.Message;
        }
        else
        {
            target.Status = ArcMarkdownChatMessageStatus.Complete;
            target.Error = null;
        }

        var isCurrent = ReferenceEquals(_session, session);
        if (isCurrent)
        {
            _session = null;
            _streamingTarget = null;
        }

        UpsertFinishedMessage(target, insertIndex);
        if (isCurrent)
        {
            view.IsVisible = false;
            if (_slot is not null)
            {
                _slot.IsVisible = false;
                _slot.Content = null;
            }

            view.Dispose();
        }
        else
        {
            // A newer stream took the slot; this view is detached and can go away after its final render.
            Dispatcher.UIThread.Post(view.Dispose, DispatcherPriority.Background);
        }

        if (ShouldAutoScrollToEnd())
        {
            ScrollToEnd();
        }
    }

    private void UpsertFinishedMessage(ArcMarkdownChatMessage message, int insertIndex)
    {
        var existing = IndexOf(message.Id);
        _mutating = true;
        try
        {
            if (existing >= 0)
            {
                if (!ReferenceEquals(Messages[existing], message))
                {
                    Messages[existing] = message;
                }
            }
            else if (insertIndex >= 0 && insertIndex <= Messages.Count)
            {
                Messages.Insert(insertIndex, message);
            }
            else
            {
                Messages.Add(message);
            }
        }
        finally
        {
            _mutating = false;
        }

        if (existing >= 0)
        {
            MessageUpdated?.Invoke(this, message);
        }
        else
        {
            MessageAdded?.Invoke(this, message);
        }
    }

    private void DetachSession(ArcMarkdownStreamingSession session)
    {
        session.Updated -= OnStreamingUpdated;
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_mutating)
        {
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (var item in e.NewItems.OfType<ArcMarkdownChatMessage>())
                {
                    MessageAdded?.Invoke(this, item);
                }

                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (var item in e.OldItems.OfType<ArcMarkdownChatMessage>())
                {
                    MessageRemoved?.Invoke(this, item);
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    foreach (var item in e.OldItems.OfType<ArcMarkdownChatMessage>())
                    {
                        MessageRemoved?.Invoke(this, item);
                    }
                }

                if (e.NewItems is not null)
                {
                    foreach (var item in e.NewItems.OfType<ArcMarkdownChatMessage>())
                    {
                        MessageAdded?.Invoke(this, item);
                    }
                }

                break;
        }

        if (e.Action != NotifyCollectionChangedAction.Move && ShouldAutoScrollToEnd())
        {
            ScrollToEnd();
        }
    }

    private void OnOptionsChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (_session is not null && _slot?.Content is ArcMarkdownView streamingView)
        {
            streamingView.Options = Options;
        }

        RefreshRows();
    }

    private void OnAutoScrollModeChanged()
    {
        if (_list is not null)
        {
            _list.StickToBottom = AutoScrollMode == ArcMarkdownChatAutoScrollMode.Always;
        }

        if (AutoScrollMode == ArcMarkdownChatAutoScrollMode.Always)
        {
            ScrollToEnd();
        }
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            ArcMarkdownStyle.Current = new ArcMarkdownStyle();
            RefreshRows();
        }, DispatcherPriority.Background);
    }

    private void RefreshRows()
    {
        if (_disposed || _list is null)
        {
            return;
        }

        var offset = _scroll?.Offset ?? default;
        _list.ItemsSource = null;
        _list.ItemTemplate = BuildItemTemplate();
        _list.ItemsSource = Messages;
        if (_scroll is not null && offset.Y > 0)
        {
            var scroll = _scroll;
            Dispatcher.UIThread.Post(() => scroll.Offset = new Vector(scroll.Offset.X, offset.Y), DispatcherPriority.Loaded);
        }
    }

    private void UpdateSlotMaxHeight()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.MaxHeight = Math.Max(160, Bounds.Height * 0.5);
    }

    private void HookScrollViewer()
    {
        if (_list is null)
        {
            return;
        }

        var scroll = _list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (ReferenceEquals(scroll, _scroll))
        {
            return;
        }

        DetachScrollViewer();
        _scroll = scroll;
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _lastOffset = Math.Max(0, _scroll.Offset.Y);
        }
    }

    private void DetachScrollViewer()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
        {
            return;
        }

        var offset = Math.Max(0, scroll.Offset.Y);
        var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var distance = maximum - offset;

        // Only a real upward move pauses follow; content growth (extent) does not.
        if (offset + 0.5 < _lastOffset)
        {
            _follow = false;
        }

        if (distance <= NearBottomThreshold)
        {
            _follow = true;
        }

        _lastOffset = offset;
    }

    private bool ShouldAutoScrollToEnd()
        => AutoScrollMode switch
        {
            ArcMarkdownChatAutoScrollMode.Always => true,
            ArcMarkdownChatAutoScrollMode.NearBottom => _follow,
            _ => false,
        };

    private static string RoleLabel(ArcMarkdownChatRole role) => role switch
    {
        ArcMarkdownChatRole.User => "你",
        ArcMarkdownChatRole.System => "系统",
        _ => "助手",
    };

    static ArcMarkdownChatView()
    {
        OptionsProperty.Changed.AddClassHandler<ArcMarkdownChatView>((view, _) => view.OnOptionsChanged());
        AutoScrollModeProperty.Changed.AddClassHandler<ArcMarkdownChatView>((view, _) => view.OnAutoScrollModeChanged());
    }
}
