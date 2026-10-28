using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Multi-step position indicator.</summary>
public sealed class StepsPage : ArcPage
{
    public override string Title => "Steps";

    public override string Group => "Feedback";

    public override string? Description =>
        "Where a person is in a strict multi-step flow, what is done, and what went wrong.";

    public override int Order => 302;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Stepper",
                "Use to show position in a strict multi-step flow, including an error step and a completed flow.",
                FeedbackDemos.Stepper())
        }
    };
}
