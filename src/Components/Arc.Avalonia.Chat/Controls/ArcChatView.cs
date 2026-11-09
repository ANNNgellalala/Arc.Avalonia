using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Chat;

/// <summary>Grouping information computed for one message row.</summary>
internal readonly record struct ArcChatGrouping(
    bool IsOwn,
    bool FirstInGroup,
    bool LastInGroup,
    bool ShowHeader,
    bool ShowStatus,
    bool ShowAvatar);

/// <summary>
/// Bubble conversation surface: a virtualized list of <see cref="ArcChatBubble"/> rows grouped by author
/// and time with day separators, a streaming slot outside the list, a typing row, an empty state and an
/// optional composer. Every body is a Markdown <see cref="ArcMarkdownView"/>; the same surface serves human
/// chat and AI answers, while the agent layer composes bubbles with its full-width event cards.
/// </summary>
[TemplatePart("PART_List", typeof(ArcVirtualList))]
[TemplatePart("PART_StreamingSlot", typeof(ContentControl))]
[TemplatePart("PART_Typing", typeof(ContentControl))]
[TemplatePart("PART_ComposerHost", typeof(ContentControl))]
[TemplatePart("PART_Empty", typeof(ContentControl))]
public class ArcChatView : TemplatedControl, IDisposable
{
    /// <summary>Distance from the end (px) that still counts as "near bottom".</summary>
    public const double NearBottomThreshold = 56;

    public static readonly StyledProperty<ArcChatAutoScrollMode> AutoScrollModeProperty =
        AvaloniaProperty.Register<ArcChatView, ArcChatAutoScrollMode>(
            nameof(AutoScrollMode), ArcChatAutoScrollMode.NearBottom);

    public static readonly StyledProperty<ArcMarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ArcChatView, ArcMarkdownOptions?>(nameof(MarkdownOptions));

    public static readonly StyledProperty<IEnumerable<ArcChatParticipant>?> ParticipantsProperty =
        AvaloniaProperty.Register<ArcChatView, IEnumerable<ArcChatParticipant>?>(nameof(Participants));

    public static readonly StyledProperty<string> CurrentUserIdProperty =
        AvaloniaProperty.Register<ArcChatView, string>(nameof(CurrentUserId), "me");

    public static readonly StyledProperty<IEnumerable<string>?> TypingProperty =
        AvaloniaProperty.Register<ArcChatView, IEnumerable<string>?>(nameof(Typing));

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcChatView, string>(nameof(Placeholder), "输入消息…");

    public static readonly StyledProperty<bool> ShowComposerProperty =
        AvaloniaProperty.Register<ArcChatView, bool>(nameof(ShowComposer), true);

