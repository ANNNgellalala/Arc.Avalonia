using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PuraMarkdown.Syntax;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// The Arc Markdown document view: parses Markdown with the PuraMarkdown syntax layer and renders it
/// with Arc controls and tokens. Supports full documents, pre-parsed documents, append-only streaming
/// sessions, outline/anchor navigation, selection and copy, and agent-component interaction state.
/// </summary>
[TemplatePart("PART_Scroll", typeof(ScrollViewer))]
[TemplatePart("PART_Host", typeof(Panel))]
public partial class ArcMarkdownView : TemplatedControl, IDisposable
{
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<ArcMarkdownView, string>(nameof(Markdown), string.Empty);

    public static readonly StyledProperty<ArcMarkdownOptions?> OptionsProperty =
        AvaloniaProperty.Register<ArcMarkdownView, ArcMarkdownOptions?>(nameof(Options));

    private readonly List<(MarkdownObject Node, Control Control)> _committed = [];
    private readonly List<(MarkdownObject Node, Control Control)> _tailControls = [];
    private readonly Dictionary<string, Control> _anchors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcMarkdownInteraction> _interactionStates = new(StringComparer.Ordinal);

    private ScrollViewer? _scroll;
    private Panel? _host;
    private PuraDocument? _document;
    private ArcMarkdownStreamingSession? _activeSession;
    private string? _plainText;
    private bool _subscribedToTheme;
    private bool _disposed;

    // Touch follow-mode: a drag pauses streaming auto-scroll (mirrors the chat transcript behaviour).
    private const double DragSlop = 8;
    private Point _pointerOrigin;
    private bool _pointerPressed;
    private bool _userPausedFollow;

    static ArcMarkdownView()
    {
        MarkdownProperty.Changed.AddClassHandler<ArcMarkdownView>((view, _) => view.Render());
        OptionsProperty.Changed.AddClassHandler<ArcMarkdownView>((view, _) => view.Render());
    }

    /// <summary>Markdown source. Setting it re-renders the whole document.</summary>
    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Rendering options; null uses <see cref="ArcMarkdownOptions.Default"/>.</summary>
    public ArcMarkdownOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Raised when a link is clicked.</summary>
    public static readonly RoutedEvent<ArcMarkdownLinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<ArcMarkdownView, ArcMarkdownLinkClickedEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    /// <summary>Raised for recoverable renderer warnings.</summary>
    public static readonly RoutedEvent<ArcMarkdownWarningEventArgs> WarningEvent =
        RoutedEvent.Register<ArcMarkdownView, ArcMarkdownWarningEventArgs>(nameof(Warning), RoutingStrategies.Bubble);

    /// <summary>Raised when an interactive agent component reports a choice.</summary>
    public static readonly RoutedEvent<ArcMarkdownInteractionEventArgs> InteractionOccurredEvent =
        RoutedEvent.Register<ArcMarkdownView, ArcMarkdownInteractionEventArgs>(nameof(InteractionOccurred), RoutingStrategies.Bubble);

    /// <summary>Link click (routed).</summary>
    public event EventHandler<ArcMarkdownLinkClickedEventArgs> LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    /// <summary>Renderer warning (routed).</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs> Warning
    {
        add => AddHandler(WarningEvent, value);
        remove => RemoveHandler(WarningEvent, value);
    }

    /// <summary>Agent-component interaction (routed).</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs> InteractionOccurred
    {
        add => AddHandler(InteractionOccurredEvent, value);
        remove => RemoveHandler(InteractionOccurredEvent, value);
    }

    /// <summary>Accessibility announcement text (live region).</summary>
    public event EventHandler<string>? Announced;

    /// <summary>Top-level block metadata of the last render.</summary>
    public IReadOnlyList<ArcMarkdownBlockInfo> Blocks { get; private set; } = Array.Empty<ArcMarkdownBlockInfo>();

    /// <summary>Outline (headings and fold ranges) of the last parsed document.</summary>
    public PuraOutline? Outline => _document?.Outline;

    /// <summary>Diagnostics produced by the syntax sanitizer.</summary>
    public IReadOnlyList<PuraDiagnostic> Diagnostics => _document?.Diagnostics ?? Array.Empty<PuraDiagnostic>();

    /// <summary>Currently active streaming session, if any.</summary>
    public ArcMarkdownStreamingSession? ActiveSession => _activeSession;

    /// <summary>Whether a streaming session is appending.</summary>
    public bool IsStreaming => _activeSession?.State == ArcMarkdownStreamingState.Streaming;

    /// <summary>Persisted agent-interaction state, keyed by component id.</summary>
    public IReadOnlyDictionary<string, ArcMarkdownInteraction> InteractionStates => _interactionStates;

