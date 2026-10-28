using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the agent loop illustration.</summary>
public sealed class AgentLoopPage : ArcItemPage
{
    public override string ItemSlug => "agent-loop";

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Docs and landing pages that teach how an agent gathers, acts and verifies.",
        "Showing why agents take several passes around the loop.",
        "A visual summary of a finished run with the context it built and the tools it used."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a live agent run component to stream a real run's steps.",
        "Use a tool call card for one call inside a transcript."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Station names, context blocks and tool results are real text in the figure.",
        "A failed check shows a rotate icon with a retry note, so state is never colour alone.",
        "Tab reaches the figure; Space pauses or resumes the run."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The head eases between stations and drags a trail that shrinks as it lands.",
        "Context blocks arrive one per pass, and a tool chip drops out of the ring on a short pin.",
        "Reduced motion opens on the finished run with the ring drawn in."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public agent-loop markdown. Approximations: the run is a fixed three loop sample timeline, the tool marks are Arc icons rather than brand logos, and the per-beat status region is represented by the caption and the accessible description.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcAgentLoop
    {
        Height = 395,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
