using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>
/// Dot grid: the pointer lens and its ripple field, drawn as one interactive surface. The demo seeds the
/// lens off-center so a still frame (and the screenshot harness, which runs reduced motion) still shows
/// the field reacting.
/// </summary>
public sealed class DotGridPage : ArcItemPage
{
    public override string ItemSlug => "dot-grid";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-lens", "variant-field", "variant-falloff"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A landing page hero that should feel alive without shipping a video.",
        "A background for an empty state, a sign-in panel or a launch countdown that invites a poke."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Behind dense text or data: the moving dots compete with reading.",
        "When a static dot pattern is enough, or when a second reactive background is already on screen."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The field is decoration. It exposes one focus stop named \"Dot grid\" with help text for the keys.",
        "Arrow keys move the lens (Shift moves it four times as far); Space or Enter sends a ripple from the lens.",
        "Does not convey information by motion alone, so the still frame loses nothing that matters."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Dots ride individual damped springs, so the field wobbles and settles rather than snapping.",
        "The lens peaks about a third of the way out and fades to nothing at its rim; ripples travel at 620 px/s and fade over 1.5 s, up to six at once.",
        "Reduced motion keeps one still frame: the lens rests at a fixed point and tints nearby dots, and clicks do not ripple."
    };

    public override string? ImplementationNotes =>
        "Drawn with DrawingContext instead of a canvas element: one dot field at a 22 px gap, coloured from " +
        "ArcBorderStrong and ArcAccent. Arc's reduced-motion branch tints dots without moving them; the gallery " +
        "still shows a static lens bulge so the effect reads in a screenshot.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var grid = new ArcDotGrid
        {
            Width = 960,
            Height = 400,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                grid,
                DemoUi.Caption("Move the pointer across the field. Click for a ripple. Focus the field and use the arrow keys and Space.")
            }
        };
    }
}
