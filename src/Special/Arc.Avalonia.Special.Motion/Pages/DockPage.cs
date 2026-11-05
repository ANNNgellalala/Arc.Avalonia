using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Motion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>Dock: a floating tool dock with a gliding label, a springy selection and trays.</summary>
public sealed class DockPage : ArcItemPage
{
    public override string ItemSlug => "dock";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-selected", "state-tray-open", "state-member",
        "variant-badge", "variant-label", "variant-chevron"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A floating tool or mode switcher, such as the tools of a canvas or whiteboard editor.",
        "Toolbars where related tools share one slot and open a tray, like shape variants.",
        "Tool sets people may reorder by drag, or with Alt and the arrow keys."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Two to four inline options; use a segmented control.",
        "One-off actions that do not stay selected; use an action button.",
        "Switching app sections rather than tools; use a tab bar."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The dock is a toolbar with a roving tabindex; plain items use a pressed state for the selection.",
        "Left and right arrows move along the dock, Home and End jump; Enter chooses an item or opens a group tray.",
        "Up opens a tray, Down and Escape close it, and Alt with the arrow keys reorders the focused item."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The floating label glides between items after a short first hover delay, then follows at once.",
        "The accent tint springs between items and a press dips the item before it releases with a little overshoot.",
        "A group grows a tab of the same material out of the dock with its members staggering in; reduced motion jumps."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: the label is shown for the hovered item and pinned to the selected one, because " +
        "a static screenshot has no pointer; the glow behind the dock is drawn as layered translucent rectangles " +
        "rather than a backdrop blur. Reordering swaps positions without a layout animation. Items share one focus " +
        "stop that announces the dock name and keys.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var dock = new ArcDock();
        var stage = SmPage.Stage(dock, height: 360, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Pick a tool, or open the Shapes tray and choose a variant. Alt with the arrow keys reorders the dock.")
            }
        };
    }
}
