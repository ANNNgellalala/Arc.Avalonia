using System;
using System.Collections.Generic;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Media.Controls;

/// <summary>Routed args for the card decision events (additive companion to the CLR events).</summary>
public sealed class ArcCardDismissedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcCardDismissedRoutedEventArgs(RoutedEvent routedEvent, ArcCardStackItem item, ArcCardDecision decision)
        : base(routedEvent)
    {
        Item = item;
        Decision = decision;
    }

    /// <summary>The card that left the deck.</summary>
    public ArcCardStackItem Item { get; }

    /// <summary>Which way it went.</summary>
    public ArcCardDecision Decision { get; }
}

/// <summary>Routed args for the card undo event.</summary>
public sealed class ArcCardUndoneRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcCardUndoneRoutedEventArgs(RoutedEvent routedEvent, ArcCardStackItem item, ArcCardDecision decision)
        : base(routedEvent)
    {
        Item = item;
        Decision = decision;
    }

    /// <summary>The restored card.</summary>
    public ArcCardStackItem Item { get; }

    /// <summary>The decision that was undone.</summary>
    public ArcCardDecision Decision { get; }
}

/// <summary>Routed args for the deck reset event.</summary>
public sealed class ArcStackResetRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcStackResetRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Routed args for the carousel index change event.</summary>
public sealed class ArcCarouselIndexChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcCarouselIndexChangedRoutedEventArgs(RoutedEvent routedEvent, int index) : base(routedEvent) => Index = index;

    /// <summary>The new zero-based index.</summary>
    public int Index { get; }
}

/// <summary>Routed args for the image compare position change event.</summary>
public sealed class ArcImageComparePositionChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcImageComparePositionChangedRoutedEventArgs(RoutedEvent routedEvent, double position) : base(routedEvent) =>
        Position = position;

    /// <summary>Split position from 0 to 1.</summary>
    public double Position { get; }
}

/// <summary>Routed args for the reactions value change event.</summary>
public sealed class ArcReactionsValueChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcReactionsValueChangedRoutedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ArcReaction> next, ArcReactionChange change)
        : base(routedEvent)
    {
        Next = next;
        Change = change;
    }

    /// <summary>The full list after the change.</summary>
    public IReadOnlyList<ArcReaction> Next { get; }

    /// <summary>The reaction that changed.</summary>
    public ArcReactionChange Change { get; }
}

/// <summary>Routed args for the resizable panels layout change event.</summary>
public sealed class ArcPanelsLayoutChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcPanelsLayoutChangedRoutedEventArgs(RoutedEvent routedEvent, IReadOnlyList<double> shares) : base(routedEvent) =>
        Shares = shares;

    /// <summary>Pane share fractions after the drag.</summary>
    public IReadOnlyList<double> Shares { get; }
}

/// <summary>Routed args for the swipe action event.</summary>
public sealed class ArcSwipeActionInvokedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcSwipeActionInvokedRoutedEventArgs(RoutedEvent routedEvent, ArcSwipeAction action) : base(routedEvent) =>
        Action = action;

    /// <summary>The action the person invoked.</summary>
    public ArcSwipeAction Action { get; }
}

/// <summary>Routed args for the hold-to-confirm confirmed event.</summary>
public sealed class ArcHoldConfirmedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcHoldConfirmedRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}
