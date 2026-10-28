using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Countdowns and rolling totals.</summary>
public sealed class TimePage : ArcPage
{
    public override string Title => "Time";

    public override string Group => "Feedback";

    public override string? Description =>
        "Time until a fixed moment, and totals that roll when the value changes.";

    public override int Order => 307;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Countdown",
                "Use for a fixed moment people wait for; at zero the digits give way to the live state.",
                FeedbackDemos.Countdown()),
            DemoUi.Section(
                "Animated counter",
                "Use for standalone totals that change; keep metric cards for numbers that need a card around them.",
                FeedbackDemos.Counters())
        }
    };
}
