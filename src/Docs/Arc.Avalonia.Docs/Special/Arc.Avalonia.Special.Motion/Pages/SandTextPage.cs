using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Motion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>Sand text: a headline of grains that pours into the letters and crumbles under the pointer.</summary>
public sealed class SandTextPage : ArcItemPage
{
    public override string ItemSlug => "sand-text";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-formed", "variant-grain-fine", "variant-grain-coarse"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A hero headline or section title that should be fun to touch.",
        "Short words that change on a selection, such as a product or theme name.",
        "Launch pages where one line of type can carry the interaction."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Body copy or long headings; use a text reveal or in-view title instead.",
        "Text that must stay readable while people work near it, such as form labels."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The real text stays available as the control's accessible name, so the headline is read as text.",
        "The grain canvas is decorative; digging never removes the word from the accessibility tree.",
        "Digging is pointer only, so keep the same message in a visible line of copy elsewhere on the page."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Letters fill bottom first like jars; the dust above the front falls along with the pour.",
        "Dug grains fly with the pointer velocity, tumble, and fade as the letters heal.",
        "Reduced motion shows the letters formed, turns digging off and swaps words instantly."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: grains are a deterministic cellular grid rasterised into one small bitmap and " +
        "scaled up without smoothing, because Avalonia has no CSS filters and one rectangle per grain would be " +
        "too slow. Digging uses the same seeded automaton; a real fluid pile is not simulated.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var sand = new ArcSandText
        {
            Text = "DUNES",
            Grain = 2.4,
            DigRadius = 20
        };

        var words = new[] { "DUNES", "ARC", "SAND" };
        var wordIndex = 0;

        var next = SmPage.Toggle("New word", () =>
        {
            wordIndex = (wordIndex + 1) % words.Length;
            sand.Text = words[wordIndex];
        });
        var again = SmPage.Toggle("Pour again", sand.Reveal);

        var stage = SmPage.Stage(sand, height: 320, background: ArcTokens.SurfaceMuted);

        // A second word at a coarser grain, so the grain-size variant is visible without changing the slider.
        var coarse = new ArcSandText
        {
            Text = "ARC",
            Grain = 4.2,
            DigRadius = 20
        };
        var coarseStage = SmPage.Stage(coarse, height: 220, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                coarseStage,
                SmPage.Hint(
                    "Hover or drag through the letters to dig; the grains tumble away and the letters heal as the pointer rests.",
                    again,
                    next)
            }
        };
    }
}
