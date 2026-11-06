using Avalonia.Interactivity;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>Payload for the bubbled <see cref="ArcActionSwap.ValueChangedRouted"/> event.</summary>
public sealed class ArcActionSwapValueChangedEventArgs : RoutedEventArgs
{
    public ArcActionSwapValueChangedEventArgs()
    {
    }

    public ArcActionSwapValueChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>State the action button moved to.</summary>
    public ArcActionSwapState State { get; init; } = null!;
}

/// <summary>Payload for the bubbled <see cref="ArcAlert.DismissedRouted"/> event.</summary>
public sealed class ArcAlertDismissedEventArgs : RoutedEventArgs
{
    public ArcAlertDismissedEventArgs()
    {
    }

    public ArcAlertDismissedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcConfirmMorph.StateChangedRouted"/> event.</summary>
public sealed class ArcConfirmMorphStateChangedEventArgs : RoutedEventArgs
{
    public ArcConfirmMorphStateChangedEventArgs()
    {
    }

    public ArcConfirmMorphStateChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>State the confirm morph moved to.</summary>
    public ArcConfirmMorphState State { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcCopyButton.CopiedRouted"/> event.</summary>
public sealed class ArcCopyButtonCopiedEventArgs : RoutedEventArgs
{
    public ArcCopyButtonCopiedEventArgs()
    {
    }

    public ArcCopyButtonCopiedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcExpandableText.ExpandedChangedRouted"/> event.</summary>
public sealed class ArcExpandableTextExpandedChangedEventArgs : RoutedEventArgs
{
    public ArcExpandableTextExpandedChangedEventArgs()
    {
    }

    public ArcExpandableTextExpandedChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>True when the text is expanded.</summary>
    public bool IsExpanded { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcExpandingButtonGroup.ActionInvokedRouted"/> event.</summary>
public sealed class ArcExpandingButtonGroupActionInvokedEventArgs : RoutedEventArgs
{
    public ArcExpandingButtonGroupActionInvokedEventArgs()
    {
    }

    public ArcExpandingButtonGroupActionInvokedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the invoked item.</summary>
    public string? ActionId { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcFloatingButtonGroup.ActionInvokedRouted"/> event.</summary>
public sealed class ArcFloatingButtonGroupActionInvokedEventArgs : RoutedEventArgs
{
    public ArcFloatingButtonGroupActionInvokedEventArgs()
    {
    }

    public ArcFloatingButtonGroupActionInvokedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Id of the invoked item.</summary>
    public string? ActionId { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcSplitButton.ClickRouted"/> event.</summary>
public sealed class ArcSplitButtonClickEventArgs : RoutedEventArgs
{
    public ArcSplitButtonClickEventArgs()
    {
    }

    public ArcSplitButtonClickEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcThemeSwitcher.ThemeChangedRouted"/> event.</summary>
public sealed class ArcThemeSwitcherThemeChangedEventArgs : RoutedEventArgs
{
    public ArcThemeSwitcherThemeChangedEventArgs()
    {
    }

    public ArcThemeSwitcherThemeChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Scheme selected in the switcher.</summary>
    public ArcColorScheme Scheme { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcToast.ClosedRouted"/> event.</summary>
public sealed class ArcToastClosedEventArgs : RoutedEventArgs
{
    public ArcToastClosedEventArgs()
    {
    }

    public ArcToastClosedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}

/// <summary>Payload for the bubbled <see cref="ArcUserMenu.ThemeChangedRouted"/> event.</summary>
public sealed class ArcUserMenuThemeChangedEventArgs : RoutedEventArgs
{
    public ArcUserMenuThemeChangedEventArgs()
    {
    }

    public ArcUserMenuThemeChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>Theme mode selected in the user menu.</summary>
    public ArcUserMenuTheme Theme { get; init; }
}
