using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// CTA section: a dismissible launch banner above a centered closing section over a token wash. The banner is
/// the shared announcement bar, so dismissal, its status line and the reduced-motion branch come from the
/// component. Undo restores it, which is what people expect after an accidental dismiss.
/// </summary>
public sealed class CtaSectionPage : ArcItemPage
{
    public override string ItemSlug => "cta-section";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The closing section of a landing or pricing page.",
        "Pages that need one clear action after the argument is made.",
        "Announcing a launch at the top of the page without a modal."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use the hero signup when the action belongs at the top of the page.",
        "Use a dialog when the decision needs an interruption rather than a closing section."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The dismiss control is a labelled button, and the banner keeps its status line readable before it goes.",
        "The primary action is reachable first and is the only accent-filled control in the section.",
        "Undo returns the banner so a dismissal is never destructive."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The banner collapses out of the layout when dismissed; undo brings it back in place.",
        "The wash is still under reduced motion, and the section swaps without travel."
    };

    public override string? ImplementationNotes =>
        "The banner is the shared ArcAnnouncementBar, dismissed locally and restorable with Undo; nothing is persisted between sessions. The closing section is sample copy over the token wash. The reference's split variant beside a setup card is not reproduced in this specimen.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var banner = new ArcAnnouncementBar
        {
            Messages = new List<ArcAnnouncement>
            {
                new()
                {
                    Id = "launch",
                    Message = "Arc 3.0 is out. One timeline for specs, tasks and releases.",
                    Action = new ArcAnnouncementAction { Label = "Read the announcement" }
                }
            },
            Dismissible = true,
            Controls = false,
            AutoPlay = false,
            Label = "Product announcement"
        };

        var dismissed = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            IsVisible = false,
            Children =
            {
                Mk.Caption("Banner dismissed."),
                Mk.Btn("Undo", ArcButtonVariant.Ghost, ArcButtonSize.Sm)
            }
        };

        var undo = (ArcButton)dismissed.Children[1];

        banner.Dismissed += (_, _) =>
        {
            banner.IsVisible = false;
            dismissed.IsVisible = true;
        };

        undo.Click += (_, _) =>
        {
            banner.IsVisible = true;
            dismissed.IsVisible = false;
        };

        var title = Mk.Text("Start building today.", "arc-title-sm", 560);
        title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Center;

        var body = Mk.Body("Bring specs, tasks and releases onto one surface. Fourteen days, no card, and a workspace you can keep.", 460);
        body.TextAlignment = TextAlignment.Center;
        body.HorizontalAlignment = HorizontalAlignment.Center;

        var actions = Mk.Row(10,
            Mk.Btn("Start free", ArcButtonVariant.Primary, ArcButtonSize.Lg),
            Mk.Btn("Talk to sales", ArcButtonVariant.Secondary, ArcButtonSize.Lg));
        actions.HorizontalAlignment = HorizontalAlignment.Center;

        var meta = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Mk.Meta("check", "No card required"),
                Mk.Meta("users", "Free for 5 seats"),
                Mk.Meta("shield-check", "SOC 2 Type II")
            }
        };

        var closing = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16,
            Children = { title, body, actions, meta }
        };

        var section = Mk.Wash(new Border
        {
            Padding = new Thickness(32, 40, 32, 40),
            Child = closing
        }, intensity: 0.85);

        return Mk.Stack(12,
            banner,
            dismissed,
            section);
    }
}



