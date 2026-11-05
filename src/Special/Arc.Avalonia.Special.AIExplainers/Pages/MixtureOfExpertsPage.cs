using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the mixture of experts illustration.</summary>
public sealed class MixtureOfExpertsPage : ArcItemPage
{
    public override string ItemSlug => "mixture-of-experts";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-merged",
        "variant-input", "variant-router", "variant-experts", "variant-output", "variant-top-k", "variant-footer"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Explaining why a sparse mixture of experts model is cheap per token.",
        "Showing how a gate scores experts, picks the top k and blends their outputs.",
        "Teaching load balancing and expert capacity, where a full expert skips a token."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a model router to pick which model handles a whole request.",
        "Use speculative decoding to explain draft and verify speedups."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each expert row carries its load in words, and a full expert is marked, not just tinted.",
        "The Top 1 and Top 2 pills or the 1 and 2 keys change the routing, the readout and the load.",
        "Tab reaches the figure; Space pauses or resumes the sequence."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "One clock drives the token in, a packet to the router, the gate bars growing and the merge.",
        "Chosen expert rows light in first and second choice colours while their counters roll.",
        "Reduced motion lands each token in place and stops on the summary."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public mixture-of-experts markdown. Approximations: the sequence is eight sample tokens with hand tuned gate scores, the parameter readout is illustrative, and capacity dropping is computed over the whole sequence rather than tracked live.";

    public override bool DemoOnCanvas => false;

    // The figure needs the full 1068 px it was drawn for (merge point and output card sit past 960), so the
    // docs column scales it down with a Viewbox instead of clipping the output card.
    protected override Control BuildDemo(ArcDemoContext context) => new Viewbox
    {
        Child = new ArcMixtureOfExperts
        {
            Width = 1068,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
    };
}
