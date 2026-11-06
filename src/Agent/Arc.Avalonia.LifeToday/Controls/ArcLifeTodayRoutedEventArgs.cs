using System;
using Avalonia.Interactivity;

namespace Arc.Avalonia.LifeToday.Controls;

/// <summary>
/// Shared routed-intent payload for the LifeToday controls (additive companions to the existing
/// <c>Action</c>/<c>EventHandler</c> events). Only the fields relevant to the intent are set.
/// </summary>
public sealed class ArcLifeTodayRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcLifeTodayRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The item id or text carried by the intent, when applicable.</summary>
    public string? Text { get; init; }

    /// <summary>The boolean payload, when applicable.</summary>
    public bool Flag { get; init; }

    /// <summary>The time-span payload, when applicable.</summary>
    public TimeSpan Span { get; init; }

    /// <summary>The schedule payload, when applicable.</summary>
    public ArcScheduleSpec? Spec { get; init; }
}
