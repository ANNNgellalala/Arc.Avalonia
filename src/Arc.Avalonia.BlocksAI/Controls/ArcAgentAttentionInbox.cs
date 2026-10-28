using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksAI.Controls;

/// <summary>Why an item is waiting in the attention inbox.</summary>
public enum ArcAttentionKind
{
    /// <summary>The agent needs permission to run a risky step.</summary>
    Approval,

    /// <summary>The agent needs one detail before it can continue.</summary>
    Ask,

    /// <summary>A run or routine failed and needs a decision.</summary>
    Failure,

    /// <summary>A routine ran and left a result for review.</summary>
    Routine,

    /// <summary>A suggestion the agent could act on if the person agrees.</summary>
    Suggestion
}

/// <summary>How much attention an item needs.</summary>
public enum ArcAttentionRisk
{
    /// <summary>Informational or easily undone.</summary>
    Low,

    /// <summary>Changes something the person will notice.</summary>
    Medium,

    /// <summary>Blocks work that cannot continue without the person.</summary>
    High
}

/// <summary>Where an item is in its own lifecycle.</summary>
public enum ArcAttentionItemStatus
{
    /// <summary>Nobody has opened it yet.</summary>
    New,

    /// <summary>The person opened it but did not decide.</summary>
    Seen,

    /// <summary>The person is working through it.</summary>
    InProgress,

    /// <summary>It was handled.</summary>
    Resolved,

    /// <summary>It timed out before it was handled.</summary>
    Expired
}

/// <summary>One item in the "waiting on you" queue.</summary>
public sealed class ArcAttentionItem
{
    /// <summary>Stable id returned when the item is opened or dismissed.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The family of work, used to group the queue.</summary>
    public ArcAttentionKind Kind { get; set; }

    /// <summary>One line describing what needs a decision.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One sentence of context: amount, source, consequence.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Attention level shown as a badge on blocking items.</summary>
    public ArcAttentionRisk Risk { get; set; }

    /// <summary>How old the item is, for example "4m" or "2h".</summary>
    public string Age { get; set; } = string.Empty;

    /// <summary>The primary action label, for example "Review", "Answer" or "Retry".</summary>
    public string ActionLabel { get; set; } = "Open";

    /// <summary>Where the item is in its lifecycle; resolved items are dimmed.</summary>
    public ArcAttentionItemStatus Status { get; set; }
}

/// <summary>
/// The "waiting on you" queue for a desktop agent: everything that blocks the agent or needs a person's
/// judgement (approvals, questions, failures, finished routines and suggestions) in one list, grouped by kind,
/// ordered from most to least blocking, with a per-item action and dismiss.
///
/// The block is pure UI: it renders <see cref="Items"/> and raises <see cref="ItemActionRequested"/> or
/// <see cref="Dismissed"/> with the item id. Routing the decision to the approval sheet, the question card or a
/// recovery flow, and keeping the counts live, stays with the host.
/// </summary>
public class ArcAgentAttentionInbox : Panel
{
    /// <summary>The queue, grouped by <see cref="ArcAttentionItem.Kind"/> when rendered.</summary>
    public static readonly StyledProperty<IEnumerable<ArcAttentionItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcAgentAttentionInbox, IEnumerable<ArcAttentionItem>?>(nameof(Items), SampleItems());

    /// <summary>The section title above the queue.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcAgentAttentionInbox, string>(nameof(Title), "Waiting on you");

