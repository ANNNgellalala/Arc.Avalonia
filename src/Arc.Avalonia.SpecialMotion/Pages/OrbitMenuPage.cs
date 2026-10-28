using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Orbit menu: hold a round button and its actions orbit out, picked by a drag.</summary>
public sealed class OrbitMenuPage : ArcItemPage
{
    public override string ItemSlug => "orbit-menu";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Three to six quick actions on one object, such as a note, a photo or a row.",
        "Touch-first surfaces where press, drag and release should fire an action in one gesture."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Longer or text-heavy lists; use a dropdown or context menu.",
        "Tools that stay selected; use a dock.",
        "A single action; use an action button."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The trigger is a menu button with an expanded state; each action is a menu item named by its label.",
        "Arrow keys open the arc and move around it, Home and End jump, and Enter or Space fires the focused action.",
        "Escape closes the arc and returns focus to the trigger; confirmations such as Pinned are announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Actions swirl out along the arc in reading order and fold back in reverse; a lens marks the target.",
        "The trigger glyph morphs between ellipsis, close and check, and the label pill springs to fit its text.",
        "Reduced motion drops the swirl and the magnet pull, leaving short fades and instant positions."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: backdrop-blurred materials become layered translucent brushes, and the pointer " +
        "magnet lean is dropped. Targeting still goes by angle with a 34px dead zone, and releasing off the arc " +
        "folds it away without firing, so the pie menu behaves as documented. The actions share one focus stop that " +
        "moves around the arc with the arrow keys.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var menu = new ArcOrbitMenu();
        var stage = SmPage.Stage(menu, height: 380, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Press and drag toward an action and release, or tap the button and click one. Notice the ellipsis becomes a close.")
            }
        };
    }
}
