using Avalonia.Interactivity;

namespace Arc.Avalonia.Overlays.Controls;

/// <summary>Payload for the bubbled <see cref="ArcDialog.ClosedRouted"/> event.</summary>
public sealed class ArcDialogClosedEventArgs : RoutedEventArgs
{
    public ArcDialogClosedEventArgs()
    {
    }

    public ArcDialogClosedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcDrawer.ClosedRouted"/> event.</summary>
public sealed class ArcDrawerClosedEventArgs : RoutedEventArgs
{
    public ArcDrawerClosedEventArgs()
    {
    }

    public ArcDrawerClosedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.DoneRouted"/> event.</summary>
public sealed class ArcShareAccessDoneEventArgs : RoutedEventArgs
{
    public ArcShareAccessDoneEventArgs()
    {
    }

    public ArcShareAccessDoneEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.RemoveRouted"/> event.</summary>
public sealed class ArcShareAccessRemoveEventArgs : RoutedEventArgs
{
    public ArcShareAccessRemoveEventArgs()
    {
    }

    public ArcShareAccessRemoveEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Person being removed from the share list.</summary>
    public ArcSharePerson? Person { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.UndoRemoveRouted"/> event.</summary>
public sealed class ArcShareAccessUndoRemoveEventArgs : RoutedEventArgs
{
    public ArcShareAccessUndoRemoveEventArgs()
    {
    }

    public ArcShareAccessUndoRemoveEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Person whose removal is being undone.</summary>
    public ArcSharePerson? Person { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.PeopleChangedRouted"/> event.</summary>
public sealed class ArcShareAccessPeopleChangedEventArgs : RoutedEventArgs
{
    public ArcShareAccessPeopleChangedEventArgs()
    {
    }

    public ArcShareAccessPeopleChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.LinkAccessChangedRouted"/> event.</summary>
public sealed class ArcShareAccessLinkAccessChangedEventArgs : RoutedEventArgs
{
    public ArcShareAccessLinkAccessChangedEventArgs()
    {
    }

    public ArcShareAccessLinkAccessChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>New link access mode.</summary>
    public ArcShareLinkAccess Access { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcShareAccess.LinkCopiedRouted"/> event.</summary>
public sealed class ArcShareAccessLinkCopiedEventArgs : RoutedEventArgs
{
    public ArcShareAccessLinkCopiedEventArgs()
    {
    }

    public ArcShareAccessLinkCopiedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Link text that was copied.</summary>
    public string Link { get; init; } = string.Empty;
}
