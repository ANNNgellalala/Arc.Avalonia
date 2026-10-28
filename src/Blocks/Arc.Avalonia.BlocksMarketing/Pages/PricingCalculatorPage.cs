using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Arc.Avalonia.BlocksMarketing.Controls;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Pricing calculator: a seat slider and a billing switch drive three plan cards whose prices, totals and
/// volume discount update live, with the recommended plan following the seat count. The trial action is
/// simulated. Volume pricing and seat caps are sample rules declared in the page.
/// </summary>
public sealed class PricingCalculatorPage : ArcItemPage
{
    public override string ItemSlug => "pricing-calculator";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Per-seat pricing where the exact total for a team size helps people choose.",
        "Pricing pages with a monthly and yearly switch and a visible yearly saving.",
        "Plans with seat caps or volume discounts that are hard to explain in a static table."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use the plan comparison when the differences between tiers are the argument.",
        "Use a plain billing toggle when the price does not depend on team size."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The slider is labelled Team size and reads its value in words, such as 20 seats.",
        "The billing switch is a segmented control: arrows move the selection and the value is announced.",
        "The chosen plan and the trial state are written in a polite status line, and each card is labelled by its heading."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Prices roll to the new amount and the Recommended badge moves to the plan the seat count now favours.",
        "Reduced motion jumps the numbers and moves the badge without travel."
    };

    public override string? ImplementationNotes =>
        "Seat stops, prices and the recommendation rule are sample data in the page. The trial is simulated and never leaves the page. The Recommended badge re-renders inside the favoured card rather than travelling between cards, because the port has no shared-layout animation; the number control rolls and always lands on the exact value.";

    private sealed record Plan(string Name, double PerSeat, int MaxSeats, string Blurb, string[] Features);

    private static readonly Plan[] Plans =
    {
        new("Starter", 0, 10, "For a team finding its rhythm.", new[] { "One project", "Community support" }),
        new("Team", 12, 100, "For teams shipping every week.", new[] { "Unlimited projects", "SSO and audit history" }),
        new("Business", 22, 500, "For organisations with controls.", new[] { "Custom data region", "Priority support" })
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var seats = 20.0;
        var yearly = false;
        var cardsHost = new ContentControl();
        var status = Mk.Caption(string.Empty);
        var total = Mk.Text(string.Empty, "arc-caption");
        var discount = Mk.Caption(string.Empty);

        void Rebuild()
        {
            var recommended = seats <= 10 ? 0 : seats <= 50 ? 1 : 2;
            var discountPerSeat = seats >= 100 ? 4 : seats >= 25 ? 2 : 0;
            var period = yearly ? 0.8 : 1;

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
            for (var index = 0; index < Plans.Length; index++)
            {
                var plan = Plans[index];
                var position = index;
                var perSeat = Math.Max(0, plan.PerSeat - (plan.PerSeat > 0 ? discountPerSeat : 0));
                var monthly = perSeat * seats * period;
                var overLimit = seats > plan.MaxSeats;

                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                head.Children.Add(Mk.Text(plan.Name, "arc-body arc-medium"));
                if (index == recommended)
                {
                    var badge = Mk.Pill("Recommended", ArcBadgeTone.Accent);
                    Grid.SetColumn(badge, 1);
                    head.Children.Add(badge);
                }

                var priceRow = Mk.Row(6,
                    new ArcStatNumber
                    {
                        Value = plan.PerSeat == 0 ? 0 : perSeat,
                        Prefix = "$",
                        FontSize = 30,
                        FontWeight = FontWeight.Medium
                    },
                    Mk.Caption(plan.PerSeat == 0 ? "free" : "per seat / month"));

                var totalLine = Mk.Text(
                    plan.PerSeat == 0
                        ? "No card required"
                        : $"${monthly:0} per month, {seats:0} seats",
                    "arc-caption");

                var features = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
                foreach (var feature in plan.Features)
                {
                    features.Children.Add(Mk.Row(6,
                        new ArcIcon { Icon = "check", Size = 12, Foreground = Mk.Brush(ArcTokens.Success) },
                        Mk.Caption(feature)));
                }

                if (overLimit)
                {
                    features.Children.Add(Mk.Caption($"Up to {plan.MaxSeats} seats on {plan.Name}"));
                }

                var choose = Mk.Btn(
                    $"Choose {plan.Name}",
                    index == recommended ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                    ArcButtonSize.Sm,
                    onClick: () =>
                    {
                        status.Text = $"{plan.Name} selected. The trial is simulated.";
                    });
                choose.IsEnabled = !overLimit;

                var body = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 8,
                    Children = { head, priceRow, totalLine, Mk.Divider(), features, choose }
                };

                var card = new Border
                {
                    Background = index == recommended ? Mk.Tint(ArcTokens.Accent, 0.05) : Mk.Brush(ArcTokens.Surface),
                    BorderBrush = index == recommended ? Mk.Tint(ArcTokens.Accent, 0.3) : Mk.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(14),
                    Child = body,
                    Margin = new Thickness(position == 0 ? 0 : 8, 0, 0, 0)
                };

                Grid.SetColumn(card, index);
                grid.Children.Add(card);
            }

            cardsHost.Content = grid;
            var recommendedPlan = Plans[recommended];
            total.Text = recommended == 0
                ? "Starter is free at this team size."
                : recommendedPlan.Name + " fits " + seats.ToString("0") + " seats best.";
            discount.Text = discountPerSeat > 0
                ? $"Volume discount applied: ${discountPerSeat} off each seat."
                : "Volume discounts start at 25 seats.";
        }

        var slider = new ArcSlider
        {
            Min = 1,
            Max = 120,
            Step = 1,
            Value = seats,
            ShowValue = true,
            Label = "Team size",
            Description = "Drag or use the arrow keys. Prices update as you go.",
            Format = value => $"{(int)value} seats"
        };
        slider.ValueChanged += (_, _) =>
        {
            seats = Math.Round(slider.Value);
            Rebuild();
        };

        var billing = Mk.Segmented(
            new[] { ("monthly", "Monthly"), ("yearly", "Yearly, save 20%") },
            "monthly",
            value =>
            {
                yearly = value == "yearly";
                Rebuild();
            },
            "Billing period");

        Rebuild();

        var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        controls.Children.Add(slider);
        billing.VerticalAlignment = VerticalAlignment.Center;
        billing.Margin = new Thickness(24, 0, 0, 0);
        Grid.SetColumn(billing, 1);
        controls.Children.Add(billing);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(Mk.Stack(2, discount, status));
        total.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(total, 1);
        footer.Children.Add(total);

        return Mk.Surface(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 14,
            Margin = new Thickness(24, 20, 24, 18),
            Children =
            {
                Mk.Stack(3,
                    Mk.Heading("Price it for your team", 460),
                    Mk.Caption("Sample pricing with volume discounts from 25 seats.")),
                controls,
                cardsHost,
                footer
            }
        });
    }
}

