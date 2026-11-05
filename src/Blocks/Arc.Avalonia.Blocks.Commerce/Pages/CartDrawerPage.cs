using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Cart drawer: a small storefront grid with a slide-in cart. Adding an item raises the bag count, the cart
/// handles quantities, removal, a free shipping threshold and a promo code, and checkout folds into a
/// confirmation state. The drawer is a page-hosted <see cref="ArcDrawer"/> inside the specimen panel, so it
/// scrims the shop behind it instead of the whole gallery window. Checkout is simulated; nothing is charged.
/// </summary>
public sealed class CartDrawerPage : ArcItemPage
{
    public override string ItemSlug => "cart-drawer";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-drawer-open", "layout-storefront", "variant-cart-lines", "variant-checkout-footer"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Product listings where people add several items and review them in a side cart.",
        "Stores that show a free shipping threshold and promo codes before checkout."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A full multi-step checkout with address and payment; use a checkout flow."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The cart button names the item count and the drawer is a labelled dialog that focuses its close button.",
        "Quantity groups, removal and promo errors are labelled, and adds, removals and the placed order go to a polite live region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The drawer slides in on the shared ArcDrawer spring; reduced motion fades it instead.",
        "The reference's photo flight into the bag is approximated by the count and the drawer content changing together."
    };

    public override string? ImplementationNotes =>
        "Products and prices are sample data in the page. The reference flies the product photo into the cart and supports swipe to remove; the port raises the bag count, opens the drawer and uses explicit Remove buttons. Checkout waits 1.3 s and shows a local confirmation; no payment happens.";

    private sealed class CartLine
    {
        public CartLine(string name, string detail, double price, string icon, int quantity)
        {
            Name = name;
            Detail = detail;
            Price = price;
            Icon = icon;
            Quantity = quantity;
        }

        public string Name { get; }

        public string Detail { get; }

        public double Price { get; }

        public string Icon { get; }

        public int Quantity { get; set; }
    }

    private sealed record ShopItem(string Name, string Detail, double Price, string Icon, string HoverIcon);

    private static readonly ShopItem[] Shop =
    {
        new("Halo table lamp", "Brushed brass, dimmable", 189, "circle-dot", "zap"),
        new("Linen throw", "Stone washed, 130 × 180", 84, "layers", "wind"),
        new("Stoneware mug", "Set of two, matte glaze", 38, "circle", "droplet")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        const double freeShipping = 250;
        var lines = new List<CartLine>
        {
            new("Halo table lamp", "Brass · Medium", 189, "circle-dot", 1),
            new("Stoneware mug", "Mist · Set of two", 38, "circle", 1)
        };

        string? promo = null;
        var promoError = string.Empty;
        var status = Ck.Status();
        var bodyHost = new ContentControl();
        var footerHost = new ContentControl();
        var cartCount = new TextBlock { Text = "2", Classes = { "arc-caption" }, VerticalAlignment = VerticalAlignment.Center };
        var cartButton = Ck.Btn("Bag", ArcButtonVariant.Secondary, ArcButtonSize.Sm, null, "shopping-cart", height: 34);
        ((StackPanel)cartButton.Content!).Children.Add(new Border
        {
            Background = Ck.Tint(ArcTokens.Accent, 0.16),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(6, 0, 6, 0),
            Child = cartCount
        });

        var drawer = new ArcDrawer
        {
            Side = ArcDrawerSide.Right,
            Title = "Your bag (2)",
            Description = "Free returns within 30 days.",
            Content = bodyHost,
            Footer = footerHost,
            IsOpen = true
        };

        cartButton.Click += (_, _) =>
        {
            drawer.IsOpen = true;
            Ck.Say(status, "Bag opened.");
        };

        void Close()
        {
            drawer.IsOpen = false;
            Ck.Say(status, "Bag closed. Your items are kept.");
        }

        drawer.Closed += (_, _) => Ck.Say(status, "Bag closed. Your items are kept.");

        double Subtotal() => lines.Sum(line => line.Price * line.Quantity);

        double Discount() => promo is null ? 0 : Math.Round(Subtotal() * 0.1, 2);

        double Shipping() => Subtotal() >= freeShipping ? 0 : 9;

        double Total() => Subtotal() - Discount() + Shipping();

        void RenderCart()
        {
            var count = lines.Sum(line => line.Quantity);
            cartCount.Text = count.ToString();
            drawer.Title = $"Your bag ({count})";

            if (lines.Count == 0)
            {
                bodyHost.Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Ck.Disc("shopping-cart", 44, 20, accent: true),
                        Ck.Text("Your bag is empty", "arc-body arc-medium"),
                        Ck.Body("Add something from the shop and it will land here.", 300)
                    }
                };
                footerHost.Content = Ck.Btn("Browse the shop", ArcButtonVariant.Secondary, ArcButtonSize.Md, Close);
                return;
            }

            var rows = new StackPanel { Spacing = 10 };
            foreach (var line in lines.ToList())
            {
                var reference = line;
                var price = Ck.Text(Ck.Money(line.Price * line.Quantity), "arc-body-sm arc-medium");
                price.HorizontalAlignment = HorizontalAlignment.Right;

                var qty = Ck.Qty(line.Quantity, value =>
                {
                    reference.Quantity = Math.Max(1, value);
                    RenderCart();
                }, min: 1, max: 9);

                var remove = Ck.IconBtn("trash-2", $"Remove {line.Name}", () =>
                {
                    lines.Remove(reference);
                    Ck.Say(status, $"{reference.Name} removed from the bag.");
                    RenderCart();
                }, 28);

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
                row.Children.Add(Ck.Photo(line.Icon, 52, 44, 18));
                var middle = Place(new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Ck.Text(line.Name, "arc-body-sm arc-medium", 170),
                        Ck.Caption(line.Detail),
                        Ck.Row(6, qty, remove)
                    }
                }, 1);
                row.Children.Add(middle);
                row.Children.Add(Place(price, 2));
                rows.Children.Add(row);
            }

            var progress = Ck.Progress(Math.Min(Subtotal(), freeShipping), freeShipping, ArcProgressTone.Success);
            var remaining = Math.Max(0, freeShipping - Subtotal());
            var shippingNote = remaining <= 0
                ? Ck.Caption("Free shipping unlocked.")
                : Ck.Caption($"{Ck.Money(remaining)} away from free shipping.");

            var promoRow = new StackPanel { Spacing = 6 };
            if (promo is null)
            {
                var input = Ck.Input("Promo code", null, null, null);
                input.Width = 170;
                var apply = Ck.Btn("Apply", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                {
                    var code = (input.Value ?? string.Empty).Trim().ToUpperInvariant();
                    if (code == "WELCOME10")
                    {
                        promo = code;
                        promoError = string.Empty;
                        Ck.Say(status, "Promo code WELCOME10 applied, 10% off.");
                        RenderCart();
                        return;
                    }

                    promoError = code.Length == 0 ? "Enter a code first." : "That code is not valid.";
                    input.Error = promoError;
                    Ck.Say(status, promoError);
                }, height: 36);
                promoRow.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { input, apply }
                });
                if (promoError.Length > 0)
                {
                    promoRow.Children.Add(Ck.Caption(promoError));
                }
            }
            else
            {
                var chip = new Border
                {
                    Background = Ck.Tint(ArcTokens.Success, 0.12),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(10, 4, 6, 4),
                    Child = Ck.Row(6,
                        new ArcIcon { Icon = "tag", Size = 13, Foreground = Ck.Brush(ArcTokens.Success) },
                        Ck.Text($"{promo} · 10% off", "arc-caption"),
                        Ck.IconBtn("x", "Remove promo code", () =>
                        {
                            promo = null;
                            promoError = string.Empty;
                            RenderCart();
                        }, 22))
                };
                chip.HorizontalAlignment = HorizontalAlignment.Left;
                promoRow.Children.Add(chip);
            }

            var summary = Ck.Stack(8,
                Ck.Line("Subtotal", Ck.Money(Subtotal())),
                promo is null ? null! : Ck.Line("Discount", "−" + Ck.Money(Discount()), accent: true, note: promo),
                Ck.Line("Shipping", Shipping() == 0 ? "Free" : Ck.Money(Shipping())),
                Ck.Divider(),
                Ck.Line("Total", Ck.Money(Total()), strong: true));

            bodyHost.Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    rows,
                    new StackPanel { Spacing = 6, Children = { progress, shippingNote } },
                    promoRow,
                    summary
                }
            };

            footerHost.Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Ck.Btn($"Check out · {Ck.Money(Total())}", ArcButtonVariant.Primary, ArcButtonSize.Md,
                        () => PlaceOrder(), icon: "lock"),
                    Ck.Caption("Encrypted payment. Sample order, nothing is charged.")
                }
            };

        }

        void PlaceOrder()
        {
            var checkout = (ArcButton)((StackPanel)footerHost.Content!).Children[0];
            checkout.IsEnabled = false;
            checkout.Loading = true;
            Ck.Say(status, "Placing your order.");

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var total = Ck.Money(Total());
                var count = lines.Sum(line => line.Quantity);
                bodyHost.Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        Ck.Disc("check", 52, 24, accent: true),
                        Ck.Text("Order placed", "arc-heading"),
                        Ck.Body($"{count} items, {total}. A receipt is on its way to you@acme.com and your card ends in 4242.", 320),
                        Ck.Card(new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                Ck.Line("Order", "ARC-2481"),
                                Ck.Line("Delivery", "Thu 26 Oct, before 6 pm"),
                                Ck.Line("Total paid", total, strong: true)
                            }
                        }, 14)
                    }
                };
                footerHost.Content = Ck.Btn("Continue shopping", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                {
                    lines.Clear();
                    promo = null;
                    promoError = string.Empty;
                    drawer.IsOpen = false;
                    RenderCart();
                    Ck.Say(status, "Order ARC-2481 placed. Your bag is empty.");
                });
                Ck.Say(status, $"Order ARC-2481 placed for {total}.");
            };
            timer.Start();
        }

        RenderCart();

        var shopHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        shopHeader.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                Ck.Heading("Everyday carry", 420, "arc-subtitle"),
                Ck.Caption("Small things for the desk, the hall and the bag.")
            }
        });
        cartButton.HorizontalAlignment = HorizontalAlignment.Right;
        cartButton.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(cartButton, 1);
        shopHeader.Children.Add(cartButton);

        var cards = new List<Control>();
        foreach (var item in Shop)
        {
            var copy = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    Ck.Text(item.Name, "arc-body-sm arc-medium"),
                    Ck.Caption(item.Detail),
                    Ck.Row(8,
                        Ck.Text(Ck.Money(item.Price), "arc-body-sm arc-medium"),
                        Ck.Link("Add", () =>
                        {
                            var existing = lines.FirstOrDefault(line => line.Name == item.Name);
                            if (existing is null)
                            {
                                lines.Add(new CartLine(item.Name, "Brass · Medium", item.Price, item.Icon, 1));
                            }
                            else
                            {
                                existing.Quantity++;
                            }

                            drawer.IsOpen = true;
                            Ck.Say(status, $"{item.Name} added to the bag.");
                            RenderCart();
                        }))
                }
            };

            cards.Add(new StackPanel
            {
                Spacing = 8,
                Children = { Ck.HoverPhoto(item.Icon, item.HoverIcon, 150), copy }
            });
        }

        var shopBody = new StackPanel
        {
            Spacing = 18,
            Children =
            {
                shopHeader,
                Ck.Divider(),
                Ck.GridOf(3, 14, cards),
                Ck.Band(Ck.Row(10,
                    new ArcIcon { Icon = "refresh-cw", Size = 15, Foreground = Ck.Brush(ArcTokens.Accent) },
                    Ck.Caption("Free shipping over $250 · 30 day returns · carbon neutral delivery")), 12),
                status
            }
        };

        var shop = Ck.Specimen(shopBody);

        var stage = new Panel { Width = 1000, Height = 600, Children = { shop, drawer } };
        return stage;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