    /// <summary>Current options (never null).</summary>
    public ArcMarkdownOptions CurrentOptions => Options ?? ArcMarkdownOptions.Default;

    /// <summary>Scroll viewer hosting the blocks.</summary>
    public ScrollViewer? ScrollViewer => _scroll;

    /// <summary>Re-renders the current <see cref="Markdown"/>.</summary>
    public void Render()
    {
        if (_host is null)
        {
            return;
        }

        var options = CurrentOptions;
        var text = Markdown ?? string.Empty;
        var document = options.Pipeline.Syntax.ParseDocument(text, NextVersion(), SyntaxOptions(options));
        RenderDocument(document, isStreaming: false);
    }

    /// <summary>Renders an already parsed document without re-parsing.</summary>
    public void RenderDocument(PuraDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        RenderDocument(document, isStreaming: false);
    }

    /// <summary>Starts an append-only streaming session.</summary>
    public ArcMarkdownStreamingSession BeginStreaming(ArcMarkdownStreamingOptions? options = null)
    {
        AbortStreaming();
        _activeSession = new ArcMarkdownStreamingSession(this, options ?? new ArcMarkdownStreamingOptions());
        return _activeSession;
    }

    /// <summary>Aborts the active session, keeping the text received so far.</summary>
    public void AbortStreaming()
    {
        var session = _activeSession;
        _activeSession = null;
        if (session is { State: ArcMarkdownStreamingState.Streaming })
        {
            session.AbortSilently();
        }
    }

    /// <summary>Refreshes tokens and re-renders (call after a theme or accent change).</summary>
    public void RefreshTheme()
    {
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();
        Render();
    }

    /// <summary>Parses the text once without rendering, warming fonts and JIT paths.</summary>
    public void Prewarm(string markdown)
        => CurrentOptions.Pipeline.Syntax.ParseDocument(markdown ?? string.Empty, NextVersion(), SyntaxOptions(CurrentOptions));

    /// <summary>Returns the outline headings.</summary>
    public IReadOnlyList<PuraHeading> GetOutline()
        => _document?.Outline.Headings ?? Array.Empty<PuraHeading>();

    /// <summary>Extracts the plain text of the current document.</summary>
    public string GetPlainText()
    {
        if (_plainText is not null)
        {
            return _plainText;
        }

        if (_document is null)
        {
            return Markdown ?? string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var block in _document.Markdown)
        {
            AppendBlockText(block, builder);
        }

        _plainText = builder.ToString().TrimEnd();
        return _plainText;
    }

