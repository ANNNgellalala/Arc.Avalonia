using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Markdig.Syntax;
using PuraMarkdown.Core;
using PuraMarkdown.Core.Security;
using PuraMarkdown.UI;
using PuraMarkdown.UI.Engine;
using PuraMarkdown.UI.Rendering;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// The Arc Markdown document view. 0.2 delegates parsing, rendering engine, streaming, virtualization and
/// scroll-follow behaviour to <see cref="PuraMarkdownView"/> (PuraMarkdown.UI); Arc keeps its own renderer
/// pipeline (registered as node renderers through <see cref="ArcMarkdownPipeline"/>), Arc tokens/theme and
/// the public Arc surface (events, outline, blocks, interaction state, copy/announce).
/// </summary>
[TemplatePart("PART_Scroll", typeof(ScrollViewer))]
[TemplatePart("PART_Host", typeof(Panel))]
public partial class ArcMarkdownView : TemplatedControl, IDisposable
{
    /// <summary><see cref="Markdown"/> 样式属性。</summary>
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<ArcMarkdownView, string>(nameof(Markdown), string.Empty);

    /// <summary><see cref="Options"/> 样式属性。</summary>
    public static readonly StyledProperty<ArcMarkdownOptions?> OptionsProperty =
        AvaloniaProperty.Register<ArcMarkdownView, ArcMarkdownOptions?>(nameof(Options));

    private readonly PuraMarkdownView _engine;
    private readonly Dictionary<string, ArcMarkdownInteraction> _interactionStates = new(StringComparer.Ordinal);

    private ScrollViewer? _scroll;
    private Panel? _host;
    private PuraDocument? _document;
    private ArcMarkdownStreamingSession? _activeSession;
    private string? _plainText;
    private bool _subscribedToTheme;
    private bool _disposed;
    private int _version;
    private bool _updatingOptions;

    static ArcMarkdownView()
    {
        MarkdownProperty.Changed.AddClassHandler<ArcMarkdownView>((view, _) => view.OnMarkdownChanged());
        OptionsProperty.Changed.AddClassHandler<ArcMarkdownView>((view, _) => view.OnOptionsChanged());
    }

