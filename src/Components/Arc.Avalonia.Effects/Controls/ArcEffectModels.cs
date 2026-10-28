using System;
using System.Collections.Generic;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>The rendered element of a text effect, mapping Arc's `as` prop onto the type scale.</summary>
public enum ArcTextLevel
{
    Span,
    Paragraph,
    H1,
    H2,
    H3
}

/// <summary>What starts a text entrance: first paint, or the moment the text enters the viewport.</summary>
public enum ArcRevealTrigger
{
    Immediate,
    Visible
}

/// <summary>Whether a reveal staggers by word or by character.</summary>
public enum ArcTextUnit
{
    Word,
    Character
}

/// <summary>The five in-view title reveals documented by Arc.</summary>
public enum ArcInViewVariant
{
    Word,
    Line,
    Blur,
    Tracking,
    Wipe
}

public enum ArcAnnouncementTone
{
    Neutral,
    Inverted
}

public enum ArcHoverCardSide
{
    Bottom,
    Top,
    Left,
    Right
}

public enum ArcHoverCardAlign
{
    Start,
    Center,
    End
}

public enum ArcCookieConsentPosition
{
    Bottom,
    BottomLeft,
    BottomRight
}

public enum ArcCookieConsentReopenSide
{
    Left,
    Right
}

/// <summary>Which consent action produced a change.</summary>
public enum ArcConsentAction
{
    AcceptAll,
    RejectAll,
    Save,
    Reset
}

/// <summary>One rotating message in an <see cref="ArcAnnouncementBar"/>.</summary>
public sealed class ArcAnnouncement
{
    public string Id { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public ArcAnnouncementAction? Action { get; init; }

    public ArcAnnouncementCountdown? Countdown { get; init; }
}

/// <summary>The call to action of an announcement.</summary>
public sealed class ArcAnnouncementAction
{
    public string Label { get; init; } = string.Empty;

    /// <summary>Runs before the bar raises <see cref="ArcAnnouncementBar.ActionInvoked"/>.</summary>
    public Action? OnClick { get; init; }
}

/// <summary>A live countdown to the end of an announcement.</summary>
public sealed class ArcAnnouncementCountdown
{
    public DateTimeOffset To { get; init; }

    public string? Label { get; init; }
}

/// <summary>One consent group in an <see cref="ArcCookieConsent"/>.</summary>
public sealed class ArcCookieCategory
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Required categories are always on and show "Always on" instead of a toggle.</summary>
    public bool Required { get; init; }

    public bool DefaultEnabled { get; init; }
}

/// <summary>Copy overrides for <see cref="ArcCookieConsent"/>, mirroring Arc's `labels` prop.</summary>
public sealed class ArcCookieConsentLabels
{
    public string Message { get; init; } = "We use cookies to keep the site working and to understand how it is used.";

    public string Customize { get; init; } = "Customize";

    public string AcceptAll { get; init; } = "Accept all";

    public string RejectAll { get; init; } = "Reject all";

    public string Save { get; init; } = "Save choices";

    public string PreferencesTitle { get; init; } = "Cookie preferences";

    public string PreferencesDescription { get; init; } =
        "Choose which categories this site may use. Necessary cookies are always on.";

    public string Reopen { get; init; } = "Cookie settings";

    public string AlwaysOn { get; init; } = "Always on";

    public string Close { get; init; } = "Close";

    public static ArcCookieConsentLabels Default { get; } = new();
}

/// <summary>The link to the full cookie policy shown in the bar and the panel.</summary>
public sealed class ArcCookiePolicy
{
    public string Label { get; init; } = "Cookie policy";

    /// <summary>Runs when the policy link is pressed. The port never navigates anywhere by itself.</summary>
    public Action? OnClick { get; init; }
}

/// <summary>A saved consent choice: category id to allowed.</summary>
public sealed class ArcConsentValue
{
    public ArcConsentValue(IReadOnlyDictionary<string, bool> allowed) => Allowed = allowed;

    public IReadOnlyDictionary<string, bool> Allowed { get; }

    public bool IsAllowed(string id) => Allowed.TryGetValue(id, out var allowed) && allowed;
}

/// <summary>Raised when the visitor accepts, rejects, saves or resets consent.</summary>
public sealed class ArcConsentChangedEventArgs : EventArgs
{
    public ArcConsentChangedEventArgs(ArcConsentValue? value, ArcConsentAction action, DateTimeOffset updatedAt)
    {
        Value = value;
        Action = action;
        UpdatedAt = updatedAt;
    }

    /// <summary>Null after a reset, which means the visitor has not decided again.</summary>
    public ArcConsentValue? Value { get; }

    public ArcConsentAction Action { get; }

    public DateTimeOffset UpdatedAt { get; }
}

/// <summary>Arc's default consent categories.</summary>
public static class ArcDefaultCookieCategories
{
    public static IReadOnlyList<ArcCookieCategory> All { get; } = new[]
    {
        new ArcCookieCategory
        {
            Id = "necessary",
            Label = "Strictly necessary",
            Description = "Keeps sign-in, security and billing working.",
            Required = true,
            DefaultEnabled = true
        },
        new ArcCookieCategory
        {
            Id = "preferences",
            Label = "Preferences",
            Description = "Remembers language, theme and layout choices.",
            DefaultEnabled = true
        },
        new ArcCookieCategory
        {
            Id = "analytics",
            Label = "Analytics",
            Description = "Counts visits so pages can be made clearer.",
            DefaultEnabled = false
        },
        new ArcCookieCategory
        {
            Id = "marketing",
            Label = "Marketing",
            Description = "Measures campaigns on other sites.",
            DefaultEnabled = false
        }
    };
}

/// <summary>A person preview shown by an <see cref="ArcHoverCard"/> (Arc's HoverCardProfile).</summary>
public sealed class ArcHoverProfile
{
    public string Name { get; init; } = string.Empty;

    public string? Role { get; init; }

    public string? Bio { get; init; }

    public IReadOnlyList<ArcHoverStat> Stats { get; init; } = Array.Empty<ArcHoverStat>();

    /// <summary>Footer line such as location and local time.</summary>
    public string? Meta { get; init; }
}

/// <summary>One label/value pair in a hover profile.</summary>
public sealed class ArcHoverStat
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
