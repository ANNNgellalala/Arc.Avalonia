using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Media.Pages;

/// <summary>Visual comparison and browsing: images, carousel slides and a review deck.</summary>
public sealed class MediaPage : ArcPage
{
    public override string Title => "Media";

    public override string Group => "Media";

    public override string? Description =>
        "Compare two versions of an image, browse a short row of peer items, and review a deck one card at a time.";

    public override int Order => 1000;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Image compare",
                "Use when two versions of the image need to be read against each other.",
                MediaDemos.ImageCompare()),
            DemoUi.Section(
                "Carousel",
                "Use for a short set of peer items where the neighbouring slides hint that there is more.",
                MediaDemos.Carousel()),
            DemoUi.Section(
                "Card stack",
                "Use for one-by-one triage where each decision should be reversible.",
                MediaDemos.CardStack())
        }
    };
}

