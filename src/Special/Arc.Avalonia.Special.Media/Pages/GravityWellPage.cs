using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Gravity well: a wall of tiles that bends toward the pointer and rings from a click.</summary>
public sealed class GravityWellPage : ArcItemPage
{
    public override string ItemSlug => "gravity-well";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-bend", "variant-wall", "variant-halo", "variant-depth"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A team, customer or moodboard wall where playfulness is the point.",
        "Landing page sections that show breadth: many logos, avatars or photos at once."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Grids people must scan or click precisely, like file pickers or large collections."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tiles keep their grid slot and only move with transforms, so reading order never changes.",
        "Tab focuses the wall, arrow keys move the well from tile to tile, Home and End jump, Space or Enter sends a shockwave."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The well follows the pointer on a critically damped spring; each tile follows on an under-damped spring, so a fast move leaves a jelly wake.",
        "Tiles deeper in the well shrink, dim and slide under their neighbours; a click sends a shockwave at 900 px/s."
    };

    public override string? ImplementationNotes =>
        "Tiles are in-code sample photography. Avalonia has no z-index-per-frame compositor, so paint order is " +
        "sorted by pull depth instead. Under reduced motion the wall rests at a fixed authored bend with no " +
        "pulse, which keeps the still frame legible.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcGravityWell
    {
        Width = 930,
        Height = 430,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