    /// <summary>Copies the plain text of the document to the clipboard.</summary>
    public async Task CopyAllAsync(CancellationToken cancellationToken = default)
    {
        var text = GetPlainText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        await CurrentOptions.Clipboard.SetTextAsync(text, cancellationToken).ConfigureAwait(true);
        if (ReferenceEquals(CurrentOptions.Clipboard, ArcMarkdownPlatform.DefaultClipboard))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text);
            }
        }
    }

    /// <summary>Scrolls the heading with the given outline anchor into view.</summary>
    public bool ScrollToAnchor(string anchor)
    {
        if (!_anchors.TryGetValue(anchor, out var control))
        {
            return false;
        }

        control.BringIntoView();
        return true;
    }

    /// <summary>Scrolls to the end (streaming follow mode).</summary>
    public void ScrollToEnd()
    {
        if (_scroll is not null)
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, _scroll.Extent.Height);
        }
    }

    /// <summary>Stores an agent interaction for re-render/virtualization restore.</summary>
    public void SetInteractionState(ArcMarkdownInteraction interaction)
        => _interactionStates[interaction.ComponentId] = interaction;

    /// <summary>Applies a set of stored interactions.</summary>
    public void ApplyInteractionStates(IEnumerable<ArcMarkdownInteraction> states)
    {
        foreach (var state in states)
        {
            _interactionStates[state.ComponentId] = state;
        }

        Render();
    }

    /// <summary>Clears stored interactions.</summary>
    public void ClearInteractionStates()
    {
        if (_interactionStates.Count == 0)
        {
            return;
        }

        _interactionStates.Clear();
        Render();
    }

    /// <summary>Announces a message to assistive technology.</summary>
    public void Announce(string message) => Announced?.Invoke(this, message);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }

        _scroll = e.NameScope.Find<ScrollViewer>("PART_Scroll");
        _host = e.NameScope.Find<Panel>("PART_Host");
        if (_scroll is not null)
        {
            _scroll.IsScrollChainingEnabled = false;
            _scroll.AllowAutoHide = true;
            _scroll.ScrollChanged += OnScrollChanged;
        }

        Render();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_subscribedToTheme || Application.Current is null)
        {
            return;
        }

        Application.Current.ActualThemeVariantChanged += OnThemeVariantChanged;
        _subscribedToTheme = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_subscribedToTheme && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _subscribedToTheme = false;
        }

        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Wheel is not a touch gesture: it is a helpful extra signal, never the only one. In Avalonia
        // Delta.Y > 0 scrolls up, i.e. away from the streaming end, so that direction pauses follow.
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y > 0)
        {
            PauseFollow();
        }
        else if (e.Delta.Y < 0)
        {
            ResumeFollowIfNearEnd();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerPressed = true;
        _pointerOrigin = e.GetPosition(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pointerOrigin.X) > DragSlop || Math.Abs(position.Y - _pointerOrigin.Y) > DragSlop)
        {
            PauseFollow();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pointerPressed = false;
        ResumeFollowIfNearEnd();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pointerPressed = false;
        ResumeFollowIfNearEnd();
    }

    private void PauseFollow()
    {
        _userPausedFollow = true;
        _activeSession?.NotifyUserScrolled();
    }

    private void ResumeFollowIfNearEnd()
    {
        if (!_userPausedFollow || _scroll is null)
        {
            return;
        }

        var maximum = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        if (maximum - Math.Max(0, _scroll.Offset.Y) <= 56)
        {
            _userPausedFollow = false;
            _activeSession?.NotifyUserScrolledToEnd();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
        {
            return;
        }

        var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var distance = maximum - Math.Max(0, scroll.Offset.Y);

        // Scrolling up (finger drag or wheel) pauses streaming follow: never yank the reader back down.
        if (e.OffsetDelta.Y < -0.5)
        {
            PauseFollow();
        }
        else if (!_pointerPressed && _userPausedFollow && distance <= 56)
        {
            _userPausedFollow = false;
            _activeSession?.NotifyUserScrolledToEnd();
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
        AbortStreaming();
        DetachThemeHandler();
        GC.SuppressFinalize(this);
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshTheme);

    private void DetachThemeHandler()
    {
        if (_subscribedToTheme && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _subscribedToTheme = false;
        }
    }

    internal static PuraSyntaxOptions SyntaxOptions(ArcMarkdownOptions options)
        => options.Pipeline.Syntax.CreateOptions(options.Limits, options.UrlPolicy);

    internal ArcMarkdownRenderContext CreateContext(PuraDocument document, bool isStreaming, bool isTail)
        => new()
        {
            Options = CurrentOptions,
            Renderers = CurrentOptions.Pipeline.Renderers,
            Sanitized = document.Sanitized ?? new PuraMarkdown.Security.SanitizeResult(),
            Dispatcher = Dispatcher.UIThread,
            IsStreaming = isStreaming,
            IsTail = isTail,
            ComponentStates = _interactionStates,
            InteractionSink = OnInteraction,
            WarningSink = warning => RaiseEvent(new ArcMarkdownWarningEventArgs(warning) { RoutedEvent = WarningEvent, Source = this }),
            LinkClickSink = click =>
            {
                var args = new ArcMarkdownLinkClickedEventArgs(click.Url, click.Text) { RoutedEvent = LinkClickedEvent, Source = this };
                RaiseEvent(args);
                click.Handled = args.HandledByHost;
            },
            ImageLoadingSink = _ => { },
            ImageLoadedSink = (_, _) => { },
        };

    private void OnInteraction(ArcMarkdownInteraction interaction)
    {
        if (!CurrentOptions.InteractionEnabled)
        {
            return;
        }

        _interactionStates[interaction.ComponentId] = interaction;
        RaiseEvent(new ArcMarkdownInteractionEventArgs(interaction) { RoutedEvent = InteractionOccurredEvent, Source = this });
    }

    private void RenderDocument(PuraDocument document, bool isStreaming)
    {
        if (_host is null)
        {
            _document = document;
            _plainText = null;
            return;
        }

        _host.Children.Clear();
        _committed.Clear();
        _tailControls.Clear();
        _anchors.Clear();

        var context = CreateContext(document, isStreaming, isTail: false);
        var index = 0;
        var blocks = new List<ArcMarkdownBlockInfo>(document.Markdown.Count);
        foreach (var node in document.Markdown)
        {
            var control = context.RenderBlock(node);
            AddBlockControl(node, control);
            blocks.Add(new ArcMarkdownBlockInfo(index++, node.GetType().Name, node.Span.Start, node.Span.End, node.Line));
            if (node is HeadingBlock heading)
            {
                var anchor = FindAnchor(document, heading);
                if (anchor is not null)
                {
                    _anchors[anchor] = control;
                }
            }
        }

        Blocks = blocks;
        _document = document;
        _plainText = null;
    }

    private void AddBlockControl(MarkdownObject node, Control control)
    {
        _host!.Children.Add(control);
        _committed.Add((node, control));
    }

    internal void ApplyStreamingWindows(PuraDocument committed, PuraDocument? tail)
    {
        if (_host is null)
        {
            _document = committed;
            return;
        }

        // Fall back to a full rebuild when the committed block list is not a prefix of what is on screen
        // (reference-definition reparse, page switch, theme change).
        var compatible = _committed.Count <= committed.Markdown.Count;
        if (compatible)
        {
            for (var i = 0; i < _committed.Count; i++)
            {
                if (!SameKind(_committed[i].Node, committed.Markdown[i]))
                {
                    compatible = false;
                    break;
                }
            }
        }

        if (!compatible)
        {
            RenderDocument(committed, isStreaming: true);
        }
        else
        {
            var context = CreateContext(committed, isStreaming: true, isTail: false);
            for (var i = _committed.Count; i < committed.Markdown.Count; i++)
            {
                var node = committed.Markdown[i];
                AddBlockControl(node, context.RenderBlock(node));
            }
        }

        var previousTail = _tailControls.ToArray();
        foreach (var entry in previousTail)
        {
            _host.Children.Remove(entry.Control);
        }

        _tailControls.Clear();

        if (tail is not null && tail.Markdown.Count > 0)
        {
            var tailContext = CreateContext(tail, isStreaming: true, isTail: true);
            var index = 0;
            foreach (var node in tail.Markdown)
            {
                Control control;
                if (index < previousTail.Length
                    && SameKind(previousTail[index].Node, node)
                    && TryUpdateIncremental(previousTail[index].Node, node, previousTail[index].Control, tailContext))
                {
                    control = previousTail[index].Control;
                }
                else
                {
                    control = tailContext.RenderBlock(node);
                }

                _tailControls.Add((node, control));
                _host.Children.Add(control);
                index++;
            }
        }

        _document = committed;
        _plainText = null;
        Blocks = _committed
            .Select((entry, index) => new ArcMarkdownBlockInfo(index, entry.Node.GetType().Name, entry.Node.Span.Start, entry.Node.Span.End, entry.Node.Line))
            .ToArray();
    }

    internal void ClearStreamingTail()
    {
        if (_host is null)
        {
            return;
        }

        foreach (var entry in _tailControls)
        {
            _host.Children.Remove(entry.Control);
        }

        _tailControls.Clear();
    }

    internal void SetActiveSession(ArcMarkdownStreamingSession? session) => _activeSession = session;

    internal int NextVersion() => ++_version;

    private int _version;

    private static bool SameKind(MarkdownObject left, MarkdownObject right)
        => left.GetType() == right.GetType();

    /// <summary>
    /// Streaming tail update: reuse the existing control when the node type is unchanged and the
    /// renderer implements <see cref="IArcMarkdownIncrementalRenderer"/>, so the unstable tail does
    /// not rebuild its whole visual tree on every flush.
    /// </summary>
    private static bool TryUpdateIncremental(
        MarkdownObject oldNode,
        MarkdownObject newNode,
        Control control,
        ArcMarkdownRenderContext context)
    {
        foreach (var renderer in context.Renderers.ResolveCandidates(newNode, context))
        {
            if (renderer is not IArcMarkdownIncrementalRenderer incremental)
            {
                continue;
            }

            try
            {
                if (!incremental.CanUpdate(oldNode, newNode, context))
                {
                    continue;
                }

                incremental.Update(control, newNode, context);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static string? FindAnchor(PuraDocument document, HeadingBlock heading)
    {
        // PuraOutline preserves heading order; match by inline text.
        var text = ArcMarkdownDefaultInlineRenderer.GetInlineText(heading.Inline).Trim();
        return document.Outline.Headings.FirstOrDefault(entry => entry.Level == heading.Level && entry.Text == text)?.Anchor;
    }

    private static void AppendBlockText(MarkdownObject block, StringBuilder builder)
    {
        switch (block)
        {
            case LeafBlock leaf when leaf.Inline is not null:
                builder.AppendLine(ArcMarkdownDefaultInlineRenderer.GetInlineText(leaf.Inline));
                break;
            case LeafBlock leaf when leaf.Lines.Lines is { } lines:
                foreach (var line in lines)
                {
                    builder.AppendLine(line.ToString());
                }

                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AppendBlockText(child, builder);
                }

                break;
        }
    }
}

