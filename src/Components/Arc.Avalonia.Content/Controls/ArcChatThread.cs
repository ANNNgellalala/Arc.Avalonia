using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

public enum ArcChatMessageStatus
{
    Sending,
    Sent,
    Delivered,
    Read,
    Failed
}

/// <summary>A person taking part in an <see cref="ArcChatThread"/>.</summary>
public sealed class ArcChatParticipant
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional photo path or avares URI. Initials are the fallback.</summary>
    public string? Avatar { get; set; }
}

/// <summary>A file shown in a message or staged in the composer.</summary>
public sealed class ArcChatAttachment
{
    public string Name { get; set; } = string.Empty;

    public long Size { get; set; }

    public string? Url { get; set; }

    public bool IsImage { get; set; }
}

/// <summary>One message in the thread, oldest first.</summary>
public sealed class ArcChatMessage
{
    public string Id { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public List<ArcChatAttachment> Attachments { get; set; } = new();

    /// <summary>Delivery state of the viewer's own message.</summary>
    public ArcChatMessageStatus? Status { get; set; }
}

/// <summary>The text and files handed to the Send event.</summary>
public sealed record ArcChatDraft(string Text, IReadOnlyList<ArcChatAttachment> Attachments);

/// <summary>
/// A two-way conversation with grouped bubbles, day separators, initials avatars, a typing row,
/// a composer with an attachment tray, and a message log that stays pinned to the newest message.
/// </summary>
public class ArcChatThread : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcChatParticipant>?> ParticipantsProperty =
        AvaloniaProperty.Register<ArcChatThread, IEnumerable<ArcChatParticipant>?>(nameof(Participants));

    public static readonly StyledProperty<string> CurrentUserIdProperty =
        AvaloniaProperty.Register<ArcChatThread, string>(nameof(CurrentUserId), "me");

    public static readonly StyledProperty<IList<ArcChatMessage>?> MessagesProperty =
        AvaloniaProperty.Register<ArcChatThread, IList<ArcChatMessage>?>(nameof(Messages));

    public static readonly StyledProperty<IEnumerable<ArcChatMessage>?> DefaultMessagesProperty =
        AvaloniaProperty.Register<ArcChatThread, IEnumerable<ArcChatMessage>?>(nameof(DefaultMessages));

    public static readonly StyledProperty<IEnumerable<string>?> TypingProperty =
        AvaloniaProperty.Register<ArcChatThread, IEnumerable<string>?>(nameof(Typing));

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcChatThread, string>(nameof(Placeholder), "Message");

    public static readonly StyledProperty<bool> ShowComposerProperty =
        AvaloniaProperty.Register<ArcChatThread, bool>(nameof(ShowComposer), true);

