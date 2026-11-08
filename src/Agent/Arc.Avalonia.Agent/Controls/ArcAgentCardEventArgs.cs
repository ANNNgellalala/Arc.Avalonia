using System;
using System.Collections.Generic;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Agent;

/// <summary>Payload of <see cref="ArcAgentChoiceCard.Submitted"/> / <see cref="ArcAgentChoiceCard.SubmittedRouted"/>.</summary>
public sealed class ArcAgentChoiceSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentChoiceSubmittedEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentChoiceSubmittedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the component that raised the interaction.</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>Selected option ids, in declaration order.</summary>
    public IReadOnlyList<string> SelectedIds { get; init; } = Array.Empty<string>();

    /// <summary>Optional free-text answer (when the card allows one and the person typed something).</summary>
    public string? FreeText { get; init; }
}

/// <summary>Payload of <see cref="ArcAgentChoiceCard.DeferredRequested"/> and its routed companion.</summary>
public sealed class ArcAgentDeferredRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentDeferredRequestedEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentDeferredRequestedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the component that was deferred.</summary>
    public string ComponentId { get; init; } = string.Empty;
}

/// <summary>Payload of <see cref="ArcAgentConfirmCard.Decided"/> / <see cref="ArcAgentConfirmCard.DecidedRouted"/>.</summary>
public sealed class ArcAgentConfirmDecidedEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentConfirmDecidedEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentConfirmDecidedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the component that raised the interaction.</summary>
    public string ComponentId { get; init; } = string.Empty;

    /// <summary>True for the confirm action, false for cancel.</summary>
    public bool Confirmed { get; init; }

    /// <summary>Copy of the button the person pressed (useful for persistence and transcripts).</summary>
    public string? Value { get; init; }
}
