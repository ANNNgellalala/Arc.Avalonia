using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Onboarding checklist's completeness preview: a checklist in progress with an expanded step, and the
/// completed checklist with its DoneTitle and ring check. Declared keys match Docs/onboarding-checklist.json.
/// </summary>
public sealed class OnboardingChecklistPage : ArcItemPage
{
    public override string ItemSlug => "onboarding-checklist";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-in-progress", "variant-expanded", "state-complete"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var inProgress = new ArcOnboardingChecklist
        {
            Width = 660,
            Title = "Set up your workspace",
            DoneTitle = "Workspace ready",
            DefaultCompleted = new[] { "profile", "invite" },
            Steps = Steps()
        };

        // Expanding before the template is applied is kept by the control, so the detail copy is visible
        // in a static preview and in screenshots.
        inProgress.ExpandForDemo("connect");

        var complete = new ArcOnboardingChecklist
        {
            Width = 660,
            Title = "Set up your workspace",
            DoneTitle = "Workspace ready",
            DefaultCompleted = new[] { "profile", "invite", "connect" },
            Steps = Steps().GetRange(0, 3)
        };

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Labelled("进行中 · 展开一步", inProgress));
        panel.Children.Add(Labelled("已完成", complete));
        return panel;
    }

    private static List<ArcChecklistStep> Steps() => new()
    {
        new()
        {
            Id = "profile",
            Title = "Add your profile photo",
            Description = "A face makes comments and mentions easier to scan."
        },
        new()
        {
            Id = "invite",
            Title = "Invite your team",
            Description = "Bring the people who will review, build, and ship with you.",
            ActionLabel = "Invite"
        },
        new()
        {
            Id = "connect",
            Title = "Connect GitHub",
            Description = "Sync issues and pull requests so releases stay in one place.",
            ActionLabel = "Connect"
        },
        new()
        {
            Id = "domain",
            Title = "Verify your domain",
            Description = "Use your own address for links and notification emails.",
            ActionLabel = "Verify"
        },
        new()
        {
            Id = "plan",
            Title = "Choose a plan",
            Description = "Pick the plan that matches your team size. You can change it later.",
            ActionLabel = "View plans"
        }
    };

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
