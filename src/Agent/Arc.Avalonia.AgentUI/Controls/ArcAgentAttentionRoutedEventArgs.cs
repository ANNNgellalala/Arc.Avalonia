using System;
using Avalonia.Interactivity;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>Routed args for the attention inbox action events (additive companions to the CLR events).</summary>
public sealed class ArcAttentionActionRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAttentionActionRoutedEventArgs(RoutedEvent routedEvent, string entryId) : base(routedEvent) =>
        EntryId = entryId;

    /// <summary>The attention entry the person acted on.</summary>
    public string EntryId { get; }
}
