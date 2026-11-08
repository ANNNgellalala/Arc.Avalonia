using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Chat;

/// <summary>
/// One chat bubble: avatar column, name/time header, the Markdown body rendered by
/// <see cref="ArcMarkdownView"/>, attachments and a delivery/status line. Own messages align right on the
/// accent-tinted surface; other messages align left on the muted surface; system messages render as a quiet
/// centred pill.
/// <para>
/// The bubble is the visual building block shared by <see cref="ArcChatView"/> and the agent conversation
/// layer, so a message looks the same in a human chat, an AI answer and an agent run. The body
/// <see cref="ArcMarkdownView"/> is created eagerly, which means <see cref="BeginStreaming"/> works even
/// before the control template has been applied (the window host may attach the bubble later).
/// </para>
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcChatBubble : TemplatedControl
{
    public static readonly StyledProperty<ArcChatMessage?> MessageProperty =
        AvaloniaProperty.Register<ArcChatBubble, ArcChatMessage?>(nameof(Message));

    public static readonly StyledProperty<bool> IsOwnProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(IsOwn));

    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(ShowHeader), true);

    public static readonly StyledProperty<bool> ShowStatusProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(ShowStatus), true);

    public static readonly StyledProperty<bool> ShowAvatarProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(ShowAvatar), true);

    public static readonly StyledProperty<bool> FirstInGroupProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(FirstInGroup), true);

    public static readonly StyledProperty<bool> LastInGroupProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(LastInGroup), true);

    public static readonly StyledProperty<ArcMarkdownOptions?> MarkdownOptionsProperty =
        AvaloniaProperty.Register<ArcChatBubble, ArcMarkdownOptions?>(nameof(MarkdownOptions));

    public static readonly StyledProperty<bool> AnimateEntryProperty =
        AvaloniaProperty.Register<ArcChatBubble, bool>(nameof(AnimateEntry));

    private Panel? _host;
    private Border? _bubble;
    private ArcMarkdownView? _body;
    private TextBlock? _status;
    private ArcChatMessage? _subscribed;
    private bool _suspendRebuild;
    private bool _entryAnimated;

    static ArcChatBubble()
    {
        MessageProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        IsOwnProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        ShowHeaderProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        ShowStatusProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        ShowAvatarProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        FirstInGroupProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        LastInGroupProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        MarkdownOptionsProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.Rebuild());
        AnimateEntryProperty.Changed.AddClassHandler<ArcChatBubble>((bubble, _) => bubble.AnimateIfNeeded());
    }

    /// <summary>The message shown by this bubble.</summary>
    public ArcChatMessage? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>True for the local user's message (right aligned, accent bubble).</summary>
    public bool IsOwn
    {
        get => GetValue(IsOwnProperty);
        set => SetValue(IsOwnProperty, value);
    }

    /// <summary>Shows the name/time header (group start).</summary>
    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    /// <summary>Shows the delivery/status line.</summary>
    public bool ShowStatus
    {
        get => GetValue(ShowStatusProperty);
        set => SetValue(ShowStatusProperty, value);
    }

    /// <summary>Shows the avatar column.</summary>
    public bool ShowAvatar
    {
        get => GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    /// <summary>First bubble of a grouped run (top corners are round).</summary>
    public bool FirstInGroup
    {
        get => GetValue(FirstInGroupProperty);
        set => SetValue(FirstInGroupProperty, value);
    }

    /// <summary>Last bubble of a grouped run (bottom corners are round).</summary>
    public bool LastInGroup
    {
        get => GetValue(LastInGroupProperty);
        set => SetValue(LastInGroupProperty, value);
    }

    /// <summary>Markdown options for the body.</summary>
    public ArcMarkdownOptions? MarkdownOptions
    {
        get => GetValue(MarkdownOptionsProperty);
        set => SetValue(MarkdownOptionsProperty, value);
    }

    /// <summary>Plays the entrance transition once when the bubble is first realized.</summary>
    public bool AnimateEntry
    {
        get => GetValue(AnimateEntryProperty);
        set => SetValue(AnimateEntryProperty, value);
    }

    /// <summary>The body renderer (created on first use, before the template when needed).</summary>
    public ArcMarkdownView? Body => _body;

    /// <summary>Raised for agent-component interactions inside the body.</summary>
    public event EventHandler<ArcMarkdownInteractionEventArgs>? InteractionOccurred;

    /// <summary>Raised for renderer warnings inside the body.</summary>
    public event EventHandler<ArcMarkdownWarningEventArgs>? Warning;

    /// <summary>Raised for body accessibility announcements.</summary>
    public event EventHandler<string>? Announced;

    /// <summary>Starts a streaming session into this bubble's body (works before template application).</summary>
    public ArcMarkdownStreamingSession? BeginStreaming() => EnsureBody().BeginStreaming();

    /// <summary>
    /// Applies the grouping state in one shot: <paramref name="isOwn"/> right/left, first/last run corners,
    /// header/status/avatar visibility. Used by <see cref="ArcChatView"/> and the agent conversation layer.
    /// </summary>
    public void ApplyGrouping(bool isOwn, bool firstInGroup, bool lastInGroup, bool showHeader, bool showStatus, bool showAvatar)
    {
        _suspendRebuild = true;
        try
        {
            IsOwn = isOwn;
            FirstInGroup = firstInGroup;
            LastInGroup = lastInGroup;
            ShowHeader = showHeader;
            ShowStatus = showStatus;
            ShowAvatar = showAvatar;
        }
        finally
        {
            _suspendRebuild = false;
        }

        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private ArcMarkdownView EnsureBody()
    {
        if (_body is not null)
        {
            return _body;
        }

        var body = new ArcMarkdownView
        {
            Options = MarkdownOptions,
            Markdown = Message?.Markdown ?? string.Empty,
        };
        body.InteractionOccurred += (_, args) => InteractionOccurred?.Invoke(this, args);
        body.Warning += (_, args) => Warning?.Invoke(this, args);
        body.Announced += (_, text) => Announced?.Invoke(this, text);
        _body = body;
        return body;
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnMessageChanged;
            _subscribed = null;
        }
    }

    private void OnMessageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateFromMessage();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateFromMessage, DispatcherPriority.Background);
        }
    }

    private void UpdateFromMessage()
    {
        var message = Message;
        var body = EnsureBody();
        if (message is not null && body.ActiveSession is null
            && !string.Equals(body.Markdown, message.Markdown, StringComparison.Ordinal))
        {
            body.Markdown = message.Markdown;
        }

        UpdateStatus(message);
    }

    private void Rebuild()
    {
        if (_suspendRebuild)
        {
            return;
        }

        var body = EnsureBody();
        body.Options = MarkdownOptions;
        var message = Message;
        if (message is not null && body.ActiveSession is null
            && !string.Equals(body.Markdown, message.Markdown, StringComparison.Ordinal))
        {
            body.Markdown = message.Markdown;
        }

        if (!ReferenceEquals(_subscribed, message))
        {
            Unsubscribe();
            _subscribed = message;
            if (message is not null)
            {
                message.Changed += OnMessageChanged;
            }
        }

        if (_host is null)
        {
            return;
        }

        DetachBody(body);
        _host.Children.Clear();
        _bubble = null;
        _status = null;

        if (message is null)
        {
            return;
        }

        if (message.Role == ArcChatRole.System)
        {
            _host.Children.Add(BuildSystem(body, message));
            return;
        }

        var own = IsOwn;
        var outer = new Grid
        {
            ColumnDefinitions = own ? new ColumnDefinitions("*,36") : new ColumnDefinitions("36,*"),
            Margin = new Thickness(0, 2, 0, 2),
        };

        var column = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        if (ShowHeader)
        {
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(own ? 0 : 2, 0, own ? 2 : 0, 2),
            };
            var name = ArcChatUi.Text(
                string.IsNullOrWhiteSpace(AuthorName(message)) ? (own ? "你" : "助手") : AuthorName(message),
                ArcTokens.TextXs,
                ArcTokens.TextSecondary);
            header.Children.Add(name);
            header.Children.Add(ArcChatUi.Text(ArcChatUi.FormatTime(message.CreatedAt), ArcTokens.TextXs, ArcTokens.TextMuted));
            column.Children.Add(header);
        }

        var bubble = new Border
        {
            MaxWidth = 560,
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            CornerRadius = GroupCornerRadius(own),
        };
        bubble.Paint(Border.BackgroundProperty, own ? ArcTokens.AccentSubtle : ArcTokens.SurfaceMuted);
        bubble.Paint(Border.BorderBrushProperty, own ? ArcTokens.Accent : ArcTokens.BorderSubtle);

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(body);
        if (message.Attachments.Count > 0)
        {
            foreach (var attachment in message.Attachments)
            {
                content.Children.Add(BuildAttachment(attachment, own));
            }
        }

        bubble.Child = content;
        column.Children.Add(bubble);
        _bubble = bubble;

        _status = new TextBlock
        {
            FontSize = ArcChatUi.TextSize(ArcTokens.TextXs, 12),
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(2, 2, 2, 0),
        };
        _status.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        column.Children.Add(_status);
        UpdateStatus(message);

        Grid.SetColumn(column, own ? 0 : 1);
        outer.Children.Add(column);

        if (ShowAvatar)
        {
            var avatar = ArcChatUi.Avatar(ResolveParticipant(message), own);
            avatar.HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            avatar.Margin = own ? new Thickness(6, 2, 0, 0) : new Thickness(0, 2, 6, 0);
            Grid.SetColumn(avatar, own ? 1 : 0);
            outer.Children.Add(avatar);
        }

        AutomationProperties.SetName(outer, $"{AuthorName(message)}，{ArcChatUi.FormatTime(message.CreatedAt)}");
        AutomationProperties.SetHelpText(outer, message.Markdown);
        _host.Children.Add(outer);
        AnimateIfNeeded();
    }

    private void DetachBody(ArcMarkdownView body)
    {
        switch (body.Parent)
        {
            case Panel panel:
                panel.Children.Remove(body);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl contentControl:
                contentControl.Content = null;
                break;
        }
    }

    private CornerRadius GroupCornerRadius(bool own)
    {
        var radius = ArcChatUi.Radius(ArcTokens.RadiusControl);
        var big = radius.TopLeft > 0 ? radius.TopLeft : 18;
        const double small = 6;
        return own
            ? new CornerRadius(big, FirstInGroup ? big : small, LastInGroup ? big : small, small)
            : new CornerRadius(FirstInGroup ? big : small, big, small, LastInGroup ? big : small);
    }

    private Control BuildSystem(ArcMarkdownView body, ArcChatMessage message)
    {
        var pill = ArcChatUi.Pill(body, ArcTokens.SurfaceMuted, ArcTokens.BorderSubtle);
        pill.HorizontalAlignment = HorizontalAlignment.Center;
        pill.MaxWidth = 560;
        pill.Margin = new Thickness(8, 4, 8, 4);
        pill.Padding = new Thickness(12, 6, 12, 6);
        AutomationProperties.SetName(pill, $"系统消息，{ArcChatUi.FormatTime(message.CreatedAt)}");
        return pill;
    }

    private Control BuildAttachment(ArcChatAttachment attachment, bool own)
    {
        var name = ArcChatUi.Text(attachment.Name, ArcTokens.TextXs, own ? ArcTokens.AccentStrong : ArcTokens.Foreground);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var size = ArcChatUi.Text(
            attachment.Size > 0 ? ArcChatUi.FormatBytes(attachment.Size) : string.Empty,
            ArcTokens.TextXs,
            own ? ArcTokens.AccentStrong : ArcTokens.TextMuted);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), Width = 240 };
        row.Children.Add(ArcChatUi.Icon(
            attachment.IsImage ? "image" : "file",
            15,
            own ? ArcTokens.AccentStrong : ArcTokens.TextSecondary));
        var info = new StackPanel { Spacing = 0 };
        info.Children.Add(name);
        if (attachment.Size > 0)
        {
            info.Children.Add(size);
        }

        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        var frame = new Border
        {
            CornerRadius = ArcChatUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(1),
            Child = row,
        };
        frame.Paint(Border.BackgroundProperty, own ? ArcTokens.AccentSubtle : ArcTokens.Surface);
        frame.Paint(Border.BorderBrushProperty, own ? ArcTokens.AccentStrong : ArcTokens.BorderSubtle);
        return frame;
    }

    private void UpdateStatus(ArcChatMessage? message)
    {
        if (_status is null || message is null)
        {
            return;
        }

        var (text, danger) = message.Status switch
        {
            ArcChatMessageStatus.Streaming => ("生成中…", false),
            ArcChatMessageStatus.Failed => (string.IsNullOrWhiteSpace(message.Error) ? "生成失败" : message.Error!, true),
            ArcChatMessageStatus.Aborted => ("已中断", false),
            ArcChatMessageStatus.Draft => (string.Empty, false),
            _ => (DeliveryText(message), false),
        };

        _status.Text = text;
        _status.Paint(TextBlock.ForegroundProperty, danger ? ArcTokens.Danger : ArcTokens.TextMuted);
        _status.IsVisible = ShowStatus && !string.IsNullOrEmpty(text);
    }

    private static string DeliveryText(ArcChatMessage message)
    {
        if (message.Role != ArcChatRole.User && message.Delivery == ArcChatDeliveryState.None)
        {
            return string.Empty;
        }

        return message.Delivery switch
        {
            ArcChatDeliveryState.Sent => "已发送",
            ArcChatDeliveryState.Delivered => "已送达",
            ArcChatDeliveryState.Read => "已读",
            _ => string.Empty,
        };
    }

    private void AnimateIfNeeded()
    {
        if (_entryAnimated || _bubble is null || !AnimateEntry || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        _entryAnimated = true;
        var bubble = _bubble;
        var own = IsOwn;
        bubble.Opacity = 0;
        bubble.RenderTransform = new TranslateTransform(0, own ? 8 : -8);
        bubble.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = ArcMotion.Standard, Easing = ArcMotion.EaseEnter },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = ArcMotion.SpringSmoothDuration,
                Easing = ArcMotion.SpringSmooth,
            },
        };
        Dispatcher.UIThread.Post(() =>
        {
            bubble.Opacity = 1;
            bubble.RenderTransform = new TranslateTransform(0, 0);
        }, DispatcherPriority.Loaded);
    }

    private static string AuthorName(ArcChatMessage message)
        => message.Author?.Name ?? (message.Role switch
        {
            ArcChatRole.User => "你",
            ArcChatRole.System => "系统",
            _ => "助手",
        });

    private static ArcChatParticipant? ResolveParticipant(ArcChatMessage message)
        => message.Author ?? new ArcChatParticipant
        {
            Id = message.Role.ToString().ToLowerInvariant(),
            Name = AuthorName(message),
        };
}
