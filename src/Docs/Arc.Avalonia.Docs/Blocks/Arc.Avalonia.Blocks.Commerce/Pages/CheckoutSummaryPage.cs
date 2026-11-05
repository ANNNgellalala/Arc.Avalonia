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
/// Checkout summary: the last step before payment, with a product column, automatic adjustment lines, a promo
/// code that becomes a chip, discount and tax lines, and a pay button that moves from idle to processing to
/// paid. The payment is simulated; coupon rules are local to the page.
/// </summary>
public sealed class CheckoutSummaryPage : ArcItemPage
{
    public override string ItemSlug => "checkout-summary";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-two-column", "variant-product-card", "variant-price-lines", "variant-promo", "state-pay-idle"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The last step before a hosted checkout such as a Stripe session redirect.",
        "A single product purchase with a launch discount or a promo code."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A cart with several items and quantities; use cart-drawer."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter applies a promo code from the field; the applied chip's remove button returns focus to the field.",
        "Errors are tied to the field with an alert line, and the pay button announces Processing, Paid and Try again."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Adjustment and coupon lines open with a short fade and height change; reduced motion shows them instantly.",
        "The promo field swaps to the applied chip in place, and an invalid code sets the field error rather than shaking."
    };

    public override string? ImplementationNotes =>
        "The item, launch adjustment and LAUNCH10 coupon are sample data. Payment is a 900 ms local timer that always succeeds; there is no card form, hosted session or network call, and tax is shown as calculated at checkout rather than computed.";

    private const double Price = 199;
    private const double Launch = 50;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        string? coupon = null;
        var couponError = string.Empty;
        var paid = false;
        var status = Ck.Status();

        var discount = 0.0;
        double Total() => Math.Max(0, Price - Launch - discount);

        var couponHost = new ContentControl();
        var linesHost = new StackPanel { Spacing = 7 };
        var totalText = Ck.Text(Ck.Money(Total(), 2), "arc-heading");
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
        var payButton = new ArcButton
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

        void RebuildTotals()
        {
            discount = coupon is null ? 0 : Math.Round(Price * 0.1, 2);
            totalText.Text = Ck.Money(Total(), 2);
            payLabel.Text = paid ? "Paid" : $"Pay {Ck.Money(Total(), 2)}";

            linesHost.Children.Clear();
            linesHost.Children.Add(Ck.Line("Subtotal", Ck.Money(Price, 2)));
            linesHost.Children.Add(Ck.Line("Launch price", "−" + Ck.Money(Launch, 2), note: "38 of 50 left", accent: true));
            if (coupon is not null)
            {
                linesHost.Children.Add(Ck.Line("Promo " + coupon, "−" + Ck.Money(discount, 2), accent: true));
            }

            linesHost.Children.Add(Ck.Line("Tax", "Calculated at checkout"));
        }

        void BuildCoupon()
        {
            if (coupon is not null)
            {
                var chip = new Border
                {
                    Background = Ck.Tint(ArcTokens.Success, 0.12),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(10, 4, 5, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = Ck.Row(6,
                        new ArcIcon { Icon = "tag", Size = 13, Foreground = Ck.Brush(ArcTokens.Success) },
                        Ck.Text($"{coupon} applied", "arc-caption"),
                        Ck.IconBtn("x", $"Remove code {coupon}", () =>
                        {
                            coupon = null;
                            couponError = string.Empty;
                            BuildCoupon();
                            RebuildTotals();
                            Ck.Say(status, "Promo code removed.");
                        }, 22))
                };
                couponHost.Content = chip;
                return;
            }

            var field = Ck.Input("Promo code", null, null, null);
            field.Width = 180;
            var apply = Ck.Btn("Apply", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
            {
                var code = (field.Value ?? string.Empty).Trim().ToUpperInvariant();
                if (code == "LAUNCH10")
                {
                    coupon = code;
                    couponError = string.Empty;
                    BuildCoupon();
                    RebuildTotals();
                    Ck.Say(status, "LAUNCH10 applied, 10% off.");
                    return;
                }

                couponError = code.Length == 0 ? "Enter a code first." : "That code is not valid.";
                field.Error = couponError;
                Ck.Say(status, couponError);
            }, height: 36);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { field, apply } };
            var host = new StackPanel { Spacing = 5, Children = { row } };
            if (couponError.Length > 0)
            {
                host.Children.Add(Ck.Caption(couponError));
            }

            couponHost.Content = host;
        }

        payButton.Click += (_, _) =>
        {
            if (paid)
            {
                return;
            }

            payButton.IsEnabled = false;
            payButton.Loading = true;
            payLabel.Text = "Processing";
            Ck.Say(status, "Processing payment.");

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                paid = true;
                payButton.Loading = false;
                payButton.IsEnabled = true;
                payIcon.Icon = "check";
                payLabel.Text = "Paid";
                RebuildTotals();
                Ck.Say(status, $"Paid {Ck.Money(Total(), 2)}. Receipt sent to you@acme.com.");
            };
            timer.Start();
        };

        BuildCoupon();
        RebuildTotals();

        var itemTile = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Ck.Photo("layers", 190, 0, 44),
                Ck.Row(10,
                    Ck.Pill("One payment", ArcBadgeTone.Neutral),
                    Ck.Caption("Lifetime licence")),
                Ck.Text("Arc Pro", "arc-heading"),
                Ck.Body("Every component, block and special component, with the source and the design tokens.", 400),
                Ck.Divider(),
                Ck.RowWrap(16,
                    Ck.Meta("check", "All 300+ items"),
                    Ck.Meta("refresh-cw", "Free updates for a year"),
                    Ck.Meta("users", "Up to 5 seats"))
            }
        };

        var summary = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Ck.Row(10, Ck.Text("Order summary", "arc-body arc-medium"), Ck.Pill(paid ? "Paid" : "Secure", paid ? ArcBadgeTone.Success : ArcBadgeTone.Neutral)),
                new Border
                {
                    Background = Ck.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12),
                    Child = Ck.Row(12,
                        Ck.Mark("layers", 40, 18),
                        new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                Ck.Text("Arc Pro", "arc-body-sm arc-medium"),
                                Ck.Caption("Lifetime licence, one payment"),
                                Ck.Text(Ck.Money(Price, 2), "arc-caption")
                            }
                        })
                },
                linesHost,
                Ck.Divider(),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        Ck.Label("Total"),
                        Place(new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children = { totalText, Ck.Caption("USD") }
                        }, 1)
                    }
                },
                couponHost,
                payButton,
                Ck.Row(6,
                    new ArcIcon { Icon = "lock", Size = 12, Foreground = Ck.Brush(ArcTokens.TextMuted) },
                    Ck.Caption("Encrypted payment. Sample order, nothing is charged.")),
                status
            }
        };

        var card = Ck.Card(summary, 18);

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("460,28,*"),
            Children = { itemTile, Place(card, 2) }
        };

        return Ck.Specimen(stage, 20);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
