using System;
using Avalonia;
using Avalonia.Controls;

namespace Arc.Avalonia.Controls;

public enum ArcBadgeTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>A small label for status, category, or metadata. Always paired with a short word.</summary>
public class ArcBadge : ContentControl
{
    public static readonly StyledProperty<ArcBadgeTone> ToneProperty =
        AvaloniaProperty.Register<ArcBadge, ArcBadgeTone>(nameof(Tone), ArcBadgeTone.Neutral);

    public static readonly StyledProperty<bool> DotProperty =
        AvaloniaProperty.Register<ArcBadge, bool>(nameof(Dot));

    static ArcBadge()
    {
        ToneProperty.Changed.AddClassHandler<ArcBadge>((badge, _) => badge.UpdateClasses());
        DotProperty.Changed.AddClassHandler<ArcBadge>((badge, _) => badge.Classes.Set("dot", badge.Dot));
    }

    public ArcBadge()
    {
        UpdateClasses();
    }

    public ArcBadgeTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Shows a leading status dot.</summary>
    public bool Dot
    {
        get => GetValue(DotProperty);
        set => SetValue(DotProperty, value);
    }

    private void UpdateClasses()
    {
        Classes.Set("neutral", Tone == ArcBadgeTone.Neutral);
        Classes.Set("accent", Tone == ArcBadgeTone.Accent);
        Classes.Set("success", Tone == ArcBadgeTone.Success);
        Classes.Set("warning", Tone == ArcBadgeTone.Warning);
        Classes.Set("danger", Tone == ArcBadgeTone.Danger);
    }
}
