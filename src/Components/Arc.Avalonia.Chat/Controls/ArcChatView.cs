using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PuraMarkdown.Agent.Chat;
using PuraMarkdown.UI;
using PuraMarkdown.UI.Platform;
using PuraMarkdown.UI.Rendering;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Arc's chat surface. The messages, their rendering (Markdown, code, tables, pura-agent components) and the
/// streaming lifecycle all come from <see cref="PuraChatView"/> (PuraMarkdown.Agent) on top of the
/// PuraMarkdown.UI render engine (ADR 0007/0008); Arc only supplies the design-system wrapper, the composer
/// and the token-derived theme mapping.
/// </summary>
public sealed class ArcChatView : ContentControl, IDisposable
{
    private readonly PuraChatView _host;
    private readonly ArcChatLinkHandler _linkHandler;
    private readonly ArcChatComposer _composer;
    private readonly Grid _root;
    private readonly Border _composerHost;
    private bool _showComposer;
    private bool _disposed;
    private ILinkHandler? _optionsLinkHandler;

    /// <summary>Identifies the <see cref="LinkClicked"/> routed event.</summary>
    public static readonly RoutedEvent<ArcChatLinkClickedEventArgs> LinkClickedEvent =
        RoutedEvent.Register<ArcChatView, ArcChatLinkClickedEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    /// <summary>Creates the surface with the Arc theme mapped onto the PuraMarkdown render engine.</summary>
    public ArcChatView()
    {
        // The upstream chat view defaults to NullLinkHandler and exposes no link event, so Arc installs its
        // own handler to keep message links clickable (see ArcChatLinkHandler).
        _linkHandler = new ArcChatLinkHandler(this);
        _host = new PuraChatView { Options = ArcChatThemeAdapter.CreateOptions(_linkHandler) };
        _host.MessageAdded += (_, message) => MessageAdded?.Invoke(this, message);
        _host.MessageUpdated += (_, message) => MessageUpdated?.Invoke(this, message);
        _host.MessageRemoved += (_, message) => MessageRemoved?.Invoke(this, message);
        _host.InteractionOccurred += (_, args) => InteractionOccurred?.Invoke(this, args);
        _host.Warning += (_, args) => Warning?.Invoke(this, args);

        _composer = new ArcChatComposer();
        _composer.Send += OnComposerSend;
        _composer.StopRequested += OnComposerStop;
        _composerHost = new Border
        {
            Margin = new Thickness(0, 8, 0, 0),
            Child = _composer,
            IsVisible = false,
        };
        _root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(_host, 0);
        _root.Children.Add(_host);
        Grid.SetRow(_composerHost, 1);
        _root.Children.Add(_composerHost);
        Content = _root;
    }

    /// <summary>The upstream view; use it for advanced scenarios (virtualization, snapshots).</summary>
    public PuraChatView Host => _host;

    /// <summary>The upstream message collection (one model for human chat, AI answers and agents).</summary>
    public ObservableCollection<PuraChatMessage> Messages => _host.Messages;

    /// <summary>
    /// Render options (pipeline + theme); defaults to the Arc token mapping. Setting custom options keeps the
    /// Arc link wiring: the options are copied with the Arc link handler installed, and a
    /// <see cref="PuraMarkdownOptions.LinkHandler"/> carried by them stays the fallback for message links.
    /// </summary>
    public PuraMarkdownOptions? Options
    {
        get => _host.Options;
        set
        {
            // PuraMarkdownOptions is immutable, so a host-supplied instance is copied with the Arc handler
            // injected; its own LinkHandler (if any) is kept as the fallback so both seams stay usable.
            _optionsLinkHandler = value is { LinkHandler: { } carried } &&
                                  !ReferenceEquals(carried, _linkHandler) &&
                                  carried is not NullLinkHandler
                ? carried
                : null;
            _host.Options = value is null ? null : ArcChatThemeAdapter.WithLinkHandler(value, _linkHandler);
        }
    }

    /// <summary>
    /// Host link handler. When set, a message link that <see cref="LinkClicked"/> did not handle is passed to
    /// it instead of the built-in launcher (http/https/mailto/tel through <c>TopLevel.Launcher</c>). It wins
    /// over a <see cref="PuraMarkdownOptions.LinkHandler"/> carried by <see cref="Options"/>.
    /// </summary>
    public ILinkHandler? LinkHandler { get; set; }

    /// <summary>The composer input surface; host it anywhere in the shell.</summary>
    public ArcChatComposer Composer => _composer;

    /// <summary>Shows the built-in composer under the messages.</summary>
    public bool ShowComposer
    {
        get => _showComposer;
        set
        {
            _showComposer = value;
            _composerHost.IsVisible = value;
        }
    }

    /// <summary>Composer placeholder text.</summary>
    public string Placeholder
    {
        get => _composer.Placeholder;
        set => _composer.Placeholder = value;
    }

    /// <summary>Whether the composer accepts attachments.</summary>
    public bool AllowAttachments
    {
        get => _composer.AllowAttachments;
        set => _composer.AllowAttachments = value;
    }

    /// <summary>Whether a response is currently streaming.</summary>
    public bool IsStreaming => _host.IsStreaming;

    /// <summary>The active streaming session, if any.</summary>
    public PuraStreamingSession? ActiveSession => _host.ActiveSession;

