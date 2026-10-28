using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the reasoning tree illustration.</summary>
public sealed class ReasoningTreePage : ArcItemPage
{
    public override string ItemSlug => "reasoning-tree";

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Explainers that teach tree of thoughts, beam search or test time search.",
        "Showing why a wider search can beat a greedy one, with a control to try.",
        "A visual summary of how an agent weighed options before it chose a plan."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a live agent run to stream a real model's steps.",
        "Use speculative decoding for token level mechanics rather than whole thoughts."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every thought carries its score and its fate: kept, pruned, on the best path, or answer.",
        "A pruned thought fades and loses its edge; the answer has a check, so state is never colour alone.",
        "Tab reaches the figure; 1, 2 and 3 change the beam width and start the search over."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Thoughts fade in as their edges draw out of the parent, 20 ms apart per sibling.",
        "Scores fill as a bar with rolling digits; pruning fades to 40% and retracts the edge.",
        "Reduced motion shows the finished search with the winning path already drawn."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public reasoning-tree markdown. Approximations: the tree is a fixed sample with hand picked scores rather than arbitrary thought data, and pruning is drawn as a settled fade rather than retracting every edge. The beam width is a real control, and three beats two and one by finding a better answer.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcReasoningTree
    {
        Height = 400,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
