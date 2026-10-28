using System;
using Avalonia;
using Avalonia.Controls;

namespace Arc.Avalonia.Controls;

/// <summary>
/// Arc's card: a contained group of related content. Rests on a hairline border, never a shadow,
/// and uses the concentric surface radius (34) from the token set.
/// </summary>
public class ArcCard : ContentControl
{
    public static readonly StyledProperty<bool> InteractiveProperty =
        AvaloniaProperty.Register<ArcCard, bool>(nameof(Interactive));

    public static readonly StyledProperty<bool> MutedProperty =
        AvaloniaProperty.Register<ArcCard, bool>(nameof(Muted));

    static ArcCard()
    {
        InteractiveProperty.Changed.AddClassHandler<ArcCard>((card, _) => card.Classes.Set("interactive", card.Interactive));
        MutedProperty.Changed.AddClassHandler<ArcCard>((card, _) => card.Classes.Set("muted", card.Muted));
    }

    /// <summary>Adds a hover lift so the card reads as clickable.</summary>
    public bool Interactive
    {
        get => GetValue(InteractiveProperty);
        set => SetValue(InteractiveProperty, value);
    }

    /// <summary>Sits on a muted surface instead of a white one, for nested groups.</summary>
    public bool Muted
    {
        get => GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }
}
