using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Orbit logos: an integrations showcase with brand marks orbiting the product mark.</summary>
public sealed class OrbitLogosPage : ArcItemPage
{
    public override string ItemSlug => "orbit-logos";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An integrations or ecosystem hero on a marketing site.",
        "An integrations settings screen where people browse, read about and connect tools.",
        "A launch page showing everything a product plugs into."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A long list of customer logos; use a quiet logo marquee.",
        "Catalogues larger than about twenty integrations; use a searchable grid or table."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The marks are a labelled list of toggle buttons with a single roving tab stop; the orbit rests while focus is inside.",
        "Arrow keys step to the previous or next mark in the current category, Home and End jump to the ends.",
        "Enter or Space opens or closes a mark, Escape closes the card and returns focus to its mark.",
        "Connected status is written as text and a tick, never by colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "One frame loop writes positions; the rings turn once every 64 and 88 seconds at normal speed.",
        "The turn eases to rest whenever the pointer enters, a category is picked or a card is open, so nothing moves while you aim.",
        "Opening a mark springs the ring the short way and the card morphs out of the chip; reduced motion places everything at once."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: brand marks are drawn as neutral Lucide glyphs with sample names rather than " +
        "trademarked logo art, and the card appears beside the orbit instead of morphing out of the chip. The " +
        "rings keep their positions and draw an accent connector instead of springing the chosen mark to a focus spot. The marks share one focus stop that steps with the " +
        "arrow keys.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var orbit = new ArcOrbitLogos();
        var stage = SmPage.Stage(orbit, height: 420, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Pick a mark to read what it does and connect it. Chips filter the rings; the orbit rests while you aim.")
            }
        };
    }
}
