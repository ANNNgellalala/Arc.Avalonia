using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIExplainers.Controls;

namespace Arc.Avalonia.SpecialAIExplainers.Pages;

/// <summary>Gallery page for the time dial.</summary>
public sealed class TimeDialPage : ArcItemPage
{
    public override string ItemSlug => "time-dial";

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Planning a moment across two to five cities: calls, releases or handovers.",
        "Distributed teams that need to see at a glance who is inside working hours."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a time picker for a single local time.",
        "Use a calendar when the day matters more than the hour."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The dial is a focusable slider with a spoken value in the home city's time.",
        "Arrow keys step a quarter hour, Page Up an hour, Home and End the ends of the day.",
        "Each city row is a button that sets the home city, and the readout names who is at work."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A drag turns the dial with soft quarter hour detents and a light hover tick.",
        "The centre digits read as a clock face rather than rolling like an odometer.",
        "Reduced motion removes the hover tick and applies a step at once."
    };

    public override string? ImplementationNotes =>
        "Recreated independently from Arc's public time-dial markdown. Approximations: offsets are fixed minutes rather than a time zone database, so pass daylight saving aware offsets in a real app, and the dial is dragged with a mouse rather than flicked with touch momentum. The value is UTC minutes from midnight, as the documented API describes.";

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context) => new ArcTimeDial
    {
        Height = 400,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
