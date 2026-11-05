using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Motion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>Morph nav: a navigation bar that becomes panels and search as one surface.</summary>
public sealed class MorphNavPage : ArcItemPage
{
    public override string ItemSlug => "morph-nav";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-panel-open", "variant-bar-items", "variant-panel-links",
        "variant-brand", "variant-action", "state-compact"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The top navigation of a marketing or product site with a few sections that each hold several destinations.",
        "Headers where search should grow out of the bar instead of opening a separate modal.",
        "Sites that pin one primary action, such as Sign up, to the end of the bar."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "An app-style switcher between peer sections; use a liquid tab bar.",
        "Switching views inside a page; use tabs.",
        "One menu of commands rather than site navigation; use a dropdown."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The bar is a nav landmark; the current item carries the current-page state and triggers expose their expanded state.",
        "Left and right arrows move between top level items, Down or Enter opens a panel, and focus moves into it.",
        "Search is a combobox over a list of results: Down and Up move, Enter opens, Escape closes and returns focus."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The surface springs to each panel size, with a little bounce when it grows and none when it shrinks.",
        "Moving to a neighbour slides the new panel in from that side while the highlight glides between items.",
        "Reduced motion drops travel, scale and blur and keeps short fades; compact retunes the bar height instead."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: the 22px backdrop blur becomes layered translucent surface brushes, and the " +
        "panel is hosted inside the stage rather than in a top-layer popup. Narrow containers fold to one menu in " +
        "the reference; this demo keeps the bar and relies on compact plus search for tighter widths. The drawn items share one focus stop, so the " +
        "control announces its name and keys rather than one automation peer per item.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var nav = new ArcMorphNav();
        var compact = SmPage.Toggle("Compact", () => nav.Compact = !nav.Compact);
        var search = SmPage.Toggle("Search", nav.OpenSearch);
        var stage = SmPage.Stage(nav, height: 380, background: ArcTokens.Background);

        // A second bar pinned to the compact height, so both bar heights are visible without pressing anything.
        var tight = new ArcMorphNav { Compact = true };
        var tightStage = SmPage.Stage(tight, height: 300, background: ArcTokens.Background);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                tightStage,
                SmPage.Hint(
                    "Open Product to see the panel, then press Search (or /). The bar is one surface through every state.",
                    search,
                    compact)
            }
        };
    }
}
