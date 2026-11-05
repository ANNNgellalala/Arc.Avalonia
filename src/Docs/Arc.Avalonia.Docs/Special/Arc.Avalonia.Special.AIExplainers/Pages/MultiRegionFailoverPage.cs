using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the multi-region failover illustration.</summary>
public sealed class MultiRegionFailoverPage : ArcItemPage
{
    public override string ItemSlug => "multi-region-failover";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-steady",
        "variant-balancer", "variant-platforms", "variant-policy", "variant-stats", "variant-stage-pills"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A reliability or infrastructure page that shows why two regions keep a service up.",
        "Docs for health checks, failover policies and traffic weights.",
        "Incident write-ups that explain how traffic moved during an outage."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a database replication diagram for a primary database failing over.",
        "Use a real status surface to report actual region health."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The number pills or the arrow keys jump between the six settled stages.",
        "Status never relies on colour: failed instances change their outline and every chip has words."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Requests are a deterministic stream, each routed by the balancer weight as it arrives.",
        "The weight bar eases between stages; reduced motion opens on the shift itself."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public markdown. Approximations: the stage is two skewed platforms rather than a full isometric scene, traffic is a fixed pool of fourteen dots, and the outage is a six stage simulation with sample numbers.";

    public override bool DemoOnCanvas => false;

    // The figure is drawn for a 1068 px gallery column; the docs preview is 860 px wide, so scale it to fit
    // rather than clipping the right edge of the regional platforms.
    protected override Control BuildDemo(ArcDemoContext context) => new Viewbox
    {
        Child = new ArcMultiRegionFailover
        {
            Width = 960,
            Height = 410,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
    };
}
