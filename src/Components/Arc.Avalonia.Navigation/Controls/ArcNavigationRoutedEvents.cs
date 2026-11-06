using Avalonia.Interactivity;

namespace Arc.Avalonia.Navigation.Controls;

/// <summary>Payload for the bubbled <see cref="ArcMegaMenu.SectionChangedRouted"/> event.</summary>
public sealed class ArcMegaMenuSectionChangedEventArgs : RoutedEventArgs
{
    public ArcMegaMenuSectionChangedEventArgs()
    {
    }

    public ArcMegaMenuSectionChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Section key now active, or null when no section is open.</summary>
    public string? Section { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcTabs.SelectionChangedRouted"/> event.</summary>
public sealed class ArcTabsSelectionChangedEventArgs : RoutedEventArgs
{
    public ArcTabsSelectionChangedEventArgs()
    {
    }

    public ArcTabsSelectionChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Tab that became selected.</summary>
    public ArcTabItem? Item { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcPagination.PageChangedRouted"/> event.</summary>
public sealed class ArcPaginationPageChangedEventArgs : RoutedEventArgs
{
    public ArcPaginationPageChangedEventArgs()
    {
    }

    public ArcPaginationPageChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Zero-based page that became active.</summary>
    public int PageIndex { get; init; }
}
