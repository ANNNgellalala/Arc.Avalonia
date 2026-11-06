using System;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Agent;

/// <summary>Routed args for the approval tray decision (additive companion to the CLR event).</summary>
public sealed class ArcAgentDecisionRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentDecisionRoutedEventArgs(RoutedEvent routedEvent, ArcApprovalDecision decision) : base(routedEvent) =>
        Decision = decision;

    /// <summary>The decision the person made.</summary>
    public ArcApprovalDecision Decision { get; }
}

/// <summary>Routed args for the composer submit event.</summary>
public sealed class ArcComposerSubmitRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcComposerSubmitRoutedEventArgs(RoutedEvent routedEvent, ArcComposerSubmit submit) : base(routedEvent) =>
        Submit = submit;

    /// <summary>The submitted draft and optional model.</summary>
    public ArcComposerSubmit Submit { get; }
}

/// <summary>Routed args for the surface command events (both CommandRequested and CommandRaised).</summary>
public sealed class ArcAgentCommandRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentCommandRoutedEventArgs(RoutedEvent routedEvent, ArcAgentCommand command) : base(routedEvent) =>
        Command = command;

    /// <summary>The command sent to the host.</summary>
    public ArcAgentCommand Command { get; }
}

/// <summary>Routed args for the tool-call card action intents (approve/deny/retry/details).</summary>
public sealed class ArcAgentToolActionRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentToolActionRoutedEventArgs(RoutedEvent routedEvent, string callId) : base(routedEvent) => CallId = callId;

    /// <summary>The tool call the person acted on.</summary>
    public string CallId { get; }
}
