using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMedia.Controls;

namespace Arc.Avalonia.SpecialMedia.Pages;

/// <summary>Product gallery: the magnifier, thumbnail rail and colour/size variants on one product surface.</summary>
public sealed class ProductGalleryPage : ArcItemPage
{
    public override string ItemSlug => "product-gallery";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Product detail pages with several photos per colour and a size picker.",
        "Stores where shoppers need to zoom into fabric, texture or small print."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A photo grid that opens into a viewer with no variants: use lightbox gallery or photo grid."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The main photo is a button named with the photo's label that opens the zoom viewer; Escape closes it.",
        "Up and Down move between photo, thumbnails, colours, sizes and Add to bag; Left and Right move within a group and Enter activates.",
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The thumbnail highlight glides on a spring and the lens trails the pointer; a colour change crossfades every photo.",
        "The viewer grows out of the photo and returns to it on close. Reduced motion jumps the track, lens and zoom, and keeps the crossfade as a fade."
    };

    public override string? ImplementationNotes =>
        "Photos are procedural sample scenes re-tinted per colourway, so no image assets are needed. The fullscreen " +
        "viewer is drawn inside the control instead of a portalled dialog; focus trapping is approximated by the " +
        "control's single keyboard stop. Magnification is 2.4x, matching the documented default.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcProductGallery
    {
        Width = 840,
        Height = 452,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
