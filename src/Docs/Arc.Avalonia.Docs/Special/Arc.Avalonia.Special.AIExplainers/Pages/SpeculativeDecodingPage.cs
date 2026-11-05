using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the speculative decoding illustration.</summary>
public sealed class SpeculativeDecodingPage : ArcItemPage
{
    public override string ItemSlug => "speculative-decoding";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-verifying",
        "variant-draft", "variant-target", "variant-answer", "variant-compare", "variant-draft-length", "variant-stats"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Explaining why a product answers faster: docs, model pages and latency write-ups.",
        "Teaching the trade off between draft length, acceptance and speedup.",
        "Replaying a recorded speculative run with acceptance and tokens per pass."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a streaming text component to show an answer arriving.",
        "Use a model router when the subject is choosing a model per request."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Accepted guesses show a check, the first miss is struck through, and the rest fade.",
        "The answer reads as words rather than tokens, and every stat has a spoken value.",
        "Tab reaches the figure; the 2, 4, 6 and 8 keys change the draft length."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Draft chips fade in one by one, then the target sweep fills each verify cell in order.",
        "Accepted tokens travel into the answer about 20 ms apart, so the line never reflows.",
        "Reduced motion places the tokens without travel and steps the sweep per column."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public speculative-decoding markdown. Approximations: the run is a fixed three round sample rather than a seeded simulation, and the draft length pills change the lane and the readout rather than replanning every acceptance. The compare line races the same clock at one token per pass.";

    public override bool DemoOnCanvas => false;

    // The draft-length pills reach 852 px; scale the figure into the 860 px docs column so nothing is clipped.
    protected override Control BuildDemo(ArcDemoContext context) => new Viewbox
    {
        Child = new ArcSpeculativeDecoding
        {
            Width = 900,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
    };
}
