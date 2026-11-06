using System;
using System.Collections.Generic;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Content.Controls;

/// <summary>Routed args for the chat send event (additive companion to the CLR event).</summary>
public sealed class ArcChatSendRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcChatSendRoutedEventArgs(RoutedEvent routedEvent, ArcChatDraft draft) : base(routedEvent) => Draft = draft;

    /// <summary>The text and attachments the person sent.</summary>
    public ArcChatDraft Draft { get; }
}

/// <summary>Routed args for the onboarding step invoke event.</summary>
public sealed class ArcChecklistStepInvokedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcChecklistStepInvokedRoutedEventArgs(RoutedEvent routedEvent, ArcChecklistStep step) : base(routedEvent) =>
        Step = step;

    /// <summary>The step that was invoked.</summary>
    public ArcChecklistStep Step { get; }
}

/// <summary>Routed args for the reorderable list order change event.</summary>
public sealed class ArcReorderOrderChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcReorderOrderChangedRoutedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ArcReorderItem> items) : base(routedEvent) =>
        Items = items;

    /// <summary>The items in their new order.</summary>
    public IReadOnlyList<ArcReorderItem> Items { get; }
}

/// <summary>Routed args for the rich text editor text change event.</summary>
public sealed class ArcRichTextChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcRichTextChangedRoutedEventArgs(RoutedEvent routedEvent, ArcRichTextValue value) : base(routedEvent) =>
        Value = value;

    /// <summary>The new editor value.</summary>
    public ArcRichTextValue Value { get; }
}

/// <summary>Routed args for the onboarding completion change event.</summary>
public sealed class ArcChecklistCompletedChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcChecklistCompletedChangedRoutedEventArgs(RoutedEvent routedEvent, IReadOnlyList<string> completedIds) : base(routedEvent) =>
        CompletedIds = completedIds;

    /// <summary>Ids of the completed steps.</summary>
    public IReadOnlyList<string> CompletedIds { get; }
}

/// <summary>Routed args for the onboarding hidden change event.</summary>
public sealed class ArcChecklistHiddenChangedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcChecklistHiddenChangedRoutedEventArgs(RoutedEvent routedEvent, bool hidden) : base(routedEvent) => Hidden = hidden;

    /// <summary>True when the checklist is hidden.</summary>
    public bool Hidden { get; }
}

/// <summary>Routed args for the onboarding dismissed event.</summary>
public sealed class ArcChecklistDismissedRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args for a routed event.</summary>
    public ArcChecklistDismissedRoutedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }
}