    /// <summary>Auto-scroll policy (mapped onto the upstream mode).</summary>
    public ArcChatAutoScrollMode AutoScrollMode
    {
        get => _host.AutoScrollMode switch
        {
            PuraChatAutoScrollMode.Always => ArcChatAutoScrollMode.Always,
            PuraChatAutoScrollMode.NearBottom => ArcChatAutoScrollMode.NearBottom,
            _ => ArcChatAutoScrollMode.Never,
        };
        set => _host.AutoScrollMode = value switch
        {
            ArcChatAutoScrollMode.Always => PuraChatAutoScrollMode.Always,
            ArcChatAutoScrollMode.NearBottom => PuraChatAutoScrollMode.NearBottom,
            _ => PuraChatAutoScrollMode.Never,
        };
    }

    /// <summary>Raised after a message was appended.</summary>
    public event EventHandler<PuraChatMessage>? MessageAdded;

    /// <summary>Raised after a message changed.</summary>
    public event EventHandler<PuraChatMessage>? MessageUpdated;

    /// <summary>Raised after a message was removed.</summary>
    public event EventHandler<PuraChatMessage>? MessageRemoved;

    /// <summary>Raised when a pura-agent component reports an interaction.</summary>
    public event EventHandler<PuraInteractionEventArgs>? InteractionOccurred;

    /// <summary>
    /// Raised when a link inside a rendered message is activated, before Arc opens it. Set
    /// <see cref="ArcChatLinkClickedEventArgs.HandledByHost"/> to take over the click.
    /// </summary>
    public event EventHandler<ArcChatLinkClickedEventArgs> LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    /// <summary>
    /// Recoverable render/link problems. Forwards the upstream warnings and adds Arc's
    /// <c>ARC-CHAT-LINK-*</c> codes when a link cannot be handed to a launcher or a host handler fails.
    /// </summary>
    public event EventHandler<PuraWarningEventArgs>? Warning;

    /// <summary>Composer send; the host starts the run (e.g. through its agent transport).</summary>
    public event EventHandler<ArcChatDraft>? Send;

    /// <summary>Composer stop.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Appends a message.</summary>
    public void AddMessage(PuraChatMessage message) => _host.AddMessage(message);

    /// <summary>Inserts a message at an index.</summary>
    public void InsertMessage(int index, PuraChatMessage message) => _host.InsertMessage(index, message);

    /// <summary>Notifies the view that a message changed.</summary>
    public void UpdateMessage(PuraChatMessage message) => _host.UpdateMessage(message);

    /// <summary>Removes a message by id.</summary>
    public bool RemoveMessage(string messageId) => _host.RemoveMessage(messageId);

    /// <summary>Removes every message.</summary>
    public void ClearMessages() => _host.ClearMessages();

    /// <summary>Replaces the collection.</summary>
    public void ReplaceMessages(IEnumerable<PuraChatMessage> messages) => _host.ReplaceMessages(messages);

    /// <summary>Finds a message by id.</summary>
    public PuraChatMessage? FindMessage(string messageId) => _host.FindMessage(messageId);

    /// <summary>Starts a streaming assistant message; append through the returned session.</summary>
    public PuraStreamingSession BeginStreamingMessage(PuraChatMessage? message = null) =>
        _host.BeginStreamingMessage(message);

    /// <summary>Aborts the active stream (partial content is kept).</summary>
    public void AbortStreaming() => _host.AbortStreaming();

    /// <summary>Scrolls to the newest message.</summary>
    public void ScrollToEnd() => _host.ScrollToEnd();

    /// <summary>Scrolls to a message by id.</summary>
    public void ScrollToMessage(string messageId) => _host.ScrollToMessage(messageId);

    /// <summary>Restores a serialized snapshot.</summary>
    public void RestoreSnapshot(PuraChatSnapshot snapshot) => _host.RestoreSnapshot(snapshot);

    /// <summary>Seeds the view from a message list.</summary>
    public IEnumerable<PuraChatMessage>? DefaultMessages
    {
        get => null;
        set
        {
            if (value is null)
            {
                return;
            }

            ReplaceMessages(value);
        }
    }

    /// <summary>Raises <see cref="LinkClicked"/> for <see cref="ArcChatLinkHandler"/> (UI thread, from the renderer).</summary>
    internal void RaiseLinkClicked(ArcChatLinkClickedEventArgs args)
    {
        args.RoutedEvent = LinkClickedEvent;
        args.Source = this;
        RaiseEvent(args);
    }

    /// <summary>Link handler carried by host-supplied <see cref="Options"/> (fallback after <see cref="LinkHandler"/>).</summary>
    internal ILinkHandler? OptionsLinkHandler => _optionsLinkHandler;

    /// <summary>Reports a recoverable link problem through <see cref="Warning"/>.</summary>
    internal void RaiseWarning(string code, string message) =>
        Warning?.Invoke(this, new PuraWarningEventArgs(new PuraWarning(code, message, "LinkInline", nameof(ArcChatLinkHandler))));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _composer.Send -= OnComposerSend;
        _composer.StopRequested -= OnComposerStop;
    }

    private void OnComposerSend(object? sender, ArcChatDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Text) && draft.Attachments.Count == 0)
        {
            return;
        }

        AddMessage(new PuraChatMessage
        {
            Role = PuraChatRole.User,
            Markdown = draft.Text,
            Status = PuraChatMessageStatus.Complete,
        });
        Composer.Clear();
        Send?.Invoke(this, draft);
    }

    private void OnComposerStop(object? sender, EventArgs args)
    {
        AbortStreaming();
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