    /// <summary>Creates the view.</summary>
    public ArcMarkdownView()
    {
        _engine = new PuraMarkdownView();
        _engine.Warning += OnEngineWarning;
        _engine.LinkClicked += OnEngineLinkClicked;
        _engine.InteractionOccurred += OnEngineInteraction;
        _engine.Announced += (_, text) => Announced?.Invoke(this, text);
        _engine.RenderCompleted += OnEngineRenderCompleted;
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

    /// <summary>Outline (headings and fold ranges) of the last rendered document.</summary>
    public PuraOutline? Outline => _document?.Outline ?? _engine.Document?.Outline;

    /// <summary>Diagnostics produced by the syntax sanitizer.</summary>
    public IReadOnlyList<PuraDiagnostic> Diagnostics
        => _document?.Diagnostics ?? _engine.Document?.Diagnostics ?? Array.Empty<PuraDiagnostic>();

    /// <summary>Currently active streaming session, if any.</summary>
    public ArcMarkdownStreamingSession? ActiveSession => _activeSession;

    /// <summary>Whether a streaming session is appending.</summary>
    public bool IsStreaming => _activeSession?.State == ArcMarkdownStreamingState.Streaming;

    /// <summary>Persisted agent-interaction state, keyed by component id.</summary>
    public IReadOnlyDictionary<string, ArcMarkdownInteraction> InteractionStates => _interactionStates;

    /// <summary>Current options (never null).</summary>
    public ArcMarkdownOptions CurrentOptions => Options ?? ArcMarkdownOptions.Default;

    /// <summary>Scroll viewer hosting the document.</summary>
    public ScrollViewer? ScrollViewer => _scroll;

    /// <summary>Internal snapshot used by the Pura bridge (renderers read persisted component state).</summary>
    internal IReadOnlyDictionary<string, ArcMarkdownInteraction> InteractionStatesSnapshot => _interactionStates;

    /// <summary>Re-renders the current <see cref="Markdown"/> (synchronous).</summary>
    public void Render()
    {
        EnsureEngineOptions();
        _engine.Render();
        AdoptEngineDocument();
    }

    /// <summary>Renders an already parsed document without re-parsing.</summary>
    /// <param name="document">Parsed PuraMarkdown syntax document.</param>
    public void RenderDocument(PuraDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureEngineOptions();
        _engine.RenderDocument(document);
        _document = document;
        _plainText = null;
        UpdateBlocksFromEngine();
    }

    /// <summary>Starts an append-only streaming session.</summary>
    /// <param name="options">Streaming options.</param>
    /// <returns>The Arc streaming session wrapper.</returns>
    public ArcMarkdownStreamingSession BeginStreaming(ArcMarkdownStreamingOptions? options = null)
    {
        if (_activeSession is { State: ArcMarkdownStreamingState.Streaming })
        {
            throw new InvalidOperationException("已有进行中的流式会话");
        }

        var streamingOptions = options ?? new ArcMarkdownStreamingOptions();
        EnsureEngineOptions();
        var inner = _engine.BeginStreaming(MapStreamingOptions(streamingOptions));
        _activeSession = new ArcMarkdownStreamingSession(this, inner, streamingOptions);
        return _activeSession;
    }

    /// <summary>Aborts the active streaming session, keeping the partial document.</summary>
    public void AbortStreaming()
    {
        if (_activeSession is { State: ArcMarkdownStreamingState.Streaming } session)
        {
            _ = session.AbortAsync();
        }
    }

    /// <summary>Refreshes Arc tokens and re-renders.</summary>
    public void RefreshTheme()
    {
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();
        _engine.RefreshTheme();
        RebuildEngineOptions();
        Render();
    }

    /// <summary>Warms the render path with representative Markdown.</summary>
    /// <param name="markdown">Markdown used for warming.</param>
    public void Prewarm(string markdown)
    {
        EnsureEngineOptions();
        _engine.Prewarm(markdown);
    }

    /// <summary>Returns the outline headings.</summary>
    /// <returns>Headings in document order.</returns>
    public IReadOnlyList<PuraHeading> GetOutline()
    {
        var document = _document ?? _engine.Document;
        if (document is not null)
        {
            return document.Outline.Headings;
        }

        var text = GetPlainText();
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<PuraHeading>();
        }

        try
        {
            return CurrentOptions.Pipeline.Syntax
                .ParseDocument(text, NextVersion(), SyntaxOptions(CurrentOptions))
                .Outline.Headings;
        }
        catch
        {
            return Array.Empty<PuraHeading>();
        }
    }

    /// <summary>Returns the plain text of the current content.</summary>
    /// <returns>Plain text without Markdown markers.</returns>
    public string GetPlainText()
    {
        if (_plainText is not null)
        {
            return _plainText;
        }

        var document = _document ?? _engine.Document;
        if (document is null)
        {
            if (_activeSession is not null)
            {
                return _activeSession.CurrentText;
            }

            return _engine.GetPlainText();
        }

        var builder = new StringBuilder();
        foreach (var block in document.Markdown)
        {
            AppendBlockText(block, builder);
        }

        _plainText = builder.ToString().TrimEnd();
        return _plainText;
    }

    /// <summary>Copies the plain text to the Arc clipboard (and the top-level clipboard when default).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>Scrolls the given heading anchor into view.</summary>
    /// <param name="anchor">Anchor text, with or without a leading <c>#</c>.</param>
    /// <returns>True when a target was found and scrolled.</returns>
    public bool ScrollToAnchor(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return false;
        }

