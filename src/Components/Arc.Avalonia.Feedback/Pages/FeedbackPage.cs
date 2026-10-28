using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Progress, loading placeholders, and empty states.</summary>
public sealed class FeedbackPage : ArcPage
{
    public override string Title => "Feedback";

    public override string Group => "Feedback";

    public override string? Description =>
        "How much of a known task is done, what is still loading, and what to do when there is nothing yet.";

    public override int Order => 300;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Progress",
                "Use for a known task where a percentage is meaningful; the fill and the counted number move together.",
                FeedbackDemos.Progress()),
            DemoUi.Section(
                "Skeleton",
                "Use while the shape of incoming content is known; match the layout instead of showing a spinner.",
                FeedbackDemos.Skeleton()),
            DemoUi.Section(
                "Empty state",
                "Use when there is nothing to show yet, and always pair it with the one next step that helps.",
                FeedbackDemos.EmptyStates())
        }
    };
}
