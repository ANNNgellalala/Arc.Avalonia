using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Stepper's completeness preview: horizontal with all details, vertical with an error step, the compact
/// complete form and a horizontal run that only describes the current step. Keys match Docs/stepper.json.
/// </summary>
public sealed class StepperPage : ArcItemPage
{
    public override string ItemSlug => "stepper";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "orientation-horizontal", "orientation-vertical", "details-all", "details-current",
        "state-error", "state-compact", "state-complete"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 26, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Labelled("横向 · 全部说明", new ArcStepper
        {
            Steps = CheckoutSteps(),
            Current = 1,
            Details = ArcStepperDetails.All,
            Label = "Checkout progress"
        }));

        panel.Children.Add(Labelled("纵向 · 错误步骤", new ArcStepper
        {
            Width = 440,
            Steps = new List<ArcStep>
            {
                new() { Id = "account", Label = "Account", Description = "Name, email, and password" },
                new() { Id = "workspace", Label = "Workspace", Description = "Team name and region" },
                new() { Id = "billing", Label = "Billing", Error = "No payment method on file" },
                new() { Id = "invite", Label = "Invite people", Description = "Optional" }
            },
            Current = 2,
            Orientation = ArcStepperOrientation.Vertical,
            Details = ArcStepperDetails.All,
            Label = "Account setup"
        }));

        panel.Children.Add(Labelled("横向 · 只说明当前步骤", new ArcStepper
        {
            Width = 720,
            Steps = CheckoutSteps(),
            Current = 1,
            Details = ArcStepperDetails.Current,
            Label = "Checkout progress"
        }));

        panel.Children.Add(Labelled("紧凑 · 已完成", new ArcStepper
        {
            Width = 620,
            Steps = CheckoutSteps(),
            Current = 4,
            Compact = true,
            CompleteLabel = "All steps complete"
        }));

        return panel;
    }

    private static List<ArcStep> CheckoutSteps() => new()
    {
        new() { Id = "cart", Label = "Cart" },
        new() { Id = "shipping", Label = "Shipping", Description = "Address and delivery" },
        new() { Id = "payment", Label = "Payment", Description = "Card and billing address" },
        new() { Id = "review", Label = "Review" }
    };

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