    public static readonly StyledProperty<bool> AllowAttachmentsProperty =
        AvaloniaProperty.Register<ArcChatThread, bool>(nameof(AllowAttachments), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcChatThread, string>(nameof(Label), "Conversation");

    public static readonly StyledProperty<TimeSpan> GroupWindowProperty =
        AvaloniaProperty.Register<ArcChatThread, TimeSpan>(nameof(GroupWindow), TimeSpan.FromMinutes(5));

    private readonly List<ArcChatMessage> _messages = new();
    private readonly List<ArcChatAttachment> _pending = new();
    private readonly List<Ellipse> _typingDots = new();
    private readonly Dictionary<string, ArcChatParticipant> _people = new(StringComparer.Ordinal);

    private Panel? _host;
    private StackPanel? _messageHost;
    private ScrollViewer? _scroller;
    private Panel? _composerHost;
    private StackPanel? _attachmentTray;
    private TextBox? _composer;
    private ArcButton? _send;
    private ArcButton? _attach;
    private TextBlock? _typingStatus;
    private DispatcherTimer? _typingTimer;
    private bool _loadedDefaults;
    private int _typingPhase;
    private string? _animateId;

    static ArcChatThread()
    {
        ParticipantsProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.RenderMessages(false));
        CurrentUserIdProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.RenderMessages(false));
        MessagesProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.ReloadMessages());
        DefaultMessagesProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.ReloadMessages());
        TypingProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.RenderMessages(false));
        PlaceholderProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) =>
        {
            if (thread._composer is not null)
            {
                thread._composer.PlaceholderText = thread.Placeholder;
                AutomationProperties.SetName(thread._composer, thread.Placeholder);
            }
        });
        ShowComposerProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) =>
        {
            if (thread._composerHost is not null)
            {
                thread._composerHost.IsVisible = thread.ShowComposer;
            }
        });
        AllowAttachmentsProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) =>
        {
            if (thread._attach is not null)
            {
                thread._attach.IsVisible = thread.AllowAttachments;
            }
        });
        GroupWindowProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) => thread.RenderMessages(false));
        LabelProperty.Changed.AddClassHandler<ArcChatThread>((thread, _) =>
            AutomationProperties.SetName(thread, thread.Label));
    }

    /// <summary>Everyone in the thread.</summary>
    public IEnumerable<ArcChatParticipant>? Participants
    {
        get => GetValue(ParticipantsProperty);
        set => SetValue(ParticipantsProperty, value);
    }

    /// <summary>Id of the viewer; their messages sit on the right in the accent colour.</summary>
    public string CurrentUserId
    {
        get => GetValue(CurrentUserIdProperty);
        set => SetValue(CurrentUserIdProperty, value);
    }

    /// <summary>Controlled or live messages, oldest first. Sends append here.</summary>
    public IList<ArcChatMessage>? Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    /// <summary>Initial messages used when <see cref="Messages"/> is not supplied.</summary>
    public IEnumerable<ArcChatMessage>? DefaultMessages
    {
        get => GetValue(DefaultMessagesProperty);
        set => SetValue(DefaultMessagesProperty, value);
    }

    /// <summary>Participant ids currently typing; the viewer is ignored.</summary>
    public IEnumerable<string>? Typing
    {
        get => GetValue(TypingProperty);
        set => SetValue(TypingProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool ShowComposer
    {
        get => GetValue(ShowComposerProperty);
        set => SetValue(ShowComposerProperty, value);
    }

    public bool AllowAttachments
    {
        get => GetValue(AllowAttachmentsProperty);
        set => SetValue(AllowAttachmentsProperty, value);
    }

    /// <summary>Accessible name of the message log.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Consecutive messages from one person within this window share a group.</summary>
    public TimeSpan GroupWindow
    {
        get => GetValue(GroupWindowProperty);
        set => SetValue(GroupWindowProperty, value);
    }

    /// <summary>Raised with the trimmed composer text and files after a send.</summary>
    public event EventHandler<ArcChatDraft>? Send;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        AutomationProperties.SetName(this, Label);
        BuildShell();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _typingTimer?.Stop();
        _typingTimer = null;
    }

    private void BuildShell()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();

        var shell = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.Surface),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPanel),
            ClipToBounds = true
        };
        shell.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };

        _messageHost = new StackPanel { Spacing = 14, Margin = new Thickness(16, 14, 16, 14) };
        _scroller = new ScrollViewer
        {
            Content = _messageHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AutomationProperties.SetName(_scroller, Label);
        AutomationProperties.SetHelpText(_scroller, "Message log, oldest first");
        root.Children.Add(_scroller);

        _typingStatus = new TextBlock
        {
            IsHitTestVisible = false,
            Opacity = 0,
            Height = 1,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetLiveSetting(_typingStatus, AutomationLiveSetting.Polite);
        root.Children.Add(_typingStatus);

        _composerHost = BuildComposer();
        _composerHost.IsVisible = ShowComposer;
        Grid.SetRow(_composerHost, 1);
        root.Children.Add(_composerHost);

        shell.Child = root;
        _host.Children.Add(shell);
        LoadMessages();
    }

    private Panel BuildComposer()
    {
        var host = new StackPanel { Spacing = 0 };
        var frame = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 12, 14, 12)
        };
        frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

        var stack = new StackPanel { Spacing = 8 };
        _attachmentTray = new StackPanel { Spacing = 6 };
        stack.Children.Add(_attachmentTray);

        var inputFrame = new Border
        {
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4, 6, 4),
            Background = Brushes.Transparent
        };
        inputFrame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        inputFrame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        _attach = new ArcButton
        {
            Content = ArcContentUi.Icon("plus", 16, ArcTokens.TextSecondary),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 36,
            Height = 36,
            MinHeight = 36,
            Padding = new Thickness(0),
            IsVisible = AllowAttachments,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        AutomationProperties.SetName(_attach, "Attach files");
        ToolTip.SetTip(_attach, "Attach files");
        _attach.Click += async (_, _) => await AttachFilesAsync();
        grid.Children.Add(_attach);

        _composer = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 7),
            MinHeight = 38,
            MaxHeight = 120,
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            PlaceholderText = Placeholder,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ArcContentUi.BareTextBox(_composer);
        AutomationProperties.SetName(_composer, Placeholder);
        AutomationProperties.SetHelpText(_composer, "Type a message. Enter sends, Shift+Enter adds a line.");
        _composer.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        _composer.Paint(TextBox.PlaceholderForegroundProperty, ArcTokens.TextMuted);
        _composer.TextChanged += (_, _) => UpdateSendEnabled();
        _composer.KeyDown += OnComposerKeyDown;
        _composer.GotFocus += (_, _) => inputFrame.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
        _composer.LostFocus += (_, _) => inputFrame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        Grid.SetColumn(_composer, 1);
        grid.Children.Add(_composer);

        _send = new ArcButton
        {
            Content = ArcContentUi.Icon("arrow-up", 16, ArcTokens.AccentForeground),
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 38,
            Height = 38,
            MinHeight = 38,
            Padding = new Thickness(0),
            IsEnabled = false,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        AutomationProperties.SetName(_send, "Send message");
        ToolTip.SetTip(_send, "Send (Enter)");
        _send.Click += (_, _) => Submit();
        Grid.SetColumn(_send, 2);
        grid.Children.Add(_send);

        inputFrame.Child = grid;
        stack.Children.Add(inputFrame);
        stack.Children.Add(ArcContentUi.Caption("Enter to send · Shift+Enter for a line"));
        frame.Child = stack;
        host.Children.Add(frame);
        return host;
    }

    private void LoadMessages()
    {
        _messages.Clear();
        if (Messages is not null)
        {
            _messages.AddRange(Messages);
            _loadedDefaults = true;
        }
        else if (!_loadedDefaults)
        {
            _loadedDefaults = true;
            if (DefaultMessages is not null)
            {
                _messages.AddRange(DefaultMessages);
            }
        }

        BuildPeople();
        RenderMessages(true);
    }

    private void ReloadMessages()
    {
        if (_messageHost is null)
        {
            return;
        }

        LoadMessages();
    }

    private void BuildPeople()
    {
        _people.Clear();
        foreach (var participant in Participants ?? Enumerable.Empty<ArcChatParticipant>())
        {
            _people[participant.Id] = participant;
        }

        if (!_people.ContainsKey(CurrentUserId))
        {
            _people[CurrentUserId] = new ArcChatParticipant { Id = CurrentUserId, Name = "You" };
        }
    }

    private ArcChatParticipant Person(string id)
    {
        if (_people.TryGetValue(id, out var participant))
        {
            return participant;
        }

        return new ArcChatParticipant { Id = id, Name = "Unknown participant" };
    }

    private void RenderMessages(bool scrollToEnd)
    {
        if (_messageHost is null)
        {
            return;
        }

        var offset = _scroller?.Offset ?? default;
        var pinned = _scroller is null ||
                     _scroller.Offset.Y + _scroller.Viewport.Height >= _scroller.Extent.Height - 24;

        _typingTimer?.Stop();
        _typingTimer = null;
        _typingDots.Clear();
        _messageHost.Children.Clear();

        if (_messages.Count == 0)
        {
            var empty = new Border
            {
                Background = ArcContentUi.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
                Padding = new Thickness(14, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = ArcContentUi.Text("No messages yet. Start the conversation.", "arc-body-sm")
            };
            if (empty.Child is TextBlock text)
            {
                text.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            }

            _messageHost.Children.Add(empty);
        }
        else
        {
            var groups = BuildGroups();
            DateTime? day = null;
            foreach (var group in groups)
            {
                var date = _messages[group.Start].CreatedAt.ToLocalTime().Date;
                if (day != date)
                {
                    _messageHost.Children.Add(BuildDaySeparator(_messages[group.Start].CreatedAt));
                    day = date;
                }

                _messageHost.Children.Add(BuildGroup(group));
            }
        }

        var typing = (Typing ?? Enumerable.Empty<string>())
            .Where(id => !string.Equals(id, CurrentUserId, StringComparison.Ordinal))
            .Select(Person)
            .ToList();
        if (typing.Count > 0)
        {
            _messageHost.Children.Add(BuildTyping(typing));
            if (_typingStatus is not null)
            {
                _typingStatus.Text = typing.Count == 1
                    ? $"{typing[0].Name} is typing"
                    : $"{string.Join(", ", typing.Select(person => person.Name))} are typing";
            }
        }
        else if (_typingStatus is not null)
        {
            _typingStatus.Text = string.Empty;
        }

        if (scrollToEnd || pinned)
        {
            Dispatcher.UIThread.Post(() => _scroller?.ScrollToEnd(), DispatcherPriority.Background);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_scroller is not null)
                {
                    _scroller.Offset = offset;
                }
            }, DispatcherPriority.Background);
        }
    }

    private sealed record MessageGroup(int Start, int End, bool Own);

    private List<MessageGroup> BuildGroups()
    {
        var groups = new List<MessageGroup>();
        if (_messages.Count == 0)
        {
            return groups;
        }

        var start = 0;
        for (var i = 1; i <= _messages.Count; i++)
        {
            var endGroup = i == _messages.Count ||
                           _messages[i].AuthorId != _messages[i - 1].AuthorId ||
                           _messages[i].CreatedAt - _messages[i - 1].CreatedAt > GroupWindow ||
                           _messages[i].CreatedAt.ToLocalTime().Date != _messages[i - 1].CreatedAt.ToLocalTime().Date;
            if (endGroup)
            {
                groups.Add(new MessageGroup(start, i - 1, _messages[start].AuthorId == CurrentUserId));
                start = i;
            }
        }

        return groups;
    }

    private Control BuildDaySeparator(DateTimeOffset time)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        grid.Children.Add(ArcContentUi.Divider(ArcTokens.BorderSubtle));
        var pill = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(10, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = ArcContentUi.Caption(ArcContentUi.FormatDay(time))
        };
        grid.Children.Add(pill);
        return grid;
    }

    private Control BuildGroup(MessageGroup group)
    {
        var author = Person(_messages[group.Start].AuthorId);
        var own = group.Own;
        var last = _messages[group.End];

        var outer = new Grid
        {
            ColumnDefinitions = own ? new ColumnDefinitions("*,36") : new ColumnDefinitions("36,*"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        var content = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(own ? 0 : 2, 0, own ? 2 : 0, 2)
        };
        var name = ArcContentUi.Text(own ? "You" : author.Name, "arc-label");
        name.FontSize = 12;
        name.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        header.Children.Add(name);
        header.Children.Add(ArcContentUi.Caption(ArcContentUi.FormatTime(_messages[group.Start].CreatedAt)));
        content.Children.Add(header);

        for (var i = group.Start; i <= group.End; i++)
        {
            var message = _messages[i];
            content.Children.Add(BuildBubble(message, own, i == group.Start, i == group.End,
                group.Start, group.End, author));
        }

        if (own)
        {
            var receipt = ArcContentUi.Caption(StatusText(last));
            receipt.HorizontalAlignment = HorizontalAlignment.Right;
            receipt.Margin = new Thickness(0, 3, 3, 0);
            content.Children.Add(receipt);
        }

        Grid.SetColumn(content, own ? 0 : 1);
        outer.Children.Add(content);

        var avatar = BuildAvatar(author, own);
        avatar.HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        avatar.VerticalAlignment = VerticalAlignment.Top;
        avatar.Margin = new Thickness(own ? 8 : 0, 2, own ? 0 : 8, 0);
        Grid.SetColumn(avatar, own ? 1 : 0);
        outer.Children.Add(avatar);

        AutomationProperties.SetName(outer,
            $"{author.Name}, {ArcContentUi.FormatTime(_messages[group.Start].CreatedAt)}, {_messages[group.Start].Text}");
        return outer;
    }

    private Control BuildBubble(ArcChatMessage message, bool own, bool first, bool last, int groupStart, int groupEnd,
        ArcChatParticipant author)
    {
        var stack = new StackPanel { Spacing = 5 };
        var text = new TextBlock
        {
            Text = message.Text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            LineHeight = 20
        };
        text.Paint(TextBlock.ForegroundProperty, own ? ArcTokens.AccentForeground : ArcTokens.Foreground);
        stack.Children.Add(text);

        foreach (var attachment in message.Attachments)
        {
            stack.Children.Add(BuildAttachment(attachment, own));
        }

        // Use the control radius and tighten the inner corners so a group reads as one stack.
        var radius = own
            ? new CornerRadius(18, first ? 18 : 6, first ? 6 : 6, last ? 6 : 6)
            : new CornerRadius(first ? 18 : 6, first ? 18 : 6, last ? 6 : 6, last ? 6 : 6);

        var bubble = new Border
        {
            Background = own
                ? ArcContentUi.Brush(ArcTokens.Accent)
                : ArcContentUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = radius,
            Padding = new Thickness(12, 8, 12, 8),
            MaxWidth = 420,
            HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Child = stack
        };
        if (!own)
        {
            bubble.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        }

        if (_animateId == message.Id && !ArcThemeManager.ReducedMotion)
        {
            bubble.Opacity = 0;
            bubble.RenderTransform = new TranslateTransform(0, own ? 8 : -8);
            bubble.Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = ArcMotion.Standard, Easing = ArcMotion.EaseEnter },
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                }
            };
            Dispatcher.UIThread.Post(() =>
            {
                bubble.Opacity = 1;
                bubble.RenderTransform = new TranslateTransform(0, 0);
            }, DispatcherPriority.Loaded);
        }

        return bubble;
    }

    private Control BuildAttachment(ArcChatAttachment attachment, bool own)
    {
        var toneBrush = own ? ArcTokens.AccentForeground : ArcTokens.Foreground;
        var icon = ArcContentUi.Icon(attachment.IsImage ? "image" : "file", 15, toneBrush);
        var info = new StackPanel { Spacing = 0 };
        var name = ArcContentUi.Text(attachment.Name, "arc-label");
        name.FontSize = 12;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Paint(TextBlock.ForegroundProperty, toneBrush);
        info.Children.Add(name);
        if (attachment.Size > 0)
        {
            var size = ArcContentUi.Caption(ArcContentUi.FormatBytes(attachment.Size));
            size.Paint(TextBlock.ForegroundProperty, own ? ArcTokens.AccentForeground : ArcTokens.TextMuted);
            info.Children.Add(size);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), Width = 240 };
        row.Children.Add(icon);
        Grid.SetColumn(info, 1);
        row.Children.Add(info);

        var frame = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(8, 6),
            BorderThickness = new Thickness(1),
            Child = row
        };
        frame.Paint(Border.BackgroundProperty, own ? ArcTokens.AccentStrong : ArcTokens.Surface);
        frame.Paint(Border.BorderBrushProperty, own ? ArcTokens.AccentStrong : ArcTokens.BorderSubtle);
        return frame;
    }

    private Control BuildTyping(IReadOnlyList<ArcChatParticipant> typing)
    {
        var outer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("36,*"),
            Margin = new Thickness(0, 8, 0, 0)
        };

        var avatar = BuildAvatar(typing[0], own: false);
        avatar.Margin = new Thickness(0, 2, 8, 0);
        avatar.VerticalAlignment = VerticalAlignment.Top;
        outer.Children.Add(avatar);

        var bubble = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = BuildDots()
        };
        bubble.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        Grid.SetColumn(bubble, 1);
        outer.Children.Add(bubble);
        AutomationProperties.SetName(bubble,
            typing.Count == 1 ? $"{typing[0].Name} is typing" : "Several people are typing");
        return outer;
    }

    private Control BuildDots()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Height = 14
        };
        for (var i = 0; i < 3; i++)
        {
            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = ArcContentUi.Brush(ArcTokens.TextMuted),
                Opacity = ArcThemeManager.ReducedMotion ? 0.55 : 0.35,
                VerticalAlignment = VerticalAlignment.Center
            };
            _typingDots.Add(dot);
            panel.Children.Add(dot);
        }

        if (!ArcThemeManager.ReducedMotion)
        {
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

        return panel;
    }

    private Control BuildAvatar(ArcChatParticipant participant, bool own)
    {
        var diameter = 28;
        var border = new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        border.Paint(Border.BorderBrushProperty, own ? ArcTokens.Accent : ArcTokens.BorderSubtle);

        var image = LoadAvatar(participant.Avatar);
        if (image is not null)
        {
            border.Background = image;
        }
        else
        {
            border.Paint(Border.BackgroundProperty, own ? ArcTokens.AccentSubtle : ArcTokens.SurfaceMuted);
            var initials = ArcContentUi.Text(ArcContentUi.Initials(participant.Name), "arc-caption");
            initials.FontSize = 10;
            initials.FontWeight = FontWeight.Medium;
            initials.HorizontalAlignment = HorizontalAlignment.Center;
            initials.Paint(TextBlock.ForegroundProperty, own ? ArcTokens.AccentStrong : ArcTokens.TextSecondary);
            border.Child = initials;
        }

        AutomationProperties.SetName(border, participant.Name);
        return border;
    }

    private static ImageBrush? LoadAvatar(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            Bitmap? bitmap = null;
            if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = AssetLoader.Open(new Uri(source));
                bitmap = new Bitmap(stream);
            }
            else if (File.Exists(source))
            {
                bitmap = new Bitmap(source);
            }

            return bitmap is null
                ? null
                : new ImageBrush(bitmap)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
        }
        catch
        {
            return null;
        }
    }

    private static string StatusText(ArcChatMessage message)
    {
        var status = message.Status switch
        {
            ArcChatMessageStatus.Sending => "Sending",
            ArcChatMessageStatus.Delivered => "Delivered",
            ArcChatMessageStatus.Read => "Read",
            ArcChatMessageStatus.Failed => "Not sent",
            _ => "Sent"
        };
        return $"{status} · {ArcContentUi.FormatTime(message.CreatedAt)}";
    }

    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        Submit();
        e.Handled = true;
    }

    private void Submit()
    {
        if (_composer is null)
        {
            return;
        }

        var text = _composer.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 && _pending.Count == 0)
        {
            return;
        }

        var draft = new ArcChatDraft(text,
            _pending.Select(item => new ArcChatAttachment
            {
                Name = item.Name,
                Size = item.Size,
                Url = item.Url,
                IsImage = item.IsImage
            }).ToList());

        var message = new ArcChatMessage
        {
            Id = $"m{Guid.NewGuid():N}",
            AuthorId = CurrentUserId,
            Text = text,
            CreatedAt = DateTimeOffset.Now,
            Attachments = draft.Attachments.Select(item => new ArcChatAttachment
            {
                Name = item.Name,
                Size = item.Size,
                Url = item.Url,
                IsImage = item.IsImage
            }).ToList(),
            Status = ArcChatMessageStatus.Sent
        };

        _messages.Add(message);
        if (Messages is { IsReadOnly: false })
        {
            Messages.Add(message);
        }
        _animateId = message.Id;
        _composer.Text = string.Empty;
        _pending.Clear();
        BuildAttachmentTray();
        UpdateSendEnabled();
        RenderMessages(true);
        Dispatcher.UIThread.Post(() => _animateId = null, DispatcherPriority.Background);
        Send?.Invoke(this, draft);
        _composer.Focus();
    }

    private void UpdateSendEnabled()
    {
        if (_send is not null)
        {
            _send.IsEnabled = !string.IsNullOrWhiteSpace(_composer?.Text) || _pending.Count > 0;
        }
    }

    private async System.Threading.Tasks.Task AttachFilesAsync()
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is null)
            {
                return;
            }

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Attach files",
                AllowMultiple = true
            });

            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                long size = 0;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        size = new FileInfo(path).Length;
                    }
                    catch
                    {
                        // A size is optional metadata; the attachment still works without it.
                    }
                }

                _pending.Add(new ArcChatAttachment
                {
                    Name = file.Name,
                    Size = size,
                    Url = path,
                    IsImage = file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                              file.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                              file.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                              file.Name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                              file.Name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                });
            }

            BuildAttachmentTray();
            UpdateSendEnabled();
        }
        catch
        {
            // The storage picker is unavailable in some hosts; the composer stays usable without it.
        }
    }

    private void BuildAttachmentTray()
    {
        if (_attachmentTray is null)
        {
            return;
        }

        _attachmentTray.Children.Clear();
        foreach (var attachment in _pending.ToList())
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("22,*,Auto"),
                Margin = new Thickness(2, 0)
            };
            row.Children.Add(ArcContentUi.Icon(attachment.IsImage ? "image" : "file", 14, ArcTokens.TextSecondary));
            var name = ArcContentUi.Text(attachment.Name, "arc-caption");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var remove = new ArcButton
            {
                Content = ArcContentUi.Icon("x", 12, ArcTokens.TextSecondary),
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Width = 24,
                Height = 24,
                MinHeight = 24,
                Padding = new Thickness(0)
            };
            AutomationProperties.SetName(remove, $"Remove {attachment.Name}");
            remove.Click += (_, _) =>
            {
                _pending.Remove(attachment);
                BuildAttachmentTray();
                UpdateSendEnabled();
            };
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);

            var frame = new Border
            {
                CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPill),
                Padding = new Thickness(10, 3, 4, 3),
                Child = row
            };
            frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            frame.BorderThickness = new Thickness(1);
            _attachmentTray.Children.Add(frame);
        }
    }
}
