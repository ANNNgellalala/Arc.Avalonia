using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMedia.Controls;

namespace Arc.Avalonia.SpecialMedia.Pages;

/// <summary>Sticker peel: die-cut stickers that fold, lift and stick back down on a surface.</summary>
public sealed class StickerPeelPage : ArcItemPage
{
    public override string ItemSlug => "sticker-peel";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A personal or team page where people arrange badges, achievements or brand stickers.",
        "A launch or reward moment that invites people to play with the product's marks."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Anything whose order carries meaning, or dense work screens: this is a delight surface, not an input."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each sticker is a focusable item with a label and a pressed state while carried; the surface is a labelled group.",
        "Left and Right move between stickers, Enter or Space picks one up or sticks it down, arrow keys move a carried sticker (Shift for 40 px) and Escape puts it back."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The peel is a real fold: the pulled part is mirrored across the fold line, showing a plain paper backing with one soft shade at the bend and a shadow that falls downward.",
        "Hovering lifts the nearest edge a few pixels; pulling past the middle or a quick tug peels the sticker off to carry. All springs are critically damped. Reduced motion skips the peel and hover lift."
    };

    public override string? ImplementationNotes =>
        "Sticker placements are fractions of the surface, so the arrangement scales with the container. The fold uses " +
        "polygon clipping and reflection because there is no 3D transform; the demo seeds a corner peel so the " +
        "fold is visible in a still frame, which is a deliberate approximation of a hover state.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcStickerPeel
    {
        Width = 900,
        Height = 438,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
