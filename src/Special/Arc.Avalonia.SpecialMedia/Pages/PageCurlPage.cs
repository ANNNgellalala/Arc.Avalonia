using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMedia.Controls;

namespace Arc.Avalonia.SpecialMedia.Pages;

/// <summary>Page curl: a small magazine read by dragging its corners, with a fold, lit crease and flick.</summary>
public sealed class PageCurlPage : ArcItemPage
{
    public override string ItemSlug => "page-curl";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Editorial and brand showcases where paper is part of the story.",
        "Menus, catalogs and yearbooks with a handful of spreads rather than long content."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Long content people search or select: use a scrolling page or a document viewer."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The book is one focus stop named with its visible pages; Right and Left (or PageDown and PageUp) turn the spread, Home and End jump to the covers.",
        "A polite status, drawn as the page count, reports the visible pages, and the previous and next buttons disable at the covers."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The fold line is the perpendicular bisector between the corner and the pointer, so the sheet follows from where it was grabbed; release past the middle or a flick turns the page.",
        "The lifted part shows a paper back with a lit crease and a shadow on the page below. Reduced motion swaps spreads without the curl."
    };

    public override string? ImplementationNotes =>
        "Avalonia has no CSS 3D transforms, so the fold is real 2D polygon geometry: the lifted part is clipped at the " +
        "perpendicular bisector and mirrored across it, with a gradient paper back and a crease. Spreads move two pages " +
        "at a time, so a single-corner drag reveals the matching page of the next spread rather than a single leaf.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcPageCurl
    {
        Width = 900,
        Height = 460,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
