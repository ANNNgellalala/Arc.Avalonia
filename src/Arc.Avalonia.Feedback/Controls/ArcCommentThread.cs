using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>A person taking part in an <see cref="ArcCommentThread"/>.</summary>
public sealed class ArcCommentAuthor
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }
}

/// <summary>One emoji reaction and the people who chose it.</summary>
public sealed class ArcCommentReaction
{
    public string Emoji { get; set; } = string.Empty;

    public List<string> Users { get; set; } = new();
}

/// <summary>One comment in the tree.</summary>
public sealed class ArcComment
{
    public string Id { get; set; } = string.Empty;

    public ArcCommentAuthor Author { get; set; } = new();

    public string Body { get; set; } = string.Empty;

    /// <summary>Display label such as "2h" or "Sep 18".</summary>
    public string CreatedAt { get; set; } = string.Empty;

    public bool Edited { get; set; }

    public bool Deleted { get; set; }

    public List<ArcCommentReaction> Reactions { get; set; } = new();

    public List<ArcComment> Replies { get; set; } = new();
}

/// <summary>
/// A threaded discussion: nested replies, reaction chips, a resolve state that folds the thread
/// into a chip, and a compose row at the bottom. Command/Ctrl+Enter sends from either composer.
/// </summary>
public class ArcCommentThread : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcCommentThread, string?>(nameof(Title));

    public static readonly StyledProperty<ArcCommentAuthor?> CurrentUserProperty =
        AvaloniaProperty.Register<ArcCommentThread, ArcCommentAuthor?>(nameof(CurrentUser));

    public static readonly StyledProperty<IEnumerable<ArcComment>?> CommentsProperty =
        AvaloniaProperty.Register<ArcCommentThread, IEnumerable<ArcComment>?>(nameof(Comments));

    public static readonly StyledProperty<bool> ResolvedProperty =
        AvaloniaProperty.Register<ArcCommentThread, bool>(nameof(Resolved));

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcCommentThread, string>(nameof(Placeholder), "Write a comment");

    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<ArcCommentThread, int>(nameof(MaxDepth), 2);

    private readonly List<ArcComment> _thread = new();
    private Panel? _host;
    private StackPanel? _bodyHost;
    private Panel? _headerHost;
    private TextBox? _composer;
    private ArcButton? _send;
    private string? _replyingTo;
    private TextBox? _replyBox;

    static ArcCommentThread()
    {
        TitleProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.RebuildShell());
        CurrentUserProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.RebuildShell());
        CommentsProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.LoadComments());
        ResolvedProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.RebuildShell());
        PlaceholderProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.RebuildShell());
        MaxDepthProperty.Changed.AddClassHandler<ArcCommentThread>((thread, _) => thread.LoadComments());
    }

    /// <summary>What the thread is about, shown in the header.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ArcCommentAuthor? CurrentUser
    {
        get => GetValue(CurrentUserProperty);
        set => SetValue(CurrentUserProperty, value);
    }

    public IEnumerable<ArcComment>? Comments
    {
        get => GetValue(CommentsProperty);
        set => SetValue(CommentsProperty, value);
    }

    /// <summary>Folds the thread into a resolved chip while true.</summary>
    public bool Resolved
    {
        get => GetValue(ResolvedProperty);
        set => SetValue(ResolvedProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Replies deeper than this attach to the deepest allowed parent.</summary>
    public int MaxDepth
    {
        get => GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    public event EventHandler? CommentsChanged;

    public event EventHandler<bool>? ResolvedChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        LoadComments();
    }

    private void LoadComments()
    {
        _thread.Clear();
        foreach (var comment in Comments ?? Enumerable.Empty<ArcComment>())
        {
            _thread.Add(Clone(comment));
        }

        _replyingTo = null;
        RebuildShell();
    }

    private static ArcComment Clone(ArcComment source) => new()
    {
        Id = source.Id,
        Author = source.Author,
        Body = source.Body,
        CreatedAt = source.CreatedAt,
        Edited = source.Edited,
        Deleted = source.Deleted,
        Reactions = source.Reactions.Select(reaction => new ArcCommentReaction
        {
            Emoji = reaction.Emoji,
            Users = new List<string>(reaction.Users)
        }).ToList(),
        Replies = source.Replies.Select(Clone).ToList()
    };

    private void RebuildShell()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _headerHost = null;
        _bodyHost = null;
        _composer = null;
        _send = null;

        var root = new StackPanel { Spacing = 14 };
        _headerHost = new Panel();
        root.Children.Add(_headerHost);
        BuildHeader();

        if (Resolved)
        {
            root.Children.Add(BuildResolvedNote());
        }
        else
        {
            _bodyHost = new StackPanel { Spacing = 16 };
            root.Children.Add(_bodyHost);
            RenderThread();

            if (_thread.Count > 0)
            {
                root.Children.Add(FeedbackUi.Divider());
            }

            root.Children.Add(BuildComposer());
        }

        _host.Children.Add(root);
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Title) ? "Comment thread" : Title);
    }

    private void BuildHeader()
    {
        if (_headerHost is null)
        {
            return;
        }

        _headerHost.Children.Clear();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(Title))
        {
            left.Children.Add(FeedbackUi.Text(Title!, "arc-label"));
        }

        var count = CountAll(_thread);
        var countText = FeedbackUi.Caption($"{count} {FeedbackUi.Plural(count, "comment", "comments")}");
        left.Children.Add(countText);
        grid.Children.Add(left);

        if (Resolved)
        {
            var chip = FeedbackUi.Pill(FeedbackUi.Text("Resolved", "arc-label"), ArcTokens.SuccessSubtle, ArcTokens.Success);
            Grid.SetColumn(chip, 1);
            grid.Children.Add(chip);

            var reopen = new ArcButton
            {
                Content = "Reopen",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Margin = new Thickness(8, 0, 0, 0)
            };
            reopen.Click += (_, _) => SetResolved(false);
            Grid.SetColumn(reopen, 2);
            grid.Children.Add(reopen);
        }
        else
        {
            var resolve = new ArcButton
            {
                Content = "Resolve",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            };
            resolve.Click += (_, _) => SetResolved(true);
            Grid.SetColumn(resolve, 2);
            grid.Children.Add(resolve);
        }

        _headerHost.Children.Add(grid);
    }

    private void SetResolved(bool value)
    {
        SetCurrentValue(ResolvedProperty, value);
        ResolvedChanged?.Invoke(this, value);
    }

    private Control BuildResolvedNote()
    {
        var row = FeedbackUi.Row(8,
            FeedbackUi.Icon("check", 15, ArcTokens.Success),
            FeedbackUi.Text("This thread is resolved. Reopen it to keep the discussion going.", "arc-body-sm", "arc-secondary"));
        var border = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(14, 10),
            Child = row
        };
        border.Paint(Border.BackgroundProperty, ArcTokens.SuccessSubtle);
        return border;
    }

    private Control BuildComposer()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var avatar = new ArcAvatar
        {
            DisplayName = CurrentUser?.Name ?? "You",
            Src = CurrentUser?.Avatar,
            Size = ArcAvatarSize.Sm,
            Margin = new Thickness(0, 4, 12, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(avatar);

        _composer = FeedbackUi.Input(Placeholder, 44);
        _composer.AcceptsReturn = true;
        _composer.TextWrapping = TextWrapping.Wrap;
        _composer.MaxHeight = 120;
        _composer.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && (args.KeyModifiers.HasFlag(KeyModifiers.Control) || args.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                SendTopLevel();
                args.Handled = true;
            }
        };
        _composer.TextChanged += (_, _) =>
        {
            if (_send is not null)
            {
                _send.IsEnabled = !string.IsNullOrWhiteSpace(_composer.Text);
            }
        };
        Grid.SetColumn(_composer, 1);
        grid.Children.Add(_composer);

        _send = new ArcButton
        {
            Content = "Send",
            Size = ArcButtonSize.Sm,
            IsEnabled = false,
            Margin = new Thickness(8, 4, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        _send.Click += (_, _) => SendTopLevel();
        Grid.SetColumn(_send, 2);
        grid.Children.Add(_send);

        return grid;
    }

    private void SendTopLevel()
    {
        var body = _composer?.Text?.Trim();
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        _thread.Add(new ArcComment
        {
            Id = $"c{Guid.NewGuid():N}",
            Author = CurrentUser ?? new ArcCommentAuthor { Id = "you", Name = "You" },
            Body = body,
            CreatedAt = "now"
        });

        if (_composer is not null)
        {
            _composer.Text = string.Empty;
        }

        _replyingTo = null;
        RenderThread();
        BuildHeader();
        CommentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderThread()
    {
        if (_bodyHost is null)
        {
            return;
        }

        _bodyHost.Children.Clear();
        _replyBox = null;
        foreach (var comment in _thread)
        {
            _bodyHost.Children.Add(BuildComment(comment, 0));
        }
    }

    private Control BuildComment(ArcComment comment, int depth)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var avatar = new ArcAvatar
        {
            DisplayName = comment.Author.Name,
            Src = comment.Author.Avatar,
            Size = depth == 0 ? ArcAvatarSize.Sm : ArcAvatarSize.Xs,
            Margin = new Thickness(0, 2, 12, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        row.Children.Add(avatar);

        var content = new StackPanel { Spacing = 4 };
        Grid.SetColumn(content, 1);
        row.Children.Add(content);

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var author = FeedbackUi.Text(comment.Author.Name, "arc-label");
        header.Children.Add(author);
        header.Children.Add(FeedbackUi.Text(comment.CreatedAt, "arc-caption"));
        if (comment.Edited)
        {
            header.Children.Add(FeedbackUi.Text("edited", "arc-caption"));
        }

        content.Children.Add(header);

        if (comment.Deleted)
        {
            content.Children.Add(FeedbackUi.Text("This comment was deleted.", "arc-body-sm", "arc-muted"));
        }
        else
        {
            var body = new TextBlock
            {
                Text = comment.Body,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 640
            };
            body.Classes.Add("arc-body-sm");
            body.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
            content.Children.Add(body);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        if (!comment.Deleted && depth < Math.Max(1, MaxDepth))
        {
            var reply = new ArcFlatButton { Text = "Reply", Quiet = true };
            reply.Click += (_, _) =>
            {
                _replyingTo = _replyingTo == comment.Id ? null : comment.Id;
                RenderThread();
                if (_replyBox is not null)
                {
                    _replyBox.Focus();
                }
            };
            AutomationProperties.SetName(reply, $"Reply to {comment.Author.Name}");
            actions.Children.Add(reply);
        }

        foreach (var reaction in comment.Reactions)
        {
            actions.Children.Add(BuildReactionChip(comment, reaction));
        }

        var add = new ArcFlatButton { Text = "+", Quiet = true };
        AutomationProperties.SetName(add, "Add reaction");
        add.Click += (_, _) =>
        {
            var existing = comment.Reactions.FirstOrDefault(item => item.Emoji == "👍");
            if (existing is null)
            {
                comment.Reactions.Add(new ArcCommentReaction { Emoji = "👍", Users = { CurrentUser?.Id ?? "you" } });
            }
            else
            {
                ToggleReaction(comment, existing);
            }

            RenderThread();
            CommentsChanged?.Invoke(this, EventArgs.Empty);
        };
        actions.Children.Add(add);
        content.Children.Add(actions);

        if (_replyingTo == comment.Id)
        {
            content.Children.Add(BuildReplyComposer(comment));
        }

        if (comment.Replies.Count > 0 && depth < Math.Max(1, MaxDepth))
        {
            var replies = new StackPanel { Spacing = 14, Margin = new Thickness(0, 12, 0, 0) };
            foreach (var child in comment.Replies)
            {
                replies.Children.Add(BuildComment(child, depth + 1));
            }

            var repliesFrame = new Border
            {
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(14, 0, 0, 0),
                Child = replies
            };
            repliesFrame.Paint(Border.BorderBrushProperty, ArcTokens.ControlTrack);
            content.Children.Add(repliesFrame);
        }

        AutomationProperties.SetName(row, $"{comment.Author.Name}, {comment.CreatedAt}: {comment.Body}");
        return row;
    }

    private Control BuildReactionChip(ArcComment comment, ArcCommentReaction reaction)
    {
        var mine = reaction.Users.Contains(CurrentUser?.Id ?? "you");
        var emoji = new TextBlock
        {
            Text = reaction.Emoji,
            FontFamily = FeedbackUi.EmojiFamily,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        var count = FeedbackUi.Numeric(reaction.Users.Count.ToString(), "arc-caption");
        count.FontSize = 11.5;

        var row = FeedbackUi.Row(4, emoji, count);
        var chip = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(8, 2, 9, 3),
            Child = row,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(2, 0, 0, 0)
        };
        chip.Paint(Border.BackgroundProperty, mine ? ArcTokens.AccentSubtle : ArcTokens.SurfaceMuted);
        chip.Paint(Border.BorderBrushProperty, mine ? ArcTokens.Accent : ArcTokens.BorderSubtle);
        chip.BorderThickness = new Thickness(1);
        AutomationProperties.SetName(chip, $"{reaction.Emoji} {reaction.Users.Count}{(mine ? ", including you" : string.Empty)}");
        chip.PointerPressed += (_, args) =>
        {
            ToggleReaction(comment, reaction);
            RenderThread();
            CommentsChanged?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        };
        return chip;
    }

    private void ToggleReaction(ArcComment comment, ArcCommentReaction reaction)
    {
        var me = CurrentUser?.Id ?? "you";
        if (!reaction.Users.Remove(me))
        {
            reaction.Users.Add(me);
        }
    }

    private Control BuildReplyComposer(ArcComment comment)
    {
        var border = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 6, 0, 0)
        };
        border.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);

        var stack = new StackPanel { Spacing = 8 };
        var box = FeedbackUi.Input($"Reply to {comment.Author.Name}", 38);
        box.AcceptsReturn = true;
        box.TextWrapping = TextWrapping.Wrap;
        box.MaxHeight = 120;
        _replyBox = box;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new ArcButton { Content = "Cancel", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm };
        cancel.Click += (_, _) =>
        {
            _replyingTo = null;
            RenderThread();
        };
        var send = new ArcButton { Content = "Reply", Size = ArcButtonSize.Sm };
        send.Click += (_, _) => SendReply(comment, box);
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && (args.KeyModifiers.HasFlag(KeyModifiers.Control) || args.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                SendReply(comment, box);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                _replyingTo = null;
                RenderThread();
                args.Handled = true;
            }
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(send);

        stack.Children.Add(box);
        stack.Children.Add(buttons);
        border.Child = stack;
        return border;
    }

    private void SendReply(ArcComment parent, TextBox box)
    {
        var body = box.Text?.Trim();
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        parent.Replies.Add(new ArcComment
        {
            Id = $"c{Guid.NewGuid():N}",
            Author = CurrentUser ?? new ArcCommentAuthor { Id = "you", Name = "You" },
            Body = body,
            CreatedAt = "now"
        });

        _replyingTo = null;
        _replyBox = null;
        RenderThread();
        BuildHeader();
        CommentsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static int CountAll(IEnumerable<ArcComment> comments)
    {
        var count = 0;
        foreach (var comment in comments)
        {
            count += 1 + CountAll(comment.Replies);
        }

        return count;
    }
}
