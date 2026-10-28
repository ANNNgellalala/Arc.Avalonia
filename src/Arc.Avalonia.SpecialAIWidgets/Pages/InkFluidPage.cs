using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Catalog page for ink fluid. The demo is a hero surface with copy and two actions over the ink; moving the
/// pointer across it stirs the field, and the still frame under reduced motion is a warmed up composition.
/// </summary>
public sealed class InkFluidPage : ArcItemPage
{
    public override string ItemSlug => "ink-fluid";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcInkFluid
    {
        Height = 342,
        Title = "Ship interfaces that feel alive",
        Subtitle = "A live ink wash behind the hero, stirred by the pointer and calm when left alone. It follows the theme and the accent, and it stops still under reduced motion.",
        PrimaryLabel = "Start building",
        SecondaryLabel = "See the components"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A landing hero or sign in screen should feel alive without a video.",
        "An empty state or waiting screen rewards curiosity with a gentle, interactive background.",

    };


    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The surface is decorative: it is hidden from assistive technology and content above it stays fully interactive.",
        "Ink is capped at a low opacity so text over it keeps its contrast in both themes.",
        "With reduced motion the field is drawn once as a still frame and the pointer no longer stirs it."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The pointer stirs ink tinted with the accent; the tinted share hands itself to neutral as it spreads, so fresh strokes carry colour and old ones go grey.",
        "Ink fades out over a couple of seconds and the field keeps a slow ambient drift while nobody interacts.",
        "Reduced motion keeps one composed frame instead of an animation loop."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public ink-fluid documentation without the original GPU solver: this is a seeded particle and curl-noise composition with " +
        "smoothed trails, so the motion reads as swirling ink but it is not a solved velocity and pressure grid. There is no backdrop blur; the ink sits on " +
        "the surface's faint gradient, tinted with the accent token and the foreground token only.";
}
