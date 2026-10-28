using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Lightbox gallery: a masonry grid whose photos fly from their slot into a draggable viewer.</summary>
public sealed class LightboxGalleryPage : ArcItemPage
{
    public override string ItemSlug => "lightbox-gallery";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Photo collections people browse and inspect up close, such as trips, portfolios or listings.",
        "Mixed portrait and landscape sets that should keep their aspect ratios in a masonry grid."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A square grid with pinch density: use photo grid. One arbitrary piece of content: use dialog."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The grid is a labelled collection of photo buttons; Enter or Space opens the focused photo.",
        "In the viewer, Left and Right change photo, plus and minus zoom, 0 resets and Escape closes.",
        "The open photo is announced with its title and position, and the close control sits inside the stage."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Opening flies the photo from its grid slot to the stage on a spring with a constant corner radius; closing flies it back to the current photo's slot.",
        "Drag down to dismiss with resistance, swipe sideways with momentum, and zoom around the stage centre. Reduced motion skips the flight and keeps fades."
    };

    public override string? ImplementationNotes =>
        "Photos are procedural scenes, so width and height come from a fixed ratio table. The viewer is drawn inside " +
        "the control rather than a portalled dialog; swipe momentum uses the pointer's last horizontal velocity and " +
        "focus trapping is approximated by the single keyboard stop.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcLightboxGallery
    {
        Width = 980,
        Height = 438,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
