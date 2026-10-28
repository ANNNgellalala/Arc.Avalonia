using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Slot text: a KPI value where every character is a reel that spins to the next value.</summary>
public sealed class SlotTextPage : ArcItemPage
{
    public override string ItemSlug => "slot-text";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Pricing toggles, KPI cards and dashboards where the figure changes on purpose.",
        "Launch moments, raffles and reveals where a value deserves a beat before it lands.",
        "Short values only: numbers, prices, percentages and status words."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Values that update several times a second; the spin gets tiring fast.",
        "Long sentences or paragraph copy, because a reel per character becomes noise."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The full value is real text on the control, so assistive technology reads the new value once.",
        "The reels are decorative; the value is announced politely after it settles.",
        "Whatever triggers the change (a toggle, a period switch) keeps its own keyboard focus and ring."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Digits count through the wheel in the direction the number moved; letters shuffle through the alphabet.",
        "Reels stop left to right with a fast start and a long, soft landing that never passes the target.",
        "Reduced motion swaps the value instantly, with no travel."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: CSS speed blur on a spinning reel becomes an edge alpha fade, and the reel " +
        "width follows the new glyph immediately rather than on a layout spring. The cadence is frame driven " +
        "and deterministic, so the screenshot harness always renders the landed value.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var value = new ArcSlotText
        {
            Value = "$12,480",
            FontSize = 64,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        meta.Children.Add(SmPage.Pill("+12.4%", ArcTokens.SuccessSubtle, ArcTokens.Success));
        meta.Children.Add(new TextBlock
        {
            Text = "vs previous period",
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        });

        var card = SmPage.Card(
            new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Revenue",
                        Classes = { "arc-caption" },
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    value,
                    meta
                }
            },
            padding: 26);
        card.Width = 460;

        var monthly = SmPage.Toggle("Monthly", () => Set("$12,480"), active: true);
        var yearly = SmPage.Toggle("Yearly", () => Set("$143,760"));
        var launch = SmPage.Toggle("Launch", () => Set("Live now"));

        void Set(string next)
        {
            value.Value = next;
        }

        var stage = SmPage.Stage(SmPage.Center(card), height: 280, background: ArcTokens.SurfaceMuted, padding: 24);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Every character is a reel: digits count toward the new figure and stop left to right. Try a period change.",
                    monthly,
                    yearly,
                    launch)
            }
        };
    }
}