    public static readonly StyledProperty<bool> AllowAttachmentsProperty =
        AvaloniaProperty.Register<ArcChatView, bool>(nameof(AllowAttachments), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcChatView, string>(nameof(Label), "对话");

    public static readonly StyledProperty<TimeSpan> GroupWindowProperty =
        AvaloniaProperty.Register<ArcChatView, TimeSpan>(nameof(GroupWindow), TimeSpan.FromMinutes(5));

    public static readonly StyledProperty<bool> ShowAvatarsProperty =
        AvaloniaProperty.Register<ArcChatView, bool>(nameof(ShowAvatars), true);

    public static readonly StyledProperty<bool> ShowTimestampsProperty =
        AvaloniaProperty.Register<ArcChatView, bool>(nameof(ShowTimestamps), true);

    /// <summary>
    /// Distance from the end (px) at which auto-follow re-arms after the user dragged or scrolled up.
    /// Defaults to <see cref="NearBottomThreshold"/>.
    /// </summary>
    public static readonly StyledProperty<double> AutoFollowResumeThresholdProperty =
        AvaloniaProperty.Register<ArcChatView, double>(nameof(AutoFollowResumeThreshold), NearBottomThreshold);

    /// <summary>Soft-keyboard inset supplied by the host; the composer is lifted by its bottom value.</summary>
    public static readonly StyledProperty<Thickness> KeyboardInsetsProperty =
        AvaloniaProperty.Register<ArcChatView, Thickness>(nameof(KeyboardInsets));

    /// <summary>Device safe-area inset supplied by the host; the composer is lifted by its bottom value.</summary>
    public static readonly StyledProperty<Thickness> SafeAreaProperty =
        AvaloniaProperty.Register<ArcChatView, Thickness>(nameof(SafeArea));

    private readonly Dictionary<string, ArcChatParticipant> _people = new(StringComparer.Ordinal);
    private readonly List<Ellipse> _typingDots = [];
    private ArcVirtualList? _list;
    private ContentControl? _streamingSlot;
    private ContentControl? _typingHost;
    private ContentControl? _composerHost;
    private ContentControl? _emptyHost;
    private ScrollViewer? _scroll;
    private ArcChatComposer? _composer;
    private ArcChatBubble? _slotBubble;
    private ArcMarkdownStreamingSession? _session;
    private ArcChatMessage? _streamingTarget;
    private DispatcherTimer? _typingTimer;
    private IEnumerable<ArcChatMessage>? _defaultMessages;
    private int _typingPhase;
    private int _mutationDepth;
    private string? _animateId;
    private bool _scrollPosted;
    private bool _follow = true;
    private double _lastOffset;
    private bool _themeSubscribed;
    private bool _defaultsLoaded;
    private bool _disposed;

    // Touch handling: a drag latches "user is scrolling" so streaming appends never yank the viewport
    // back to the end, and taps after a drag are not treated as clicks (8 px slop).
    private const double DragSlop = 8;
    private const double NarrowScrollBarWidth = 480;
    private Point _pointerOrigin;
    private bool _pointerPressed;
    private bool _pointerMoved;
    private bool _userScrolling;

    static ArcChatView()
    {
        AutoScrollModeProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnAutoScrollModeChanged());
        MarkdownOptionsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnMarkdownOptionsChanged());
        ParticipantsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnParticipantsChanged());
        CurrentUserIdProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnParticipantsChanged());
        TypingProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.RenderTyping());
        PlaceholderProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnPlaceholderChanged());
        ShowComposerProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.ApplyComposerVisibility());
        AllowAttachmentsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.OnAllowAttachmentsChanged());
        LabelProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.UpdateAutomation());
        GroupWindowProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.RefreshRows());
        ShowAvatarsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.RefreshRows());
        ShowTimestampsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.RefreshRows());
        KeyboardInsetsProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.UpdateComposerInsets());
        SafeAreaProperty.Changed.AddClassHandler<ArcChatView>((view, _) => view.UpdateComposerInsets());
    }

    /// <summary>Creates the chat surface.</summary>
    public ArcChatView()
    {
        Messages = new ObservableCollection<ArcChatMessage>();
        Messages.CollectionChanged += OnMessagesCollectionChanged;
    }

    /// <summary>Messages, oldest first. The collection may be mutated directly; the surface mirrors it.</summary>
    public ObservableCollection<ArcChatMessage> Messages { get; }

    /// <summary>Auto-scroll policy.</summary>
    public ArcChatAutoScrollMode AutoScrollMode
    {
        get => GetValue(AutoScrollModeProperty);
        set => SetValue(AutoScrollModeProperty, value);
    }

    /// <summary>Markdown options for every bubble body.</summary>
    public ArcMarkdownOptions? MarkdownOptions
    {
        get => GetValue(MarkdownOptionsProperty);
        set => SetValue(MarkdownOptionsProperty, value);
    }

    /// <summary>Participants used to resolve author names and avatars.</summary>
    public IEnumerable<ArcChatParticipant>? Participants
    {
        get => GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }

    /// <summary>Id of the local user; their messages align right.</summary>
    public string CurrentUserId
    {
        get => GetValue(CurrentUserIdProperty);
        set => SetValue(CurrentUserIdProperty, value);
    }

    /// <summary>Ids of participants currently typing.</summary>
    public IEnumerable<string>? Typing
    {
        get => GetValue(TypingProperty);
        set => SetValue(TypingProperty, value);
    }

    /// <summary>Composer placeholder.</summary>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Whether the composer is shown.</summary>
    public bool ShowComposer
    {
        get => GetValue(ShowComposerProperty);
        set => SetValue(ShowComposerProperty, value);
    }

    /// <summary>Whether the composer allows attachments.</summary>
    public bool AllowAttachments
    {
        get => GetValue(AllowAttachmentsProperty);
        set => SetValue(AllowAttachmentsProperty, value);
    }

    /// <summary>Accessible label of the conversation.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Messages from the same author within this window are grouped into one bubble run.</summary>
    public TimeSpan GroupWindow
    {
        get => GetValue(GroupWindowProperty);
        set => SetValue(GroupWindowProperty, value);
    }

    /// <summary>Whether avatars are shown.</summary>
    public bool ShowAvatars
    {
        get => GetValue(ShowAvatarsProperty);
        set => SetValue(ShowAvatarsProperty, value);
    }

    /// <summary>Whether timestamps are shown.</summary>
    public bool ShowTimestamps
    {
        get => GetValue(ShowTimestampsProperty);
        set => SetValue(ShowTimestampsProperty, value);
    }

    /// <summary>Re-arm threshold for auto-follow after a user drag (see <see cref="AutoFollowResumeThresholdProperty"/>).</summary>
    public double AutoFollowResumeThreshold
    {
        get => GetValue(AutoFollowResumeThresholdProperty);
        set => SetValue(AutoFollowResumeThresholdProperty, value);
    }

    /// <summary>Host-supplied soft-keyboard inset; forwarded to the composer.</summary>
    public Thickness KeyboardInsets
    {
        get => GetValue(KeyboardInsetsProperty);
        set => SetValue(KeyboardInsetsProperty, value);
    }

    /// <summary>Host-supplied safe-area inset; forwarded to the composer.</summary>
    public Thickness SafeArea
    {
        get => GetValue(SafeAreaProperty);
        set => SetValue(SafeAreaProperty, value);
    }

    /// <summary>True while a touch/pointer drag has paused auto-follow until the viewport returns near the end.</summary>
    public bool IsAutoFollowPaused => _userScrolling;

    /// <summary>Built-in composer (created by the template).</summary>
    public ArcChatComposer? Composer => _composer;

    /// <summary>Active streaming session, if any.</summary>
    public ArcMarkdownStreamingSession? ActiveSession => _session;

    /// <summary>Whether a message is currently streaming.</summary>
    public bool IsStreaming => _session?.State is ArcMarkdownStreamingState.Streaming or ArcMarkdownStreamingState.Created;

    /// <summary>Loads initial messages (the obsolete ArcChatThread compatibility surface).</summary>
    public IEnumerable<ArcChatMessage>? DefaultMessages
    {
        get => _defaultMessages;
        set
        {
            _defaultMessages = value;
            LoadDefaults();
        }
    }

    /// <summary>Raised when a message is added.</summary>
    public event EventHandler<ArcChatMessage>? MessageAdded;

    /// <summary>Raised when a message body/status changes.</summary>
    public event EventHandler<ArcChatMessage>? MessageUpdated;

    /// <summary>Raised when a message is removed.</summary>
    public event EventHandler<ArcChatMessage>? MessageRemoved;

    /// <summary>Raised when the composer sends a draft.</summary>
    public event EventHandler<ArcChatDraft>? Send;

    /// <summary>Raised when the user stops the active run.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Raised for agent-component interactions inside a bubble.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Raised for renderer warnings inside a bubble.</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs>? Warning;

    /// <summary>Raised for accessibility announcements.</summary>
    public event EventHandler<string>? Announced;

    /// <summary>Focused message id; used by ScrollToMessage.</summary>
    public string? FocusedMessageId { get; private set; }

    /// <summary>Whether the viewport is close to the end.</summary>
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

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ArcVirtualList>("PART_List");
        _streamingSlot = e.NameScope.Find<ContentControl>("PART_StreamingSlot");
        _typingHost = e.NameScope.Find<ContentControl>("PART_Typing");
        _composerHost = e.NameScope.Find<ContentControl>("PART_ComposerHost");
        _emptyHost = e.NameScope.Find<ContentControl>("PART_Empty");

        if (_list is not null)
        {
            _list.ItemsSource = null;
            _list.ItemTemplate = BuildItemTemplate();
            _list.ItemsSource = Messages;
            _list.StickToBottom = AutoScrollMode == ArcChatAutoScrollMode.Always;
        }

        if (_streamingSlot is not null)
        {
            AutomationProperties.SetLiveSetting(_streamingSlot, AutomationLiveSetting.Polite);
        }

        if (_typingHost is not null)
        {
            AutomationProperties.SetLiveSetting(_typingHost, AutomationLiveSetting.Polite);
        }

        BuildComposer();
        ApplyComposerVisibility();
        RebuildPeople();
        RenderTyping();
        UpdateEmptyState();
        UpdateAutomation();
        AttachStreamingSlot();
        LoadDefaults();
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
        StopTypingTimer();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerPressed = true;
        _pointerMoved = false;
        _pointerOrigin = e.GetPosition(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerPressed || _pointerMoved)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pointerOrigin.X) <= DragSlop && Math.Abs(position.Y - _pointerOrigin.Y) <= DragSlop)
        {
            return;
        }

        // A real drag: pause follow immediately so content growth (streaming) cannot fight the finger.
        _pointerMoved = true;
        _userScrolling = true;
        _follow = false;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pointerPressed = false;
        _pointerMoved = false;
        ResumeFollowIfNearEnd();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointerPressed = false;
        _pointerMoved = false;
        ResumeFollowIfNearEnd();
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Wheel is not a touch gesture, so it is never the only pause signal - but it is a cheap extra hint:
        // scrolling up (Delta.Y > 0) moves away from the end and pauses follow; scrolling back re-arms it.
        if (e.Delta.Y > 0)
        {
            _userScrolling = true;
            _follow = false;
        }
        else if (e.Delta.Y < 0)
        {
            ResumeFollowIfNearEnd();
        }
    }

    private void ResumeFollowIfNearEnd()
    {
        if (_scroll is null)
        {
            return;
        }

        var maximum = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        if (maximum - Math.Max(0, _scroll.Offset.Y) <= Math.Max(0, AutoFollowResumeThreshold))
        {
            _userScrolling = false;
            _follow = true;
        }
    }

    /// <summary>Adds a message to the end (same id updates in place).</summary>
    public void AddMessage(ArcChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var follow = ShouldFollow();
        PrepareMessage(message);
        var index = IndexOf(message.Id);
        if (index >= 0)
        {
            if (!ReferenceEquals(Messages[index], message))
            {
                Mutate(() => Messages[index] = message);
            }

            MessageUpdated?.Invoke(this, message);
        }
        else
        {
            _animateId = message.Id;
            Mutate(() => Messages.Add(message));
            MessageAdded?.Invoke(this, message);
        }

        if (follow)
        {
            ScrollToEnd();
        }
    }

    /// <summary>Inserts a message at an index (same id updates in place).</summary>
    public void InsertMessage(int index, ArcChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var follow = ShouldFollow();
        PrepareMessage(message);
        var existing = IndexOf(message.Id);
        if (existing >= 0)
        {
            AddMessage(message);
            return;
        }

        _animateId = message.Id;
        Mutate(() => Messages.Insert(Math.Clamp(index, 0, Messages.Count), message));
        MessageAdded?.Invoke(this, message);
        if (follow)
        {
            ScrollToEnd();
        }
    }

    /// <summary>Replaces a message with the same id (adds it when absent).</summary>
    public void UpdateMessage(ArcChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        PrepareMessage(message);
        var index = IndexOf(message.Id);
        if (index < 0)
        {
            AddMessage(message);
            return;
        }

        if (!ReferenceEquals(Messages[index], message))
        {
            Mutate(() => Messages[index] = message);
        }
        else
        {
            RefreshRows();
        }

        MessageUpdated?.Invoke(this, message);
    }

    /// <summary>Removes a message by id.</summary>
    public bool RemoveMessage(string messageId)
    {
        var index = IndexOf(messageId);
        if (index < 0)
        {
            return false;
        }

        var message = Messages[index];
        Mutate(() => Messages.RemoveAt(index));
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
        Mutate(Messages.Clear);
        foreach (var message in removed)
        {
            MessageRemoved?.Invoke(this, message);
        }
    }

    /// <summary>Replaces the whole list (fires removed then added for every affected message).</summary>
    public void ReplaceMessages(IEnumerable<ArcChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var replacement = messages.ToArray();
        foreach (var message in replacement)
        {
            PrepareMessage(message);
        }

        var removed = Messages.ToArray();
        Mutate(() =>
        {
            Messages.Clear();
            foreach (var message in replacement)
            {
                Messages.Add(message);
            }
        });

        foreach (var message in removed)
        {
            MessageRemoved?.Invoke(this, message);
        }

        foreach (var message in replacement)
        {
            MessageAdded?.Invoke(this, message);
        }

        if (ShouldFollow())
        {
            ScrollToEnd();
        }
    }

    /// <summary>Finds a message by id.</summary>
    public ArcChatMessage? FindMessage(string messageId)
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
    /// Starts streaming into a message (created when null). The message leaves the virtualized list while
    /// it streams (the dedicated slot below the list shows it) and is upserted back into
    /// <see cref="Messages"/> when the stream completes or aborts.
    /// </summary>
    public ArcMarkdownStreamingSession BeginStreamingMessage(ArcChatMessage? message = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is not null)
        {
            AbortStreaming();
        }

        var target = message ?? new ArcChatMessage { Role = ArcChatRole.Assistant };
        var insertIndex = IndexOf(target.Id);
        if (insertIndex >= 0)
        {
            var index = insertIndex;
            Mutate(() => Messages.RemoveAt(index));
        }

        target.Status = ArcChatMessageStatus.Streaming;
        target.Error = null;
        target.Markdown = string.Empty;
        _streamingTarget = target;

        // A fresh bubble per session: aborting the previous stream may still finish asynchronously, and it
        // must render into its own body instead of the next stream's slot.
        var bubble = CreateSlotBubble();
        _slotBubble = bubble;
        bubble.MarkdownOptions = MarkdownOptions;
        bubble.Message = target;
        bubble.ApplyGrouping(isOwn: false, firstInGroup: true, lastInGroup: true, showHeader: true, showStatus: true, showAvatar: ShowAvatars);

        if (_streamingSlot is not null)
        {
            _streamingSlot.Content = bubble;
            _streamingSlot.IsVisible = true;
        }

        bubble.ApplyTemplate();
        _streamingSlot?.UpdateLayout();

        var session = bubble.BeginStreaming() ?? BeginHiddenStreaming(target);
        _session = session;
        session.Updated += OnStreamingUpdated;
        session.Completed += (_, _) => FinishStreaming(session, target, insertIndex, aborted: false);
        session.Aborted += (_, _) => FinishStreaming(session, target, insertIndex, aborted: true);
        AttachStreamingSlot();

        if (_composer is not null)
        {
            _composer.IsRunning = true;
            _composer.FocusInput();
        }

        UpdateEmptyState();
        if (ShouldFollow())
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

        _ = session.AbortAsync();
    }

    /// <summary>Aborts the active stream and waits until the partial body has been written back.</summary>
    public async Task AbortStreamingAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        await session.AbortAsync().ConfigureAwait(true);
    }

    /// <summary>Scrolls to the newest message and re-arms follow mode.</summary>
    public void ScrollToEnd()
    {
        _follow = true;
        _userScrolling = false;
        RequestScrollToEnd();
    }

    /// <summary>Scrolls a message into view.</summary>
    public void ScrollToMessage(string messageId)
    {
        FocusedMessageId = messageId;
        var index = IndexOf(messageId);
        if (index >= 0 && _list is not null)
        {
            _list.ScrollToIndex(index);
        }
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

        StopTypingTimer();
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

        if (_streamingSlot is not null)
        {
            _streamingSlot.Content = null;
        }

        _slotBubble = null;
        _composer = null;
        GC.SuppressFinalize(this);
    }

    internal bool ShouldFollow() => AutoScrollMode switch
    {
        ArcChatAutoScrollMode.Never => false,
        // "Always" means the transcript follows new content, but never while the reader is actively
        // dragging or has scrolled away - that is the touch feedback bug this slice fixes.
        ArcChatAutoScrollMode.Always => !_userScrolling,
        _ => _follow && !_userScrolling,
    };

    internal void OnBubbleInteraction(ArcMarkdownInteractionEventArgs args) => InteractionOccurred?.Invoke(this, args);

    internal void OnBubbleWarning(ArcMarkdownWarningEventArgs args) => Warning?.Invoke(this, args);

    internal void OnBubbleAnnounced(string text) => Announced?.Invoke(this, text);

    internal void PrepareMessage(ArcChatMessage message)
    {
        if (message.Author is null && message.Role != ArcChatRole.System)
        {
            message.Author = ResolveParticipant(message);
        }
    }

    internal ArcChatGrouping GetGroupState(ArcChatMessage message)
    {
        if (message.Role == ArcChatRole.System)
        {
            return new ArcChatGrouping(false, true, true, false, false, false);
        }

        var index = IndexOf(message.Id);
        var previous = index > 0 ? Messages[index - 1] : null;
        var next = index >= 0 && index + 1 < Messages.Count ? Messages[index + 1] : null;
        var own = IsOwnMessage(message);
        var first = !SameRun(previous, message);
        var last = !SameRun(next, message);
        return new ArcChatGrouping(
            own,
            first,
            last,
            first && ShowTimestamps,
            last,
            ShowAvatars && !own);
    }

    internal bool ConsumeAnimation(string messageId)
    {
        if (!string.Equals(_animateId, messageId, StringComparison.Ordinal))
        {
            return false;
        }

        _animateId = null;
        return true;
    }

    private FuncDataTemplate<ArcChatMessage> BuildItemTemplate()
        => new(
            (message, _) => message is null ? new Panel() : new ArcChatRow(this, message),
            supportsRecycling: false);

    private ArcMarkdownStreamingSession BeginHiddenStreaming(ArcChatMessage target)
    {
        // Defensive fallback: the body view always exists on ArcChatBubble, but a hostile subclass can still
        // return null; stream into a hidden view so the model keeps updating.
        var view = new ArcMarkdownView { Options = MarkdownOptions, Markdown = target.Markdown };
        return view.BeginStreaming();
    }

    private ArcChatBubble CreateSlotBubble()
    {
        var bubble = new ArcChatBubble();
        bubble.InteractionOccurred += (_, args) => OnBubbleInteraction(args);
        bubble.Warning += (_, args) => OnBubbleWarning(args);
        bubble.Announced += (_, text) => OnBubbleAnnounced(text);
        return bubble;
    }

    private void AttachStreamingSlot()
    {
        if (_streamingSlot is null)
        {
            return;
        }

        if (_session is not null && _slotBubble is not null)
        {
            _streamingSlot.Content = _slotBubble;
            _streamingSlot.IsVisible = true;
        }
        else
        {
            _streamingSlot.IsVisible = false;
        }
    }

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

        if (ShouldFollow())
        {
            ScrollToEnd();
        }

        UpdateEmptyState();
    }

    private void FinishStreaming(
        ArcMarkdownStreamingSession session,
        ArcChatMessage target,
        int insertIndex,
        bool aborted)
    {
        if (_disposed)
        {
            return;
        }

        session.Updated -= OnStreamingUpdated;
        var current = ReferenceEquals(_session, session);
        if (current)
        {
            _session = null;
            _streamingTarget = null;
            _slotBubble = null;
            if (_streamingSlot is not null)
            {
                _streamingSlot.IsVisible = false;
                _streamingSlot.Content = null;
            }

            if (_composer is not null)
            {
                _composer.IsRunning = false;
            }
        }

        target.Markdown = session.CurrentText;
        if (aborted)
        {
            target.Status = ArcChatMessageStatus.Aborted;
            target.Error ??= "已中断";
        }
        else if (session.LastWorkerError is { } error)
        {
            target.Status = ArcChatMessageStatus.Failed;
            target.Error = error.Message;
        }
        else
        {
            target.Status = ArcChatMessageStatus.Complete;
            target.Error = null;
        }

        _animateId = target.Id;
        UpsertFinished(target, insertIndex);
        Announced?.Invoke(this, aborted
            ? "已中断"
            : target.Status == ArcChatMessageStatus.Failed ? "生成失败" : "回复完成");
        UpdateEmptyState();
        if (ShouldFollow())
        {
            ScrollToEnd();
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (string.Equals(_animateId, target.Id, StringComparison.Ordinal))
            {
                _animateId = null;
            }
        }, DispatcherPriority.Background);
    }

    private void UpsertFinished(ArcChatMessage message, int insertIndex)
    {
        var existing = IndexOf(message.Id);
        if (existing >= 0)
        {
            if (!ReferenceEquals(Messages[existing], message))
            {
                Mutate(() => Messages[existing] = message);
            }
            else
            {
                RefreshRows();
            }

            MessageUpdated?.Invoke(this, message);
            return;
        }

        if (insertIndex >= 0 && insertIndex <= Messages.Count)
        {
            Mutate(() => Messages.Insert(insertIndex, message));
        }
        else
        {
            Mutate(() => Messages.Add(message));
        }

        MessageAdded?.Invoke(this, message);
    }

    private void Mutate(Action action)
    {
        _mutationDepth++;
        try
        {
            action();
        }
        finally
        {
            _mutationDepth--;
        }

        if (_mutationDepth == 0)
        {
            AfterMessagesChanged();
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_mutationDepth > 0)
        {
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (var item in e.NewItems.OfType<ArcChatMessage>())
                {
                    PrepareMessage(item);
                    MessageAdded?.Invoke(this, item);
                }

                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (var item in e.OldItems.OfType<ArcChatMessage>())
                {
                    MessageRemoved?.Invoke(this, item);
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    foreach (var item in e.OldItems.OfType<ArcChatMessage>())
                    {
                        MessageRemoved?.Invoke(this, item);
                    }
                }

                if (e.NewItems is not null)
                {
                    foreach (var item in e.NewItems.OfType<ArcChatMessage>())
                    {
                        PrepareMessage(item);
                        MessageAdded?.Invoke(this, item);
                    }
                }

                break;
        }

        AfterMessagesChanged();
        if (e.Action != NotifyCollectionChangedAction.Move && ShouldFollow())
        {
            ScrollToEnd();
        }
    }

    private void AfterMessagesChanged()
    {
        if (_disposed)
        {
            return;
        }

        RefreshRows();
        UpdateEmptyState();
    }

    private void RefreshRows()
    {
        if (_disposed || _list is null)
        {
            return;
        }

        foreach (var row in _list.GetVisualDescendants().OfType<ArcChatRow>())
        {
            row.Refresh();
        }
    }

    private void BuildComposer()
    {
        if (_composerHost is null)
        {
            return;
        }

        if (_composer is null)
        {
            _composer = new ArcChatComposer { Placeholder = Placeholder, AllowAttachments = AllowAttachments };
            _composer.Send += (_, draft) => Send?.Invoke(this, draft);
            _composer.StopRequested += (_, _) => OnComposerStopRequested();
        }

        _composerHost.Content = _composer;
        ApplyComposerVisibility();
        UpdateComposerInsets();
    }

    /// <summary>Lifts the composer by the host-supplied keyboard and safe-area bottom insets.</summary>
    private void UpdateComposerInsets()
    {
        if (_composerHost is null)
        {
            return;
        }

        if (_composer is not null)
        {
            _composer.KeyboardInsets = KeyboardInsets;
            _composer.SafeArea = SafeArea;
        }
    }

    private void OnComposerStopRequested()
    {
        AbortStreaming();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyComposerVisibility()
    {
        if (_composerHost is not null)
        {
            _composerHost.IsVisible = ShowComposer;
        }
    }

    private void OnAutoScrollModeChanged()
    {
        if (_list is not null)
        {
            _list.StickToBottom = AutoScrollMode == ArcChatAutoScrollMode.Always;
        }

        if (AutoScrollMode == ArcChatAutoScrollMode.Always)
        {
            ScrollToEnd();
        }
    }

    private void OnMarkdownOptionsChanged()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var bubble in this.GetVisualDescendants().OfType<ArcChatBubble>())
        {
            bubble.MarkdownOptions = MarkdownOptions;
        }

        if (_slotBubble is not null)
        {
            _slotBubble.MarkdownOptions = MarkdownOptions;
        }
    }

    private void OnParticipantsChanged()
    {
        RebuildPeople();
        RefreshRows();
    }

    private void OnPlaceholderChanged()
    {
        if (_composer is not null)
        {
            _composer.Placeholder = Placeholder;
        }
    }

    private void OnAllowAttachmentsChanged()
    {
        if (_composer is not null)
        {
            _composer.AllowAttachments = AllowAttachments;
        }
    }

    private void RebuildPeople()
    {
        _people.Clear();
        foreach (var participant in Participants ?? Enumerable.Empty<ArcChatParticipant>())
        {
            _people[participant.Id] = participant;
        }

        if (!string.IsNullOrEmpty(CurrentUserId) && !_people.ContainsKey(CurrentUserId))
        {
            _people[CurrentUserId] = new ArcChatParticipant { Id = CurrentUserId, Name = "你" };
        }
    }

    private ArcChatParticipant ResolveParticipant(ArcChatMessage message)
    {
        if (message.Role == ArcChatRole.User)
        {
            return _people.TryGetValue(CurrentUserId, out var self)
                ? self
                : new ArcChatParticipant { Id = CurrentUserId, Name = "你" };
        }

        return new ArcChatParticipant
        {
            Id = message.Role.ToString().ToLowerInvariant(),
            Name = message.Role == ArcChatRole.System ? "系统" : "助手",
        };
    }

    private bool IsOwnMessage(ArcChatMessage message)
    {
        if (!string.IsNullOrEmpty(message.Author?.Id))
        {
            return string.Equals(message.Author!.Id, CurrentUserId, StringComparison.Ordinal);
        }

        return message.Role == ArcChatRole.User;
    }

    private bool SameRun(ArcChatMessage? other, ArcChatMessage current)
    {
        if (other is null || current.Role == ArcChatRole.System || other.Role != current.Role)
        {
            return false;
        }

        if (other.CreatedAt.ToLocalTime().Date != current.CreatedAt.ToLocalTime().Date)
        {
            return false;
        }

        var delta = current.CreatedAt - other.CreatedAt;
        if (delta < TimeSpan.Zero)
        {
            delta = delta.Negate();
        }

        if (delta > GroupWindow)
        {
            return false;
        }

        return string.Equals(AuthorId(other), AuthorId(current), StringComparison.Ordinal);
    }

    private string AuthorId(ArcChatMessage message)
        => message.Author?.Id
           ?? (message.Role == ArcChatRole.User ? CurrentUserId : message.Role.ToString().ToLowerInvariant());

    private void RenderTyping()
    {
        if (_typingHost is null)
        {
            return;
        }

        StopTypingTimer();
        var typing = (Typing ?? Enumerable.Empty<string>())
            .Where(id => !string.Equals(id, CurrentUserId, StringComparison.Ordinal))
            .Select(id => _people.TryGetValue(id, out var person)
                ? person
                : new ArcChatParticipant { Id = id, Name = id })
            .ToArray();

        if (typing.Length == 0)
        {
            _typingHost.Content = null;
            _typingHost.IsVisible = false;
            return;
        }

        var names = typing.Select(person => person.Name).ToArray();
        _typingHost.Content = BuildTypingRow(typing, names);
        _typingHost.IsVisible = true;
        AutomationProperties.SetName(_typingHost, $"{string.Join("、", names)} 正在输入");
        Announced?.Invoke(this, names.Length == 1
            ? $"{names[0]} 正在输入"
            : $"{string.Join("、", names)} 正在输入");
    }

    private Control BuildTypingRow(IReadOnlyList<ArcChatParticipant> typing, IReadOnlyList<string> names)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("36,*"),
            Margin = new Thickness(0, 6, 0, 6),
        };

        var avatar = ArcChatUi.Avatar(typing[0], own: false, diameter: 24);
        avatar.Margin = new Thickness(0, 2, 8, 0);
        grid.Children.Add(avatar);

        var dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 14,
        };
        _typingDots.Clear();
        for (var i = 0; i < 3; i++)
        {
            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.35,
            };
            dot.Paint(Shape.FillProperty, ArcTokens.TextMuted);
            _typingDots.Add(dot);
            dots.Children.Add(dot);
        }

        StartTypingTimer();
        var bubbleBorder = new Border
        {
            Background = null,
            CornerRadius = ArcChatUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10, 14, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = dots,
        };
        bubbleBorder.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        bubbleBorder.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        AutomationProperties.SetName(bubbleBorder, $"{string.Join("、", names)} 正在输入");
        Grid.SetColumn(bubbleBorder, 1);
        grid.Children.Add(bubbleBorder);
        return grid;
    }

    private void StartTypingTimer()
    {
        if (ArcThemeManager.ReducedMotion || _typingDots.Count == 0)
        {
            foreach (var dot in _typingDots)
            {
                dot.Opacity = 0.55;
            }

            return;
        }

        _typingPhase = 0;
        _typingTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(320), DispatcherPriority.Render, (_, _) =>
        {
            _typingPhase = (_typingPhase + 1) % 3;
            for (var i = 0; i < _typingDots.Count; i++)
            {
                _typingDots[i].Opacity = (i + _typingPhase) % 3 == 0 ? 1 : 0.35;
            }
        });
        _typingTimer.Start();
    }

    private void StopTypingTimer()
    {
        _typingTimer?.Stop();
        _typingTimer = null;
        _typingDots.Clear();
    }

    private void UpdateEmptyState()
    {
        if (_emptyHost is null)
        {
            return;
        }

        var empty = Messages.Count == 0 && _session is null;
        _emptyHost.IsVisible = empty;
        if (!empty)
        {
            _emptyHost.Content = null;
            return;
        }

        if (_emptyHost.Content is null)
        {
            var text = ArcChatUi.Text("还没有消息，开始对话吧。", ArcTokens.TextSm, ArcTokens.TextMuted);
            var frame = new Border
            {
                CornerRadius = ArcChatUi.Radius(ArcTokens.RadiusControl),
                Padding = new Thickness(14, 12, 14, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = text,
            };
            frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            _emptyHost.Content = frame;
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label);
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, Label);
            AutomationProperties.SetHelpText(_list, "消息列表，从旧到新");
        }
    }

    private void LoadDefaults()
    {
        if (_defaultsLoaded || _defaultMessages is null || Messages.Count > 0)
        {
            return;
        }

        _defaultsLoaded = true;
        var items = _defaultMessages.ToArray();
        foreach (var message in items)
        {
            PrepareMessage(message);
        }

        Mutate(() =>
        {
            foreach (var message in items)
            {
                Messages.Add(message);
            }
        });

        foreach (var message in items)
        {
            MessageAdded?.Invoke(this, message);
        }
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        // Token-bound brushes update themselves; this only refreshes anything built from a snapshot.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            RefreshRows();
            RenderTyping();
        }, DispatcherPriority.Background);
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
            ApplyScrollHostPolicy(scroll);
            return;
        }

        DetachScrollViewer();
        _scroll = scroll;
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _scroll.SizeChanged += OnScrollHostSizeChanged;
            _lastOffset = Math.Max(0, _scroll.Offset.Y);
            ApplyScrollHostPolicy(_scroll);
        }
    }

    /// <summary>
    /// Nested scroll disambiguation and non-persistent scrollbars: the transcript owns its own gestures
    /// (never chains to a page/sheet scroller) and hides the scrollbar on phone-width surfaces.
    /// </summary>
    private void ApplyScrollHostPolicy(ScrollViewer? scroll)
    {
        if (scroll is null)
        {
            return;
        }

        scroll.IsScrollChainingEnabled = false;
        scroll.AllowAutoHide = true;
        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        var narrow = Bounds.Width > 0 && Bounds.Width < NarrowScrollBarWidth;
        scroll.VerticalScrollBarVisibility = narrow ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void OnScrollHostSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyScrollHostPolicy(_scroll);

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyScrollHostPolicy(_scroll);
    }

    private void DetachScrollViewer()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.SizeChanged -= OnScrollHostSizeChanged;
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

        // Only a real upward move pauses follow; content growth does not.
        if (offset + 0.5 < _lastOffset)
        {
            _follow = false;
            _userScrolling = true;
        }

        // Re-arm only after the gesture ended (finger up) and the viewport is back near the end.
        if (!_pointerPressed && distance <= Math.Max(0, AutoFollowResumeThreshold))
        {
            _follow = true;
            _userScrolling = false;
        }

        _lastOffset = offset;
    }

    private void RequestScrollToEnd()
    {
        if (_scrollPosted)
        {
            return;
        }

        _scrollPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scrollPosted = false;
            _list?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
