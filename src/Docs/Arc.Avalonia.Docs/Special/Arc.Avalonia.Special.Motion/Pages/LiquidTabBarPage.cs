using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Motion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>Liquid tab bar: a lens that slides between tabs, swapping labels and filling icons.</summary>
public sealed class LiquidTabBarPage : ArcItemPage
{
    public override string ItemSlug => "liquid-tab-bar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-selected", "state-unselected", "variant-badge", "variant-lens", "variant-content"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Switching between a handful of peer sections that each have an icon, such as an app's main areas.",
        "Bottom or top bars on mobile-style layouts where the selection should feel physical.",
        "Tabs that carry a badge count for new items."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Text-only panels inside a page; use tabs instead.",
        "Compact filters or view toggles; use a segmented control.",
        "Site navigation with dropdown panels; use a morphing nav instead."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The bar is one tab list with a roving tabindex; the selected tab carries aria-selected.",
        "Left and right arrows select the previous or next tab and move focus, wrapping at the ends.",
        "Home and End select the first or last tab; the inverted copy inside the lens is decorative."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The lens slides and resizes as one shape on the morph spring, with no stretch or squash.",
        "The label being left fades out at once and the new one fades in as the lens arrives, so two never show.",
        "Reduced motion jumps the lens and labels to their places without travel or lean."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: Arc clips an inverted copy of the tabs to the lens; here the icon and label " +
        "inside the lens are redrawn in the accent foreground at a heavier stroke, which reads as the same fill " +
        "without rendering every tab twice. Drag momentum settles on the nearest tab rather than a thrown velocity. " +
        "The tabs share one focus stop that announces the selected section.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var bar = new ArcLiquidTabBar { SelectedIndex = 1 };
        var next = SmPage.Toggle("Next section", () => bar.SelectedIndex++);
        var first = SmPage.Toggle("Home", () => bar.SelectedIndex = 0);

        var stage = SmPage.Stage(bar, height: 340, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Click a tab or drag the lens. The label opens as the lens arrives and the badge keeps its place.",
                    next,
                    first)
            }
        };
    }
}
