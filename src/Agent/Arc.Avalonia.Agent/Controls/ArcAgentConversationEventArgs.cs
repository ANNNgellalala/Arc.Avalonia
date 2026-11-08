using System;
using System.Collections.Generic;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Payload of <see cref="ArcAgentConversation.AnswerRequested"/>: the answer a person gave to a structured
/// question rendered as a choice card inside the conversation. The host turns it into an
/// <see cref="ArcAnswerCommand"/>.
/// </summary>
public sealed class ArcAgentQuestionAnswerEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentQuestionAnswerEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentQuestionAnswerEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the question request that was answered.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Selected option ids/texts, in declaration order.</summary>
    public IReadOnlyList<string> SelectedOptions { get; init; } = Array.Empty<string>();

    /// <summary>Optional free-text answer.</summary>
    public string? FreeText { get; init; }

    /// <summary>True when the person asked to answer later instead of answering.</summary>
    public bool Deferred { get; init; }
}

/// <summary>
/// Payload of <see cref="ArcAgentConversation.ApprovalDecisionRequested"/>: an approve/deny decision taken on
/// an approval event card. The host turns it into an <see cref="ArcApprovalCommand"/>.
/// </summary>
public sealed class ArcAgentApprovalDecisionEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentApprovalDecisionEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentApprovalDecisionEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the approval request.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>True for approve, false for deny.</summary>
    public bool Approved { get; init; }

    /// <summary>Optional deny reason.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Payload of <see cref="ArcAgentConversation.RetryRequested"/>: the person asked to retry a failed tool call.
/// </summary>
public sealed class ArcAgentToolRetryEventArgs : RoutedEventArgs
{
    /// <summary>Creates empty args for the routed-event pattern.</summary>
    public ArcAgentToolRetryEventArgs()
    {
    }

    /// <summary>Creates args bound to a routed event.</summary>
    /// <param name="routedEvent">The event being raised.</param>
    public ArcAgentToolRetryEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the failed tool call.</summary>
    public string CallId { get; init; } = string.Empty;
}