        return _engine.ScrollToAnchor(anchor.TrimStart('#'));
    }

    /// <summary>Scrolls to the end of the document (outer Arc scroll host).</summary>
    public void ScrollToEnd()
    {
        _scroll?.ScrollToEnd();
    }

    /// <summary>Stores an interaction state for re-render/virtualization restore.</summary>
    /// <param name="interaction">Interaction record.</param>
    public void SetInteractionState(ArcMarkdownInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        _interactionStates[interaction.ComponentId] = interaction;
    }

    /// <summary>Applies stored interaction states and re-renders.</summary>
    /// <param name="states">Interaction records.</param>
    public void ApplyInteractionStates(IEnumerable<ArcMarkdownInteraction> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        foreach (var state in states)
        {
            _interactionStates[state.ComponentId] = state;
        }

        if (!string.IsNullOrWhiteSpace(Markdown))
        {
            Render();
        }
    }

    /// <summary>Clears stored interaction states and re-renders.</summary>
    public void ClearInteractionStates()
    {
        if (_interactionStates.Count == 0)
        {
            return;
        }

        _interactionStates.Clear();
        if (!string.IsNullOrWhiteSpace(Markdown))
        {
            Render();
        }
    }

    /// <summary>Raises an accessibility announcement.</summary>
    /// <param name="message">Announcement text.</param>
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

        if (_host is not null)
        {
            _host.Children.Clear();
            _host.Children.Add(_engine);
            _engine.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        RebuildEngineOptions();
        if (!string.IsNullOrEmpty(Markdown))
        {
            _engine.Markdown = Markdown;
        }
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
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y > 0)
        {
            _userPausedFollow = true;
        }
        else if (e.Delta.Y < 0)
        {
            ResumeFollowIfNearEnd();
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
        if (_activeSession is not null)
        {
            _ = _activeSession.DisposeAsync();
            _activeSession = null;
        }

        _engine.Dispose();
        DetachThemeHandler();
        GC.SuppressFinalize(this);
    }

    internal static PuraSyntaxOptions SyntaxOptions(ArcMarkdownOptions options)
        => options.Pipeline.Syntax.CreateOptions(options.Limits, options.UrlPolicy);

    internal int NextVersion() => ++_version;

    /// <summary>Called by the streaming wrapper when the stream completes/aborts.</summary>
    /// <param name="text">Final streamed text.</param>
    internal void AdoptStreamedText(string text)
    {
        try
        {
            _document = CurrentOptions.Pipeline.Syntax.ParseDocument(text, NextVersion(), SyntaxOptions(CurrentOptions));
        }
        catch
        {
            _document = null;
        }

        _plainText = null;
        UpdateBlocksFromEngine();
    }

    internal void RaiseArcInteraction(ArcMarkdownInteraction interaction)
    {
        if (!CurrentOptions.InteractionEnabled)
        {
            return;
        }

        if (!string.IsNullOrEmpty(interaction.ComponentId))
        {
            _interactionStates[interaction.ComponentId] = interaction;
        }

        RaiseEvent(new ArcMarkdownInteractionEventArgs(interaction)
        {
            RoutedEvent = InteractionOccurredEvent,
            Source = this,
        });
    }

    internal void RaiseArcWarning(ArcMarkdownWarning warning)
    {
        RaiseEvent(new ArcMarkdownWarningEventArgs(warning)
        {
            RoutedEvent = WarningEvent,
            Source = this,
        });
    }

    internal void HandleArcLinkClick(ArcMarkdownLinkClick click)
    {
        var args = new ArcMarkdownLinkClickedEventArgs(click.Url, click.Text)
        {
            RoutedEvent = LinkClickedEvent,
            Source = this,
        };
        RaiseEvent(args);
        click.Handled = args.HandledByHost;
        if (!click.Handled && TryNavigateFragment(click.Url))
        {
            click.Handled = true;
        }
    }

    private void OnMarkdownChanged()
    {
        _plainText = null;
        EnsureEngineOptions();
        _engine.Markdown = Markdown ?? string.Empty;
        AdoptEngineDocument();
    }

    private void OnOptionsChanged()
    {
        if (_updatingOptions)
        {
            return;
        }

        RebuildEngineOptions();
        if (!string.IsNullOrEmpty(Markdown))
        {
            _engine.Markdown = Markdown;
        }
    }

    private void EnsureEngineOptions()
    {
        if (_engine.Options is null)
        {
            RebuildEngineOptions();
        }
    }

    private void RebuildEngineOptions()
    {
        var arcOptions = CurrentOptions;
        var state = new ArcMarkdownHostState
        {
            Options = arcOptions,
            Pipeline = arcOptions.Pipeline,
            View = this,
        };
        _updatingOptions = true;
        try
        {
            _engine.Options = ArcMarkdownPuraOptions.Create(arcOptions, arcOptions.Pipeline, state);
        }
        finally
        {
            _updatingOptions = false;
        }
    }

    private void OnEngineRenderCompleted(object? sender, PuraRenderCompletedEventArgs e)
    {
        AdoptEngineDocument();
    }

    private void AdoptEngineDocument()
    {
        if (_engine.Document is not null)
        {
            _document = _engine.Document;
            _plainText = null;
            UpdateBlocksFromEngine();
        }
    }

    private void UpdateBlocksFromEngine()
    {
        var blocks = _engine.Blocks;
        if (blocks.Count == 0)
        {
            if (_document is not null)
            {
                var fromDocument = new List<ArcMarkdownBlockInfo>(_document.Markdown.Count);
                var index = 0;
                foreach (var node in _document.Markdown)
                {
                    fromDocument.Add(new ArcMarkdownBlockInfo(
                        index++,
                        node.GetType().Name,
                        node.Span.Start,
                        node.Span.End,
                        node.Line));
                }

                Blocks = fromDocument;
            }

            return;
        }

        var infos = new List<ArcMarkdownBlockInfo>(blocks.Count);
        foreach (var record in blocks)
        {
            infos.Add(new ArcMarkdownBlockInfo(
                record.Index,
                record.Node.GetType().Name,
                record.Start,
                record.End,
                record.Node.Line));
        }

        Blocks = infos;
    }

    private void OnEngineWarning(object? sender, PuraWarningEventArgs e)
        => RaiseArcWarning(new ArcMarkdownWarning(
            e.Warning.Code,
            e.Warning.Message,
            e.Warning.NodeType,
            e.Warning.Renderer));

    private void OnEngineLinkClicked(object? sender, PuraLinkClickedEventArgs e)
        => HandleArcLinkClick(new ArcMarkdownLinkClick(e.Url, e.Text ?? e.Url));

    private void OnEngineInteraction(object? sender, PuraInteractionEventArgs e)
    {
        var interaction = new ArcMarkdownInteraction(
            e.ComponentType,
            e.ComponentId ?? string.Empty,
            e.SelectedIds,
            e.Confirmed ?? false);
        RaiseArcInteraction(interaction);
    }

    private static PuraStreamingOptions MapStreamingOptions(ArcMarkdownStreamingOptions options)
        => new()
        {
            TailWindowChars = options.TailWindowChars,
            MaxTailChars = options.MaxTailChars,
            AutoScrollToEnd = options.AutoScrollToEnd,
            Synchronous = options.Synchronous,
        };

    private void OnThemeVariantChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshTheme);

    private void DetachThemeHandler()
    {
        if (_subscribedToTheme && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _subscribedToTheme = false;
        }
    }

    private bool TryNavigateFragment(string url)
    {
        if (url.Length < 2 || url[0] != '#')
        {
            return false;
        }

        var anchor = Uri.UnescapeDataString(url[1..]);
        return !string.IsNullOrEmpty(anchor) && ScrollToAnchor(anchor);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroll)
        {
            return;
        }

        var maximum = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var distance = maximum - Math.Max(0, scroll.Offset.Y);
        if (e.OffsetDelta.Y < -0.5)
        {
            _userPausedFollow = true;
        }
        else if (!_pointerPressed && _userPausedFollow && distance <= 56)
        {
            _userPausedFollow = false;
        }
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
        }
    }

    private bool _pointerPressed;
    private bool _userPausedFollow;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerPressed = true;
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
