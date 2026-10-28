using System;
using System.Collections.Generic;

namespace Arc.Avalonia.Media.Controls;

/// <summary>Fill colour of <see cref="ArcHoldToConfirm"/>; danger for irreversible actions.</summary>
public enum ArcHoldToConfirmTone
{
    Danger,
    Neutral
}

/// <summary>Revealed action colours on a swipe row. Status tones only ever report status.</summary>
public enum ArcSwipeActionTone
{
    Neutral,
    Accent,
    Danger
}

/// <summary>One action hidden under a swipe row's edge.</summary>
public sealed class ArcSwipeAction
{
    public ArcSwipeAction()
    {
    }

    public ArcSwipeAction(string label, string icon, ArcSwipeActionTone tone = ArcSwipeActionTone.Neutral, Action? action = null)
    {
        Label = label;
        Icon = icon;
        Tone = tone;
        Action = action;
    }

    /// <summary>Shown under the icon, in the accessible name and in the menu.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Lucide icon name drawn at 18px.</summary>
    public string? Icon { get; set; }

    public ArcSwipeActionTone Tone { get; set; } = ArcSwipeActionTone.Neutral;

    /// <summary>Springs the row home after the action instead of letting the caller remove it.</summary>
    public bool KeepRow { get; set; }

    /// <summary>Runs on tap.</summary>
    public Action? Action { get; set; }
}

/// <summary>One reaction the picker offers.</summary>
public sealed class ArcReactionOption
{
    public ArcReactionOption()
    {
    }

    public ArcReactionOption(string id, string label, string icon)
    {
        Id = id;
        Label = label;
        Icon = icon;
    }

    /// <summary>Stable id used by <see cref="ArcReaction.Id"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Accessible name, for example "Love".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Lucide icon name.</summary>
    public string Icon { get; set; } = "heart";
}

/// <summary>One pill in an <see cref="ArcReactions"/> bar.</summary>
public sealed class ArcReaction
{
    public ArcReaction()
    {
    }

    public ArcReaction(string id, int count = 0, bool reacted = false)
    {
        Id = id;
        Count = count;
        Reacted = reacted;
    }

    public string Id { get; set; } = string.Empty;

    public int Count { get; set; }

    /// <summary>True when the current person added this reaction.</summary>
    public bool Reacted { get; set; }
}

/// <summary>The single change that produced a new reaction list.</summary>
public sealed class ArcReactionChange
{
    public ArcReactionChange()
    {
    }

    public ArcReactionChange(string id, bool reacted)
    {
        Id = id;
        Reacted = reacted;
    }

    public string Id { get; set; } = string.Empty;

    public bool Reacted { get; set; }
}

/// <summary>Raised after a reaction is added, removed or toggled.</summary>
public sealed class ArcReactionsChangedEventArgs : EventArgs
{
    public ArcReactionsChangedEventArgs(IReadOnlyList<ArcReaction> next, ArcReactionChange change)
    {
        Next = next;
        Change = change;
    }

    /// <summary>The full list after the change, in pill order.</summary>
    public IReadOnlyList<ArcReaction> Next { get; }

    /// <summary>The single reaction that changed.</summary>
    public ArcReactionChange Change { get; }
}

/// <summary>One slide of an <see cref="ArcCarousel"/>.</summary>
public sealed class ArcCarouselItem
{
    public ArcCarouselItem()
    {
    }

    public ArcCarouselItem(string title, string? description = null, object? content = null)
    {
        Title = title;
        Description = description;
        Content = content;
    }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>A control rendered above the copy; otherwise the slide shows a gradient wash.</summary>
    public object? Content { get; set; }
}

/// <summary>Which way a card left a <see cref="ArcCardStack"/>.</summary>
public enum ArcCardDecision
{
    None,
    Left,
    Right
}

/// <summary>One card of an <see cref="ArcCardStack"/> deck.</summary>
public sealed class ArcCardStackItem
{
    public ArcCardStackItem()
    {
    }

    public ArcCardStackItem(string key, string label, string title, string? description = null, object? content = null)
    {
        Key = key;
        Label = label;
        Title = title;
        Description = description;
        Content = content;
    }

    /// <summary>Stable identity, reported back through the decision event.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Short name for labels and announcements, such as "Lisbon".</summary>
    public string Label { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional content that replaces the generated card body.</summary>
    public object? Content { get; set; }
}

/// <summary>Reports a card that left the deck and the way it went.</summary>
public sealed class ArcCardDecisionEventArgs : EventArgs
{
    public ArcCardDecisionEventArgs(ArcCardStackItem item, ArcCardDecision decision)
    {
        Item = item;
        Decision = decision;
    }

    public ArcCardStackItem Item { get; }

    public ArcCardDecision Decision { get; }
}

/// <summary>Horizontal splits images left and right; vertical stacks them top and bottom.</summary>
public enum ArcImageCompareOrientation
{
    Horizontal,
    Vertical
}

/// <summary>Horizontal or vertical split direction.</summary>
public enum ArcPanelOrientation
{
    Horizontal,
    Vertical
}

/// <summary>One pane of an <see cref="ArcResizablePanels"/> group.</summary>
public sealed class ArcResizablePane
{
    public ArcResizablePane()
    {
    }

    public ArcResizablePane(string id, string label, double defaultSize = 50, double minSize = 80, object? content = null)
    {
        Id = id;
        Label = label;
        DefaultSize = defaultSize;
        MinSize = minSize;
        Content = content;
    }

    /// <summary>Stable id used by the divider that controls it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Names the pane for its divider.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Starting share of the group; shares are relative.</summary>
    public double DefaultSize { get; set; } = 50;

    /// <summary>Size in pixels where the pane starts to resist.</summary>
    public double MinSize { get; set; } = 80;

    /// <summary>Maximum size in pixels.</summary>
    public double? MaxSize { get; set; }

    /// <summary>Lets a drag past the minimum or Enter on the divider close the pane.</summary>
    public bool Collapsible { get; set; }

    public bool DefaultCollapsed { get; set; }

    public object? Content { get; set; }
}

/// <summary>Axes a scroll area can scroll.</summary>
public enum ArcScrollOrientation
{
    Vertical,
    Horizontal,
    Both
}

/// <summary>When the thin overlay scrollbars are visible.</summary>
public enum ArcScrollbarsMode
{
    Auto,
    Always
}

/// <summary>The four content edges of a scroll area; true means content extends past that edge.</summary>
public readonly record struct ArcScrollAreaEdges(bool Top, bool Bottom, bool Left, bool Right);
