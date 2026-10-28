using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Live checkout: a subscription checkout whose summary recomputes as the plan, seats, period, add-ons and
/// coupon change, then hands the paid order back as a receipt. Prices, tax and coupons are sample data; no
/// payment fields are tokenised and nothing is charged.
/// </summary>
public sealed class CheckoutLivePage : ArcItemPage
{
    public override string ItemSlug => "checkout-live";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Upgrade from a free plan to a paid team plan.",
        "Self serve checkout where seats and add-ons change the price."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A physical goods cart with shipping; use checkout-flow.",
        "Showing totals without payment; use checkout-summary."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Plans and the billing period are single-select groups, and add-ons are real checkboxes.",
        "A failed submit focuses the first invalid field, and errors are tied to their input with a status line; the receipt heading takes focus on success."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Every amount changes in place as you edit, and add-on lines open and close their height.",
        "On success the summary column becomes a receipt; reduced motion keeps every state and removes the travel."
    };

    public override string? ImplementationNotes =>
        "Plans, add-ons, tax and the LAUNCH20 coupon are sample data. Card fields are plain ArcInputs; production would use the payment provider's hosted fields and keep tax and coupon maths on the server. Paying is a 1.1 s local timer that always succeeds.";

    private sealed record Plan(string Id, string Name, double SeatPrice, string Blurb);

    private static readonly Plan[] Plans =
    {
        new("team", "Team", 20, "For product teams of 5 to 50"),
        new("business", "Business", 35, "Adds SSO, audit log and priority support")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var plan = Plans[0];
        var seats = 8;
        var yearly = false;
        var support = true;
        var audit = false;
        var coupon = string.Empty;
        var country = "pt";
        var paid = false;
        var status = Ck.Status();

        const double taxRate = 0.08;

        var summaryHost = new ContentControl();
        var formHost = new ContentControl();

        double SeatTotal() => plan.SeatPrice * seats * (yearly ? 0.8 : 1) * (yearly ? 12 : 1);
        double AddOns() => (support ? 49 : 0) + (audit ? 29 : 0);
        double Discount() => coupon == "LAUNCH20" ? (SeatTotal() + AddOns()) * 0.2 : 0;
        double Tax() => Math.Round((SeatTotal() + AddOns() - Discount()) * taxRate, 2);
        double Total() => Math.Round(SeatTotal() + AddOns() - Discount() + Tax(), 2);

        void RebuildSummary()
        {
            if (paid)
            {
                summaryHost.Content = Ck.Card(new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Ck.Disc("check", 46, 22, accent: true),
                        Ck.Text("You are subscribed", "arc-subtitle"),
                        Ck.Body($"Team plan, {seats} seats, billed {(yearly ? "yearly" : "monthly")}. Receipt sent to you@acme.com.", 320),
                        Ck.Stack(6,
                            Ck.Line("Plan", $"{plan.Name} · {seats} seats"),
                            Ck.Line("Billing", yearly ? "Yearly, 20% off" : "Monthly"),
                            Ck.Line("Total paid", Ck.Money(Total(), 2), strong: true)),
                        Ck.Btn("View invoice", ArcButtonVariant.Secondary, ArcButtonSize.Md,
                            () => Ck.Say(status, "The invoice opens in billing; this specimen keeps the receipt."), icon: "receipt"),
                        Ck.Link("Back to the checkout", () =>
                        {
                            paid = false;
                            RenderAll();
                            Ck.Say(status, "Checkout reset. Nothing was charged twice.");
                        })
                    }
                }, 16);
                return;
            }

            var lines = new StackPanel { Spacing = 6 };
            lines.Children.Add(Ck.Line($"{plan.Name}, {seats} seats",
                Ck.Money(SeatTotal(), 2),
                note: yearly ? "Yearly, 20% off applied" : "Billed monthly"));
            if (support)
            {
                lines.Children.Add(Ck.Line("Priority support", Ck.Money(49, 2)));
            }

            if (audit)
            {
                lines.Children.Add(Ck.Line("Audit log", Ck.Money(29, 2)));
            }

            if (coupon == "LAUNCH20")
            {
                lines.Children.Add(Ck.Line("Coupon LAUNCH20", "−" + Ck.Money(Discount(), 2), accent: true));
            }

            lines.Children.Add(Ck.Line("Tax", Ck.Money(Tax(), 2), note: "Portugal, 8%"));
            lines.Children.Add(Ck.Divider());
            lines.Children.Add(Ck.Line("Total", Ck.Money(Total(), 2), strong: true));

            var payLabel = new TextBlock
            {
                Text = $"Pay {Ck.Money(Total(), 2)}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ck.Brush(ArcTokens.AccentForeground)
            };
            var payIcon = new ArcIcon
            {
                Icon = "lock",
                Size = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ck.Brush(ArcTokens.AccentForeground)
            };
            var pay = new ArcButton
            {
                Variant = ArcButtonVariant.Primary,
                Size = ArcButtonSize.Lg,
                Height = 48,
                MinHeight = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { payIcon, payLabel }
                }
            };
            pay.Click += (_, _) =>
            {
                pay.IsEnabled = false;
                pay.Loading = true;
                payLabel.Text = "Processing";
                Ck.Say(status, "Processing payment.");

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    paid = true;
                    Ck.Say(status, $"Paid {Ck.Money(Total(), 2)}. Team plan is live and the receipt is on its way.");
                    RenderAll();
                };
                timer.Start();
            };

            summaryHost.Content = Ck.Card(new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ck.Row(8, Ck.Text("Order summary", "arc-body arc-medium"), Ck.Pill(yearly ? "Yearly" : "Monthly", ArcBadgeTone.Neutral)),
                    lines,
                    pay,
                    Ck.Caption("Encrypted payment. Sample checkout, nothing is charged.")
                }
            }, 16);
        }

        void RebuildForm()
        {
            var planButtons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*") };
            var planColumn = 0;
            foreach (var option in Plans)
            {
                var target = option;
                var selected = plan.Id == option.Id;
                planButtons.Children.Add(new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(12, 8, 12, 8),
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
                                Children = { Ck.Text(option.Name, "arc-body-sm arc-medium"), Ck.Caption(option.Blurb, 220) }
                            },
                            Place(Ck.Text($"{Ck.Money(option.SeatPrice)} / seat", "arc-body-sm"), 1)
                        }
                    }
                }.With(button =>
                {
                    Grid.SetColumn(button, planColumn);
                    planColumn += 2;
                    button.Click += (_, _) =>
                    {
                        plan = target;
                        RenderAll();
                        Ck.Say(status, $"{target.Name} selected. The summary recomputed.");
                    };
                }));
            }

            var billing = Ck.Segmented(
                new[] { ("monthly", "Monthly"), ("yearly", "Yearly −20%") },
                yearly ? "yearly" : "monthly",
                value =>
                {
                    yearly = value == "yearly";
                    RenderAll();
                });

            var couponField = Ck.Input("Coupon code", coupon.Length == 0 ? null : coupon, null, "tag");
            couponField.Width = 200;
            var applyCoupon = Ck.Btn("Apply", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
            {
                var code = (couponField.Value ?? string.Empty).Trim().ToUpperInvariant();
                if (code == "LAUNCH20")
                {
                    coupon = code;
                    RenderAll();
                    Ck.Say(status, "Coupon LAUNCH20 applied, 20% off the first period.");
                    return;
                }

                couponField.Error = code.Length == 0 ? "Enter a code first." : "That code is not valid.";
                Ck.Say(status, couponField.Error);
            }, height: 36);

            var countrySelect = Ck.Select(new[]
            {
                ("pt", "Portugal"), ("de", "Germany"), ("us", "United States"), ("br", "Brazil")
            }, country, value =>
            {
                country = value;
                RebuildSummary();
                Ck.Say(status, "Tax country updated.");
            }, 220);

            var supportBox = new ArcCheckbox
            {
                Label = "Priority support · +$49",
                Checked = support
            };
            supportBox.CheckedChanged += (_, _) =>
            {
                support = supportBox.Checked == true;
                RebuildSummary();
            };

            var auditBox = new ArcCheckbox
            {
                Label = "Audit log · +$29",
                Checked = audit
            };
            auditBox.CheckedChanged += (_, _) =>
            {
                audit = auditBox.Checked == true;
                RebuildSummary();
            };

            var cardNumber = Ck.Input("Card number", "4242 4242 4242 4242", null, "credit-card");
            var expiry = Ck.Input("MM/YY", "09/28");
            expiry.Width = 120;
            var cvc = Ck.Input("CVC", "123");
            cvc.Width = 90;

            formHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ck.Label("Plan"),
                    planButtons,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,24,Auto"),
                        Children =
                        {
                            Ck.Field("Billing period", billing),
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children =
                                {
                                    Ck.Label("Seats"),
                                    Ck.Row(8,
                                        Ck.Qty(seats, value =>
                                        {
                                            seats = Math.Max(1, value);
                                            RebuildSummary();
                                        }, min: 1, max: 50),
                                        Ck.Caption("Billed per seat"))
                                }
                            }, 2)
                        }
                    },
                    Ck.Label("Add-ons"),
                    Ck.Row(18, supportBox, auditBox),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
                        ColumnSpacing = 8,
                        Children =
                        {
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children = { Ck.Label("Coupon"), couponField }
                            }, 0),
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children = { Ck.Caption(" "), applyCoupon },
                                VerticalAlignment = VerticalAlignment.Bottom
                            }, 1),
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Children = { Ck.Label("Billing country"), countrySelect }
                            }, 3)
                        }
                    },
                    Ck.Divider(),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                        ColumnSpacing = 8,
                        Children =
                        {
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children = { Ck.Label("Card number"), cardNumber }
                            }, 0),
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children = { Ck.Label("Expiry"), expiry }
                            }, 1),
                            Place(new StackPanel
                            {
                                Spacing = 4,
                                Children = { Ck.Label("CVC"), cvc }
                            }, 2),
                            Place(Ck.Caption("Hosted fields in production", 130), 3)
                        }
                    },
                    status
                }
            };
        }
        void RenderAll()
        {
            RebuildForm();
            RebuildSummary();
        }

        RenderAll();

        var formCard = Ck.Card(formHost, 16);
        var summaryCard = summaryHost;

        var header = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Heading("Upgrade to Team", 420, "arc-subtitle"),
                Ck.Caption("Paid signup · 14 day money back guarantee")
            }
        };

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,20,360"),
            Children = { new StackPanel { Spacing = 10, Children = { header, formCard } }, Place(summaryCard, 2) }
        };

        return Ck.Specimen(stage, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