    /// <summary>Copy for the empty state.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ArcAgentAttentionInbox, string>(
            nameof(EmptyText), "Nothing needs you right now. Approvals, questions and failed runs will appear here.");

    static ArcAgentAttentionInbox()
    {
        ItemsProperty.Changed.AddClassHandler<ArcAgentAttentionInbox>((inbox, _) => inbox.Rebuild());
        TitleProperty.Changed.AddClassHandler<ArcAgentAttentionInbox>((inbox, _) => inbox.Rebuild());
        EmptyTextProperty.Changed.AddClassHandler<ArcAgentAttentionInbox>((inbox, _) => inbox.Rebuild());
    }

    public ArcAgentAttentionInbox()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>Everything waiting on the person.</summary>
    public IEnumerable<ArcAttentionItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The section title above the queue.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Copy shown when there is nothing to handle.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Raised with the item id when the person opens the item's primary action.</summary>
    public event EventHandler<string>? ItemActionRequested;

    /// <summary>Raised with the item id when the person dismisses the item.</summary>
    public event EventHandler<string>? Dismissed;

    private void Rebuild()
    {
        var items = (Items ?? Array.Empty<ArcAttentionItem>()).ToList();
        Children.Clear();

        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(BuildHeader(items));

        if (items.Count == 0)
        {
            body.Children.Add(BuildEmptyState());
        }
        else
        {
            body.Children.Add(ArcUi.Divider());
            foreach (var group in items.GroupBy(item => item.Kind).OrderBy(group => KindOrder(group.Key)))
            {
                body.Children.Add(BuildGroup(group.Key, group.ToList()));
            }
        }

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(18),
            Child = body
        });

        var blocking = items.Count(item => item.Status is ArcAttentionItemStatus.New or ArcAttentionItemStatus.Seen
                                            && item.Kind is ArcAttentionKind.Approval or ArcAttentionKind.Ask);
        ArcUi.Name(
            this,
            $"{Title}, {items.Count} items, {blocking} blocking",
            "Approvals, questions, failed runs, finished routines and suggestions that need a decision.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private Control BuildHeader(IReadOnlyList<ArcAttentionItem> items)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var line = ArcUi.HStack(8);
        line.Children.Add(ArcUi.Label(Title));
        line.Children.Add(ArcUi.Badge(items.Count.ToString(), items.Count > 0 ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral));
        titles.Children.Add(line);
        titles.Children.Add(ArcUi.Caption(Summary(items)));
        header.Children.Add(titles);

        if (items.Count > 0)
        {
            var hint = ArcUi.Pill("Sorted by how much it blocks", "layers");
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hint, 1);
            header.Children.Add(hint);
        }

        return header;
    }

    private static string Summary(IReadOnlyList<ArcAttentionItem> items)
    {
        if (items.Count == 0)
        {
            return "Everything is handled";
        }

        var blocking = items.Count(item => item.Kind is ArcAttentionKind.Approval or ArcAttentionKind.Ask);
        var failed = items.Count(item => item.Kind == ArcAttentionKind.Failure);
        return $"{blocking} waiting on a decision · {failed} failed · {items.Count - blocking - failed} to review";
    }

    private Control BuildGroup(ArcAttentionKind kind, IReadOnlyList<ArcAttentionItem> items)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(BuildGroupHeader(kind, items.Count));
        foreach (var item in items)
        {
            panel.Children.Add(BuildRow(item));
        }

        return panel;
    }

    private Control BuildGroupHeader(ArcAttentionKind kind, int count)
    {
        var header = ArcUi.HStack(8);
        header.Margin = new Thickness(0, 8, 0, 2);
        header.Children.Add(ArcUi.Icon(KindIcon(kind), 14, KindToken(kind)));
        var label = ArcUi.Label(KindWord(kind));
        header.Children.Add(label);
        header.Children.Add(ArcUi.Caption(count.ToString()));
        return header;
    }

    private Control BuildRow(ArcAttentionItem item)
    {
        var resolved = item.Status is ArcAttentionItemStatus.Resolved or ArcAttentionItemStatus.Expired;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 10
        };

        row.Children.Add(new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(11),
            Background = ArcUi.Brush(KindBackground(item.Kind)),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ArcIcon
            {
                Icon = KindIcon(item.Kind),
                Size = 15,
                Foreground = ArcUi.Brush(KindToken(item.Kind)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var copy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var line = ArcUi.HStack(6);
        var title = ArcUi.Text(item.Title, "arc-body-sm arc-medium", 460);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.VerticalAlignment = VerticalAlignment.Center;
        if (resolved)
        {
            title.Foreground = ArcUi.Brush(ArcTokens.TextMuted);
        }

        line.Children.Add(title);
        if (item.Risk == ArcAttentionRisk.High)
        {
            line.Children.Add(ArcUi.Badge("High risk", ArcBadgeTone.Danger));
        }
        else if (item.Risk == ArcAttentionRisk.Medium)
        {
            line.Children.Add(ArcUi.Badge("Medium", ArcBadgeTone.Warning));
        }

        if (item.Status != ArcAttentionItemStatus.New)
        {
            line.Children.Add(StatusBadge(item.Status));
        }

        copy.Children.Add(line);

        var summary = ArcUi.Text(item.Summary, "arc-caption", 480);
        if (resolved)
        {
            summary.Foreground = ArcUi.Brush(ArcTokens.TextMuted);
        }

        copy.Children.Add(summary);

        var meta = ArcUi.HStack(6);
        meta.Children.Add(ArcUi.Caption(item.Age));
        meta.Children.Add(ArcUi.Caption("·"));
        meta.Children.Add(ArcUi.Caption(KindWord(item.Kind)));
        copy.Children.Add(meta);

        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);

        var action = new ArcButton
        {
            Content = item.ActionLabel,
            Variant = item.Kind is ArcAttentionKind.Approval or ArcAttentionKind.Ask
                ? ArcButtonVariant.Primary
                : ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0)
        };
        AutomationProperties.SetName(action, $"{item.ActionLabel}: {item.Title}");
        ArcUi.Name(action, $"{item.ActionLabel}: {item.Title}");
        action.Click += (_, _) => ItemActionRequested?.Invoke(this, item.Id);
        Grid.SetColumn(action, 2);
        row.Children.Add(action);

        var dismiss = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Content = ArcUi.Icon("x", 14, ArcTokens.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(dismiss, $"Dismiss: {item.Title}");
        ToolTip.SetTip(dismiss, "Dismiss");
        dismiss.Click += (_, _) => Dismissed?.Invoke(this, item.Id);
        Grid.SetColumn(dismiss, 3);
        row.Children.Add(dismiss);

        return new Border
        {
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(8, 7, 6, 7),
            Background = null,
            Child = row
        };
    }

    private Control BuildEmptyState()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 30, 0, 26)
        };

        panel.Children.Add(new ArcIcon
        {
            Icon = "check-check",
            Size = 26,
            Foreground = ArcUi.Brush(ArcTokens.Success),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var title = ArcUi.Text("All caught up", "arc-body-sm arc-medium");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(title);

        var copy = ArcUi.Text(EmptyText, "arc-caption", 380);
        copy.HorizontalAlignment = HorizontalAlignment.Center;
        copy.TextAlignment = TextAlignment.Center;
        panel.Children.Add(copy);
        AutomationProperties.SetName(panel, EmptyText);
        return panel;
    }

    private static int KindOrder(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => 0,
        ArcAttentionKind.Ask => 1,
        ArcAttentionKind.Failure => 2,
        ArcAttentionKind.Routine => 3,
        _ => 4
    };

    private static string KindWord(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => "Approvals",
        ArcAttentionKind.Ask => "Questions",
        ArcAttentionKind.Failure => "Failures",
        ArcAttentionKind.Routine => "Routines",
        _ => "Suggestions"
    };

    private static string KindIcon(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => "shield-check",
        ArcAttentionKind.Ask => "circle-help",
        ArcAttentionKind.Failure => "triangle-alert",
        ArcAttentionKind.Routine => "clock",
        _ => "sparkles"
    };

    private static string KindToken(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => ArcTokens.Warning,
        ArcAttentionKind.Ask => ArcTokens.Accent,
        ArcAttentionKind.Failure => ArcTokens.Danger,
        _ => ArcTokens.TextSecondary
    };

    private static string KindBackground(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => ArcTokens.WarningSubtle,
        ArcAttentionKind.Ask => ArcTokens.AccentSubtle,
        ArcAttentionKind.Failure => ArcTokens.DangerSubtle,
        _ => ArcTokens.SurfaceMuted
    };

    private static ArcBadge StatusBadge(ArcAttentionItemStatus status) => status switch
    {
        ArcAttentionItemStatus.Seen => ArcUi.Badge("Seen", ArcBadgeTone.Neutral),
        ArcAttentionItemStatus.InProgress => ArcUi.Badge("In progress", ArcBadgeTone.Accent),
        ArcAttentionItemStatus.Resolved => ArcUi.Badge("Resolved", ArcBadgeTone.Success),
        ArcAttentionItemStatus.Expired => ArcUi.Badge("Expired", ArcBadgeTone.Neutral),
        _ => ArcUi.Badge("New", ArcBadgeTone.Neutral)
    };

    private static IEnumerable<ArcAttentionItem> SampleItems() => new[]
    {
        new ArcAttentionItem
        {
            Id = "att-1",
            Kind = ArcAttentionKind.Approval,
            Title = "Approve the electricity payment",
            Summary = "84.20 EUR from Household · checking; the bill is due tomorrow.",
            Risk = ArcAttentionRisk.High,
            Age = "4m",
            ActionLabel = "Review"
        },
        new ArcAttentionItem
        {
            Id = "att-2",
            Kind = ArcAttentionKind.Approval,
            Title = "Allow calendar changes for this task",
            Summary = "The agent wants to move two appointments and add a reminder.",
            Risk = ArcAttentionRisk.Medium,
            Age = "12m",
            ActionLabel = "Approve"
        },
        new ArcAttentionItem
        {
            Id = "att-3",
            Kind = ArcAttentionKind.Ask,
            Title = "Which account should the budget read?",
            Summary = "Three accounts match the request; the rollup needs one answer.",
            Risk = ArcAttentionRisk.Low,
            Age = "18m",
            ActionLabel = "Answer"
        },
        new ArcAttentionItem
        {
            Id = "att-4",
            Kind = ArcAttentionKind.Failure,
            Title = "Routine failed: weekly meal plan",
            Summary = "The grocery site changed its layout; retry after the fix or record the plan by hand.",
            Risk = ArcAttentionRisk.Medium,
            Age = "1h",
            ActionLabel = "Retry"
        },
        new ArcAttentionItem
        {
            Id = "att-5",
            Kind = ArcAttentionKind.Routine,
            Title = "Morning brief ran while you were away",
            Summary = "Three reminders moved and two events need a decision.",
            Risk = ArcAttentionRisk.Low,
            Age = "2h",
            ActionLabel = "Review",
            Status = ArcAttentionItemStatus.Seen
        },
        new ArcAttentionItem
        {
            Id = "att-6",
            Kind = ArcAttentionKind.Suggestion,
            Title = "Move the dentist reminder to Thursday",
            Summary = "Thursday 15:00 is free; the agent can book it before the next reminder fires.",
            Risk = ArcAttentionRisk.Low,
            Age = "3h",
            ActionLabel = "Adopt"
        }
    };
}
