using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the prompt cache illustration.</summary>
public sealed class PromptCachePage : ArcItemPage
{
    public override string ItemSlug => "prompt-cache";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-partial-hit",
        "variant-requests", "variant-segments", "variant-cache-panel", "variant-savings", "variant-edit-pill"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A docs page or post that explains why a stable prefix is cheaper and faster.",
        "A pricing or usage page that shows how cache reads, writes and the TTL work.",
        "Developer onboarding where prompt order and cache breakpoints matter."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a context window component to show how full a model's window is.",
        "Use a real chart of cache hit rate for production analytics."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each request states its outcome, tokens read, tokens processed, cost and first token time.",
        "Cache write, hit and partial hit pair an icon with a label, never colour alone.",
        "Tab reaches the figure; E, or the edit pill, changes a segment for the next request."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Processing sweeps each segment once; a hit turns the prefix to its cached tone.",
        "The new part of the prompt travels into the cache panel and the TTL ring refills.",
        "Reduced motion opens on a finished round and changes values in place."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public prompt-cache markdown. Approximations: prices are fixed sample values in the header rather than a model price prop, and the simulated clock is compressed so three requests and the expiry fit one loop. Costs are still computed from the segment sizes shown.";

    public override bool DemoOnCanvas => false;

    // The cache panel starts at 644 px and needs the wider canvas it was drawn for, so scale it into the
    // 860 px docs column instead of letting the panel text overflow.
    protected override Control BuildDemo(ArcDemoContext context) => new Viewbox
    {
        Child = new ArcPromptCache
        {
            Width = 1000,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
    };
}
