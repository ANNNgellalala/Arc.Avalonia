using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Usage pricing: a calculator that recommends a plan as seats and traffic change. The plan table, seat rates
/// and included traffic are sample data; the trial button only sets local state, and the yearly toggle applies
/// the discount to the monthly figure.
/// </summary>
public sealed class UsagePricingPage : ArcItemPage
{
    public override string ItemSlug => "usage-pricing";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A pricing page for plans priced by seats plus usage.",
        "Calculators that should recommend a plan as people move the sliders."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Fixed tiers compared side by side; use plan-comparison."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Both sliders are native range inputs with labels, so arrows step seats by one and traffic by 100.",
        "The recommendation and price are real text and announced in a polite live region when the plan changes."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The price rolls to its new amount and the feature list reflows as the plan changes.",
        "Reduced motion drops the layout animation; values still update immediately."
    };

    public override string? ImplementationNotes =>
        "Plans, seat rates, included traffic and features are sample data. The reference's meter beside the plan card is omitted and the trial button only sets local state; no signup or card is collected.";

    private sealed record Plan(string Id, string Name, double Base, double SeatRate, double TrafficRate, int IncludedTraffic, string[] Features);

    private static readonly Plan[] Plans =
    {
        new("starter", "Starter", 0, 9, 0.02, 500,
            new[] { "1 workspace", "Community support", "7 day history" }),
        new("growth", "Growth", 49, 7, 0.015, 2000,
            new[] { "5 workspaces", "Email support", "90 day history", "Spend alerts" }),
        new("scale", "Scale", 199, 5, 0.01, 10000,
            new[] { "Unlimited workspaces", "Priority support", "2 year history", "SAML single sign-on", "Custom contracts" })
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var seats = 12.0;
        var traffic = 1200.0;
        var yearly = false;
        var status = Ck.Status();

        var planName = Ck.Text("Growth", "arc-subtitle");
        var planBadge = Ck.Pill("Recommended", ArcBadgeTone.Accent);
        var price = new ArcBillingPrice
        {
            Amount = 0,
            Currency = "$",
            Period = "/month",
            Decimals = 0
        };
        var wasPrice = Ck.Caption(string.Empty);
        var featureHost = new StackPanel { Spacing = 6 };
        var breakdownHost = new StackPanel { Spacing = 6 };
        var trialNote = Ck.Caption(string.Empty);

        Plan Recommend() => seats <= 5 && traffic <= 500
            ? Plans[0]
            : seats <= 20 && traffic <= 2000
                ? Plans[1]
                : Plans[2];

        void Rebuild()
        {
            var plan = Recommend();
            planName.Text = plan.Name;
            var monthly = plan.Base + seats * plan.SeatRate + Math.Max(0, traffic - plan.IncludedTraffic) * plan.TrafficRate;
            var effective = yearly ? monthly * 0.8 : monthly;
            price.Amount = Math.Round(effective);
            price.Was = yearly ? Math.Round(monthly) : 0;
            wasPrice.Text = yearly ? $"Billed yearly, {Ck.Money(Math.Round(monthly * 12 * 0.8))} a year" : "Billed monthly, cancel any time";
            trialNote.Text = $"14 days free on {plan.Name}, no card required.";

            featureHost.Children.Clear();
            foreach (var feature in plan.Features)
            {
                featureHost.Children.Add(Ck.Row(7,
                    new ArcIcon { Icon = "check", Size = 13, Foreground = Ck.Brush(ArcTokens.Success) },
                    Ck.Caption(feature)));
            }

            breakdownHost.Children.Clear();
            breakdownHost.Children.Add(Ck.Line($"{plan.Name} base", plan.Base == 0 ? "Included" : Ck.Money(plan.Base)));
            breakdownHost.Children.Add(Ck.Line($"{seats:0} seats × {Ck.Money(plan.SeatRate, 2)}", Ck.Money(seats * plan.SeatRate, 2)));
            breakdownHost.Children.Add(Ck.Line(
                traffic <= plan.IncludedTraffic ? $"Traffic, {plan.IncludedTraffic:N0} GB included" : $"{(traffic - plan.IncludedTraffic):N0} GB overage",
                traffic <= plan.IncludedTraffic ? "Included" : Ck.Money((traffic - plan.IncludedTraffic) * plan.TrafficRate, 2)));
            if (yearly)
            {
                breakdownHost.Children.Add(Ck.Line("Yearly discount", "−20%", accent: true));
            }

            Ck.Say(status, $"Recommended plan: {plan.Name}, {Ck.Money(Math.Round(effective))} a month.");
        }

        var seatSlider = new ArcSlider
        {
            Value = seats,
            Min = 1,
            Max = 50,
            Step = 1,
            LargeStep = 5,
            Label = "Seats",
            ShowValue = true
        };
        seatSlider.ValueChanged += (_, _) =>
        {
            seats = Math.Round(seatSlider.Value);
            Rebuild();
        };

        var trafficSlider = new ArcSlider
        {
            Value = traffic,
            Min = 100,
            Max = 3000,
            Step = 100,
            LargeStep = 500,
            Label = "Traffic, GB",
            ShowValue = true
        };
        trafficSlider.ValueChanged += (_, _) =>
        {
            traffic = Math.Round(trafficSlider.Value / 100) * 100;
            Rebuild();
        };

        var billing = new ArcBillingToggle
        {
            Value = "monthly",
            Label = "Billing period",
            Options = new List<ArcBillingToggleOption>
            {
                new("monthly", "Monthly"),
                new("yearly", "Yearly", "-20%")
            }
        };
        billing.ValueChanged += (_, _) =>
        {
            yearly = billing.Value == "yearly";
            Rebuild();
        };

        var controls = new StackPanel
        {
            Spacing = 18,
            Children =
            {
                Ck.Heading("Find your plan", 420, "arc-subtitle"),
                Ck.Body("Move the sliders to the seats and traffic you expect. The recommendation follows the usage you describe, not a fixed tier.", 420),
                seatSlider,
                trafficSlider,
                billing,
                Ck.Caption("Prices in USD. Usage above the included allowance is billed at the same rate."),
                Ck.Divider(),
                Ck.Stack(6,
                    Ck.Row(8, Ck.Label("Breakdown"), Ck.Caption("per month")),
                    breakdownHost),
                status
            }
        };

        var planCard = Ck.Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Ck.Row(8, planBadge, Ck.Caption("Best fit for your usage")),
                planName,
                price,
                wasPrice,
                Ck.Divider(),
                featureHost,
                Ck.Divider(),
                Ck.Btn("Start 14 day trial", ArcButtonVariant.Primary, ArcButtonSize.Md,
                    () => Ck.Say(status, $"Trial started on {Recommend().Name}. Sample state only."), trailingIcon: "arrow-right"),
                trialNote
            }
        }, 18);

        Rebuild();

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,28,420"),
            Children = { controls, Place(planCard, 2) }
        };

        return Ck.Specimen(stage, 20);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
