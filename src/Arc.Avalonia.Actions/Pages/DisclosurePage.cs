using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>Long running text, clamped with a soft fade and opened in place.</summary>
public sealed class DisclosurePage : ArcPage
{
    public override string Title => "Disclosure";

    public override string Group => "Actions";

    public override string? Description =>
        "Clamped text for reviews, descriptions and comments: the first lines are enough for most readers, " +
        "and the toggle only appears when the text really overflows.";

    public override int Order => 205;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            ActionDemos.Section(
                "Expandable text",
                "Three lines with a soft bottom fade; Read more opens the text to its full height and Show less closes it again.",
                new ArcExpandableText
                {
                    Lines = 3,
                    Background = DemoUi.Brush(ArcTokens.Background),
                    Text = Review
                }),
            ActionDemos.Section(
                "Open state",
                "The same text already open: the fade is gone, the toggle reads Show less, and the full answer stays in the layout.",
                new ArcExpandableText
                {
                    Lines = 3,
                    IsExpanded = true,
                    Background = DemoUi.Brush(ArcTokens.Background),
                    Text = Review
                }),
            ActionDemos.Section(
                "Only when it overflows",
                "Short text keeps its natural height and shows no toggle at all, so a one-line card never gains a stray link.",
                new ArcExpandableText
                {
                    Lines = 3,
                    Background = DemoUi.Brush(ArcTokens.Background),
                    Text = "Maya merged the checkout branch and requested a review on the pricing page."
                })
        }
    };

    private const string Review =
        "The release moves the checkout flow onto the new pricing service. Orders placed before the switch " +
        "keep their original totals, and the migration job reconciles anything that lands in flight. Support " +
        "has the runbook, and the rollback path is a single flag. Expect two short windows on Thursday and " +
        "Friday mornings, each under five minutes, with no visible change for customers beyond a faster " +
        "confirmation screen. After the second window the old service is read only for a week, so any " +
        "inconsistency can still be compared against the previous totals.";
}
