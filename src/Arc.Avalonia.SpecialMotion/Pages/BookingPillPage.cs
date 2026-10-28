using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Booking pill: one pill reshaping through party, date, time and a ticket.</summary>
public sealed class BookingPillPage : ArcItemPage
{
    public override string ItemSlug => "booking-pill";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Venue or service pages where booking a table or slot is the one action people came for.",
        "Mobile-first flows that walk through party size, date, time and confirm in one surface."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Multi-field checkouts in a regular form; use a date picker and time picker.",
        "Longer flows with many fields; use a stepper or a multi-step form."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The date strip is a slider with a spoken value; times are a radio group with unavailable slots marked disabled.",
        "Arrow keys move a day or between free times, Page Up and Page Down move a week, Home and End jump to the ends.",
        "Enter confirms the step, Escape steps back, and every round icon button has a name such as Back or Fewer guests."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The pill's shape springs to fit each step while the content slides in a beat later in the direction of travel.",
        "The date strip follows drags 1:1, resists at the ends, and settles to the nearest day.",
        "Reduced motion jumps the shape, drops the slides and swaps faces with fades."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: the 24px backdrop blur is layered translucent brushes, and the pill is drawn " +
        "inside the demo surface rather than over a live page. Availability uses the documented example rule " +
        "(no tables after 20:00 for parties over six) so disabled times are visible in the demo. The pill is a " +
        "single focus stop with an internal step model.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var pill = new ArcBookingPill { Step = ArcBookingStep.Review };
        var party = SmPage.Toggle("Party", () => pill.Step = ArcBookingStep.Party);
        var date = SmPage.Toggle("Date", () => pill.Step = ArcBookingStep.Date);
        var ticket = SmPage.Toggle("Ticket", () => pill.Step = ArcBookingStep.Review);

        var stage = SmPage.Stage(pill, height: 420, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "The ticket is the review step: back up through time, date and party to watch the pill reshape. Escape steps back.",
                    party,
                    date,
                    ticket)
            }
        };
    }
}
