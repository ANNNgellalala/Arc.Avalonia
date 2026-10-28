using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Billing overview: the billing settings page of a SaaS app. The plan card grows into a picker with a
/// prorated quote, followed by usage meters, the payment method that edits in place, and invoices. Every plan,
/// charge and card is sample data; nothing is charged or saved.
/// </summary>
public sealed class BillingOverviewPage : ArcItemPage
{
    public override string ItemSlug => "billing-overview";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The billing settings page of a SaaS app.",
        "Pages that show plan, usage, payment method and invoices together."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Public pricing pages; use plan-comparison or usage-pricing."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Plans in the picker are a single-select group; Escape closes the picker or cancels card editing.",
        "The prorated quote updates in a polite live region, and save and pay actions are announced through a status line."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The plan card grows into the picker inside a region whose height springs, and the selected plan highlight moves.",
        "Reduced motion makes the morph and the meter fills instant."
    };

    public override string? ImplementationNotes =>
        "Plans, usage, card and invoices are sample data. The quote prorates the days left in the period at the new seat rate, which is a simplified version of a provider's preview invoice. Card digits are masked and the form saves to local state only; no payment provider is called.";

    private sealed record Plan(string Id, string Name, double Price, string Detail, bool Available = true);

    private static readonly Plan[] Plans =
    {
        new("starter", "Starter", 19, "For one workspace"),
        new("growth", "Growth", 49, "For teams up to 20 seats"),
        new("scale", "Scale", 99, "SSO, audit log and priority support")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var plan = Plans[1];
        var yearly = false;
        var picker = false;
        var editing = false;
        var cardDigits = "4242";
        var cardExpiry = "09/28";
        var status = Ck.Status();

        var planHost = new ContentControl();
        var paymentHost = new ContentControl();
        var quoteText = Ck.Caption(string.Empty);

        double Seats() => 12;

        double Monthly() => (yearly ? plan.Price * 0.8 : plan.Price) * Seats();

        void BuildPlan()
        {
            if (!picker)
            {
                planHost.Content = Ck.Card(new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Ck.Row(8, Ck.Pill(plan.Name, ArcBadgeTone.Accent), Ck.Caption("Current plan")),
                        Ck.Row(8,
                            Ck.Text(yearly ? Ck.Money(plan.Price * 0.8) : Ck.Money(plan.Price), "arc-heading"),
                            Ck.Caption(yearly ? "/ seat / month, billed yearly" : "/ seat / month")),
                        Ck.Caption($"{Seats():0} seats · next invoice {Ck.Money(yearly ? Monthly() * 12 : Monthly())} on 1 November 2026"),
                        Ck.Row(8,
                            Ck.Btn("Change plan", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                            {
                                picker = true;
                                BuildPlan();
                                Ck.Say(status, "Plan picker opened.");
                            }),
                            Ck.Link("Cancel plan", () => Ck.Say(status, "Cancellation opens the cancel flow in the reference.")))
                    }
                }, 16);
                return;
            }

            var options = new StackPanel { Spacing = 6 };
            foreach (var option in Plans)
            {
                var target = option;
                var selected = plan.Id == option.Id;
                options.Children.Add(new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(12, 9, 12, 9),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    BorderBrush = selected ? Ck.Brush(ArcTokens.Accent) : Ck.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(selected ? 1.5 : 1),
                    Background = selected ? Ck.Brush(ArcTokens.ControlOnSubtle) : Ck.Brush(ArcTokens.SurfaceRaised),
                    CornerRadius = new CornerRadius(12),
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 1,
                                Children = { Ck.Text(option.Name, "arc-body-sm arc-medium"), Ck.Caption(option.Detail) }
                            },
                            Place(new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    Ck.Text(Ck.Money(yearly ? option.Price * 0.8 : option.Price) + " / seat", "arc-body-sm"),
                                    Ck.Caption(yearly ? "billed yearly" : "billed monthly")
                                }
                            }, 1)
                        }
                    }
                }.With(button => button.Click += (_, _) =>
                {
                    plan = target;
                    BuildPlan();
                    Ck.Say(status, $"{target.Name} selected. The quote updates before you confirm.");
                }));
            }

            var billing = Ck.Segmented(
                new[] { ("monthly", "Monthly"), ("yearly", "Yearly −20%") },
                yearly ? "yearly" : "monthly",
                value =>
                {
                    yearly = value == "yearly";
                    BuildPlan();
                });

            var prorated = Math.Round((yearly ? plan.Price * 0.8 : plan.Price) * Seats() * 0.55, 2);
            quoteText.Text = $"Prorated today: {Ck.Money(prorated, 2)} for the 17 days left in this period.";

            planHost.Content = Ck.Card(new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ck.Row(8, Ck.Text("Change plan", "arc-body arc-medium"), Ck.Caption("Cancel any time")),
                    billing,
                    options,
                    quoteText,
                    Ck.Row(8,
                        Ck.Btn("Confirm change", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            picker = false;
                            BuildPlan();
                            Ck.Say(status, $"Plan changed to {plan.Name}. The prorated amount is charged to the card ending {cardDigits}.");
                        }, icon: "check"),
                        Ck.Link("Keep current plan", () =>
                        {
                            picker = false;
                            BuildPlan();
                            Ck.Say(status, "Plan unchanged.");
                        }))
                }
            }, 16);
        }

        void BuildPayment()
        {
            if (!editing)
            {
                paymentHost.Content = Ck.Card(new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Ck.Row(8, Ck.Label("Payment method"), Ck.Pill("Default", ArcBadgeTone.Neutral)),
                        Ck.Row(10,
                            Ck.Mark("credit-card", 38, 18),
                            new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    Ck.Text($"Visa ending {cardDigits}", "arc-body-sm arc-medium"),
                                    Ck.Caption($"Expires {cardExpiry} · billing@acme.com")
                                }
                            }),
                        Ck.Row(8,
                            Ck.Link("Update card", () =>
                            {
                                editing = true;
                                BuildPayment();
                                Ck.Say(status, "Card form opened.");
                            }),
                            Ck.Caption("Encrypted and tokenised"))
                    }
                }, 16);
                return;
            }

            var number = Ck.Input("Card number", "4242 4242 4242 4242", null, "credit-card");
            var expiry = Ck.Input("MM/YY", cardExpiry);
            expiry.Width = 110;
            var cvc = Ck.Input("CVC", "123");
            cvc.Width = 90;

            paymentHost.Content = Ck.Card(new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Ck.Label("Update card"),
                    number,
                    Ck.Row(8, expiry, cvc),
                    Ck.Row(8,
                        Ck.Btn("Save card", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            var raw = (number.Value ?? string.Empty).Replace(" ", string.Empty);
                            cardDigits = raw.Length >= 4 ? raw[^4..] : cardDigits;
                            cardExpiry = expiry.Value ?? cardExpiry;
                            editing = false;
                            BuildPayment();
                            Ck.Say(status, $"Card ending {cardDigits} saved. Nothing was charged.");
                        }, icon: "lock"),
                        Ck.Link("Cancel", () =>
                        {
                            editing = false;
                            BuildPayment();
                            Ck.Say(status, "Card change cancelled.");
                        })),
                    Ck.Caption("Hosted card fields replace these inputs in production.")
                }
            }, 16);
        }

        Control Meter(string label, string detail, double used, double limit)
        {
            var fraction = used / limit;
            return new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Ck.Row(8, Ck.Text(label, "arc-body-sm"), Ck.Text(detail, "arc-caption")),
                    Ck.Progress(used, limit, fraction >= 0.8 ? ArcProgressTone.Warning : ArcProgressTone.Accent)
                }
            };
        }

        BuildPlan();
        BuildPayment();

        var usage = Ck.Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Ck.Row(8, Ck.Label("Usage this period"), Ck.Link("View report", () => Ck.Say(status, "The usage report opens in the reference."))),
                Meter("Seats", "12 of 20", 12, 20),
                Meter("API requests", "6.4M of 10M", 6.4, 10),
                Meter("Storage", "1.2TB of 2TB", 1.2, 2)
            }
        }, 16);

        var invoiceRows = new StackPanel { Spacing = 4 };
        foreach (var (number, period, amount, open) in new[]
                 {
                     ("INV-2410", "1–30 Sep", "$588.00", false),
                     ("INV-2409", "1–31 Aug", "$588.00", false),
                     ("INV-2408", "1–31 Jul", "$412.00", true)
                 })
        {
            var invoiceNumber = number;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10 };
            row.Children.Add(new StackPanel
            {
                Spacing = 0,
                Children = { Ck.Text(invoiceNumber, "arc-body-sm"), Ck.Caption(period + " 2026") }
            });
            row.Children.Add(Place(Ck.Text(amount, "arc-body-sm arc-medium"), 1));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            if (open)
            {
                actions.Children.Add(Ck.Link("Pay now", () => Ck.Say(status, $"Payment retried for {invoiceNumber}. Sample only.")));
            }

            actions.Children.Add(Ck.IconBtn("download", $"Download {invoiceNumber}",
                () => Ck.Say(status, $"{invoiceNumber} downloaded. Sample PDF."), 28));
            row.Children.Add(Place(actions, 2));
            invoiceRows.Children.Add(row);
        }

        var invoices = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Row(8, Ck.Label("Invoices"), Ck.Caption("Last three")),
                invoiceRows
            }
        }, 16);

        var contact = Ck.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ck.Label("Billing contact"),
                Ck.Text("billing@acme.com", "arc-body-sm"),
                Ck.Caption("Acme Inc · 22 Harbour Road, Porto · VAT PT123456789"),
                Ck.Link("Edit details", () => Ck.Say(status, "Billing details open a form in the reference."))
            }
        }, 16);

        var left = new StackPanel { Spacing = 8, Children = { planHost, usage, invoices } };
        var right = new StackPanel { Spacing = 8, Children = { paymentHost, contact } };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Heading("Billing", 380, "arc-subtitle"),
                Ck.Caption("Plan, usage, payment method and invoices")
            }
        });
        header.Children.Add(Place(Ck.Pill(yearly ? "Yearly" : "Monthly", ArcBadgeTone.Neutral), 1));

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,20,330"),
            Children = { left, Place(right, 2) }
        };

        return Ck.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, stage, status }
        }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
