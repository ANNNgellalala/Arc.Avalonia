using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the black hole delete transition. Two conversations start selected: press Delete (or the row
/// selection, then Delete) to watch them spiral in, and Undo to spin them back out.
/// </summary>
public sealed class BlackHolePage : ArcItemPage
{
    public override string ItemSlug => "black-hole";

    public override bool DemoOnCanvas => true;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var hole = new ArcBlackHole
        {
            Title = "Inbox",
            Items = new List<ArcBlackHoleItem>
            {
                new() { Id = "1", Subject = "Weekly metrics digest", From = "Arc analytics", Time = "09:41" },
                new() { Id = "2", Subject = "Re: motion review notes", From = "Rowan Pike", Time = "09:12" },
                new() { Id = "3", Subject = "Invoice 2041 is ready", From = "Billing", Time = "08:50" },
                new() { Id = "4", Subject = "Deploy finished on staging", From = "Pipeline", Time = "08:30" },
                new() { Id = "5", Subject = "Old thread: pricing page copy", From = "Mina Okafor", Time = "08:02" },
                new() { Id = "6", Subject = "Your weekly calendar", From = "Calendar", Time = "07:58" }
            }
        };

        hole.Select("2", "5");
        return hole;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Deleting or archiving a handful of selected rows, cards or files, with an Undo right after.",
        "Emptying a trash, clearing notifications or dismissing a batch where the removal should feel final.",
        "A demo or changelog where the delete itself is worth showing once."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "High frequency deletes such as clearing a single chip; use a quick height collapse.",
        "Irreversible actions without Undo: the spectacle must not stand in for a confirmation."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Up and Down move between rows, Space toggles the selection, Delete runs the transition and Control Z undoes it.",
        "The effect is visual only: announce the result yourself, as the demo does with a polite status line and an Undo button.",
        "The delete control disables itself while a transition runs, so a double press cannot delete twice."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Rows fall along a spiral: points nearer the hole start earlier, so the inner edge falls faster and the row stretches into an arm.",
        "The hole opens with a slight overshoot, grows a little as it eats, then collapses and sends out one soft ring.",
        "Undo plays the same path backwards; reduced motion fades the rows out in 200 ms and fades them back in."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public black-hole documentation. Nothing is painted to a texture and there is no gravitational lensing shader: the rows are " +
        "drawn by hand, so the bend is a short vertical and horizontal displacement near the hole, the fall is a logarithmic spiral on one clock, and the " +
        "shockwave is a stroked ring. Only the visible list is animated, as the reference also recommends.";
}
