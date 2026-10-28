using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIExplainers.Controls;

namespace Arc.Avalonia.SpecialAIExplainers.Pages;

/// <summary>Gallery page for the data flow illustration.</summary>
public sealed class DataFlowPage : ArcItemPage
{
    public override string ItemSlug => "data-flow";

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A hero or feature section explaining how data moves through a product.",
        "Docs pages introducing an ingestion, transform or routing concept.",
        "Integration pages showing which sources feed which destinations."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use the Sankey flow chart to show real volumes between stages.",
        "Use a pipeline status component for the live state of a job."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The diagram is one image described by its source, core and destination labels.",
        "Raw and processed records differ in shape as well as colour.",
        "Tab reaches the figure and Space pauses or resumes the flow."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Six records per 6.6 second loop leave as hollow dots, morph in the core and fan out.",
        "Records ease along each leg of a real cubic, and the core ring turns with the story.",
        "Reduced motion shows a still mid-story frame with a record on every leg."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public data-flow markdown and live page. Approximations: records follow a smoothed cubic parameter rather than a sampled arc-length table, and the source and destination marks come from the Arc icon set instead of brand logos.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcDataFlow
    {
        Height = 400,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
