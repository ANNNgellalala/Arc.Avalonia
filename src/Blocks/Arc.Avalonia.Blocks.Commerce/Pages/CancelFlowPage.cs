using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Cancel flow: reason, a tailored retention offer, then a calm hold to cancel. Reasons, offers, price and the
/// resume date are sample data; accepting an offer or cancelling only changes local state, and the note is
/// never sent anywhere. The hold uses the shared ArcHoldToConfirm so a stray click cannot cancel.
/// </summary>
public sealed class CancelFlowPage : ArcItemPage
{
    public override string ItemSlug => "cancel-flow";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-reason-step", "variant-step-indicator", "variant-reasons"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Subscription cancellation in billing settings with a reason step and a retention offer.",
        "Flows that offer a discount, a pause or a downgrade before confirming the cancel."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A one step destructive action without offers; use dialog with hold to confirm."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Reasons are a single-select group of buttons with pressed state, and the steps announce their changes politely.",
        "The pause length is a labelled spinbutton whose value text includes the resume date; the final cancel needs a hold, not a click."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Steps swap in place and the selection highlight moves between reasons.",
        "Reduced motion swaps the slide for a fade while the offer still updates immediately."
    };

    public override string? ImplementationNotes =>
        "Reasons, offers, price and the resume date are sample data. Accepting an offer only records the choice in the status line; there is no discount, pause or cancellation API. The final step uses ArcHoldToConfirm, which supports pointer and keyboard holds.";

    private sealed record Reason(string Id, string Title, string Detail, string Icon, string OfferTitle, string OfferBody, string Action);

    private static readonly Reason[] Reasons =
    {
        new("price", "Too expensive", "The plan no longer fits the budget", "credit-card",
            "50% off for three months", "Keep every feature and pay $12 a month until January.", "Accept 50% off"),
        new("unused", "Not using it enough", "The workspace sits idle most weeks", "clock",
            "Pause for two months", "Keep your data and integrations, and pay nothing until 31 December.", "Pause my plan"),
        new("feature", "Missing a feature", "Something you need is not here yet", "sparkles",
            "A 20 minute walkthrough", "We will show the closest workaround and log the gap with the team.", "Book a walkthrough"),
        new("other", "Another reason", "Something else is going on", "circle-help",
            "Talk it through first", "Ten minutes with the team that builds Arc, before you decide.", "Talk to the team")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var step = 0;
        var reason = Reasons[0];
        var pauseMonths = 2;
        var accepted = false;
        var cancelled = false;
        var status = Ck.Status();

        var body = new ContentControl();
        var stepsHost = new ContentControl();
        var note = new ArcTextarea
        {
            Placeholder = "Anything we should know? Optional",
            MaxLength = 240,
            Height = 64
        };

        void Render()
        {
            stepsHost.Content = Ck.Steps(new[] { "Reason", "Offer", "Confirm" }, step);

            if (cancelled)
            {
                body.Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Ck.Disc("check", 44, 20, accent: true),
                        Ck.Text("Your plan is cancelled", "arc-subtitle"),
                        Ck.Body("Access stays on until 31 October, and your data is kept for 90 days. You can resume any time before then.", 420),
                        Ck.Row(8,
                            Ck.Btn("Resume my plan", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                            {
                                cancelled = false;
                                step = 0;
                                Render();
                                Ck.Say(status, "Plan resumed. Nothing was charged.");
                            }, icon: "refresh-cw"),
                            Ck.Link("Download my data", () => Ck.Say(status, "Export requested. Sample file only.")))
                    }
                };
                return;
            }

            if (step == 0)
            {
                var cards = new List<Control>();
                foreach (var option in Reasons)
                {
                    var choice = option;
                    var selected = reason.Id == option.Id;
                    var button = new ArcButton
                    {
                        Variant = ArcButtonVariant.Secondary,
                        Padding = new Thickness(12),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Top,
                        BorderBrush = selected ? Ck.Brush(ArcTokens.Accent) : Ck.Brush(ArcTokens.Border),
                        BorderThickness = new Thickness(selected ? 1.5 : 1),
                        Background = selected ? Ck.Brush(ArcTokens.ControlOnSubtle) : Ck.Brush(ArcTokens.SurfaceRaised),
                        CornerRadius = new CornerRadius(14),
                        Content = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                            ColumnSpacing = 10,
                            Children =
                            {
                                Ck.Disc(option.Icon, 30, 14),
                                Place(new StackPanel
                                {
                                    Spacing = 1,
                                    Children =
                                    {
                                        Ck.Text(option.Title, "arc-body-sm arc-medium"),
                                        Ck.Caption(option.Detail)
                                    }
                                }, 1),
                                Place(new ArcIcon
                                {
                                    Icon = selected ? "circle-check" : "circle",
                                    Size = 15,
                                    Foreground = selected ? Ck.Brush(ArcTokens.Accent) : Ck.Brush(ArcTokens.TextMuted)
                                }, 2)
                            }
                        }
                    };
                    button.Click += (_, _) =>
                    {
                        reason = choice;
                        Render();
                        Ck.Say(status, $"{choice.Title} selected. We will show an offer that fits.");
                    };
                    cards.Add(button);
                }

                body.Content = Ck.Stack(12,
                    Ck.Heading("Why are you leaving?", 460, "arc-subtitle"),
                    Ck.Body("One question, then a single offer that fits your answer. No discount maze.", 460),
                    Ck.GridOf(2, 10, cards),
                    Ck.Row(10,
                        Ck.Btn("Continue", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            step = 1;
                            Render();
                            Ck.Say(status, "Offer for " + reason.Title.ToLowerInvariant() + ".");
                        }, trailingIcon: "arrow-right"),
                        Ck.Link("Never mind, keep my plan", () => Ck.Say(status, "Nothing changed. Your plan stays as it is."))));
                return;
            }

            if (step == 1)
            {
                var offerCard = Ck.Band(new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Ck.Row(8, Ck.Pill("Offer", ArcBadgeTone.Accent), Ck.Caption(reason.Title)),
                        Ck.Text(reason.OfferTitle, "arc-heading"),
                        Ck.Body(reason.OfferBody, 460),
                        reason.Id == "unused"
                            ? PausePicker()
                            : new StackPanel
                            {
                                Spacing = 6,
                                Children =
                                {
                                    Ck.Line("Current plan", "$24 a month"),
                                    Ck.Line(reason.Id == "price" ? "With the offer" : "Next step", reason.Id == "price" ? "$12 a month for 3 months" : "A 20 minute call", strong: true)
                                }
                            },
                        Ck.Row(8,
                            Ck.Btn(reason.Action, ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                            {
                                accepted = true;
                                step = 2;
                                Render();
                                Ck.Say(status, $"{reason.Action} accepted. Nothing is charged in this preview.");
                            }, icon: "check"),
                            Ck.Link("Continue cancelling", () =>
                            {
                                step = 2;
                                Render();
                                Ck.Say(status, "Continuing to the cancellation confirmation.");
                            }))
                    }
                }, 16);

                body.Content = Ck.Stack(12,
                    Ck.Heading("Before you go", 460, "arc-subtitle"),
                    Ck.Body("The offer is tied to your answer, so it is short and specific. Declining is one click and always available.", 460),
                    offerCard);
                return;
            }

            if (accepted)
            {
                body.Content = Ck.Stack(12,
                    Ck.Heading("You are all set", 460, "arc-subtitle"),
                    Ck.Card(new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            Ck.Row(8, Ck.Pill("Applied", ArcBadgeTone.Success, true), Ck.Caption(reason.OfferTitle)),
                            Ck.Body(reason.Id == "unused"
                                ? $"Paused for {pauseMonths} months. Your data, integrations and invoices stay where they are."
                                : "The change is on the account from today. Nothing is charged in this preview.", 460),
                            Ck.Stack(6,
                                Ck.Line("Plan", reason.Id == "price" ? "Growth · $12 a month" : "Growth · $24 a month"),
                                Ck.Line("Next invoice", "1 November 2026")),
                            Ck.Btn("Back to billing", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                            {
                                accepted = false;
                                step = 0;
                                Render();
                                Ck.Say(status, "Offer cleared. Back to the first step.");
                            }, icon: "arrow-left")
                        }
                    }, 16));
                return;
            }

            var confirmCard = Ck.Card(new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Ck.Text("Cancel Growth?", "arc-heading"),
                    Ck.Body("Access runs to 31 October, then the workspace becomes read only for 90 days.", 460),
                    Ck.Stack(6,
                        Ck.Line("Plan", "Growth · $24 a month"),
                        Ck.Line("Access until", "31 October 2026"),
                        Ck.Line("Data", "Kept for 90 days")),
                    note,
                    new ArcHoldToConfirm
                    {
                        Label = "Hold to cancel subscription",
                        ConfirmedLabel = "Subscription cancelled",
                        Icon = "ban",
                        Tone = ArcHoldToConfirmTone.Danger,
                        HorizontalAlignment = HorizontalAlignment.Left
                    }.With(hold => hold.Confirmed += (_, _) =>
                    {
                        cancelled = true;
                        Render();
                        Ck.Say(status, "Subscription cancelled from the hold. Access runs to 31 October.");
                    }),
                    Ck.Row(8,
                        Ck.Link("Keep my plan", () =>
                        {
                            cancelled = false;
                            step = 1;
                            Render();
                            Ck.Say(status, "Cancellation stopped. Your plan continues.");
                        }),
                        Ck.Caption("Nothing is charged and the card stays on file."))
                }
            }, 16);

            body.Content = Ck.Stack(12,
                Ck.Heading("Cancel subscription", 460, "arc-subtitle"),
                confirmCard);
        }

        Control PausePicker()
        {
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            for (var months = 1; months <= 3; months++)
            {
                var value = months;
                var selected = value == pauseMonths;
                chips.Children.Add(Ck.Chip($"{value} month{(value == 1 ? string.Empty : "s")}", selected, () =>
                {
                    pauseMonths = value;
                    Render();
                    Ck.Say(status, $"Pause set to {value} months, resuming on the first of the month after.");
                }));
            }

            return new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    Ck.Label("Pause length"),
                    chips,
                    Ck.Caption("Resumes on 1 January 2027. Cancel or resume earlier at any time.")
                }
            };
        }

        Render();

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { stepsHost, body, status }
        }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
