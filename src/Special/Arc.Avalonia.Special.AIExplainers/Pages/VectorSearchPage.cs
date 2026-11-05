using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the vector search illustration.</summary>
public sealed class VectorSearchPage : ArcItemPage
{
    public override string ItemSlug => "vector-search";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-ranked",
        "variant-plane", "variant-clusters", "variant-index-links", "variant-matches", "variant-ranked-list", "variant-stage-pills"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A feature section on semantic search, AI answers or retrieval over a team's docs.",
        "Docs pages introducing embeddings, nearest neighbour search or approximate indexes."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a search index diagram to explain keyword search with an inverted index.",
        "Use a real scatter plot to show an actual embedding projection."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The plane and its legend form one image described with the real query and matches.",
        "Matches are written out as a ranked list with titles, sources and scores, not colour alone.",
        "Tab reaches the figure; the arrow keys move between the four stage pills."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Documents fall and fold into points, then the index links draw and the clusters fade up.",
        "The ring widens on an eased curve and each match lights as the ring reaches it.",
        "Reduced motion shows the answer as one still frame: every point placed and ranked."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public vector-search markdown. Approximations: the plane is a skewed parallelogram rather than a true 3D projection, and the search walk is a widening ring rather than a hop-by-hop signal. Scores are derived from plane distance, so they always match the picture.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcVectorSearch
    {
        Height = 395,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
