using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Photo grid: column zoom levels, favourites and a viewer that grows from the tapped cell.</summary>
public sealed class PhotoGridPage : ArcItemPage
{
    public override string ItemSlug => "photo-grid";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A browsable photo library or album where people zoom between column counts.",
        "Galleries that need favourites and a fullscreen viewer with a filmstrip."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A masonry layout that keeps each photo's ratio: use lightbox gallery. A short slideshow: use carousel."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The grid is a labelled list of photo buttons; arrow keys move focus, PageUp and PageDown jump three rows, and Enter opens the focused photo.",
        "Plus and minus change the column count, which stands in for pinch on a desktop pointer; F favourites the open photo and Escape closes the viewer."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Pinch or the zoom buttons reflow the grid around the focused photo; the viewer grows from the tapped cell.",
        "Favourite hearts pop in with a short scale. Reduced motion removes the layout travel and keeps the fades."
    };

    public override string? ImplementationNotes =>
        "Photos have no intrinsic files, so each cell carries a fixed ratio for the viewer and the filmstrip. Pinch is " +
        "approximated with Ctrl+wheel and the plus and minus keys because a desktop build has no touch scale gesture; " +
        "the grid scrolls with the wheel only once a denser zoom level makes it taller than the viewport.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcPhotoGrid
    {
        Width = 940,
        Height = 450,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
