using Avalonia.Interactivity;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>Payload for the bubbled <see cref="ArcBottomSheet.OpenChangedRouted"/> event.</summary>
public sealed class ArcBottomSheetOpenChangedEventArgs : RoutedEventArgs
{
    public ArcBottomSheetOpenChangedEventArgs()
    {
    }

    public ArcBottomSheetOpenChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>True when the sheet is open.</summary>
    public bool IsOpen { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcBottomSheet.DetentChangedRouted"/> event.</summary>
public sealed class ArcBottomSheetDetentChangedEventArgs : RoutedEventArgs
{
    public ArcBottomSheetDetentChangedEventArgs()
    {
    }

    public ArcBottomSheetDetentChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Index of the active detent.</summary>
    public int DetentIndex { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcExpandableCard.ExpandedChangedRouted"/> event.</summary>
public sealed class ArcExpandableCardExpandedChangedEventArgs : RoutedEventArgs
{
    public ArcExpandableCardExpandedChangedEventArgs()
    {
    }

    public ArcExpandableCardExpandedChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>True when the card is expanded.</summary>
    public bool IsExpanded { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcAnnouncementBar.IndexChangedRouted"/> event.</summary>
public sealed class ArcAnnouncementBarIndexChangedEventArgs : RoutedEventArgs
{
    public ArcAnnouncementBarIndexChangedEventArgs()
    {
    }

    public ArcAnnouncementBarIndexChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The active announcement index.</summary>
    public int Index { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcAnnouncementBar.OpenChangedRouted"/> event.</summary>
public sealed class ArcAnnouncementBarOpenChangedEventArgs : RoutedEventArgs
{
    public ArcAnnouncementBarOpenChangedEventArgs()
    {
    }

    public ArcAnnouncementBarOpenChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>True when the bar is open.</summary>
    public bool IsOpen { get; init; }
}
