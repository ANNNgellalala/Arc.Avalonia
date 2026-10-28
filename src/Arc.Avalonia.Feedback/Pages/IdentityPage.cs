using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Avatars, metric cards, and quota meters.</summary>
public sealed class IdentityPage : ArcPage
{
    public override string Title => "Identity";

    public override string Group => "Feedback";

    public override string? Description =>
        "People, presence, a number with context, and an allowance against its limit.";

    public override int Order => 303;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Avatar",
                "Use for one person next to their name, record, or comment; photos fall back to initials.",
                FeedbackDemos.Avatars()),
            DemoUi.Section(
                "Avatar group",
                "Use to show a team or set of contributors in a small space, with an overflow count.",
                FeedbackDemos.AvatarGroup()),
            DemoUi.Section(
                "Metric card",
                "Use for a single number that needs context; the change chip states its direction in text too.",
                FeedbackDemos.MetricCards()),
            DemoUi.Section(
                "Usage meter",
                "Use for quota against a fixed allowance, with a caption such as 8.4k of 10k requests.",
                FeedbackDemos.UsageMeters())
        }
    };
}
