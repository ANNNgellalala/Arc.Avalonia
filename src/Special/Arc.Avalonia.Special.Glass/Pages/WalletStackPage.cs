using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Wallet stack catalog page: a fan of payment cards and the activity of the lifted one.</summary>
public sealed class WalletStackPage : ArcItemPage
{
    public override string ItemSlug => "wallet-stack";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A few payment cards, passes, or memberships where people open one to see its balance and activity.",
        "Fintech or wallet screens that want a physical card metaphor with realistic materials."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "With the stack closed, Down or Right brings the next card to the front, Up or Left the previous one; Home and End jump.",
        "Enter or Space lifts the front card into its activity, or puts the open card back; Escape puts it back too.",
        "Only the front card is in the tab order; it is named with the issuer, product, last four digits and balance.",
        "The activity region is inert until a card is open, and the header switches to an All cards button."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Every card's transform is one function of the stack position, the open amount and the fan, so springs, drags and interruptions all move the same shape.",
        "Hovering fans the stack upward; choosing a card lifts it on the smooth spring while the others tuck into the pocket.",
        "Reduced motion removes the fan, lift and light: the cards switch position and the activity appears in place."
    };

    public override string? ImplementationNotes =>
        "Materials are painted with gradients and a moving specular light rather than brushed metal textures, and the balance is shown in full instead of counting up. The device tilt light follows the pointer. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var wallet = new ArcWalletStack
        {
            Label = "Wallet",
            FrontIndex = 0,
            IsOpen = true,
            Cards = new List<ArcWalletCardData>
            {
                new()
                {
                    Id = "debit",
                    Issuer = "Northbank",
                    Product = "Everyday debit",
                    LastFour = "4821",
                    Holder = "Ada Lovelace",
                    Expires = "09/29",
                    Variant = "metal",
                    Balance = 2480.50,
                    BalanceLabel = "Available",
                    Transactions = new List<ArcWalletTransaction>
                    {
                        new() { Merchant = "Blue Bottle", Detail = "Coffee · Today", Amount = -6.50, Icon = "circle-dot" },
                        new() { Merchant = "Ana Ruiz", Detail = "Dinner split · Yesterday", Amount = 32.00, Avatar = "Ana Ruiz" },
                        new() { Merchant = "Meridian Studio", Detail = "Membership · 12 Oct", Amount = -48.00, Icon = "layers" }
                    }
                },
                new()
                {
                    Id = "travel",
                    Issuer = "Northbank",
                    Product = "Travel credit",
                    LastFour = "0917",
                    Holder = "Ada Lovelace",
                    Expires = "03/28",
                    Variant = "color",
                    Balance = 812.40,
                    BalanceLabel = "Spent this month",
                    Transactions = new List<ArcWalletTransaction>
                    {
                        new() { Merchant = "TAP Air", Detail = "Lisbon to Paris · 2 Oct", Amount = -184.20, Icon = "navigation" },
                        new() { Merchant = "Hotel do Mar", Detail = "Two nights · 2 Oct", Amount = -240.00, Icon = "pin" }
                    }
                },
                new()
                {
                    Id = "black",
                    Issuer = "Northbank",
                    Product = "Studio card",
                    LastFour = "3306",
                    Holder = "Ada Lovelace",
                    Expires = "11/30",
                    Variant = "black",
                    Balance = 12400.00,
                    BalanceLabel = "Available",
                    Transactions = new List<ArcWalletTransaction>
                    {
                        new() { Merchant = "Paper & Ink", Detail = "Print run · 8 Oct", Amount = -620.00, Icon = "file" },
                        new() { Merchant = "Client retainer", Detail = "Meridian · 1 Oct", Amount = 4200.00, Icon = "briefcase" }
                    }
                }
            }
        };
        wallet.HorizontalAlignment = HorizontalAlignment.Center;
        wallet.VerticalAlignment = VerticalAlignment.Center;

        var stage = GlassDemo.Stage(wallet, ArcGlassScene.Coast, 470, 16, seed: 8);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Hover to fan the stack; click a card, press Enter or use the arrow keys to lift one into its balance and recent activity.")
            }
        };
    }
}

