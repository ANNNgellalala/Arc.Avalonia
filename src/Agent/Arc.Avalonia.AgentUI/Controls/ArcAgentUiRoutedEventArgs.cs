using System;
using Avalonia.Interactivity;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>Routed args for the run stop intent.</summary>
public sealed class ArcAgentRunStopRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentRunStopRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Routed args for the run pause intent.</summary>
public sealed class ArcAgentRunPauseRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentRunPauseRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Routed args for the run resume intent.</summary>
public sealed class ArcAgentRunResumeRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentRunResumeRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Routed args for the steer intent.</summary>
public sealed class ArcAgentRunSteerRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentRunSteerRoutedEventArgs(RoutedEvent routedEvent, string instruction) : base(routedEvent) =>
        Instruction = instruction;

    /// <summary>The new instruction the host should fold into the plan.</summary>
    public string Instruction { get; }
}

/// <summary>Routed args for the manual takeover intent.</summary>
public sealed class ArcAgentRunTakeoverRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentRunTakeoverRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Routed args for the approval intent.</summary>
public sealed class ArcAgentApprovalRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentApprovalRoutedEventArgs(RoutedEvent routedEvent, ArcApprovalDecision decision) : base(routedEvent) =>
        Decision = decision;

    /// <summary>The decision the person made.</summary>
    public ArcApprovalDecision Decision { get; }
}

/// <summary>Routed args for the deny intent.</summary>
public sealed class ArcAgentDenyRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcAgentDenyRoutedEventArgs(RoutedEvent routedEvent, string reason) : base(routedEvent) => Reason = reason;

    /// <summary>The reason the person typed when denying.</summary>
    public string Reason { get; }
}
