using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIExplainers.Controls;

namespace Arc.Avalonia.Special.AIExplainers.Pages;

/// <summary>Gallery page for the date reel.</summary>
public sealed class DateReelPage : ArcItemPage
{
    public override string ItemSlug => "date-reel";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-default",
        "variant-reels", "variant-lens", "variant-summary", "variant-presets", "variant-confirm"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Scheduling a send, reminder or post where a phone style wheel is familiar.",
        "Touch first flows where flicking a reel beats typing a date.",
        "Birthdays and dates far from today, where a wheel still works."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a date picker when people need to see a whole month.",
        "Use a time dial to compare times across cities."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each reel is a spin button named for its column, with the entry as its spoken value.",
        "Up and down turn the focused reel, left and right change reel, Enter confirms.",
        "Pointer focus draws no ring; keyboard focus rings the focused reel inside the lens."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Entries rise on a sine curve, compress with the cosine of their angle and fade at the edges.",
        "A flick glides on its measured velocity and snaps to the nearest entry.",
        "Reduced motion applies every change at once with no intro spin."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public date-reel markdown. Approximations: the drum is a drawn sine projection rather than a CSS 3D cylinder, date mode's month and year reels are folded into one day reel, and free text entry is limited to arrows, Page Up and Down, Home and End. The value is a local DateTime.";

    public override bool DemoOnCanvas => false;

    // The summary panel is drawn from 600 px outwards and needs the wider canvas, so scale the reel into the
    // 860 px docs column rather than truncating the quick picks.
    protected override Control BuildDemo(ArcDemoContext context) => new Viewbox
    {
        Child = new ArcDateReel
        {
            Width = 1000,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }
    };
}
