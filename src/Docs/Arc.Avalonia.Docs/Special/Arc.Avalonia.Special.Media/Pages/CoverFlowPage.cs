using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Cover flow: a draggable, throwable depth rail of images with grounded shadows and reflections.</summary>
public sealed class CoverFlowPage : ArcItemPage
{
    public override string ItemSlug => "cover-flow";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-front-card", "variant-depth-rail", "variant-reflection",
        "variant-position-ticks", "variant-caption"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Six to twelve visual items where looking at one at a time is the point, such as albums, places or collections.",
        "Showcase rails that should respond to drag, throw and sideways trackpad scroll."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A large library: use photo grid or lightbox gallery. General slides or content cards: use carousel."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The rail is one focus stop with a carousel roledescription; the front card is announced when it changes.",
        "ArrowLeft and ArrowRight step one card, Home and End jump to the ends. Position ticks are decorative."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A drag or throw lands on the card its momentum reaches; a quick flick always moves at least one card and the ends rubber-band.",
        "Cards scale, dim and turn toward the centre while the image drifts inside the frame; reduced motion jumps between cards and keeps short fades."
    };

    public override string? ImplementationNotes =>
        "CSS 3D transforms are not available in Avalonia, so the perspective is simulated: side cards compress " +
        "horizontally, rise slightly and lose contrast, and the reflection is a mirrored draw masked with a " +
        "gradient. Inertia is an under-damped spring on the fractional index.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcCoverFlow
    {
        Width = 980,
        Height = 440,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
