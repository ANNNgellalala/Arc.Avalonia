using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIExplainers.Controls;

namespace Arc.Avalonia.SpecialAIExplainers.Pages;

/// <summary>Gallery page for the payment flow illustration.</summary>
public sealed class PaymentFlowPage : ArcItemPage
{
    public override string ItemSlug => "payment-flow";

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A payments or checkout section that explains authorization and settlement.",
        "Docs pages introducing processors, card networks, issuers and payout timing."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a checkout block for a real payment form.",
        "Use a webhook fan-out to explain what happens after the payment event."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every hop writes a status line, so state never relies on colour alone.",
        "The approval chip carries a check and the word Approved, and every amount uses tabular figures.",
        "Tab reaches the figure and Space pauses or resumes the story."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The amount eases from hop to hop and slips under each node while it is processed.",
        "Approval repaints the return lane in success, then settlement takes the fee and the balance counts up.",
        "Reduced motion shows the moment just after the fee, with the net amount on its way."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public payment-flow markdown. Approximations: money uses a fixed en-US format rather than locale negotiation, and the four node marks come from the Arc icon set instead of brand logos.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcPaymentFlow
    {
        Height = 390,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
